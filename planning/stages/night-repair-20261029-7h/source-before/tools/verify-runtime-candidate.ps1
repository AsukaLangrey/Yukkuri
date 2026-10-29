param([ValidateSet('stress','integration','audio','soak')][string]$Kind='stress',[ValidateSet('gl_compatibility','mobile')][string]$Renderer='gl_compatibility',[ValidateSet('runtime452','runtime8')][string]$Candidate='runtime452')
. "$PSScriptRoot/env.ps1"
$engine=Join-Path $ProjectRoot '.tools/godot-4.5.2/Godot_v4.5.2-stable_mono_win64/Godot_v4.5.2-stable_mono_win64_console.exe'
$candidatePath=Join-Path $ProjectRoot "artifacts/m01/$Candidate/Yukkuri.Game"
if($Candidate -eq 'runtime8'){
    $engine=$GodotExe
    $env:DOTNET_ROOT=Join-Path $ProjectRoot '.tools/dotnet-runtime8'
    $env:DOTNET_ROOT_X64=$env:DOTNET_ROOT
    $env:DOTNET_MULTILEVEL_LOOKUP='0'
}
$engine=$engine.Replace('_console.exe','.exe')
$label=if($Candidate -eq 'runtime452'){'candidate452'}else{'candidate-runtime8'}
$evidence=Join-Path $ProjectRoot ('artifacts/m01/'+$label+'-'+$Kind+'-'+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $evidence | Out-Null
$log=Join-Path $evidence 'run.log'
$arguments=@('--path',$candidatePath,'--rendering-method',$Renderer,'--log-file',$log,'--','--smoke-output',(Join-Path $evidence 'screen.png'),'--mute',"--m01-$Kind")
$start=Get-Date
$p=Start-Process -FilePath $engine -ArgumentList $arguments -WindowStyle Hidden -PassThru
Write-Output "Candidate running: $evidence"
Start-Sleep -Seconds 2
if(-not $p.HasExited){$p.Modules | Select-Object ModuleName,FileName | ConvertTo-Json | Set-Content (Join-Path $evidence 'modules.json')}
$deadline=(Get-Date).AddSeconds($(if($Kind -eq 'soak'){780}else{180}))
while(-not $p.WaitForExit(1000)){if((Get-Date) -gt $deadline){$p.Kill();throw 'Candidate timeout'}}
$content=Get-Content $log -Raw
Get-Content $log -Tail 18
$result=Join-Path $evidence 'm01-integration-result.json'
if($p.ExitCode -ne 0 -or $content -match '(?m)^ERROR:' -or $content -notmatch 'M01_INTEGRATION_COMPLETE'){throw 'Candidate runtime failed'}
if(!(Test-Path $result) -or (Get-Item $result).LastWriteTime -lt $start -or -not (Get-Content $result -Raw | ConvertFrom-Json).success){throw 'Missing successful candidate result'}
Write-Output "Candidate passed: $evidence"
