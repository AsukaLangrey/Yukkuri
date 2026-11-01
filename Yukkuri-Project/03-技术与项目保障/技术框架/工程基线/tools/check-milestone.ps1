param(
    [ValidateSet('Plan','Ready','Advance','Accepted','NightReady')][string]$Mode = 'Plan',
    [string]$Stage = 'M01',
    [string]$Manifest = 'planning/milestones-v003.json',
    [string]$RecordDirectory = ''
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
function LocalPath([string]$relative) {
    if ([string]::IsNullOrWhiteSpace($relative)) { throw 'Missing path.' }
    $full = [IO.Path]::GetFullPath((Join-Path $root $relative))
    if (-not $full.StartsWith($root + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw "Path outside workspace: $relative" }
    return $full
}
function ReadJson([string]$relative) { Get-Content -LiteralPath (LocalPath $relative) -Raw | ConvertFrom-Json }
function Require([bool]$condition,[string]$message) { if(-not $condition) { throw $message } }
function Evidence([string]$path) {
    $full = LocalPath $path
    Require (Test-Path -LiteralPath $full -PathType Leaf) "Missing evidence file: $path"
    Require ((Get-Item -LiteralPath $full).Length -gt 0) "Empty evidence file: $path"
}
try {
    $plan = ReadJson $Manifest
    $catalog = ReadJson $plan.rules_catalog
    $ids = @($catalog.sections | ForEach-Object rules | ForEach-Object id)
    $mapped = @($plan.rule_coverage | ForEach-Object rule_id)
    Require ($ids.Count -eq ($ids | Sort-Object -Unique).Count) 'Duplicate rule ID.'
    Require ($mapped.Count -eq ($mapped | Sort-Object -Unique).Count) 'Duplicate rule coverage.'
    Require (@(Compare-Object $ids $mapped).Count -eq 0) 'Rule coverage differs from confirmed catalog.'
    $stageIds = @($plan.stages | ForEach-Object id)
    Require ($stageIds.Count -eq ($stageIds | Sort-Object -Unique).Count) 'Duplicate milestone ID.'
    foreach($entry in $plan.stages) {
        Require ($entry.minimum_work_hours -ge 0) "Invalid milestone duration: $($entry.id)"
        Require (@($entry.acceptance).Count -gt 0) "Stage $($entry.id) has no acceptance criteria."
        $checks = @($entry.acceptance | ForEach-Object id)
        Require ($checks.Count -eq ($checks | Sort-Object -Unique).Count) "Duplicate acceptance IDs: $($entry.id)"
        foreach($d in $entry.dependencies) { Require (($stageIds -contains $d) -and ($d -ne $entry.id)) 'Invalid dependency.' }
    }
    foreach($r in $plan.rule_coverage) { Require ($stageIds -contains $r.primary_stage) "Invalid owner: $($r.rule_id)" }
    Require ($plan.night_policy.minimum_work_hours -ge 6) 'Night round must retain at least six hours.'
    foreach($r in @($catalog.sections | ForEach-Object rules)) { foreach($source in $r.sources) { Evidence $source } }
    $selected = @($plan.stages | Where-Object id -eq $Stage)
    Require ($selected.Count -eq 1) "Unknown stage: $Stage"
    $selected = $selected[0]
    if($Mode -ne 'Plan') {
        if(-not $RecordDirectory) { $RecordDirectory = "planning/stages/$Stage" }
        $goal = ReadJson "$RecordDirectory/goal.json"
        $acceptance = ReadJson "$RecordDirectory/acceptance.json"
        Require (($goal.stage_id -eq $Stage) -and (-not [string]::IsNullOrWhiteSpace($goal.goal_id))) 'Missing or mismatched Goal record.'
        Require (($goal.minimum_work_hours -ge 6) -and ($goal.minimum_work_hours -ge $selected.minimum_work_hours)) 'Goal duration below stage minimum.'
        Require (($goal.manifest_sha256 -eq (Get-FileHash (LocalPath $Manifest) -Algorithm SHA256).Hash) -and ($goal.catalog_sha256 -eq (Get-FileHash (LocalPath $plan.rules_catalog) -Algorithm SHA256).Hash)) 'Baseline changed; reconcile requirements and evidence before review.'
        Require ($acceptance.stage_id -eq $Stage) 'Acceptance stage mismatch.'
        $now = [DateTimeOffset]::UtcNow
        $start = [DateTimeOffset]::Parse($goal.started_at)
        Require ($start -le $now) 'Goal start is in the future.'
        if($Mode -eq 'NightReady') {
            Require (($now - $start).TotalHours -ge $goal.minimum_work_hours) 'Goal has not reached its minimum elapsed duration.'
        }
        $sessions = @(Get-Content -LiteralPath (LocalPath "$RecordDirectory/work-log.jsonl") | Where-Object {-not [string]::IsNullOrWhiteSpace($_)} | ForEach-Object {$_ | ConvertFrom-Json} | Sort-Object {[DateTimeOffset]::Parse($_.started_at)})
        $workHours = 0.0; $previousEnd = $start
        foreach($session in $sessions) {
            $a = [DateTimeOffset]::Parse($session.started_at); $b = [DateTimeOffset]::Parse($session.ended_at)
            Require (($a -ge $start) -and ($a -ge $previousEnd) -and ($b -gt $a) -and ($b -le $now)) 'Invalid, future or overlapping work session.'
            Require (-not [string]::IsNullOrWhiteSpace($session.activity)) 'Work session needs actual activity.'
            Require (@($session.evidence).Count -gt 0) 'Work session needs evidence.'
            foreach($e in $session.evidence) { Evidence $e }
            $workHours += ($b - $a).TotalHours; $previousEnd = $b
        }
        Require ($workHours -gt 0) 'No recorded work or verification.'
        if($Mode -eq 'NightReady') {
            Require ($workHours -ge $goal.minimum_work_hours) 'Insufficient recorded work/verification time; waiting alone is not completion.'
            Require (@($goal.stage_records).Count -gt 0) 'Night round must list its milestone records.'
            foreach($record in $goal.stage_records) {
                $stageGoal = ReadJson "$record/goal.json"
                $output = & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File $PSCommandPath -Mode Ready -Stage $stageGoal.stage_id -Manifest $Manifest -RecordDirectory $record
                Require ($LASTEXITCODE -eq 0) "Night milestone not ready: $record; $output"
            }
        }
        if($Mode -eq 'Advance') {
            Require (-not [string]::IsNullOrWhiteSpace($goal.ready_at)) 'Missing actual milestone ready_at.'
            $ready = [DateTimeOffset]::Parse($goal.ready_at)
            $duration = ($ready - $start).TotalHours
            Require (($duration -gt 0) -and ($duration -lt 4) -and ($ready -le $now)) 'Automatic advance requires completion strictly under four hours.'
            Require ($previousEnd -le $ready) 'Work evidence extends beyond recorded completion.'
        }
        foreach($d in $selected.dependencies) {
            $priorDirectory = "planning/stages/$d"
            $output = & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File $PSCommandPath -Mode Ready -Stage $d -Manifest $Manifest -RecordDirectory $priorDirectory
            Require ($LASTEXITCODE -eq 0) "Prior milestone not ready: $d; $output"
            if($Mode -eq 'Accepted') {
                $prior = ReadJson "$priorDirectory/acceptance.json"
                Require ($prior.user_acceptance.status -eq 'accepted') "Prior milestone user acceptance pending: $d"
                Evidence $prior.user_acceptance.evidence
            }
        }
        Require (-not [string]::IsNullOrWhiteSpace($acceptance.build_id)) 'Missing build ID.'
        Evidence $acceptance.build_artifact
        Evidence $acceptance.runtime_evidence
        Evidence $acceptance.feedback_log
        Require ($null -ne $acceptance.known_issues) 'Known issues must be explicitly listed, even when empty.'
        Require (@($acceptance.known_issues | Where-Object {$_.blocking -eq $true}).Count -eq 0) 'Blocking issue remains.'
        $expected = @($selected.acceptance | ForEach-Object id)
        $actual = @($acceptance.checks | ForEach-Object id)
        Require (($actual.Count -eq ($actual | Sort-Object -Unique).Count) -and (@(Compare-Object $expected $actual).Count -eq 0)) 'Acceptance criteria missing, duplicated or altered.'
        foreach($check in $acceptance.checks) {
            Require ($check.status -eq 'verified') "Criterion not verified: $($check.id)"
            Require (-not [string]::IsNullOrWhiteSpace($check.result)) "Missing observed result: $($check.id)"
            Require (@($check.evidence).Count -gt 0) "Missing criterion evidence: $($check.id)"
            foreach($e in $check.evidence) { Evidence $e }
        }
        if($Mode -eq 'Accepted') {
            Require ($acceptance.user_acceptance.status -eq 'accepted') 'User playtest acceptance is pending.'
            Require ((-not [string]::IsNullOrWhiteSpace($acceptance.user_acceptance.message_id)) -and (-not [string]::IsNullOrWhiteSpace($acceptance.user_acceptance.quote))) 'Missing original user acceptance reference.'
            Require ($acceptance.user_acceptance.build_id -eq $acceptance.build_id) 'User accepted a different build.'
            Evidence $acceptance.user_acceptance.evidence
        }
    }
    [ordered]@{status='passed'; mode=$Mode; stage=$Stage; scope='Local record consistency only; inspect real artifacts and user feedback separately.'} | ConvertTo-Json -Compress
    exit 0
} catch {
    [ordered]@{status='rejected'; mode=$Mode; stage=$Stage; reason=$_.Exception.Message} | ConvertTo-Json -Compress
    exit 1
}


