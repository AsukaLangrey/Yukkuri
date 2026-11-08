. "$PSScriptRoot/env.ps1"
& "$PSScriptRoot/build-game.ps1"
& "$PSScriptRoot/import-assets.ps1"
$env:APPDATA=Join-Path $LocalRoot 'art-preview/appdata'
$env:LOCALAPPDATA=Join-Path $LocalRoot 'art-preview/localappdata'
$arguments=@('--path',('"'+$GameRoot+'"'),'--rendering-method','gl_compatibility','--resolution','1440x900','--max-fps','60','--audio-driver','Dummy','res://ArtPreview/StarterGarden.tscn')
# Explicitly user-invoked preview entry; capture automation uses the separate hidden launcher.
Start-Process -FilePath $GodotExe -ArgumentList $arguments -WindowStyle Normal
