param([switch]$Exported)
. "$PSScriptRoot/env.ps1"
$artifactRoot = Join-Path $ProjectRoot 'artifacts'
New-Item -ItemType Directory -Force $artifactRoot | Out-Null
$mode = if ($Exported) { 'exported' } else { 'editor-runtime' }
$screenshot = Join-Path $artifactRoot "$mode.png"
$log = Join-Path $artifactRoot "$mode.log"
$exe = if ($Exported) { Join-Path $ProjectRoot 'builds/windows/Yukkuri.exe' } else { $GodotExe }
$launchArguments = @('--path', ('"' + $GameRoot + '"'), '--log-file', ('"' + $log + '"'), '--', '--smoke-output', ('"' + $screenshot + '"'))
if ($Exported) {
    $launchArguments = $launchArguments[2..($launchArguments.Length - 1)]
    # Verify the bundled runtime, without the development SDK environment.
    $env:PATH = ($env:PATH -split ';' | Where-Object { $_ -notlike "$ProjectRoot\.tools*" -and $_ -notlike "$ProjectRoot/.tools*" }) -join ';'
    $env:DOTNET_ROOT = $null
    $env:DOTNET_ROOT_X64 = $null
}
if (Test-Path $screenshot) { Remove-Item -LiteralPath $screenshot }
$process = Start-Process -FilePath $exe -ArgumentList $launchArguments -WorkingDirectory (Split-Path $exe) -WindowStyle Hidden -PassThru
if (!$process.WaitForExit(60000)) { $process.Kill(); throw "Render check timed out: $mode" }
if ($process.ExitCode -ne 0 -or !(Test-Path $screenshot)) { throw "Render check failed: $mode. See $log" }
$logText = Get-Content -LiteralPath $log -Raw
if ($logText -notmatch 'PIPELINE_READY' -or $logText -notmatch 'RENDER_CAPTURE: Ok' -or $logText -match '(?m)^ERROR:') {
    throw "Render log missing success markers or contains engine errors: $log"
}
Get-Content -LiteralPath $log
