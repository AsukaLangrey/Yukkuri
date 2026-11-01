# Exercises identity rejection without executing any payload (including Godot).
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$folder=Join-Path $root ('artifacts/interaction-20261028/crash-offline-audit/launcher-fixture-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $folder|Out-Null
Set-Content -LiteralPath (Join-Path $folder 'Yukkuri.exe') -Value 'Not an executable. Offline fixture only.'
Set-Content -LiteralPath (Join-Path $folder 'Yukkuri.pck') -Value 'Original payload'
$manifest=[ordered]@{stage='M01';build_id='offline-fixture';payload=@(
    @{path='Yukkuri.exe';sha256=(Get-FileHash (Join-Path $folder 'Yukkuri.exe')).Hash},
    @{path='Yukkuri.pck';sha256=(Get-FileHash (Join-Path $folder 'Yukkuri.pck')).Hash})}
$manifest|ConvertTo-Json -Depth 4|Set-Content (Join-Path $folder 'build-info.json')
$relative=[IO.Path]::GetRelativePath($root,$folder)
$result=& (Join-Path $root 'tools/launch-m01.ps1') -ExportDirectory $relative -CheckOnly|ConvertFrom-Json
if($result.started -ne $false -or !$result.validated){throw 'Offline positive check failed.'}
Set-Content -LiteralPath (Join-Path $folder 'Yukkuri.pck') -Value 'Changed payload, unchanged executable'
$rejected=$false
try{& (Join-Path $root 'tools/launch-m01.ps1') -ExportDirectory $relative -CheckOnly|Out-Null}
catch{if($_.Exception.Message -notlike '*payload does not match*'){throw};$rejected=$true}
if(!$rejected){throw 'Changed PCK accepted.'}
$manifest.payload[1].path='../outside.pck'
$manifest|ConvertTo-Json -Depth 4|Set-Content (Join-Path $folder 'build-info.json')
$escaped=$false
try{& (Join-Path $root 'tools/launch-m01.ps1') -ExportDirectory $relative -CheckOnly|Out-Null}
catch{if($_.Exception.Message -notlike '*path escapes*'){throw};$escaped=$true}
if(!$escaped){throw 'Escaping payload path accepted.'}
[ordered]@{valid_fixture_passed=$true;changed_pck_rejected=$rejected;escaping_path_rejected=$escaped;
    game_started=$false;fixture=$folder}|ConvertTo-Json
