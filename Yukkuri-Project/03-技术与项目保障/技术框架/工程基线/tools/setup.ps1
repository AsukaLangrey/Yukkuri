[CmdletBinding()]
param([string[]]$Package = @())
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$projectRoot = Split-Path $PSScriptRoot -Parent
$toolRoot = Join-Path $projectRoot '.tools'
$downloadRoot = Join-Path $toolRoot 'downloads'
New-Item -ItemType Directory -Force -Path $downloadRoot | Out-Null
$manifest = Get-Content -LiteralPath (Join-Path $projectRoot 'toolchain.lock.json') -Raw | ConvertFrom-Json
$selected = @($manifest.packages | Where-Object { $Package.Count -eq 0 -or $_.id -in $Package })
if ($selected.Count -eq 0) { throw 'No matching toolchain packages.' }
$jobs = @()
foreach ($entry in $selected) {
    $archive = Join-Path $downloadRoot $entry.file
    if (Test-Path -LiteralPath $archive) {
        $actual = (Get-FileHash -LiteralPath $archive -Algorithm $entry.hashAlgorithm).Hash
        if ($actual -ieq $entry.hash) { Write-Output "Verified cached package: $($entry.id)"; continue }
        throw "Checksum mismatch in cached package: $archive. Preserve it for inspection."
    }
    $partial = "$archive.partial"
    $errLog = "$archive.download.log"
    $argsList = @('--fail','--location','--retry','3','--connect-timeout','20','--max-time','1200','--silent','--show-error','--output',('"' + $partial + '"'),('"' + $entry.url + '"'))
    $process = Start-Process -FilePath 'curl.exe' -ArgumentList $argsList -WindowStyle Hidden -PassThru -RedirectStandardError $errLog
    $jobs += [PSCustomObject]@{Entry=$entry; Process=$process; Archive=$archive; Partial=$partial; ErrorLog=$errLog}
    Write-Output "Downloading $($entry.id) $($entry.version)"
}
foreach ($job in $jobs) {
    $job.Process.WaitForExit()
    if ($job.Process.ExitCode -ne 0) {
        $detail = if (Test-Path -LiteralPath $job.ErrorLog) { Get-Content -LiteralPath $job.ErrorLog -Raw } else { '' }
        throw "Download failed for $($job.Entry.id): $detail"
    }
    $actual = (Get-FileHash -LiteralPath $job.Partial -Algorithm $job.Entry.hashAlgorithm).Hash
    if ($actual -ine $job.Entry.hash) { throw "Checksum mismatch for downloaded $($job.Entry.id); it will not be extracted or run." }
    Move-Item -LiteralPath $job.Partial -Destination $job.Archive
    Write-Output "Download checksum verified: $($job.Entry.id)"
}
foreach ($entry in $selected) {
    $destination = Join-Path $toolRoot $entry.directory
    $marker = Join-Path $destination '.verified-package.json'
    if (Test-Path -LiteralPath $marker) {
        $installed = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
        if ($installed.hash -eq $entry.hash) { Write-Output "Already extracted: $($entry.id)"; continue }
        throw "Different package already exists at $destination; refusing to overwrite it."
    }
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    & tar.exe -xf (Join-Path $downloadRoot $entry.file) -C $destination
    if ($LASTEXITCODE -ne 0) { throw "Extraction failed: $($entry.id)" }
    $entry | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $marker -Encoding utf8
    Write-Output "Extracted: $($entry.id) -> $destination"
}
