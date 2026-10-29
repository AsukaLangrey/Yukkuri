param([int]$Seconds=60,[ValidateSet('morphology','box','solids')][string]$Suite='morphology')
. "$PSScriptRoot/env.ps1"
$probeRoot=Join-Path $ProjectRoot 'artifacts/m01/renderer-probe'
$out=Join-Path $probeRoot ('matrix-'+[DateTimeOffset]::UtcNow.ToString('yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $out | Out-Null
$cases=@(
    @{Name='static-all';Args=@('--static')},
    @{Name='sphere-only';Args=@('--kind','SphereMesh')},
    @{Name='box-only';Args=@('--kind','BoxMesh')},
    @{Name='cylinder-only';Args=@('--kind','CylinderMesh')},
    @{Name='array-only';Args=@('--kind','ArrayMesh')},
    @{Name='torus-only';Args=@('--kind','TorusMesh')}
)
if($Suite -eq 'box'){$cases=@(
    @{Name='box-unit-mesh';Args=@('--kind','BoxMesh','--unit-boxes')},
    @{Name='box-no-shadow';Args=@('--kind','BoxMesh','--no-shadows','--no-cast')},
    @{Name='box-single-material';Args=@('--kind','BoxMesh','--single-material')},
    @{Name='box-generated';Args=@('--generated-boxes')},
    @{Name='box-flat-static';Args=@('--kind','BoxMesh','--flatten','--static')}
)}
if($Suite -eq 'solids'){$cases=@(
    @{Name='solid-backface';Args=@('--solid-backface')},
    @{Name='box-no-cast-all-models';Args=@('--box-no-cast')}
)}
$results=@()
foreach($case in $cases){
    $log=Join-Path $out ($case.Name+'.log')
    $args=@('--path',$probeRoot,'--rendering-method','mobile','--log-file',$log,'--','--duration',"$Seconds")+$case.Args
    $p=Start-Process -FilePath $GodotExe -ArgumentList $args -WindowStyle Hidden -PassThru
    Write-Output "START $($case.Name) pid=$($p.Id)"
    if(-not $p.WaitForExit(($Seconds+40)*1000)){$p.Kill($true);throw 'Probe timeout'}
    $content=Get-Content $log -Raw
    $errors=([regex]::Matches($content,'(?m)^ERROR:')).Count
    $pass=$p.ExitCode -eq 0 -and $errors -eq 0 -and $content.Contains('GD_MODEL_COMPLETE')
    $results+=@{case=$case.Name;passed=$pass;exit=$p.ExitCode;errors=$errors;log=$log}
    $results | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $out 'results.json')
    Write-Output "END $($case.Name) passed=$pass errors=$errors"
}
Write-Output "MATRIX_COMPLETE $out"
