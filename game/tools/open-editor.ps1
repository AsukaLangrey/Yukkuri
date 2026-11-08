param([ValidateRange(0,36000)][int]$QuitAfter = 0)
. "$PSScriptRoot/env.ps1"
& "$PSScriptRoot/build-game.ps1"
$env:APPDATA = Join-Path $LocalRoot 'editor/appdata'
$env:LOCALAPPDATA = Join-Path $LocalRoot 'editor/localappdata'
New-Item -ItemType Directory -Path $env:APPDATA,$env:LOCALAPPDATA -Force | Out-Null
$arguments = @('--path',$GameRoot,'--editor')
if ($QuitAfter) { $arguments += @('--quit-after',"$QuitAfter") }
& $GodotExe @arguments
if ($LASTEXITCODE -ne 0) { throw "Godot editor exited with code $LASTEXITCODE." }