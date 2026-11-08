$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$engine=Join-Path $root '.tools/engine-diagnostics/godot-4.7.2-stable'
$env:PATH=(Join-Path $root '.tools/engine-diagnostics/llvm-mingw-20261020-ucrt-x86_64/bin')+';'+$env:PATH
$env:PYTHONPATH=Join-Path $root '.tools/engine-diagnostics/python-packages'
$python=Join-Path $root '.tools/blender/blender-4.5.15-windows-x64/4.5/python/bin/python.exe'
$out=Join-Path $root ('artifacts/m01/renderer-probe/clang-template-'+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $out | Out-Null
# Separate optimized runtime candidate, retaining symbols for failure diagnosis.
# Existing ASan-only source hooks are compiled out because use_asan=no.
$buildArguments=@('-m','SCons','platform=windows','arch=x86_64','target=template_release','use_mingw=yes','use_llvm=yes','use_asan=no','dev_build=no','debug_symbols=yes','optimize=speed','d3d12=no','accesskit=no','module_mono_enabled=yes','-j8')
[ordered]@{source_tag='4.7.2-stable';source_provenance='artifacts/m01/renderer-probe/godot-source-provenance.json';toolchain='llvm-mingw-20261020-ucrt-x86_64';arguments=$buildArguments;started_at=[DateTimeOffset]::Now.ToString('o');purpose='Unvalidated compiler/runtime candidate, not the default shipping engine'} | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $out 'build-request.json')
Write-Output "Candidate runtime build: $out"
Push-Location $engine
try{
    & $python @buildArguments *> (Join-Path $out 'build.log')
    $code=$LASTEXITCODE
    [ordered]@{exit_code=$code;ended_at=[DateTimeOffset]::Now.ToString('o')} | ConvertTo-Json | Set-Content (Join-Path $out 'build-result.json')
    Get-Content (Join-Path $out 'build.log') -Tail 12
    if($code -ne 0){throw "Runtime candidate build failed: $out"}
}finally{Pop-Location}
