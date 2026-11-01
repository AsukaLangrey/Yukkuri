param(
    [Parameter(Mandatory=$true)][string[]]$EvidenceDirectories,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [Parameter(Mandatory=$true)][DateTimeOffset]$Until,
    [ValidateRange(10,60)][int]$IntervalSeconds=30
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
function Inside([string]$relative){
    $path=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(!$path.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Monitor paths must remain inside the workspace.'}
    return $path
}
$output=Inside $OutputDirectory
if(Test-Path -LiteralPath $output){throw 'Monitor output exists; preserve previous observations.'}
$targets=@(foreach($relative in $EvidenceDirectories){
    $directory=Inside $relative
    $launch=Get-Content -LiteralPath (Join-Path $directory 'launch.json') -Raw|ConvertFrom-Json
    if(!$launch.build_id -or !$launch.pid -or !$launch.executable){throw 'A recorded game launch is required.'}
    [pscustomobject]@{directory=$directory;launch=$launch}
})
if(!$targets.Count -or $Until -le [DateTimeOffset]::Now){throw 'Select targets and a future observation deadline.'}
New-Item -ItemType Directory -Path $output|Out-Null
[ordered]@{started_at=[DateTimeOffset]::Now.ToString('o');until=$Until.ToString('o');interval_seconds=$IntervalSeconds;targets=$EvidenceDirectories;scope='Read-only observation of identity-matched game processes. Memory and process survival are not gameplay or visual acceptance. No game input or process modification.'}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $output 'request.json') -Encoding UTF8
Write-Output "Process observation running: $output"
while([DateTimeOffset]::Now -lt $Until){
    $records=@(foreach($target in $targets){
        $launch=$target.launch
        $process=Get-Process -Id $launch.pid -ErrorAction SilentlyContinue
        $record=[ordered]@{observed_at=[DateTimeOffset]::Now.ToString('o');build_id=$launch.build_id;directory=$target.directory;pid=$launch.pid;identity='not_found';working_set_bytes=$null;private_bytes=$null;cpu_seconds=$null;threads=$null;process_affinity=$null;ended=$false;recorded_exit_code=$null}
        if($process){
            try{
                $delta=($process.StartTime-[DateTimeOffset]::Parse($launch.started_at).LocalDateTime).TotalSeconds
                $identityMatches=[IO.Path]::GetFullPath($process.Path) -eq [IO.Path]::GetFullPath($launch.executable) -and $delta -ge -2 -and $delta -le 30
                if($identityMatches){
                    $record.identity='matched';$record.working_set_bytes=$process.WorkingSet64;$record.private_bytes=$process.PrivateMemorySize64
                    $record.cpu_seconds=$process.TotalProcessorTime.TotalSeconds;$record.threads=$process.Threads.Count;$record.process_affinity=$process.ProcessorAffinity.ToInt64()
                }else{$record.identity='pid_reused_or_mismatch'}
            }catch{$record.identity='observation_unavailable'}
        }
        $resultPath=Join-Path $target.directory 'process-result.json'
        if(Test-Path -LiteralPath $resultPath){
            try{$result=Get-Content -LiteralPath $resultPath -Raw|ConvertFrom-Json;$record.ended=$true;$record.recorded_exit_code=$result.exit_code}catch{}
        }
        [pscustomobject]$record
    })
    foreach($record in $records){$record|ConvertTo-Json -Compress|Add-Content (Join-Path $output 'samples.jsonl') -Encoding UTF8}
    if(@($records|Where-Object {!$_.ended}).Count -eq 0){break}
    Start-Sleep -Seconds $IntervalSeconds
}
[ordered]@{ended_at=[DateTimeOffset]::Now.ToString('o');last_observation=$records;scope='Observation finished; consult each run result and actual playtest separately.'}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $output 'result.json') -Encoding UTF8
