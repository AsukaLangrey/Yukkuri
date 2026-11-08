# Read-only recovery view. Does not start jobs, change Goal state, or accept a milestone.
param([string]$ExportDirectory='builds/candidates/m01-clang-expression')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$export=[IO.Path]::GetFullPath((Join-Path $root $ExportDirectory))
if(!$export.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Export must remain inside workspace.'}
$info=Get-Content -LiteralPath (Join-Path $export 'build-info.json') -Raw|ConvertFrom-Json
$goal=Get-Content -LiteralPath (Join-Path $root 'planning/stages/M01/goal.json') -Raw|ConvertFrom-Json
$runs=@(foreach($dir in Get-ChildItem -LiteralPath (Join-Path $root 'artifacts/m01') -Directory){
    $launchFile=Join-Path $dir.FullName 'launch.json'
    if(!(Test-Path -LiteralPath $launchFile)){continue}
    $launch=Get-Content -LiteralPath $launchFile -Raw|ConvertFrom-Json
    if($launch.build_id -ne $info.build_id){continue}
    $metaFile=Join-Path $dir.FullName 'verification-metadata.json'
    $meta=if(Test-Path -LiteralPath $metaFile){Get-Content -LiteralPath $metaFile -Raw|ConvertFrom-Json}else{$null}
    $live=$false;$processState='not_found'
    $proc=Get-Process -Id $launch.pid -ErrorAction SilentlyContinue
    if($proc){
        try{
            $samePath=[IO.Path]::GetFullPath($proc.Path) -eq [IO.Path]::GetFullPath($launch.executable)
            $delta=($proc.StartTime-[DateTimeOffset]::Parse($launch.started_at).LocalDateTime).TotalSeconds
            $live=$samePath -and $delta -ge -2 -and $delta -le 30
            $processState=if($live){'matching_live_process'}else{'pid_reused_or_identity_mismatch'}
        }catch{$processState='identity_unavailable'}
    }
    $requested=0
    for($i=0;$i -lt $launch.arguments.Count-1;$i++){
        if($launch.arguments[$i] -eq '--m01-soak-seconds'){$requested=[int]$launch.arguments[$i+1]}
    }
    $state=if(Test-Path -LiteralPath (Join-Path $dir.FullName 'interruption.json')){'interrupted'}elseif($meta){
        if($meta.success -and $meta.exit_code -eq 0 -and $meta.native_stderr_checked){'recorded_success'}else{'recorded_failure'}
    }elseif(Test-Path -LiteralPath (Join-Path $dir.FullName 'process-result.json')){'failed_or_incomplete'}elseif($live){'running'}else{'incomplete_or_unknown'}
    [ordered]@{directory=$dir.Name;state=$state;process_identity=$processState;pid=$launch.pid;started_at=$launch.started_at;
        earliest_requested_end=$(if($requested){[DateTimeOffset]::Parse($launch.started_at).AddSeconds($requested).ToString('o')}else{$null});
        kind=$meta.kind;exit_code=$meta.exit_code;diagnostic_flags=$meta.diagnostic_flags}
})
[ordered]@{checked_at=[DateTimeOffset]::Now.ToString('o');build_id=$info.build_id;deadline=$goal.user_extended_work_until;
    scope='Read-only recovery aid. Recorded test success is not package validation, whole-Goal completion, or user acceptance. A live process is not proof of useful progress.';
    original_requirements='planning/milestones-v003.txt';acceptance='planning/stages/M01/acceptance.json';runs=$runs}|ConvertTo-Json -Depth 7

