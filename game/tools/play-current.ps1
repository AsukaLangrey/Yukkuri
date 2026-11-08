param(
    [switch]$Headless,
    [ValidateRange(0,36000)][int]$QuitAfter = 0,
    [ValidatePattern('^[a-zA-Z0-9_-]+$')][string]$Profile = 'play-current'
)
. "$PSScriptRoot/env.ps1"
$session = Join-Path $LocalRoot ('testing/play-current-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
New-Item -ItemType Directory -Path $session -Force | Out-Null
& "$PSScriptRoot/build-game.ps1" | Tee-Object -FilePath (Join-Path $session 'build.log')
& "$PSScriptRoot/import-assets.ps1" -LogFile (Join-Path $session 'import.log')
$env:APPDATA = Join-Path $LocalRoot "$Profile/appdata"
$env:LOCALAPPDATA = Join-Path $LocalRoot "$Profile/localappdata"
New-Item -ItemType Directory -Path $env:APPDATA,$env:LOCALAPPDATA -Force | Out-Null
$arguments = @('--path',$GameRoot,'--position','100,60','--resolution','1440x900','--max-fps','60','--log-file',(Join-Path $session 'game.log'))
if ($Headless) { $arguments += '--headless' }
if ($QuitAfter) { $arguments += @('--quit-after',"$QuitAfter") }
& $GodotExe @arguments -- --interaction-test-profile main-current
if ($LASTEXITCODE -ne 0) { throw "Game exited with code $LASTEXITCODE. See $session/game.log." }