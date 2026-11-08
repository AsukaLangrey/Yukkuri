param([string]$LogFile)
. "$PSScriptRoot/env.ps1"
& "$PSScriptRoot/sync-assets.ps1"
$env:APPDATA = Join-Path $LocalRoot 'import/appdata'
$env:LOCALAPPDATA = Join-Path $LocalRoot 'import/localappdata'
New-Item -ItemType Directory -Force -Path $env:APPDATA,$env:LOCALAPPDATA | Out-Null
$arguments = @('--headless','--path',$GameRoot,'--editor','--import')
if ($LogFile) { $arguments += @('--log-file',$LogFile) }
& $GodotExe @arguments
if ($LASTEXITCODE -ne 0) { throw 'Godot asset import failed.' }