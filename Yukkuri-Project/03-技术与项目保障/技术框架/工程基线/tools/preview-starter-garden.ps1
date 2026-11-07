$ErrorActionPreference='Stop'
$project=Split-Path $PSScriptRoot -Parent
$env:DOTNET_ROOT=Join-Path $project '.tools/dotnet'
$env:PATH="$env:DOTNET_ROOT;$env:PATH"
$env:APPDATA=Join-Path $project '.local/art-preview/appdata'
$env:LOCALAPPDATA=Join-Path $project '.local/art-preview/localappdata'
$engine=(Get-ChildItem -LiteralPath (Join-Path $project '.tools/godot') -Recurse -Filter '*console.exe' | Select-Object -First 1).FullName
if(!$engine){throw 'Godot executable not found.'}
$arguments=@('--path',('"'+(Join-Path $project 'src/Yukkuri.Game')+'"'),'--rendering-method','gl_compatibility','--resolution','1440x900','--max-fps','60','--audio-driver','Dummy','res://ArtPreview/StarterGarden.tscn')
# Explicitly user-invoked preview entry; capture automation uses the separate hidden launcher.
Start-Process -FilePath $engine -ArgumentList $arguments -WindowStyle Hidden
