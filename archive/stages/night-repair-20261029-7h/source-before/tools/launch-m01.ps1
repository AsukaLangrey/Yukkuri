param(
    [string]$ExportDirectory='builds/windows',
    [switch]$LocalCompatibility,
    [string]$ProfileFile='.local/m01-host-compatibility.json',
    [switch]$CheckOnly
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
function WorkspacePath([string]$Path){
    $full=[IO.Path]::GetFullPath((Join-Path $root $Path))
    if($full -ne $root -and !$full.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Launch paths must remain inside this workspace.'}
    return $full
}
$directory=WorkspacePath $ExportDirectory
$exe=Join-Path $directory 'Yukkuri.exe'
$info=Get-Content -LiteralPath (Join-Path $directory 'build-info.json') -Encoding UTF8 -Raw|ConvertFrom-Json
if($info.stage -ne 'M01' -or $info.diagnostic_only){throw 'A playable M01 game build is required.'}
$expected=@($info.payload|Where-Object path -eq 'Yukkuri.exe')
if($expected.Count -ne 1 -or (Get-FileHash -LiteralPath $exe).Hash -ne $expected[0].sha256){throw 'Game executable does not match its recorded build.'}
# A matching executable alone does not identify the loaded game or .NET runtime.
# Keep this check self-contained: packaged launchers do not ship build-identity.ps1.
foreach($entry in $info.payload){
    $target=[IO.Path]::GetFullPath((Join-Path $directory $entry.path))
    if(!$target.StartsWith($directory+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){
        throw 'Payload path escapes build directory.'
    }
    if(!(Test-Path -LiteralPath $target -PathType Leaf) -or (Get-FileHash -LiteralPath $target).Hash -ne $entry.sha256){
        throw "Game payload does not match its recorded build: $($entry.path)"
    }
}
$mask=[long]0
if($LocalCompatibility){
    $profile=Get-Content -LiteralPath (WorkspacePath $ProfileFile) -Encoding UTF8 -Raw|ConvertFrom-Json
    $cpu=@(Get-CimInstance Win32_Processor)
    if($profile.computer_name -ne $env:COMPUTERNAME -or $cpu.Count -ne 1 -or
       $profile.processor_name -ne $cpu[0].Name.Trim() -or
       $profile.logical_processors -ne $cpu[0].NumberOfLogicalProcessors){throw 'This temporary profile belongs to a different host. Use the normal launcher there.'}
    $mask=[long]$profile.process_affinity_mask
    . (Join-Path $PSScriptRoot 'process-affinity.ps1')
    $available=Get-YukkuriProcessAffinity ([Diagnostics.Process]::GetCurrentProcess())
    if($mask -le 0 -or ($mask -band [long]$available.system_mask) -ne $mask){throw 'Invalid process affinity in the local profile.'}
}
if($CheckOnly){[ordered]@{build_id=$info.build_id;executable=$exe;validated=$true;local_compatibility=[bool]$LocalCompatibility;requested_process_affinity=$mask;started=$false}|ConvertTo-Json;return}
# The user runs this launcher to open an interactive game window.
$p=Start-Process -FilePath $exe -WorkingDirectory $directory -WindowStyle Normal -PassThru
if($mask){
    try{$p.ProcessorAffinity=[IntPtr]$mask;if($p.ProcessorAffinity.ToInt64() -ne $mask){throw 'Requested process affinity was not applied.'}}
    catch{if(!$p.HasExited){$p.Kill()};throw}
}
$local=Join-Path $root '.local';New-Item -ItemType Directory -Path $local -Force|Out-Null
$record=[ordered]@{started_at=[DateTimeOffset]::Now.ToString('o');pid=$p.Id;build_id=$info.build_id;executable=$exe;local_compatibility=[bool]$LocalCompatibility;actual_process_affinity=$p.ProcessorAffinity.ToInt64()}
$record|ConvertTo-Json -Compress|Add-Content -LiteralPath (Join-Path $local 'launch-history.jsonl') -Encoding UTF8
$record|ConvertTo-Json
