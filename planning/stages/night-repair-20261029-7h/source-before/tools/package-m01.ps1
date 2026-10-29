param([Parameter(Mandatory=$true)][string[]]$EvidenceDirectories,[string]$ExportDirectory='builds/windows',[string]$SourceDirectory='')
. "$PSScriptRoot/env.ps1"
. "$PSScriptRoot/build-identity.ps1"
. "$PSScriptRoot/m01-release-rules.ps1"
. "$PSScriptRoot/engine-provenance.ps1"
. "$PSScriptRoot/capture-integrity.ps1"
$output=[IO.Path]::GetFullPath((Join-Path $ProjectRoot $ExportDirectory))
if(!$output.StartsWith($ProjectRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Export must stay inside the workspace.'}
$infoPath=Join-Path $output 'build-info.json'
if(!(Test-Path -LiteralPath $infoPath)){throw 'Export M01 before packaging.'}
$info=Get-Content -LiteralPath $infoPath -Raw | ConvertFrom-Json
if($info.stage -ne 'M01'){throw 'A M01 build is required.'}
Confirm-YukkuriPayload $output $info.payload
Confirm-YukkuriEngineProvenance $output $info
$sourceRoot=if($SourceDirectory){[IO.Path]::GetFullPath((Join-Path $ProjectRoot $SourceDirectory))}else{$ProjectRoot}
if($sourceRoot -ne $ProjectRoot -and !$sourceRoot.StartsWith($ProjectRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Source snapshot must stay inside workspace.'}
$source=Get-YukkuriSourceIdentity $sourceRoot
if($source.sha256 -ne $info.source.sha256){throw 'Game sources changed since export; rebuild and verify the new export.'}
$infoHash=(Get-FileHash -LiteralPath $infoPath -Algorithm SHA256).Hash
$verified=@{}
$captureReports=@()
foreach($directory in $EvidenceDirectories){
    $dir=[IO.Path]::GetFullPath((Join-Path $ProjectRoot $directory))
    if(-not $dir.StartsWith($ProjectRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Evidence must be inside the workspace.'}
    $meta=Get-Content -LiteralPath (Join-Path $dir 'verification-metadata.json') -Raw | ConvertFrom-Json
    $result=Get-Content -LiteralPath (Join-Path $dir 'm01-integration-result.json') -Raw | ConvertFrom-Json
    $log=Get-Content -LiteralPath (Join-Path $dir 'run.log') -Raw
    $stderr=Get-Content -LiteralPath (Join-Path $dir 'stderr.log') -Raw
    if(!$meta.exported -or !$meta.success -or !$result.success -or $meta.exit_code -ne 0 -or
       $meta.build_id -ne $info.build_id -or $meta.build_info_sha256 -ne $infoHash -or
       $meta.renderer_override -ne 'default' -or ($null -ne $meta.diagnostic_flags -and @($meta.diagnostic_flags).Count -gt 0) -or
       !$meta.native_stderr_checked -or $stderr -match '(?m)^ERROR:|ERROR: AddressSanitizer|runtime error:' -or
       $log -match '(?m)^ERROR:' -or $log -notmatch 'M01_INTEGRATION_COMPLETE'){
        throw "Evidence is not a clean default run of this export: $directory"
    }
    switch($meta.kind){
        'integration' {if(!$result.story.scenarioComplete){throw 'Integration must complete the actual scenario.'}}
        'stress' {if($result.cycles -lt 60){throw 'Scene stress incomplete.'}}
        'soak' {Confirm-M01NaturalPlay $result $meta}
        'audio' {if(@($result.reports).Count -lt 8){throw 'Music and reaction mix evidence incomplete.'}}
        default {throw 'Unknown runtime evidence kind.'}
    }
    if($verified.ContainsKey($meta.kind)){throw "Duplicate evidence kind: $($meta.kind)"}
    $verified[$meta.kind]=$dir
    $capture=Get-M01CaptureIntegrity $dir
    $captureReports+=[ordered]@{kind=$meta.kind;directory=$directory;files=$capture.files;valid_files=$capture.valid_files;invalid_files=$capture.invalid_files}
}
foreach($kind in @('integration','stress','soak','audio')){if(!$verified.ContainsKey($kind)){throw "Missing exported $kind evidence."}}
$guide=Join-Path $ProjectRoot 'planning/stages/M01/PLAYER-GUIDE.txt'
$limits=Join-Path $ProjectRoot 'planning/stages/M01/KNOWN-ISSUES.txt'
foreach($file in @($guide,$limits)){if(!(Test-Path -LiteralPath $file)){throw "Missing delivery document: $file"}}
$release=Join-Path $ProjectRoot ('builds/releases/'+$info.build_id)
if(Test-Path -LiteralPath $release){throw 'Release already exists; never overwrite an independently reviewable version.'}
New-Item -ItemType Directory -Path $release | Out-Null
[ordered]@{build_id=$info.build_id;scope='Runtime assertions and PNG integrity are separate. Invalid PNGs remain preserved but cannot establish visual correctness. User acceptance is pending.';captures=$captureReports}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $release 'evidence-summary.json') -Encoding UTF8
Copy-Item -LiteralPath $guide -Destination (Join-Path $output '操作指南.txt')
Copy-Item -LiteralPath $guide -Destination (Join-Path $output 'README.txt')
Copy-Item -LiteralPath $limits -Destination (Join-Path $output '已知限制.txt')
Copy-Item -LiteralPath (Join-Path $ProjectRoot 'planning/stages/M01/PLAYTEST.txt') -Destination (Join-Path $output '试玩反馈单.txt')
Copy-Item -LiteralPath (Join-Path $sourceRoot 'assets-source/asset-manifest.json') -Destination (Join-Path $output 'asset-manifest.json')
Copy-Item -LiteralPath (Join-Path $sourceRoot 'assets-source/M01-assets.txt') -Destination (Join-Path $output '素材来源.txt')
$evidenceRoot=Join-Path $release 'evidence'
New-Item -ItemType Directory -Path $evidenceRoot | Out-Null
foreach($kind in $verified.Keys){Copy-Item -LiteralPath $verified[$kind] -Destination (Join-Path $evidenceRoot $kind) -Recurse}
Compress-Archive -Path "$output/*" -DestinationPath (Join-Path $release 'Yukkuri-M01-Windows-x64.zip') -CompressionLevel Optimal
Add-Type -AssemblyName System.IO.Compression
$archive=[IO.Compression.ZipFile]::Open((Join-Path $release 'Yukkuri-M01-Source.zip'),[IO.Compression.ZipArchiveMode]::Create)
try{
    foreach($folder in @('src','checks','tools','assets-source','third-party','planning')){
        Get-ChildItem -LiteralPath (Join-Path $sourceRoot $folder) -File -Recurse |
            Where-Object {$_.FullName -notmatch '[\\/](\.godot|bin|obj)[\\/]' -and $_.Extension -notin '.blend1','.blend2'} |
            ForEach-Object {
                $relative=$_.FullName.Substring($sourceRoot.Length+1).Replace('\','/')
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$_.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
            }
    }
    foreach($file in @('README.txt','global.json','NuGet.Config','toolchain.lock.json','.gitignore','.gitattributes','.editorconfig','Open Editor.cmd','Run Preview.cmd','Play Game.cmd')){
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $sourceRoot $file),$file,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    if(Test-Path -LiteralPath (Join-Path $sourceRoot 'source-snapshot.json')){
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,(Join-Path $sourceRoot 'source-snapshot.json'),'source-snapshot.json',[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    $engineProvenance=Join-Path $output 'engine-provenance'
    if(Test-Path -LiteralPath $engineProvenance){
        foreach($file in Get-ChildItem -LiteralPath $engineProvenance -File -Recurse){
            $relative='engine-provenance/'+$file.FullName.Substring($engineProvenance.Length+1).Replace('\','/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
}finally{$archive.Dispose()}
Copy-Item -LiteralPath $infoPath -Destination (Join-Path $release 'build-info.json')
Get-ChildItem -LiteralPath $release -File | Get-FileHash -Algorithm SHA256 |
    Select-Object @{Name='file';Expression={Split-Path $_.Path -Leaf}},Hash |
    ConvertTo-Json | Set-Content (Join-Path $release 'checksums.json') -Encoding utf8
Write-Output "Packaged $release. User playtest acceptance remains separate."
