param(
    [Parameter(Mandatory=$true)][ValidatePattern('^[a-z0-9][a-z0-9-]{0,63}$')][string]$Name,
    [ValidateSet('build','core','full')][string]$Profile='core'
)
# Only pure .NET checks and compilation. Never calls a Godot/game executable or launcher.
$ErrorActionPreference='Stop'
$PSNativeCommandUseErrorActionPreference=$false
$taskRoot=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$relativeOutput='artifacts/night-repair-20261029/runs/'+$Name
$output=Join-Path $taskRoot $relativeOutput
if(Test-Path -LiteralPath $output){throw 'Run directory exists. Use a new name to preserve successful and failed evidence.'}
. (Join-Path $PSScriptRoot 'env.ps1')
. (Join-Path $PSScriptRoot 'build-identity.ps1')
$dotnet=Join-Path $env:DOTNET_ROOT 'dotnet.exe'
$sourceBefore=Get-YukkuriSourceIdentity $taskRoot
function Get-CheckFiles {
    @(Get-ChildItem -LiteralPath (Join-Path $taskRoot 'checks') -Recurse -File |
        Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]'} | Sort-Object FullName | ForEach-Object {
            [ordered]@{path=[IO.Path]::GetRelativePath($taskRoot,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()}
        })
}
$checkFiles=@(Get-CheckFiles)
$supportFiles=@(foreach($path in @('tools/check-night-repair-offline.ps1','tools/env.ps1','tools/build-identity.ps1','global.json','NuGet.Config')){
    [ordered]@{path=$path;sha256=(Get-FileHash -LiteralPath (Join-Path $taskRoot $path)).Hash.ToLowerInvariant()}
})
New-Item -ItemType Directory -Path $output | Out-Null
$steps=[Collections.Generic.List[object]]::new()
$run=[ordered]@{
    startedAt=[DateTimeOffset]::Now.ToString('o');finishedAt=$null;status='running';profile=$Profile
    scope='Pure .NET behavior, input math, geometry, save checks, and C# compilation only. No game/Godot/headless/computer-use; no runtime or human-input acceptance.'
    sourceIdentity=$sourceBefore.sha256;sourceFiles=$sourceBefore.files;checkFiles=$checkFiles;supportFiles=$supportFiles
    runnerHash=(Get-FileHash -LiteralPath $PSCommandPath).Hash.ToLowerInvariant()
    seedPolicy='Fixed seeds and synthetic trace schedules are in the captured check source/fixtures. Full soak: 72341, 592813, 1597331, 20261029; six scenarios, 3600 simulated seconds each. Combination geometry: 1728 cases with four frame schedules.'
    runtime=(Get-Item -LiteralPath $dotnet).VersionInfo.FileVersion;os=[Environment]::OSVersion.VersionString
    buildConfiguration='Release checks with UseAppHost=false; ordinary Debug game compilation'
    sourceUnchanged=$null;checkSourcesUnchanged=$null;supportFilesUnchanged=$null;steps=$steps
}
function Write-Run {
    $run | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $output 'run.json') -Encoding utf8
}
function Invoke-OfflineStep([string]$Id,[string[]]$Arguments,[string]$Report='') {
    $step=[ordered]@{id=$Id;command='dotnet';arguments=$Arguments;startedAt=[DateTimeOffset]::Now.ToString('o');finishedAt=$null;exitCode=$null;log=$Id+'.log';report=$Report;reportSummary=$null}
    $steps.Add($step);Write-Run
    Write-Output "Running offline step: $Id"
    & $dotnet @Arguments 2>&1 | Out-File -LiteralPath (Join-Path $output $step.log) -Encoding utf8
    $step.exitCode=$LASTEXITCODE;$step.finishedAt=[DateTimeOffset]::Now.ToString('o')
    if($Report -and (Test-Path -LiteralPath (Join-Path $output $Report))){
        $data=Get-Content -LiteralPath (Join-Path $output $Report) -Raw | ConvertFrom-Json
        $step.reportSummary=[ordered]@{success=$data.success;failures=$data.failures;
            results=if($null -ne $data.results){@($data.results).Count}else{$null};
            checks=if($null -ne $data.checks){@($data.checks).Count}else{$null};
            scenarios=if($null -ne $data.scenarios){@($data.scenarios).Count}else{$null};tested=$data.tested;samples=$data.samples}
    }
    Write-Run
    Get-Content -LiteralPath (Join-Path $output $step.log) -Tail 2
    if($step.exitCode -ne 0){throw "$Id failed with exit code $($step.exitCode). Its log and any partial report are retained."}
    if($Report -and !(Test-Path -LiteralPath (Join-Path $output $Report))){throw "$Id exited without its expected report."}
    if($Report -and ($data.success -eq $false -or $data.failures -gt 0)){throw "$Id reported failed checks despite a zero exit code."}
}
function Invoke-PureCheck([string]$Id,[string]$Project,[string]$Report,[string[]]$Extra=@()) {
    $arguments=@('run','--configuration','Release','--project',('checks/'+$Project+'/'+$Project+'.csproj'),'-p:UseAppHost=false')
    if($Report){$arguments+=@('--',($relativeOutput+'/'+$Report));$arguments+=$Extra}
    Invoke-OfflineStep $Id $arguments $Report
}
Push-Location $taskRoot
try {
    Write-Run
    Invoke-OfflineStep 'runtime' @('--info')
    if($Profile -ne 'build'){
        Invoke-PureCheck 'input-intent' 'Input.Intent' 'input-intent.json'
        Invoke-PureCheck 'natural-life' 'NaturalLife.Smoke' 'natural-life.json'
        Invoke-PureCheck 'actor-geometry' 'ActorGeometry.Smoke' 'actor-geometry.json'
        Invoke-PureCheck 'continuity' 'Interaction.Continuity' 'continuity.json'
        Invoke-PureCheck 'interaction' 'Interaction.Smoke' 'interaction.json'
        Invoke-PureCheck 'simulation' 'Simulation.Smoke' ''
        Invoke-PureCheck 'milestone' 'Milestone.Smoke' ''
        Invoke-OfflineStep 'save-compatibility' @('run','--configuration','Release','--project','checks/Save.Compatibility/Save.Compatibility.csproj','-p:UseAppHost=false','--',($relativeOutput+'/save-compatibility'),'artifacts/interaction-20261028/art03-native-slot1.json') 'save-compatibility/result.json'
    }
    if($Profile -eq 'full'){
        Invoke-PureCheck 'soak' 'NaturalLife.Soak' 'soak.json'
        Invoke-PureCheck 'intake-combinations' 'ActorGeometry.Smoke' 'intake-combinations.json' @('--intake-combinations','--motion')
    }
    Invoke-OfflineStep 'game-build' @('build','src/Yukkuri.Game/Yukkuri.Game.csproj','--no-restore')
    $run.sourceUnchanged=(Get-YukkuriSourceIdentity $taskRoot).sha256 -eq $sourceBefore.sha256
    $checkAfter=@(Get-CheckFiles)
    $run.checkSourcesUnchanged=($checkFiles | ConvertTo-Json -Depth 4 -Compress) -ceq ($checkAfter | ConvertTo-Json -Depth 4 -Compress)
    $run.supportFilesUnchanged=$true
    foreach($file in $supportFiles){
        if((Get-FileHash -LiteralPath (Join-Path $taskRoot $file.path)).Hash -ne $file.sha256){$run.supportFilesUnchanged=$false}
    }
    if(!$run.sourceUnchanged -or !$run.checkSourcesUnchanged -or !$run.supportFilesUnchanged){throw 'Sources changed during the run; these results do not all describe one source version.'}
    $run.status='passed'
} catch {
    $run.status='failed';$run.error=$_.Exception.Message
    Write-Output $run.error
} finally {
    $run.finishedAt=[DateTimeOffset]::Now.ToString('o');Write-Run;Pop-Location
}
if($run.status -ne 'passed'){exit 1}
Write-Output "Offline run passed: $relativeOutput/run.json (not native acceptance)."
