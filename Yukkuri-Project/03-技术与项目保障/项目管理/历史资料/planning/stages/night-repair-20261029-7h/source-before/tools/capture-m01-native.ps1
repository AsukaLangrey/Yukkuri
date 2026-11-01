param([string]$ExportDirectory='builds/candidates/m01-longplay',[ValidateRange(600,7200)][int]$Seconds=3600)
. "$PSScriptRoot/env.ps1"
. "$PSScriptRoot/build-identity.ps1"
$candidate=[IO.Path]::GetFullPath((Join-Path $ProjectRoot $ExportDirectory))
if(!$candidate.StartsWith($ProjectRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Capture only a workspace candidate.'}
$info=Get-Content -LiteralPath (Join-Path $candidate 'build-info.json') -Raw | ConvertFrom-Json
Confirm-YukkuriPayload $candidate $info.payload
$out=Join-Path $ProjectRoot ('artifacts/m01/native-capture-'+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory $out | Out-Null
$compiler=Join-Path $ProjectRoot '.tools/engine-diagnostics/llvm-mingw-20261020-ucrt-x86_64/bin/clang++.exe'
$debugger=Join-Path $out 'capture-game-crash.exe'
& $compiler -std=c++17 -O2 -static -municode (Join-Path $PSScriptRoot 'native/capture-game-crash.cpp') -ldbghelp -o $debugger
if($LASTEXITCODE -ne 0){throw 'Native capture helper build failed.'}
$exe=Join-Path $candidate 'Yukkuri.exe'
$arguments='--log-file "'+(Join-Path $out 'run.log')+'" -- --smoke-output "'+(Join-Path $out 'screen.png')+'" --mute --m01-soak --m01-soak-seconds '+$Seconds
[ordered]@{build_id=$info.build_id;started_at=[DateTimeOffset]::Now.ToString('o');executable=$exe;seconds=$Seconds;arguments=$arguments;scope='Child-process native debugger; no system debugger registration. Diagnostic run, not release acceptance.'} |
    ConvertTo-Json | Set-Content (Join-Path $out 'capture-request.json')
Write-Output "Native capture: $out"
& $debugger $exe $out ($Seconds+120) $arguments
$exitCode=$LASTEXITCODE
Get-Content (Join-Path $out 'debug-events.txt') -Tail 15
$captureEvents=Get-Content -LiteralPath (Join-Path $out 'debug-events.txt') -Raw
[ordered]@{helper_exit=$exitCode;ended_at=[DateTimeOffset]::Now.ToString('o');dump_exists=(Test-Path (Join-Path $out 'crash.dmp'));dump_saved=($captureEvents -match '(?m)^dump_saved=1 error=0');context_captured=($captureEvents -match '(?m)^context_ok=1')} |
    ConvertTo-Json | Set-Content (Join-Path $out 'capture-result.json')
exit $exitCode
