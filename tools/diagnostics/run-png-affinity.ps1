param([Parameter(Mandatory=$true)][int]$LogicalProcessor,[Parameter(Mandatory=$true)][string]$Tag,[ValidateSet('official','clang')][string]$Engine='official',[switch]$ExcludeObservedCore)
$ErrorActionPreference='Stop'
$root=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
. (Join-Path $root 'tools/env.ps1')
$base=Join-Path $root 'artifacts/m01/png-probe-20261027-061500'
if($LogicalProcessor -lt -1 -or $LogicalProcessor -ge 32){throw 'Use -1 for unchanged affinity, or a processor in the inspected first group.'}
if($Tag -notmatch '^[a-z0-9-]+$'){throw 'Invalid diagnostic tag.'}
$out=Join-Path $base $Tag
if(Test-Path -LiteralPath $out){throw 'Do not overwrite a diagnostic result.'}
New-Item -ItemType Directory -Path $out|Out-Null
$source=Join-Path $root 'artifacts/m01/exported-1280x720-20261027-055846-470-44e89156/m01-home.png'
$arguments=@('--headless','--path',('"'+$base+'"'),'--script',('"'+(Join-Path $base 'probe.gd')+'"'),'--',('"'+$source+'"'),('"'+(Join-Path $out 'images')+'"'))
if($Engine -eq 'clang'){
    $GodotExe=Join-Path $base 'ClangProbe.exe'
    $arguments=@('--headless','--',('"'+$source+'"'),('"'+(Join-Path $out 'images')+'"'))
}
$p=Start-Process -FilePath $GodotExe -ArgumentList $arguments -WorkingDirectory $base -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $out 'stdout.log') -RedirectStandardError (Join-Path $out 'stderr.log')
if($LogicalProcessor -ge 0){$mask=[long]1 -shl $LogicalProcessor;$p.ProcessorAffinity=[IntPtr]$mask}
if($ExcludeObservedCore){
    # This exact host's CPUID observation mapped APIC16/17 to logical4/5.
    if($LogicalProcessor -ne -1){throw 'Use unchanged selector with the explicit observed-core exclusion.'}
    $mask=$p.ProcessorAffinity.ToInt64() -band (-bnot [long]48)
    if($mask -eq 0){$p.Kill();throw 'Exclusion would leave no processors.'}
    $p.ProcessorAffinity=[IntPtr]$mask
}
[ordered]@{pid=$p.Id;started_at=[DateTimeOffset]::Now.ToString('o');engine=$Engine;logical_processor=$LogicalProcessor;actual_process_affinity=$p.ProcessorAffinity.ToInt64();scope='Fixed-image headless diagnostic; affinity applies only to this owned child, not the machine or game release.'}|ConvertTo-Json|Set-Content (Join-Path $out 'request.json')
if(!$p.WaitForExit(120000)){$p.Kill();$p.WaitForExit();throw 'PNG diagnostic timed out.'}
[ordered]@{exit_code=$p.ExitCode;ended_at=[DateTimeOffset]::Now.ToString('o')}|ConvertTo-Json|Set-Content (Join-Path $out 'result.json')
Write-Output "PNG affinity diagnostic $Tag exit=$($p.ExitCode)"
