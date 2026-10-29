param([ValidateSet('integration','stress','soak')][string]$Kind='stress',[ValidateRange(600,7200)][int]$SoakSeconds=600,[ValidateSet('mobile','gl_compatibility')][string]$Renderer='mobile')
. "$PSScriptRoot/env.ps1"
$llvm=Join-Path $ProjectRoot '.tools/engine-diagnostics/llvm-mingw-20261020-ucrt-x86_64/bin'
$env:PATH=$llvm+';'+$env:PATH
$env:ASAN_SYMBOLIZER_PATH=Join-Path $llvm 'llvm-symbolizer.exe'
$env:ASAN_OPTIONS='halt_on_error=1:detect_leaks=0'
$engine=Join-Path $ProjectRoot '.tools/engine-diagnostics/godot-4.7.2-stable/bin/godot.windows.editor.dev.x86_64.llvm.san.mono.exe'
$out=Join-Path $ProjectRoot ('artifacts/m01/asan-game-'+$Kind+'-'+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory $out | Out-Null
$args=@('--path',$GameRoot,'--rendering-method',$Renderer,'--log-file',(Join-Path $out 'run.log'),'--','--smoke-output',(Join-Path $out 'screen.png'),'--mute',"--m01-$Kind")
if($Kind -eq 'soak'){$args+=@('--m01-soak-seconds',"$SoakSeconds")}
$p=Start-Process -FilePath $engine -ArgumentList $args -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $out 'stdout.log') -RedirectStandardError (Join-Path $out 'stderr.log')
[ordered]@{pid=$p.Id;renderer=$Renderer;kind=$Kind;engine=$engine;started_at=[DateTimeOffset]::Now.ToString('o')} | ConvertTo-Json | Set-Content (Join-Path $out 'launch.json')
Write-Output "ASAN game pid=$($p.Id) evidence=$out"
$deadline=[DateTimeOffset]::Now.AddSeconds($(if($Kind -eq 'soak'){$SoakSeconds+180}else{300}))
while(!$p.WaitForExit(1000)){if([DateTimeOffset]::Now -gt $deadline){$p.Kill($true);throw 'ASAN game timeout'}}
[ordered]@{exit_code=$p.ExitCode;ended_at=[DateTimeOffset]::Now.ToString('o')} | ConvertTo-Json | Set-Content (Join-Path $out 'process-result.json')
Get-Content (Join-Path $out 'stderr.log') -Tail 80
Get-Content (Join-Path $out 'run.log') -Tail 12
Write-Output "ASAN game exit=$($p.ExitCode)"
if($p.ExitCode -ne 0){exit $p.ExitCode}
$log=Get-Content (Join-Path $out 'run.log') -Raw
$stderr=Get-Content (Join-Path $out 'stderr.log') -Raw
if($log -match '(?m)^ERROR:' -or $stderr -match '(?m)^ERROR:|ERROR: AddressSanitizer|runtime error:' -or $log -notmatch 'M01_INTEGRATION_COMPLETE'){throw 'ASAN game did not finish cleanly (including native stderr)'}
