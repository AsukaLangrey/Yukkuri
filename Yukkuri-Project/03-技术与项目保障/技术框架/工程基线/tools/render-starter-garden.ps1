param([string]$RunName=('capture-'+(Get-Date -Format 'yyyyMMdd-HHmmss')))
$ErrorActionPreference='Stop'
if($RunName -notmatch '\A[a-zA-Z0-9_-]{1,64}\z'){throw 'RunName must be a short directory name.'}
$project=Split-Path $PSScriptRoot -Parent
$game=Join-Path $project 'src/Yukkuri.Game'
$evidence=Join-Path $project '.local/testing/starter-garden-art-20261103'
$runDir=Join-Path $evidence $RunName
if(Test-Path -LiteralPath $runDir){throw "Evidence directory already exists: $runDir"}
New-Item -ItemType Directory -Path $runDir -Force | Out-Null
$env:DOTNET_ROOT=Join-Path $project '.tools/dotnet'
$env:PATH="$env:DOTNET_ROOT;$env:PATH"
$env:APPDATA=Join-Path $evidence 'appdata'
$env:LOCALAPPDATA=Join-Path $evidence 'localappdata'
$engine=(Get-ChildItem -LiteralPath (Join-Path $project '.tools/godot') -Recurse -Filter '*console.exe' | Select-Object -First 1).FullName
if(!$engine){throw 'Godot executable not found.'}
$scene='res://ArtPreview/StarterGarden.tscn'
$arguments=@('--path',('"'+$game+'"'),'--rendering-method','gl_compatibility','--resolution','1440x900','--max-fps','60','--audio-driver','Dummy','--log-file',('"'+(Join-Path $runDir 'engine.log')+'"'),$scene,'--','--art-output',('"'+$runDir+'"'))
$sourceFiles=@(Get-ChildItem -LiteralPath (Join-Path $game 'ArtPreview') -Recurse -File | Where-Object {$_.Extension -notin '.uid','.import'} | ForEach-Object {[ordered]@{path=[IO.Path]::GetRelativePath($project,$_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash}})
[ordered]@{date=[DateTimeOffset]::Now.ToString('o');machine=$env:COMPUTERNAME;commit=(& git -C $project rev-parse HEAD);scene=$scene;renderer='gl_compatibility';window='1440x900';render_viewport='1920x1200';scope='Standalone art sample; existing uncommitted repository changes present; no save or gameplay integration';sources=$sourceFiles}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $runDir 'request.json') -Encoding utf8
$child=Start-Process -FilePath $engine -ArgumentList $arguments -WindowStyle Hidden -PassThru
$child.Id|Set-Content -LiteralPath (Join-Path $runDir 'process-id.txt')
if(!$child.WaitForExit(45000)){$child.Kill();$child.WaitForExit();throw "Owned capture process timed out. Evidence retained: $runDir"}
$log=Get-Content -LiteralPath (Join-Path $runDir 'engine.log') -Raw
if($child.ExitCode -ne 0 -or $log -match '(?m)^(ERROR:|SCRIPT ERROR:)' -or $log -notmatch 'STARTER_ART_CAPTURE_COMPLETE'){throw "Capture failed. Evidence retained: $runDir"}
$report=Get-Content -LiteralPath (Join-Path $runDir 'render-report.json') -Raw | ConvertFrom-Json
if($report.files.Count -ne 5 -or $report.viewport[0] -ne 1920 -or $report.viewport[1] -ne 1200){throw 'Capture count or viewport size mismatch.'}
foreach($file in $report.files){if(!(Test-Path -LiteralPath $file)){throw "Missing capture: $file"}}
[pscustomobject]@{status='PASS';directory=$runDir;captures=$report.files.Count;geometry_checks=$report.geometry_checks.Count;viewport=($report.viewport -join 'x');pieces=$report.pieces;batches=$report.batches}
