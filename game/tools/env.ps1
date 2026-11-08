$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$RepositoryRoot = Split-Path $ProjectRoot -Parent
$LocalRoot = Join-Path $RepositoryRoot '.local'
$ToolRoot = Join-Path $RepositoryRoot '.tools'
$CacheRoot = Join-Path $RepositoryRoot '.cache'
$GameRoot = Join-Path $ProjectRoot 'src/Yukkuri.Game'
$env:TEMP = Join-Path $LocalRoot 'tmp'
$env:TMP = $env:TEMP
New-Item -ItemType Directory -Force -Path $env:TEMP | Out-Null
$env:DOTNET_ROOT = Join-Path $ToolRoot 'dotnet'
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
$env:DOTNET_CLI_HOME = Join-Path $CacheRoot 'dotnet-home'
$env:NUGET_PACKAGES = Join-Path $CacheRoot 'nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$GodotExe = Join-Path $ToolRoot 'godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
if (!(Test-Path "$env:DOTNET_ROOT/dotnet.exe") -or !(Test-Path $GodotExe)) {
    throw 'Toolchain missing. From the repository root run game/tools/setup.ps1 -Package godot,dotnet.'
}
$env:PATH = "$env:DOTNET_ROOT;$(Split-Path $GodotExe);$env:PATH"