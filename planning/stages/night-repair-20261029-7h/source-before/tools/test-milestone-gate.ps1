$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$relative = 'artifacts/milestone-gate-tests/' + [DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss')
$directory = Join-Path $root $relative
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$manifest = 'planning/milestones-v003.json'
$plan = Get-Content $manifest -Raw | ConvertFrom-Json
$catalog = $plan.rules_catalog
$proof = "$relative/SYNTHETIC.txt"
'SYNTHETIC checker fixture only. Not real gameplay, work time, Goal or user acceptance.' | Set-Content (Join-Path $root $proof)
$now = [DateTimeOffset]::UtcNow
$goal = @{stage_id='M01';goal_id='SYNTHETIC-NOT-A-REAL-GOAL';started_at=$now.AddHours(-7).ToString('o');ready_at=$null;stage_records=@($relative);minimum_work_hours=6;manifest_sha256=(Get-FileHash $manifest).Hash;catalog_sha256=(Get-FileHash $catalog).Hash}
$session = @{started_at=$now.AddHours(-7).ToString('o');ended_at=$now.AddMinutes(-10).ToString('o');activity='SYNTHETIC TEST';evidence=@($proof)}
$acceptance = @{stage_id='M01';build_id='SYNTHETIC';build_artifact=$proof;runtime_evidence=$proof;feedback_log=$proof;known_issues=@();checks=@($plan.stages[0].acceptance | ForEach-Object {@{id=$_.id;status='verified';result='SYNTHETIC';evidence=@($proof)}});user_acceptance=@{status='accepted';build_id='SYNTHETIC';message_id='SYNTHETIC';quote='SYNTHETIC ONLY';evidence=$proof}}
$results = @()
$cases = @('valid_fixture','night_ready','elapsed_under_six','work_under_six','overlap','missing_artifact','missing_criterion','blocking_issue','pending_user','wrong_build','baseline_changed','coverage_missing','early_ready','early_advance','early_missing_evidence','four_hour_boundary')
foreach($case in $cases) {
    $g=$goal | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $a=$acceptance | ConvertTo-Json -Depth 10 | ConvertFrom-Json
    $sessions=@($session | ConvertTo-Json -Depth 10 | ConvertFrom-Json)
    $testManifest=$manifest; $mode='Accepted'; $pattern='passed'
    if($case.StartsWith('early_')) {
        $g.started_at=$now.AddHours(-3).ToString('o');$g.ready_at=$now.AddHours(-1).ToString('o')
        $sessions[0].started_at=$g.started_at;$sessions[0].ended_at=$g.ready_at
        $a.user_acceptance.status='pending';$mode='Advance'
    }
    switch($case) {
        night_ready {$mode='NightReady'}
        elapsed_under_six {$mode='NightReady';$g.started_at=$now.AddHours(-1).ToString('o');$pattern='minimum elapsed'}
        work_under_six {$mode='NightReady';$sessions[0].ended_at=$now.AddHours(-6).ToString('o');$pattern='Insufficient recorded'}
        early_ready {$mode='Ready'}
        early_missing_evidence {$a.checks[0].evidence=@();$pattern='Missing criterion evidence'}
        four_hour_boundary {$mode='Advance';$g.started_at=$now.AddHours(-5).ToString('o');$g.ready_at=$now.AddHours(-1).ToString('o');$sessions[0].started_at=$g.started_at;$sessions[0].ended_at=$g.ready_at;$pattern='strictly under four hours'}
        overlap {$sessions += $sessions[0];$pattern='overlapping'}
        missing_artifact {$a.build_artifact="$relative/absent.zip";$pattern='Missing evidence'}
        missing_criterion {$a.checks=@($a.checks | Select-Object -Skip 1);$pattern='criteria missing'}
        blocking_issue {$a.known_issues=@(@{id='fixture';blocking=$true});$pattern='Blocking issue'}
        pending_user {$a.user_acceptance.status='pending';$pattern='User playtest acceptance is pending'}
        wrong_build {$a.user_acceptance.build_id='OLDER';$pattern='different build'}
        baseline_changed {$g.manifest_sha256='invalid';$pattern='Baseline changed'}
        coverage_missing {
            $changed=$plan | ConvertTo-Json -Depth 20 | ConvertFrom-Json
            $changed.rule_coverage=@($changed.rule_coverage | Select-Object -Skip 1)
            $testManifest="$relative/invalid-manifest.json"
            $changed | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $root $testManifest)
            $pattern='Rule coverage differs'
        }
    }
    $g | ConvertTo-Json -Depth 15 | Set-Content "$directory/goal.json"
    $a | ConvertTo-Json -Depth 15 | Set-Content "$directory/acceptance.json"
    @($sessions | ForEach-Object {$_ | ConvertTo-Json -Depth 10 -Compress}) | Set-Content "$directory/work-log.jsonl"
    $output = & (Join-Path $PSHOME 'pwsh.exe') -NoProfile -File tools/check-milestone.ps1 -Mode $mode -Stage M01 -Manifest $testManifest -RecordDirectory $relative
    $code=$LASTEXITCODE
    $expected = if($case -in @('valid_fixture','night_ready','early_ready','early_advance')){0}else{1}
    $ok=($code -eq $expected) -and (($output -join "`n") -match $pattern)
    $results += [ordered]@{case=$case;passed=$ok;exit_code=$code;output=($output -join "`n")}
}
$results | ConvertTo-Json -Depth 10 | Set-Content "$directory/results.json"
$failed = @($results | Where-Object {-not $_.passed})
[ordered]@{scope='SYNTHETIC workflow validation only';passed=($results.Count-$failed.Count);total=$results.Count;results="$relative/results.json";failures=$failed} | ConvertTo-Json -Depth 10
if($failed.Count -gt 0){exit 1}


