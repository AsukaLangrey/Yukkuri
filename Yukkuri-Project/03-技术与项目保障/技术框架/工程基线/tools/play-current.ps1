$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $project
$env:DOTNET_ROOT=Join-Path $project '.tools/dotnet'
$env:PATH="$env:DOTNET_ROOT;$env:PATH"
$env:DOTNET_CLI_HOME=Join-Path $project '.cache/dotnet-home'
$env:NUGET_PACKAGES=Join-Path $project '.cache/nuget'
$session=Join-Path $project ('.local/testing/play-current-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $session -Force | Out-Null
# A stable, isolated profile for this main-repository launcher. Never touches the old preview's saves.
$env:APPDATA=Join-Path $project '.local/play-current/appdata'
$env:LOCALAPPDATA=Join-Path $project '.local/play-current/localappdata'
New-Item -ItemType Directory -Path $env:APPDATA,$env:LOCALAPPDATA -Force | Out-Null
& "$env:DOTNET_ROOT/dotnet.exe" build (Join-Path $project 'src/Yukkuri.Game/Yukkuri.Game.csproj') --nologo 2>&1 | Tee-Object (Join-Path $session 'build.log')
if($LASTEXITCODE -ne 0){throw 'Game build failed; see build.log.'}
$engine=(Get-ChildItem -LiteralPath (Join-Path $project '.tools/godot') -Recurse -Filter '*console.exe' | Select-Object -First 1).FullName
if(!$engine){throw 'Godot console executable missing.'}
& $engine --path (Join-Path $project 'src/Yukkuri.Game') --position 100,60 --resolution 1440x900 --max-fps 60 --log-file (Join-Path $session 'game.log') -- --interaction-test-profile main-current
exit $LASTEXITCODE
