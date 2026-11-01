param([Parameter(Mandatory=$true)][string]$ReleaseDirectory,[switch]$FilesOnly)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
. "$PSScriptRoot/build-identity.ps1"
. "$PSScriptRoot/engine-provenance.ps1"
function Inside([string]$base,[string]$relative){
    $full=[IO.Path]::GetFullPath((Join-Path $base $relative))
    if(!$full.StartsWith($base+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw "Path escapes intended directory: $relative"}
    return $full
}
$release=Inside $root $ReleaseDirectory
$info=Get-Content -LiteralPath (Join-Path $release 'build-info.json') -Raw | ConvertFrom-Json
$checksums=Get-Content -LiteralPath (Join-Path $release 'checksums.json') -Raw | ConvertFrom-Json
foreach($entry in $checksums){
    $path=Inside $release $entry.file
    if((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Hash){throw "Release file changed: $($entry.file)"}
}
foreach($name in @('build-info.json','Yukkuri-M01-Windows-x64.zip','Yukkuri-M01-Source.zip')){
    if(@($checksums | Where-Object file -eq $name).Count -ne 1){throw "Missing or duplicate delivery checksum: $name"}
}
$stamp=[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')
$evidence=Inside $root "artifacts/m01/package-validation-$stamp"
New-Item -ItemType Directory -Path $evidence | Out-Null
Add-Type -AssemblyName System.IO.Compression
$sourceZip=[IO.Compression.ZipFile]::OpenRead((Join-Path $release 'Yukkuri-M01-Source.zip'))
$sourceCount=0
try{
    foreach($file in $info.source.files){
        $entries=@($sourceZip.Entries | Where-Object FullName -eq $file.path)
        if($entries.Count -ne 1){throw "Missing or duplicate source snapshot entry: $($file.path)"}
        $stream=$entries[0].Open()
        try{$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream))}finally{$stream.Dispose()}
        if($hash -ne $file.sha256){throw "Source snapshot mismatch: $($file.path)"}
        $sourceCount++
    }
}finally{$sourceZip.Dispose()}
if($sourceCount -eq 0){throw 'Source snapshot manifest is empty.'}
$unpacked=Join-Path $evidence '中文路径 试玩包'
[IO.Compression.ZipFile]::ExtractToDirectory((Join-Path $release 'Yukkuri-M01-Windows-x64.zip'),$unpacked)
$innerInfo=Get-Content -LiteralPath (Join-Path $unpacked 'build-info.json') -Raw | ConvertFrom-Json
if($innerInfo.build_id -ne $info.build_id -or $innerInfo.source.sha256 -ne $info.source.sha256){throw 'Packaged build identity differs from release identity.'}
Confirm-YukkuriPayload $unpacked $info.payload
Confirm-YukkuriEngineProvenance $unpacked $info
foreach($name in @('操作指南.txt','已知限制.txt','试玩反馈单.txt','asset-manifest.json','素材来源.txt')){
    if(!(Test-Path -LiteralPath (Join-Path $unpacked $name))){throw "Missing player-facing delivery file: $name"}
}
$runtime=$null
if(!$FilesOnly){
    $relative=$unpacked.Substring($root.Length+1)
    & "$PSScriptRoot/verify-m01.ps1" -Exported -ExportDirectory $relative
    # Resolve by executable, never by the shared LATEST file: other checks may finish concurrently.
    $runtime=Get-ChildItem -LiteralPath (Join-Path $root 'artifacts/m01') -Directory |
        ForEach-Object { $meta=Join-Path $_.FullName 'verification-metadata.json'; if(Test-Path -LiteralPath $meta){$m=Get-Content -LiteralPath $meta -Raw | ConvertFrom-Json;if($m.executable -eq (Join-Path $unpacked 'Yukkuri.exe')){$meta}} } |
        Select-Object -Last 1
    if(!$runtime){throw 'Unpacked runtime evidence was not found.'}
    Copy-Item -LiteralPath $runtime -Destination (Join-Path $evidence 'runtime-verification.json')
}
[ordered]@{
    build_id=$info.build_id;checked_at=[DateTimeOffset]::Now.ToString('o');checked_source_files=$sourceCount
    checked_payload_files=@($info.payload).Count;checked_release_files=@($checksums).Count
    unpacked_directory=$unpacked;runtime_verified=(!$FilesOnly);runtime_metadata=$runtime
    scope='Package identity, exact source files and actual unpacked runtime when requested; not user acceptance'
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $evidence 'result.json') -Encoding utf8
Write-Output "Release package verified: $evidence (runtime=$(!$FilesOnly))."
