param(
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$SourceArchive='.tools/engine-diagnostics/godot-4.7.2-stable.zip',
    [string]$CompilerArchive='.tools/engine-diagnostics/llvm-mingw-20261020-ucrt-x86_64.zip',
    [string]$PythonPath='.tools/blender/blender-4.5.15-windows-x64/4.5/python/bin/python.exe',
    [string]$SConsPackages='.tools/engine-diagnostics/python-packages',
    [long]$ProcessAffinityMask=0,
    [switch]$CheckOnly
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
function Local([string]$relative){
    $full=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(!$full.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Paths must remain inside this workspace.'}
    return $full
}
$out=Local $OutputDirectory
if(Test-Path -LiteralPath $out){throw 'Use a new output directory; previous builds are preserved.'}
$sourceZip=Local $SourceArchive
$compilerZip=Local $CompilerArchive
$python=Local $PythonPath
$packages=Local $SConsPackages
$provenance=Join-Path $root 'assets-source/engine-provenance'
foreach($pair in @(@($sourceZip,'godot-source-provenance.json'),@($compilerZip,'llvm-provenance.json'))){
    $expected=Get-Content -LiteralPath (Join-Path $provenance $pair[1]) -Raw|ConvertFrom-Json
    if((Get-FileHash -LiteralPath $pair[0] -Algorithm SHA256).Hash -ne $expected.sha256){throw "Archive checksum mismatch: $($pair[0])"}
}
if(!(Test-Path -LiteralPath $python -PathType Leaf)){throw 'Python runtime missing; run setup first.'}
$oldPythonPath=$env:PYTHONPATH
$oldPath=$env:PATH
try{
    $env:PYTHONPATH=$packages
    $version=& $python -c 'import SCons; print(SCons.__version__)'
    if($LASTEXITCODE -ne 0 -or "$version".Trim() -ne '4.11.1'){throw 'SCons 4.11.1 is required in the specified package directory.'}
    . (Join-Path $PSScriptRoot 'process-affinity.ps1')
    $available=Get-YukkuriProcessAffinity ([Diagnostics.Process]::GetCurrentProcess())
    if($ProcessAffinityMask -lt 0 -or ($ProcessAffinityMask -and ($ProcessAffinityMask -band [long]$available.system_mask) -ne $ProcessAffinityMask)){throw 'Invalid build-process affinity.'}
    if($CheckOnly){[ordered]@{validated=$true;started=$false;output=$out;source_archive=$sourceZip;compiler_archive=$compilerZip;scons="$version".Trim();requested_build_affinity=$ProcessAffinityMask}|ConvertTo-Json;return}
    New-Item -ItemType Directory -Path $out|Out-Null
    $sourceParent=Join-Path $out 'source'
    $compilerParent=Join-Path $out 'compiler'
    New-Item -ItemType Directory -Path $sourceParent,$compilerParent|Out-Null
    & tar.exe -xf $sourceZip -C $sourceParent
    if($LASTEXITCODE -ne 0){throw 'Source extraction failed.'}
    & tar.exe -xf $compilerZip -C $compilerParent
    if($LASTEXITCODE -ne 0){throw 'Compiler extraction failed.'}
    $engine=Join-Path $sourceParent 'godot-4.7.2-stable'
    $compiler=Join-Path $compilerParent 'llvm-mingw-20261020-ucrt-x86_64/bin'
    $env:PATH=$compiler+';'+$oldPath
    $arguments=@('-m','SCons','platform=windows','arch=x86_64','target=template_release','use_mingw=yes','use_llvm=yes','use_asan=no','dev_build=no','debug_symbols=yes','optimize=speed','d3d12=no','accesskit=no','module_mono_enabled=yes','-j8')
    $request=[ordered]@{started_at=[DateTimeOffset]::Now.ToString('o');source_archive_sha256=(Get-FileHash $sourceZip).Hash;compiler_archive_sha256=(Get-FileHash $compilerZip).Hash;source='Fresh verified official archive; no local patches';arguments=$arguments;python=$python;python_sha256=(Get-FileHash $python).Hash;scons_version="$version".Trim();requested_build_affinity=$ProcessAffinityMask;scope='Independent native rebuild; no game release replacement or runtime acceptance'}
    $request|ConvertTo-Json -Depth 6|Set-Content (Join-Path $out 'build-request.json') -Encoding UTF8
    $p=Start-Process -FilePath $python -ArgumentList $arguments -WorkingDirectory $engine -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $out 'build.log') -RedirectStandardError (Join-Path $out 'stderr.log')
    try{
        $handle=$p.Handle
        if($ProcessAffinityMask){$p.ProcessorAffinity=[IntPtr]$ProcessAffinityMask;if($p.ProcessorAffinity.ToInt64() -ne $ProcessAffinityMask){throw 'Build-process affinity was not applied.'}}
        $affinity=Get-YukkuriProcessAffinity $p
        [ordered]@{pid=$p.Id;started_at=$p.StartTime.ToString('o');executable=$python;process_affinity=$affinity;scope='Build process and inherited compiler children only'}|ConvertTo-Json -Depth 4|Set-Content (Join-Path $out 'launch.json') -Encoding UTF8
        Write-Output "Independent native rebuild: $out (PID $($p.Id))"
        $p.WaitForExit()
        $result=[ordered]@{exit_code=$p.ExitCode;ended_at=[DateTimeOffset]::Now.ToString('o');runtime_verified=$false}
        $result|ConvertTo-Json|Set-Content (Join-Path $out 'build-result.json') -Encoding UTF8
        if($p.ExitCode -ne 0){throw "Native rebuild failed; inspect $out"}
    }catch{
        if(!$p.HasExited){$p.Kill($true)}
        throw
    }
    $symbol=Join-Path $engine 'bin/godot.windows.template_release.x86_64.llvm.mono.exe'
    $runtime=Join-Path $out 'Yukkuri-engine.exe'
    & (Join-Path $compiler 'llvm-objcopy.exe') --strip-debug $symbol $runtime
    if($LASTEXITCODE -ne 0){throw 'Symbol stripping failed.'}
    [ordered]@{symbol_binary_sha256=(Get-FileHash $symbol).Hash;runtime_sha256=(Get-FileHash $runtime).Hash;source_tag='4.7.2-stable';build_request='build-request.json';scope='Actual independent build identity; not the earlier candidate identity'}|ConvertTo-Json|Set-Content (Join-Path $out 'binary-identity.json') -Encoding UTF8
    Write-Output "Native rebuild complete: $out"
}finally{$env:PYTHONPATH=$oldPythonPath;$env:PATH=$oldPath}
