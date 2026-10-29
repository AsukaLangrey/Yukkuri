param([ValidateRange(600,28800)][int]$SoakSeconds=600,[long]$ProcessAffinityMask=0,[switch]$Exported,[string]$ExportDirectory='builds/windows',[ValidatePattern('^\d{3,4}x\d{3,4}$')][string]$Resolution,[switch]$Preview,[switch]$Stress,[switch]$Soak,[switch]$Audio,[switch]$States,[switch]$AutoSave,[switch]$VerboseEngine,[switch]$NoShadows,[switch]$NoNameplates,[switch]$SimpleActors,[switch]$NoAccessibility,[switch]$MergeActors,[ValidateSet('default','safe','separate')][string]$RenderThread='default',[ValidateSet('default','vulkan','d3d12','opengl3')][string]$RenderDriver='default',[ValidateSet('default','mobile','forward_plus','gl_compatibility')][string]$Renderer='default')
. "$PSScriptRoot/env.ps1"
. "$PSScriptRoot/build-identity.ps1"
. "$PSScriptRoot/process-affinity.ps1"
. "$PSScriptRoot/capture-integrity.ps1"
$stamp=[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss-fff')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$tag=if($Exported){'exported'}else{'development'}
if($Preview){$tag+='-preview'}
if($Stress){$tag+='-stress'}
if($Soak){$tag+='-soak'}
if($Audio){$tag+='-audio'}
if($States){$tag+='-states'}
if($AutoSave){$tag+='-autosave'}
if($Resolution){$tag+='-'+$Resolution}
$evidence=Join-Path $ProjectRoot "artifacts/m01/$tag-$stamp"
New-Item -ItemType Directory -Path $evidence | Out-Null
$log=Join-Path $evidence 'run.log'
$screenshot=Join-Path $evidence 'screen.png'
$exportRoot=[IO.Path]::GetFullPath((Join-Path $ProjectRoot $ExportDirectory))
if($Exported -and !$exportRoot.StartsWith($ProjectRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Export verification must stay in the workspace.'}
$exe=if($Exported){Join-Path $exportRoot 'Yukkuri.exe'}else{$GodotExe}
$launch=@('--path',('"'+$GameRoot+'"'),'--log-file',('"'+$log+'"'),'--','--smoke-output',('"'+$screenshot+'"'),'--mute')
if($Resolution){$launch=@('--resolution',$Resolution)+$launch}
if($VerboseEngine){$launch=@('--verbose')+$launch}
if($Renderer -ne 'default'){$launch=@('--rendering-method',$Renderer)+$launch}
if($RenderThread -ne 'default'){$launch=@('--render-thread',$RenderThread)+$launch}
if($RenderDriver -ne 'default'){$launch=@('--rendering-driver',$RenderDriver)+$launch}
if($NoAccessibility){$launch=@('--accessibility','disabled')+$launch}
if(-not $Preview){$launch+=if($AutoSave){'--m01-autosave'}elseif($States){'--m01-states'}elseif($Audio){'--m01-audio'}elseif($Soak){'--m01-soak'}elseif($Stress){'--m01-stress'}else{'--m01-integration'}}
if($Soak){$launch+=@('--m01-soak-seconds',"$SoakSeconds")}
if($NoShadows){$launch+='--no-shadows'}
if($NoNameplates){$launch+='--no-nameplates'}
if($SimpleActors){$launch+='--simple-actors'}
if($MergeActors){$launch+='--merge-actor'}
if($Exported){
    $buildInfo=Join-Path $exportRoot 'build-info.json'
    if(!(Test-Path -LiteralPath $buildInfo)){throw 'M01 export build-info.json is required; old framework export is not eligible.'}
    $buildRecord=Get-Content -LiteralPath $buildInfo -Raw | ConvertFrom-Json
    if($buildRecord.stage -ne 'M01'){throw 'Export does not identify M01.'}
    Confirm-YukkuriPayload $exportRoot $buildRecord.payload
    $launch=$launch | Where-Object {$_ -ne '--path' -and $_ -ne ('"'+$GameRoot+'"')}
    $env:PATH=($env:PATH -split ';' | Where-Object {$_ -notlike "$ProjectRoot\.tools*" -and $_ -notlike "$ProjectRoot/.tools*"}) -join ';'
    $env:DOTNET_ROOT=$null;$env:DOTNET_ROOT_X64=$null
}
if($ProcessAffinityMask -lt 0){throw 'Process affinity must be zero (unchanged) or a positive mask.'}
$started=Get-Date
$stderr=Join-Path $evidence 'stderr.log'
$p=Start-Process -FilePath $exe -ArgumentList $launch -WorkingDirectory (Split-Path $exe) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $evidence 'stdout.log') -RedirectStandardError $stderr
$effectiveAffinity=$null
if($ProcessAffinityMask){
    try{$p.ProcessorAffinity=[IntPtr]$ProcessAffinityMask;$effectiveAffinity=$p.ProcessorAffinity.ToInt64();if($effectiveAffinity -ne $ProcessAffinityMask){throw 'Affinity did not match request.'}}
    catch{if(!$p.HasExited){$p.Kill();$p.WaitForExit()};throw}
}
$affinity=Get-YukkuriProcessAffinity $p
$effectiveAffinity=$affinity.process_mask
[ordered]@{pid=$p.Id;executable=$exe;started_at=$started.ToString('o');arguments=$launch;process_affinity_mask=$effectiveAffinity;system_affinity_mask=$affinity.system_mask;build_id=$(if($Exported){$buildRecord.build_id}else{$null})} |
    ConvertTo-Json -Depth 4 | Set-Content (Join-Path $evidence 'launch.json') -Encoding utf8
Write-Output "M01 verification running: $evidence"
$deadline=(Get-Date).AddSeconds($(if($Soak){$SoakSeconds+180}else{180}))
$timedOut=$false
while(-not $p.WaitForExit(1000)){
    if((Get-Date) -gt $deadline){$timedOut=$true;$p.Kill();$p.WaitForExit();break}
}
$content=if(Test-Path -LiteralPath $log){[string](Get-Content -LiteralPath $log -Raw)}else{''}
$nativeErrors=if(Test-Path -LiteralPath $stderr){[string](Get-Content -LiteralPath $stderr -Raw)}else{''}
[ordered]@{pid=$p.Id;exit_code=$p.ExitCode;ended_at=[DateTimeOffset]::Now.ToString('o');timed_out=$timedOut;run_log_characters=$content.Length;stderr_characters=$nativeErrors.Length} |
    ConvertTo-Json | Set-Content (Join-Path $evidence 'process-result.json') -Encoding utf8
if(Test-Path -LiteralPath $log){Get-Content -LiteralPath $log -Tail 30}
Write-Output "Process exit: $($p.ExitCode); timeout=$timedOut"
Write-Output ("Engine error lines: "+([regex]::Matches($content,'(?m)^ERROR:')).Count)
if($timedOut){throw "M01 integration timed out: $log"}
if($p.ExitCode -ne 0 -or $content -match '(?m)^ERROR:' -or $nativeErrors -match '(?m)^ERROR:|ERROR: AddressSanitizer|runtime error:'){throw "M01 failed: exit=$($p.ExitCode); $log; native stderr=$stderr"}
if(-not $Preview){
    $result=Join-Path $evidence 'm01-integration-result.json'
    if($content -notmatch 'M01_INTEGRATION_COMPLETE' -or !(Test-Path $result) -or (Get-Item $result).LastWriteTime -lt $started){throw 'Missing or stale M01 result.'}
    if(-not (Get-Content $result -Raw | ConvertFrom-Json).success){throw 'M01 result reports failure.'}
    if($Soak -and (Get-Content $result -Raw | ConvertFrom-Json).wallSeconds -lt $SoakSeconds){throw 'Soak did not reach the requested actual duration.'}
}elseif(!(Test-Path $screenshot) -or (Get-Item $screenshot).LastWriteTime -lt $started){throw 'Missing fresh M01 preview.'}
$metadata=[ordered]@{
    stage='M01';exported=[bool]$Exported;started_at=$started.ToString('o');ended_at=[DateTimeOffset]::Now.ToString('o')
    kind=$(if($Preview){'preview'}elseif($AutoSave){'autosave'}elseif($States){'states'}elseif($Audio){'audio'}elseif($Soak){'soak'}elseif($Stress){'stress'}else{'integration'})
    exit_code=$p.ExitCode;renderer_override=$Renderer;success=$true;native_stderr_checked=$true
    diagnostic_flags=@(@($(if($NoShadows){'no-shadows'}),$(if($NoNameplates){'no-nameplates'}),$(if($SimpleActors){'simple-actors'}),$(if($MergeActors){'merge-actor'}),$(if($NoAccessibility){'no-accessibility'}),$(if($RenderThread -ne 'default'){"render-thread=$RenderThread"}),$(if($RenderDriver -ne 'default'){"render-driver=$RenderDriver"})) | Where-Object {$_})
    build_id=$(if($Exported){$buildRecord.build_id}else{$null})
    build_info_sha256=$(if($Exported){(Get-FileHash -LiteralPath $buildInfo -Algorithm SHA256).Hash}else{$null})
    executable=$exe
    requested_resolution=$(if($Resolution){$Resolution}else{'default'})
    requested_soak_seconds=$(if($Soak){$SoakSeconds}else{$null})
}
if($Exported -and $buildRecord.diagnostic_only){$metadata.diagnostic_flags+='diagnostic-engine-experiment'}
if($ProcessAffinityMask){$metadata.diagnostic_flags+="process-affinity=$ProcessAffinityMask";$metadata.process_affinity_mask=$effectiveAffinity}
elseif($affinity.process_mask -ne $affinity.system_mask){$metadata.diagnostic_flags+="inherited-process-affinity=$effectiveAffinity"}
$metadata.process_affinity_mask=$effectiveAffinity
$metadata.system_affinity_mask=$affinity.system_mask
$capture=Get-M01CaptureIntegrity $evidence
$metadata.capture_integrity=[ordered]@{files=$capture.files;valid_files=$capture.valid_files;invalid_files=$capture.invalid_files;scope='Separate PNG integrity result; success above describes runtime assertions only, not visual acceptance.'}
if($capture.invalid_files -gt 0){Write-Warning "Runtime assertions passed but $($capture.invalid_files) PNG captures are invalid. Preserve them and do not use them for visual acceptance."}
if($Exported){Confirm-YukkuriPayload $exportRoot $buildRecord.payload}
$metadata | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $evidence 'verification-metadata.json') -Encoding utf8
$evidence | Set-Content (Join-Path $ProjectRoot 'artifacts/m01/LATEST-VERIFICATION.txt')



