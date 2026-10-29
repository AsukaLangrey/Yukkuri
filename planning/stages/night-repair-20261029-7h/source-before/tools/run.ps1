. "$PSScriptRoot/env.ps1"
& "$PSScriptRoot/build.ps1"
Start-Process -FilePath $GodotExe -ArgumentList @('--path', ('"' + $GameRoot + '"')) -WindowStyle Normal
