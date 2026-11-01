param([ValidateSet('debug','speed')][string]$Optimize='debug',[switch]$Mono)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$engine=Join-Path $root '.tools/engine-diagnostics/godot-4.7.2-stable'
$env:PATH=(Join-Path $root '.tools/engine-diagnostics/llvm-mingw-20261020-ucrt-x86_64/bin')+';'+$env:PATH
$env:PYTHONPATH=Join-Path $root '.tools/engine-diagnostics/python-packages'
$python=Join-Path $root '.tools/blender/blender-4.5.15-windows-x64/4.5/python/bin/python.exe'
$log=Join-Path $root ('artifacts/m01/renderer-probe/diagnostic-build-'+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss')+'.log')
$target=if($Mono){'editor'}else{'template_debug'}
$monoFlag=if($Mono){'yes'}else{'no'}
$args=@('-m','SCons','platform=windows','arch=x86_64',"target=$target",'disable_path_overrides=no','use_mingw=yes','use_llvm=yes','use_asan=yes','dev_build=yes','debug_symbols=yes',"optimize=$Optimize",'d3d12=no','accesskit=no','module_text_server_adv_enabled=no',"module_mono_enabled=$monoFlag",'-j12')
Push-Location $engine
try {
    & $python @args *> $log
    $code=$LASTEXITCODE
    Get-Content -LiteralPath $log -Tail 15
    if($code -ne 0){throw "Diagnostic engine build failed: $log"}
    Write-Output "Diagnostic-only build: $log"
} finally {Pop-Location}
