param([Parameter(Mandatory=$true)][string]$ExportDirectory,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
. "$PSScriptRoot/build-identity.ps1"
function Local([string]$path){$p=[IO.Path]::GetFullPath((Join-Path $root $path));if(!$p.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Snapshot must remain inside workspace.'};return $p}
$export=Local $ExportDirectory;$snapshot=Local $OutputDirectory
if(Test-Path -LiteralPath $snapshot){throw 'Snapshot exists; never overwrite a frozen source version.'}
$info=Get-Content -LiteralPath (Join-Path $export 'build-info.json') -Raw | ConvertFrom-Json
if((Get-YukkuriSourceIdentity $root).sha256 -ne $info.source.sha256){throw 'Current sources differ from candidate; cannot freeze them as its source.'}
New-Item -ItemType Directory -Path $snapshot | Out-Null
foreach($folder in @('src','checks','tools','assets-source','third-party','planning')){
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $root $folder) -File -Recurse | Where-Object {$_.FullName -notmatch '[\\/](\.godot|bin|obj)[\\/]' -and $_.Extension -notin '.blend1','.blend2'}){
        $relative=$file.FullName.Substring($root.Length+1);$target=Join-Path $snapshot $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
}
foreach($file in @('README.txt','global.json','NuGet.Config','toolchain.lock.json','.gitignore','.gitattributes','.editorconfig','Open Editor.cmd','Run Preview.cmd','Play Game.cmd')){Copy-Item -LiteralPath (Join-Path $root $file) -Destination (Join-Path $snapshot $file)}
if((Get-YukkuriSourceIdentity $snapshot).sha256 -ne $info.source.sha256 -or (Get-YukkuriSourceIdentity $root).sha256 -ne $info.source.sha256){throw 'Sources changed during snapshot capture.'}
[ordered]@{build_id=$info.build_id;source_sha256=$info.source.sha256;captured_at=[DateTimeOffset]::Now.ToString('o');source_directory=$snapshot;scope='Exact game sources and supporting files captured before subsequent development; not runtime acceptance'} |
    ConvertTo-Json | Set-Content (Join-Path $snapshot 'source-snapshot.json') -Encoding utf8
Write-Output "Frozen source matches $($info.build_id): $snapshot"
