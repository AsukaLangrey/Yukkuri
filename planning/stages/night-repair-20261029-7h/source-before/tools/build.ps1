. "$PSScriptRoot/env.ps1"
Push-Location $ProjectRoot
try {
    & dotnet build src/Yukkuri.Game/Yukkuri.Game.csproj
    if ($LASTEXITCODE -ne 0) { throw 'C# build failed.' }
    & dotnet run --project checks/Simulation.Smoke/Simulation.Smoke.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Simulation smoke check failed.' }
    & dotnet run --project checks/Milestone.Smoke/Milestone.Smoke.csproj
    if ($LASTEXITCODE -ne 0) { throw 'M01 gameplay regression check failed.' }
    & dotnet run --project checks/Interaction.Smoke/Interaction.Smoke.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Human-oil interaction regression check failed.' }
    & dotnet run --project checks/ActorGeometry.Smoke/ActorGeometry.Smoke.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Actor surface geometry check failed.' }
} finally { Pop-Location }
