. "$PSScriptRoot/env.ps1"
$output = Join-Path $ProjectRoot 'builds/windows'
if (!(Test-Path "$output/Yukkuri.exe")) { throw 'Export Windows before packaging.' }
$resultPath = Join-Path $ProjectRoot 'artifacts/framework/exported-integration/integration-result.json'
if (!(Test-Path $resultPath)) { throw 'Exported integration evidence is required.' }
$result = Get-Content $resultPath -Raw | ConvertFrom-Json
if (!$result.success -or (Get-Item $resultPath).LastWriteTime -lt (Get-Item "$output/Yukkuri.exe").LastWriteTime) { throw 'Integration evidence is missing or predates this build.' }
Copy-Item "$ProjectRoot/README.txt" "$output/README.txt" -Force
Copy-Item "$ProjectRoot/assets-source/asset-manifest.json" "$output/asset-manifest.json" -Force
Copy-Item "$ProjectRoot/assets-source/sanctuary-morning-prompt.txt" "$output/imagegen-prompt.txt" -Force
$release = Join-Path $ProjectRoot ('builds/releases/' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Force $release | Out-Null
Compress-Archive -Path "$output/*" -DestinationPath "$release/Yukkuri-Framework-Windows-x64.zip" -CompressionLevel Optimal
$sourceZip = Join-Path $release 'Yukkuri-Framework-Source.zip'
Add-Type -AssemblyName System.IO.Compression
$archive = [System.IO.Compression.ZipFile]::Open($sourceZip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($folder in @('src','checks','tools','assets-source','third-party','planning')) {
        Get-ChildItem -LiteralPath (Join-Path $ProjectRoot $folder) -File -Recurse | Where-Object { $_.FullName -notmatch '\\(\.godot|bin|obj)\\' -and $_.Extension -notin '.blend1','.blend2' } | ForEach-Object {
            $relative = $_.FullName.Substring($ProjectRoot.Length + 1).Replace('\','/')
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $relative, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    }
    foreach ($file in @('README.txt','global.json','NuGet.Config','toolchain.lock.json','.gitignore','.gitattributes','.editorconfig','Open Editor.cmd','Run Preview.cmd')) {
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Join-Path $ProjectRoot $file), $file, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose() }
Get-ChildItem -LiteralPath $release -File | Get-FileHash -Algorithm SHA256 | Select-Object @{Name='file';Expression={Split-Path $_.Path -Leaf}},Hash | ConvertTo-Json | Set-Content "$release/checksums.json" -Encoding utf8
Write-Output "Packaged: $release"
