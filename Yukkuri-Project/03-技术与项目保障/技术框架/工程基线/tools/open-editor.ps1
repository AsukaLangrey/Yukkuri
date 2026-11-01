. "$PSScriptRoot/env.ps1"
Start-Process -FilePath $GodotExe -ArgumentList @('--path', ('"' + $GameRoot + '"'), '--editor') -WindowStyle Normal
