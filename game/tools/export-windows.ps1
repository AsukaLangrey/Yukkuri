param([string]$OutputDirectory='builds/windows')
. "$PSScriptRoot/env.ps1"
. "$PSScriptRoot/build-identity.ps1"
. "$PSScriptRoot/process-affinity.ps1"
$buildAffinity=Get-YukkuriProcessAffinity ([Diagnostics.Process]::GetCurrentProcess())
$outputRoot=[IO.Path]::GetFullPath((Join-Path $LocalRoot $OutputDirectory))
if(!$outputRoot.StartsWith($LocalRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Export must stay inside .local.'}
& "$PSScriptRoot/build-game.ps1"
& "$PSScriptRoot/import-assets.ps1"
New-Item -ItemType Directory -Force $outputRoot | Out-Null
& $GodotExe --headless --path $GameRoot --export-release 'Windows Desktop' (Join-Path $outputRoot 'Yukkuri.exe')
if ($LASTEXITCODE -ne 0) { throw 'Windows export failed.' }
New-Item -ItemType Directory -Force (Join-Path $outputRoot 'third-party') | Out-Null
Copy-Item -Path "$ProjectRoot/third-party/*" -Destination (Join-Path $outputRoot 'third-party') -Force
Copy-Item -LiteralPath (Join-Path $RepositoryRoot 'assets/fonts/NotoSansSC-OFL.txt') -Destination (Join-Path $outputRoot 'third-party') -Force
$identity=Get-YukkuriSourceIdentity $ProjectRoot
$build=[ordered]@{
    stage='M01'; build_id=('M01-'+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss'))
    exported_at=[DateTimeOffset]::Now.ToString('o'); source=$identity
    build_environment=[ordered]@{process_affinity_mask=$buildAffinity.process_mask;system_affinity_mask=$buildAffinity.system_mask;scope='Build process affinity only. Runtime conditions are recorded separately during verification.'}
    payload=(Get-YukkuriPayloadIdentity $outputRoot)
}
$build | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $outputRoot 'build-info.json') -Encoding utf8
Write-Output "Export recorded: $($build.build_id); not yet runtime verified."
