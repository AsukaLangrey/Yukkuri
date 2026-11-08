param([Parameter(Mandatory=$true)][ValidatePattern('^[a-z0-9][a-z0-9-]{0,63}$')][string]$Name)
# Files only. Does not build, launch an engine, change a launcher, or certify runtime acceptance.
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$stage=Join-Path $taskRoot 'planning/stages/night-repair-20261029-7h'
$evidenceRoot=Join-Path $taskRoot 'artifacts/night-repair-20261029'
$destination=Join-Path $evidenceRoot ('source-checkpoints/'+$Name)
if(Test-Path -LiteralPath $destination){throw 'Checkpoint already exists; choose a new name. Existing evidence is never overwritten.'}
. (Join-Path $PSScriptRoot 'build-identity.ps1')
$before=Get-YukkuriSourceIdentity $taskRoot
$baseline=Get-Content -LiteralPath (Join-Path $stage 'baseline-manifest.json') -Raw | ConvertFrom-Json
foreach($entry in $baseline){
    $original=Join-Path (Join-Path $stage 'source-before') $entry.path
    if((Get-FileHash -LiteralPath $original -Algorithm SHA256).Hash -ne $entry.sha256){throw "Baseline changed: $($entry.path)"}
}
$files=@()
foreach($folder in @('src','checks','tools')){
    $files+=Get-ChildItem -LiteralPath (Join-Path $taskRoot $folder) -Recurse -File |
        Where-Object {$_.FullName -notmatch '[\\/](\.godot|bin|obj|__pycache__)[\\/]' -and $_.Extension -ne '.pyc'}
}
$files+=Get-ChildItem -LiteralPath $taskRoot -File |
    Where-Object {$_.Extension -in '.json','.Config','.cmd','.txt' -or $_.Name -in '.gitignore','.gitattributes','.editorconfig'}
foreach($relative in @('planning/requirements-v057.txt','planning/requirements-v058.txt','planning/requirements-v059.txt','planning/night-repair-plan-20261029-v001.txt')){
    $files+=Get-Item -LiteralPath (Join-Path $taskRoot $relative)
}
$files+=Get-ChildItem -LiteralPath $stage -File | Where-Object {$_.Extension -in '.txt','.json'}
$files=@($files | Sort-Object FullName -Unique)
New-Item -ItemType Directory -Path $destination | Out-Null
$entries=@(foreach($file in $files){
    $relative=[IO.Path]::GetRelativePath($taskRoot,$file.FullName).Replace('\','/')
    $target=Join-Path $destination ('source/'+$relative)
    New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
    $hash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    Copy-Item -LiteralPath $file.FullName -Destination $target
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hash){throw "Copy mismatch: $relative"}
    [ordered]@{path=$relative;sha256=$hash;bytes=$file.Length}
})
foreach($entry in $entries){
    if((Get-FileHash -LiteralPath (Join-Path $taskRoot $entry.path) -Algorithm SHA256).Hash -ne $entry.sha256){throw "Source changed while capturing: $($entry.path)"}
}
$after=Get-YukkuriSourceIdentity $taskRoot
$captured=Get-YukkuriSourceIdentity (Join-Path $destination 'source')
if($before.sha256 -ne $after.sha256 -or $after.sha256 -ne $captured.sha256){throw 'Game sources changed during capture or copy identity differs.'}
$old=@{};foreach($entry in $baseline){$old[$entry.path.Replace('\','/')]=$entry.sha256}
$current=@{};foreach($entry in $entries){if($entry.path -match '^(src|checks|tools)/'){$current[$entry.path]=$entry.sha256}}
$differences=@(foreach($path in @(@($old.Keys)+@($current.Keys) | Sort-Object -Unique)){
    $change=if(!$old.ContainsKey($path)){'added'}elseif(!$current.ContainsKey($path)){'removed'}elseif($old[$path] -ne $current[$path]){'modified'}else{'unchanged'}
    [ordered]@{path=$path;change=$change;before=$old[$path];after=$current[$path]}
})
$evidenceFiles=@(Get-ChildItem -LiteralPath $evidenceRoot -File)
foreach($directory in Get-ChildItem -LiteralPath $evidenceRoot -Directory | Where-Object {$_.Name -ne 'source-checkpoints'}){
    $evidenceFiles+=Get-ChildItem -LiteralPath $directory.FullName -Recurse -File
}
$evidence=@($evidenceFiles | Sort-Object FullName | ForEach-Object {
    [ordered]@{path=[IO.Path]::GetRelativePath($evidenceRoot,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
})
[ordered]@{capturedAt=[DateTimeOffset]::Now.ToString('o');scope='Independent source checkpoint and evidence hashes. No engine execution, package update, runtime acceptance, or claim that every indexed report passed.';
    sourceIdentity=$after.sha256;baselineVerified=$baseline.Count;sourceFiles=$entries;evidenceFiles=$evidence} |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $destination 'manifest.json') -Encoding utf8
$differences | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'baseline-differences.json') -Encoding utf8
Write-Output "Verified checkpoint $Name`: $($entries.Count) source/support files, $($evidence.Count) evidence hashes, $($baseline.Count) baseline files intact."
$differences | Group-Object -Property {$_.change} | Select-Object Name,Count
