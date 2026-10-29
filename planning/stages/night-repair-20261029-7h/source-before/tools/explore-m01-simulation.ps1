. "$PSScriptRoot/env.ps1"
. "$PSScriptRoot/build-identity.ps1"
$out=Join-Path $ProjectRoot ('artifacts/m01/simulation-exploration-'+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $out | Out-Null
[ordered]@{started_at=[DateTimeOffset]::Now.ToString('o');source_sha256=(Get-YukkuriSourceIdentity $ProjectRoot).sha256;scope='Bounded seeded simulation exploration; explicitly synthetic time, not native long-run acceptance'}|ConvertTo-Json|Set-Content (Join-Path $out 'request.json')
Write-Output "Simulation exploration: $out"
& dotnet run --project (Join-Path $ProjectRoot 'checks/Milestone.Exploration/Milestone.Exploration.csproj') -- $out *> (Join-Path $out 'run.log')
$code=$LASTEXITCODE
Get-Content (Join-Path $out 'run.log') -Tail 6
if($code -ne 0){throw "Simulation exploration failed: $out"}
