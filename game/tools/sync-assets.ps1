$ErrorActionPreference = 'Stop'
function Get-AssetSha256([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '')
    } finally {
        $algorithm.Dispose()
        $stream.Dispose()
    }
}
$project = Split-Path $PSScriptRoot -Parent
$repository = Split-Path $project -Parent
$manifest = Get-Content -LiteralPath (Join-Path $repository 'assets/manifests/runtime-assets.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or !$manifest.files) { throw 'Invalid runtime asset manifest.' }

$targets = @{}
foreach ($entry in $manifest.files) {
    if ($entry.source -notmatch '^assets/' -or
        $entry.target -notmatch '^src/Yukkuri\.Game/(Assets/|ArtPreview/Textures/)' -or
        ($entry.source + '/' + $entry.target) -match '(^|/)\.\.?(/|$)|[\\:]' -or
        $targets.ContainsKey($entry.target)) {
        throw "Invalid or duplicate asset mapping: $($entry.source) -> $($entry.target)"
    }
    $source = Join-Path $repository $entry.source
    if (!(Test-Path -LiteralPath $source -PathType Leaf) -or
        (Get-AssetSha256 $source) -ine $entry.sha256) {
        throw "Missing or changed asset: $($entry.source). Restore the Git LFS payload or update the manifest for an intentional asset change."
    }
    $targets[$entry.target] = $entry
}

# Preserve Godot-generated sidecars and reject obsolete payloads before importing.
foreach ($directory in @('src/Yukkuri.Game/Assets','src/Yukkuri.Game/ArtPreview/Textures')) {
    $root = Join-Path $project $directory
    if (!(Test-Path -LiteralPath $root)) { continue }
    foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File -Force) {
        $relative = $file.FullName.Substring($project.Length + 1).Replace('\','/')
        if (!$targets.ContainsKey($relative) -and
            !($relative -match '\.(import|uid)$' -and $targets.ContainsKey(($relative -replace '\.(import|uid)$','')))) {
            throw "Obsolete generated asset: $relative. Clear the generated asset directories and run sync again."
        }
    }
}
foreach ($entry in $manifest.files) {
    $source = Join-Path $repository $entry.source
    $target = Join-Path $project $entry.target
    if ((Test-Path -LiteralPath $target -PathType Leaf) -and
        (Get-AssetSha256 $target) -ieq $entry.sha256) { continue }
    New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
}
Write-Output "Runtime assets verified: $($manifest.files.Count)"