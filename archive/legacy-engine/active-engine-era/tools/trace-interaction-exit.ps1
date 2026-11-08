# Default is offline preparation only. -Run must be explicitly supplied after
# permission to run the game; this script never changes process/system affinity.
param(
    [string]$ExportDirectory='builds/playtest/HI01-art03-20261028',
    [switch]$Run
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$directory=[IO.Path]::GetFullPath((Join-Path $root $ExportDirectory))
if(!$directory.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
    throw 'Build must remain inside the workspace.'
}
$info=Get-Content -LiteralPath (Join-Path $directory 'build-info.json') -Raw|ConvertFrom-Json
. (Join-Path $PSScriptRoot 'build-identity.ps1')
Confirm-YukkuriPayload $directory $info.payload
if($info.stage -ne 'M01' -or $info.diagnostic_only){throw 'A playable build is required.'}
$exe=Join-Path $directory 'Yukkuri.exe'
if(@($info.payload|Where-Object path -eq 'Yukkuri.exe').Count -ne 1){throw 'Executable identity is missing.'}
if(!$Run){
    [ordered]@{build_id=$info.build_id;payload_verified=$true;started=$false;
        run_requires='Explicit permission and -Run';scope='Default process affinity; isolated interaction saves; exit/stdout/stderr capture, no native dump'}|ConvertTo-Json
    return
}
$id=[DateTimeOffset]::Now.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$output=Join-Path $root ('artifacts/interaction-20261028/exit-trace-'+$id)
New-Item -ItemType Directory -Path $output -ErrorAction Stop|Out-Null
$log=Join-Path $output 'godot.log'
$record=[ordered]@{build_id=$info.build_id;executable=$exe;started_at=[DateTimeOffset]::Now.ToString('o');
    pid=$null;profile=('crash-'+$id);affinity_changed=$false;state='starting';exit_code=$null;
    classification='Unclassified: an exit code alone does not establish a crash cause.'}
function WriteRecord { $record|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'process-result.json') -Encoding UTF8 }
WriteRecord
try{
    $arguments='--log-file "'+$log+'" -- --interaction-test-profile '+$record.profile
    $process=Start-Process -FilePath $exe -WorkingDirectory $directory -WindowStyle Normal -PassThru `
        -ArgumentList $arguments -RedirectStandardOutput (Join-Path $output 'stdout.log') -RedirectStandardError (Join-Path $output 'stderr.log')
    $record.pid=$process.Id;$record.state='running';WriteRecord
    $process.WaitForExit()
    $record.exit_code=$process.ExitCode;$record.state='exited';$record.ended_at=[DateTimeOffset]::Now.ToString('o');WriteRecord
}catch{
    $record.state='observer_error';$record.observer_error=$_.Exception.Message;WriteRecord
    throw
}
$record|ConvertTo-Json
# Interrupted observers leave state=running/observer_error. Never turn those into
# natural-crash counts or kill the child to force a completion record.
