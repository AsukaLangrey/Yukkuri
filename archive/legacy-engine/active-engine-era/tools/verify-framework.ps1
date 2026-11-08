param([switch]$Exported,[switch]$Integration,[ValidateSet(10,50,100)][int]$Population=10)
. "$PSScriptRoot/env.ps1"
$tag = if ($Integration) { 'integration' } else { "population-$Population" }
if ($Exported) { $tag = "exported-$tag" }
$evidence = Join-Path $ProjectRoot "artifacts/framework/$tag"
New-Item -ItemType Directory -Force $evidence | Out-Null
$log = Join-Path $evidence 'run.log'
$screenshot = Join-Path $evidence 'screen.png'
$exe = if ($Exported) { Join-Path $ProjectRoot 'builds/windows/Yukkuri.exe' } else { $GodotExe }
$launch = @('--path',('"'+$GameRoot+'"'),'--log-file',('"'+$log+'"'),'--','--smoke-output',('"'+$screenshot+'"'),'--population',"$Population",'--mute')
if ($Integration) { $launch += '--integration' }
if ($Exported) {
    $launch = $launch[2..($launch.Length-1)]
    $env:PATH = ($env:PATH -split ';' | Where-Object { $_ -notlike "$ProjectRoot\.tools*" -and $_ -notlike "$ProjectRoot/.tools*" }) -join ';'
    $env:DOTNET_ROOT=$null; $env:DOTNET_ROOT_X64=$null
}
$started = Get-Date
$p = Start-Process -FilePath $exe -ArgumentList $launch -WorkingDirectory (Split-Path $exe) -WindowStyle Hidden -PassThru
if (!$p.WaitForExit(60000)) { $p.Kill(); throw "Framework check timed out: $log" }
$content = Get-Content -LiteralPath $log -Raw
Get-Content -LiteralPath $log
if ($p.ExitCode -ne 0 -or $content -match '(?m)^ERROR:') { throw "Framework failed: exit=$($p.ExitCode); $log" }
if ($Integration) {
    $result = Join-Path $evidence 'integration-result.json'
    if ($content -notmatch 'INTEGRATION_COMPLETE' -or !(Test-Path $result) -or (Get-Item $result).LastWriteTime -lt $started) { throw 'Integration result missing or stale.' }
} elseif (!(Test-Path $screenshot) -or (Get-Item $screenshot).LastWriteTime -lt $started) { throw 'Fresh screenshot missing.' }
