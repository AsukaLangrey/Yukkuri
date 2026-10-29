$ErrorActionPreference='Stop'
. "$PSScriptRoot/env.ps1"
$diag=Join-Path $ProjectRoot '.tools/engine-diagnostics'
$archive=Join-Path $diag 'llvm-mingw-20261020-msvcrt-x86_64.zip'
$url='https://github.com/mstorsjo/llvm-mingw/releases/download/20261020/llvm-mingw-20261020-msvcrt-x86_64.zip'
$expected='52A3E3F584A74C3CDE8CEB0C6960549683178E03B36818CB02B7ED109A68FF67'
$out=Join-Path $ProjectRoot ('artifacts/m01/renderer-probe/msvcrt-comparison-'+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $out | Out-Null
[ordered]@{url=$url;sha256=$expected;hash_source='GitHub release asset digest, retrieved 2026-10-27';scope='Isolated compiler CRT comparison; not a shipping engine or root-cause fix'}|ConvertTo-Json|Set-Content (Join-Path $out 'toolchain-provenance.json')
if(!(Test-Path -LiteralPath $archive)){
    & curl.exe --fail --location --retry 2 --connect-timeout 20 --max-time 1200 --silent --show-error --output "$archive.partial" $url
    if($LASTEXITCODE -ne 0){throw 'Comparison toolchain download failed.'}
    if((Get-FileHash -LiteralPath "$archive.partial").Hash -ne $expected){throw 'Comparison toolchain hash mismatch.'}
    Move-Item -LiteralPath "$archive.partial" -Destination $archive
}
if((Get-FileHash -LiteralPath $archive).Hash -ne $expected){throw 'Cached comparison toolchain hash mismatch.'}
$compiler=Join-Path $diag 'llvm-mingw-20261020-msvcrt-x86_64'
if(!(Test-Path -LiteralPath $compiler)){
    & tar.exe -xf $archive -C $diag
    if($LASTEXITCODE -ne 0){throw 'Comparison toolchain extraction failed.'}
}
$sourceArchive=Join-Path $diag 'godot-4.7.2-stable.zip'
$provenance=Get-Content (Join-Path $ProjectRoot 'assets-source/engine-provenance/godot-source-provenance.json') -Raw|ConvertFrom-Json
if((Get-FileHash -LiteralPath $sourceArchive).Hash -ne $provenance.sha256){throw 'Official source archive hash mismatch.'}
$comparisonRoot=Join-Path $diag 'msvcrt-comparison'
if(Test-Path -LiteralPath $comparisonRoot){throw 'Comparison source already exists; preserve this independent experiment.'}
New-Item -ItemType Directory -Path $comparisonRoot | Out-Null
& tar.exe -xf $sourceArchive -C $comparisonRoot
if($LASTEXITCODE -ne 0){throw 'Comparison source extraction failed.'}
$engine=Join-Path $comparisonRoot 'godot-4.7.2-stable'
$env:PATH=(Join-Path $compiler 'bin')+';'+$env:PATH
$env:PYTHONPATH=Join-Path $diag 'python-packages'
$python=Join-Path $ProjectRoot '.tools/blender/blender-4.5.15-windows-x64/4.5/python/bin/python.exe'
$arguments=@('-m','SCons','platform=windows','arch=x86_64','target=template_release','use_mingw=yes','use_llvm=yes','use_asan=no','dev_build=no','debug_symbols=yes','optimize=speed','d3d12=no','accesskit=no','module_mono_enabled=yes','-j6')
[ordered]@{started_at=[DateTimeOffset]::Now.ToString('o');source=$engine;source_archive_sha256=$provenance.sha256;source_patch='None; original official source';compiler=$compiler;arguments=$arguments;comparison='Same LLVM release and listed build flags as UCRT candidate, MSVCRT toolchain. UCRT source has ASAN-only hooks disabled in this release; this remains a controlled diagnostic comparison, not proof of identical generated code.'}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $out 'build-request.json')
Write-Output "Comparison engine build: $out"
Push-Location $engine
try{
    & $python @arguments *> (Join-Path $out 'build.log')
    $code=$LASTEXITCODE
    [ordered]@{ended_at=[DateTimeOffset]::Now.ToString('o');exit_code=$code}|ConvertTo-Json|Set-Content (Join-Path $out 'build-result.json')
    Get-Content (Join-Path $out 'build.log') -Tail 10
    if($code -ne 0){throw 'Comparison engine build failed.'}
}finally{Pop-Location}
