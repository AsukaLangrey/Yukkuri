# Read-only evidence inventory. A completed test is not user acceptance.
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$artifactRoot=Join-Path $root 'artifacts/m01'
function ReadJson([string]$Path){if(Test-Path -LiteralPath $Path){Get-Content -LiteralPath $Path -Raw|ConvertFrom-Json}else{$null}}
$runs=@()
foreach($dir in Get-ChildItem -LiteralPath $artifactRoot -Directory){
    $meta=ReadJson (Join-Path $dir.FullName 'verification-metadata.json')
    $launch=ReadJson (Join-Path $dir.FullName 'launch.json')
    $failure=ReadJson (Join-Path $dir.FullName 'failure-report.json')
    $interruption=ReadJson (Join-Path $dir.FullName 'interruption.json')
    $process=ReadJson (Join-Path $dir.FullName 'process-result.json')
    $result=ReadJson (Join-Path $dir.FullName 'm01-integration-result.json')
    $capture=ReadJson (Join-Path $dir.FullName 'capture-integrity.json')
    $id=if($meta.build_id){$meta.build_id}elseif($launch.build_id){$launch.build_id}elseif($failure.build_id){$failure.build_id}else{$null}
    if(!$id){continue}
    $diagnostic=$meta -and (@($meta.diagnostic_flags).Count -gt 0 -or $meta.renderer_override -ne 'default')
    $status=if($interruption){'interrupted'}elseif($meta.success -and $meta.exit_code -eq 0 -and $meta.native_stderr_checked){if($diagnostic){'diagnostic_run'}else{'verified_run'}}elseif($failure -or $process){'failed_or_incomplete'}else{'pending_or_interrupted_unknown'}
    $kind=if($meta.kind){$meta.kind}elseif($dir.Name -match '^exported-([a-z]+)-'){$Matches[1]}else{'unknown'}
    $runs+=[pscustomobject][ordered]@{build_id=$id;directory=$dir.FullName.Substring($root.Length+1).Replace('\','/');kind=$kind;status=$status;
        started_at=$launch.started_at;ended_at=$process.ended_at;exit_code=$process.exit_code;wall_seconds=$result.wallSeconds;
        requested_seconds=$meta.requested_soak_seconds;diagnostic_flags=$meta.diagnostic_flags;renderer_override=$meta.renderer_override;
        capture_files=$capture.files;invalid_capture_files=$capture.invalid_files;visual_acceptance='not established by runtime status';
        reason=$(if($interruption){$interruption.reason}elseif($failure){$failure.reason}else{$null})}
}
$document=[ordered]@{generated_at=[DateTimeOffset]::Now.ToString('o');scope='Inventory of recorded exported runs only. pending_or_interrupted_unknown does not prove a live process. verified_run is not milestone acceptance, stability certification, or user approval.';runs=$runs}
$document|ConvertTo-Json -Depth 8|Set-Content (Join-Path $root 'planning/stages/M01/verification-index.json') -Encoding utf8
$runs|Group-Object build_id|ForEach-Object{[pscustomobject]@{build_id=$_.Name;recorded_runs=$_.Count;verified_runs=@($_.Group|Where-Object status -eq 'verified_run').Count;failed_or_interrupted=@($_.Group|Where-Object status -in @('failed_or_incomplete','interrupted')).Count}}
