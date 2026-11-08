. "$PSScriptRoot/env.ps1"
& "$PSScriptRoot/sync-assets.ps1"
Push-Location -LiteralPath $ProjectRoot
try {
    & "$env:DOTNET_ROOT/dotnet.exe" build 'src/Yukkuri.Game/Yukkuri.Game.csproj' --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Game build failed.' }
} finally { Pop-Location }