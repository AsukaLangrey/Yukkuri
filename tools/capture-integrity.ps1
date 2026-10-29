function Get-M01CaptureIntegrity([string]$EvidenceDirectory) {
    $project=Split-Path $PSScriptRoot -Parent
    $python=Join-Path $project '.tools/blender/blender-4.5.15-windows-x64/4.5/python/bin/python.exe'
    $report=Join-Path $EvidenceDirectory 'capture-integrity.json'
    & $python (Join-Path $PSScriptRoot 'audit-png-captures.py') $EvidenceDirectory --output $report | Out-Host
    if($LASTEXITCODE -ne 0){throw 'Capture integrity audit did not execute successfully.'}
    return Get-Content -LiteralPath $report -Raw|ConvertFrom-Json
}
