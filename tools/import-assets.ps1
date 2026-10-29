. "$PSScriptRoot/env.ps1"
if (!(Test-Path $BlenderExe)) { throw 'Blender missing. Run tools/setup.ps1 -Package blender.' }
& $BlenderExe --background --factory-startup --python "$PSScriptRoot/make-probe.py"
if ($LASTEXITCODE -ne 0) { throw 'Blender asset generation failed.' }
& $GodotExe --headless --path $GameRoot --editor --import
if ($LASTEXITCODE -ne 0) { throw 'Godot asset import failed.' }
