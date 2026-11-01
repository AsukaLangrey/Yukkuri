param([ValidateRange(600,3600)][int]$Seconds=1800,[switch]$ContinuousBreakpoint)
. "$PSScriptRoot/env.ps1"
. "$PSScriptRoot/build-identity.ps1"
$candidate=Join-Path $ProjectRoot 'builds/candidates/m01-care-polish'
$info=Get-Content (Join-Path $candidate 'build-info.json') -Raw | ConvertFrom-Json
if($info.build_id -ne 'M01-20261027-033258'){throw 'This diagnostic is pinned to the observed official binary.'}
Confirm-YukkuriPayload $candidate $info.payload
$out=Join-Path $ProjectRoot ('artifacts/m01/native-writer-'+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory $out | Out-Null
$compiler=Join-Path $ProjectRoot '.tools/engine-diagnostics/llvm-mingw-20261020-ucrt-x86_64/bin/clang++.exe'
$helper=Join-Path $out 'watch-render-list.exe'
$source=Join-Path $out 'watch-render-list.cpp'
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'native/watch-render-list.cpp') -Destination $source
& $compiler -std=c++17 -O2 -static -municode $source -ldbghelp -o $helper
if($LASTEXITCODE -ne 0){throw 'Writer watcher build failed.'}
$args='--log-file "'+(Join-Path $out 'run.log')+'" -- --smoke-output "'+(Join-Path $out 'screen.png')+'" --mute --m01-soak --m01-soak-seconds '+$Seconds
[ordered]@{build_id=$info.build_id;started_at=[DateTimeOffset]::Now.ToString('o');seconds=$Seconds;continuous_breakpoint=[bool]$ContinuousBreakpoint;scope='Diagnostic only: own child validated MOV breakpoint and hardware write watchpoints on render-list first element and pointer field. Default restores MOV after first hit; optional continuous mode emulates it each frame. Changes timing, never acceptance evidence.';source_sha256=(Get-FileHash $source).Hash} | ConvertTo-Json | Set-Content (Join-Path $out 'watch-request.json')
Write-Output "Writer diagnostic: $out"
$helperArguments=@((Join-Path $candidate 'Yukkuri.exe'),$out,($Seconds+120),$args)
if($ContinuousBreakpoint){$helperArguments+='continuous'}
& $helper @helperArguments
$code=$LASTEXITCODE
$events=Get-Content (Join-Path $out 'watch-events.txt') -Raw
[ordered]@{helper_exit=$code;ended_at=[DateTimeOffset]::Now.ToString('o');writer_captured=($events -match 'capture_reason=misaligned_pointer_written');dump_saved=($events -match 'dump_saved=1 error=0')} | ConvertTo-Json | Set-Content (Join-Path $out 'watch-result.json')
Get-Content (Join-Path $out 'watch-events.txt') -Tail 4
exit $code
