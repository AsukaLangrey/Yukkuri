function Confirm-YukkuriEngineProvenance([string]$Directory,$BuildInfo) {
    if($BuildInfo.diagnostic_only){throw 'Diagnostic engine experiments are not eligible for delivery packaging.'}
    if(!$BuildInfo.engine_candidate){return}
    foreach($notice in @('LLVM-LICENSE.txt','MinGW-w64-COPYING.MinGW-w64-runtime.txt','MinGW-w64-COPYING.winpthreads.txt','Custom-Engine-Notices.txt')){
        if(!(Test-Path -LiteralPath (Join-Path $Directory ('third-party/'+$notice)) -PathType Leaf)){throw "Custom runtime notice missing: $notice"}
    }
    $provenance=Join-Path $Directory 'engine-provenance'
    foreach($name in @('README.txt','godot-source-provenance.json','llvm-provenance.json','build-request.json','build-result.json','asan-only-source.patch','original-allocator-headers.json','binary-identity.json')){
        if(!(Test-Path -LiteralPath (Join-Path $provenance $name) -PathType Leaf)){throw "Custom engine provenance missing: $name"}
    }
    $identity=Get-Content -LiteralPath (Join-Path $provenance 'binary-identity.json') -Raw | ConvertFrom-Json
    if($identity.runtime_sha256 -ne $BuildInfo.engine_candidate.runtime_sha256 -or
       $identity.symbol_binary_sha256 -ne $BuildInfo.engine_candidate.symbol_binary_sha256 -or
       (Get-FileHash -LiteralPath (Join-Path $Directory 'Yukkuri.exe')).Hash -ne $identity.runtime_sha256){throw 'Custom engine provenance does not identify this binary.'}
    $result=Get-Content -LiteralPath (Join-Path $provenance 'build-result.json') -Raw | ConvertFrom-Json
    if($result.exit_code -ne 0){throw 'Custom engine build was not successful.'}
}
