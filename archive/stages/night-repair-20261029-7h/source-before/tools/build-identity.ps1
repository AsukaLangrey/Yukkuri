# Shared by export, runtime verification and packaging. These hashes establish
# which files were tested; they do not establish gameplay quality or acceptance.
function Get-YukkuriSourceIdentity([string]$Root) {
    $files=@()
    foreach($directory in @('src/Yukkuri.Game','src/Yukkuri.Simulation')) {
        $files+=Get-ChildItem -LiteralPath (Join-Path $Root $directory) -File -Recurse |
            Where-Object {$_.FullName -notmatch '[\\/](\.godot|bin|obj)[\\/]'}
    }
    $files+=Get-Item -LiteralPath (Join-Path $Root 'toolchain.lock.json')
    $entries=@($files | Sort-Object FullName | ForEach-Object {
        [ordered]@{path=$_.FullName.Substring($Root.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
    })
    $canonical=($entries | ForEach-Object { $_.path+' '+$_.sha256 }) -join "`n"
    $hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($canonical))).ToLowerInvariant()
    return [ordered]@{sha256=$hash;files=$entries}
}
function Get-YukkuriPayloadIdentity([string]$Directory) {
    return @(Get-ChildItem -LiteralPath $Directory -File -Recurse |
        Where-Object {$_.Extension -in '.exe','.dll','.pck','.dat','.bin' -or $_.Name -like '*.deps.json' -or $_.Name -like '*.runtimeconfig.json'} |
        Sort-Object FullName | ForEach-Object {
            [ordered]@{path=$_.FullName.Substring($Directory.Length+1).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
        })
}
function Confirm-YukkuriPayload([string]$Directory,$Expected) {
    if(@($Expected).Count -eq 0){throw 'Build payload manifest is empty.'}
    foreach($entry in $Expected) {
        $target=[IO.Path]::GetFullPath((Join-Path $Directory $entry.path))
        if(-not $target.StartsWith([IO.Path]::GetFullPath($Directory)+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) {throw 'Payload path escapes build directory.'}
        if(-not (Test-Path -LiteralPath $target -PathType Leaf) -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $entry.sha256) {throw "Build payload changed: $($entry.path)"}
    }
}
