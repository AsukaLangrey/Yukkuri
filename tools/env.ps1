$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path $PSScriptRoot -Parent
$env:DOTNET_ROOT = Join-Path $ProjectRoot '.tools/dotnet'
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
$env:DOTNET_CLI_HOME = Join-Path $ProjectRoot '.cache/dotnet-home'
$env:NUGET_PACKAGES = Join-Path $ProjectRoot '.cache/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$GodotExe = Join-Path $ProjectRoot '.tools/godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe'
$BlenderExe = Join-Path $ProjectRoot '.tools/blender/blender-4.5.15-windows-x64/blender.exe'
$GameRoot = Join-Path $ProjectRoot 'src/Yukkuri.Game'
if (!(Test-Path "$env:DOTNET_ROOT/dotnet.exe") -or !(Test-Path $GodotExe)) {
    throw 'Toolchain missing. Run tools/setup.ps1 first.'
}
$env:PATH = "$env:DOTNET_ROOT;$(Split-Path $GodotExe);$(Split-Path $BlenderExe);$env:PATH"
