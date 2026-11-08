param([int]$Seconds=120,[ValidateSet('mobile','gl_compatibility')][string]$Renderer='mobile',[string]$AsanOptions='halt_on_error=1:detect_leaks=0',[switch]$Mono)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$llvmBin=Join-Path $root '.tools/engine-diagnostics/llvm-mingw-20261020-ucrt-x86_64/bin'
$env:PATH=$llvmBin+';'+$env:PATH
$env:ASAN_SYMBOLIZER_PATH=Join-Path $llvmBin 'llvm-symbolizer.exe'
$env:ASAN_OPTIONS=$AsanOptions
$engine=Join-Path $root '.tools/engine-diagnostics/godot-4.7.2-stable/bin/godot.windows.template_debug.dev.x86_64.llvm.san.exe'
if($Mono){
    $engine=Join-Path $root '.tools/engine-diagnostics/godot-4.7.2-stable/bin/godot.windows.editor.dev.x86_64.llvm.san.mono.exe'
    $env:DOTNET_ROOT=Join-Path $root '.tools/dotnet'
    $env:DOTNET_ROOT_X64=$env:DOTNET_ROOT
}
$out=Join-Path $root ('artifacts/m01/renderer-probe/asan-'+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $out | Out-Null
$args=@('--rendering-method',$Renderer,'--log-file',(Join-Path $out 'run.log'),'--','--duration',"$Seconds")
$p=Start-Process -FilePath $engine -ArgumentList $args -WorkingDirectory (Join-Path $root 'artifacts/m01/renderer-probe') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $out 'stdout.log') -RedirectStandardError (Join-Path $out 'stderr.log')
[ordered]@{pid=$p.Id;path=$out;renderer=$Renderer;started_at=[DateTimeOffset]::Now.ToString('o');asan_options=$AsanOptions} | ConvertTo-Json | Set-Content (Join-Path $out 'launch.json')
Write-Output "ASAN probe pid=$($p.Id) evidence=$out"
if(-not $p.WaitForExit(($Seconds+60)*1000)){$p.Kill($true);throw 'ASAN probe timeout'}
Get-Content (Join-Path $out 'stderr.log') -Tail 70
if(Test-Path (Join-Path $out 'run.log')){Get-Content (Join-Path $out 'run.log') -Tail 8}
Write-Output "ASAN process exit=$($p.ExitCode)"
exit $p.ExitCode
