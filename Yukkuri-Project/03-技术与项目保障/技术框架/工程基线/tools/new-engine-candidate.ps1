param([Parameter(Mandatory=$true)][string]$FromDirectory,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
. "$PSScriptRoot/build-identity.ps1"
. "$PSScriptRoot/engine-provenance.ps1"
function Local([string]$path){$p=[IO.Path]::GetFullPath((Join-Path $root $path));if(!$p.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Candidate must be inside workspace.'};return $p}
$from=Local $FromDirectory;$to=Local $OutputDirectory
if(Test-Path -LiteralPath $to){throw 'Preserve existing candidate; use a new directory.'}
$info=Get-Content -LiteralPath (Join-Path $from 'build-info.json') -Raw | ConvertFrom-Json
Confirm-YukkuriPayload $from $info.payload
if((Get-YukkuriSourceIdentity $root).sha256 -ne $info.source.sha256){throw 'Export does not match current game sources.'}
$template=Local '.tools/engine-diagnostics/godot-4.7.2-stable/bin/godot.windows.template_release.x86_64.llvm.mono.exe'
$objcopy=Local '.tools/engine-diagnostics/llvm-mingw-20261020-ucrt-x86_64/bin/llvm-objcopy.exe'
New-Item -ItemType Directory -Path $to | Out-Null
Get-ChildItem -LiteralPath $from | Copy-Item -Destination $to -Recurse
& $objcopy --strip-debug $template (Join-Path $to 'Yukkuri.exe')
if($LASTEXITCODE -ne 0){throw 'Candidate symbol stripping failed.'}
$baseId=$info.build_id
$info.build_id='M01-CLANG-'+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss')
$info.exported_at=[DateTimeOffset]::Now.ToString('o')
$info.payload=Get-YukkuriPayloadIdentity $to
$info | Add-Member -NotePropertyName engine_candidate -NotePropertyValue ([ordered]@{
    base_game_export=$baseId;engine='Godot 4.7.2 source, LLVM-MinGW UCRT template_release';build_request='artifacts/m01/renderer-probe/clang-template-20261027-034705/build-request.json'
    original_symbol_binary=$template;symbol_binary_sha256=(Get-FileHash $template -Algorithm SHA256).Hash
    runtime_sha256=(Get-FileHash (Join-Path $to 'Yukkuri.exe') -Algorithm SHA256).Hash
    status='unvalidated candidate; no root-cause fix claimed';features_disabled=@('D3D12','AccessKit');asan=$false
})
$info | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $to 'build-info.json') -Encoding utf8
Copy-Item -LiteralPath (Local 'assets-source/engine-provenance') -Destination (Join-Path $to 'engine-provenance') -Recurse
Get-ChildItem -LiteralPath (Local 'third-party') -File | Copy-Item -Destination (Join-Path $to 'third-party') -Force
Confirm-YukkuriEngineProvenance $to $info
Write-Output "Created isolated engine candidate $($info.build_id). Default export is unchanged."
