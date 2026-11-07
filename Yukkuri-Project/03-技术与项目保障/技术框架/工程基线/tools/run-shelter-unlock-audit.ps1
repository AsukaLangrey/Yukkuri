param(
    [Parameter(Mandatory)][ValidateSet('baseline','final','guards')][string]$Stage,
    [Parameter(Mandatory)][string]$TestRoot,
    [Parameter(Mandatory)][string]$Dotnet,
    [Parameter(Mandatory)][string]$Godot,
    [Parameter(Mandatory)][string]$PackageSource,
    [ValidatePattern('^[a-zA-Z0-9_-]{1,24}$')][string]$Attempt='initial'
)
$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$allowed=(Join-Path $project '.local\testing')+[IO.Path]::DirectorySeparatorChar
$TestRoot=[IO.Path]::GetFullPath($TestRoot)
if(!$TestRoot.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'TestRoot must be inside this project .local/testing.'}
foreach($binary in @($Dotnet,$Godot)){if(!(Test-Path -LiteralPath $binary -PathType Leaf)){throw "Missing binary: $binary"}}
if(!(Test-Path -LiteralPath $PackageSource -PathType Container)){throw 'Missing offline package source.'}
$stageName=if($Attempt -eq 'initial'){$Stage}else{"$Stage-$Attempt"}
$stageRoot=Join-Path $TestRoot $stageName
if(Test-Path -LiteralPath $stageRoot){throw 'Stage directory already exists. Preserve evidence; use a fresh run root for a new run.'}
New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
$budgetFile=Join-Path $TestRoot 'budget.json'
$firstBuild=!(Test-Path -LiteralPath $budgetFile)
if(!(Test-Path -LiteralPath $budgetFile)){
    if($Stage -eq 'guards'){throw 'Build baseline or final before running guard cases.'}
    @{startedUtc=[DateTimeOffset]::UtcNow.ToString('O');limitSeconds=1800}|ConvertTo-Json|Set-Content -LiteralPath $budgetFile -Encoding utf8
}
$budget=Get-Content -Raw -LiteralPath $budgetFile|ConvertFrom-Json
$deadline=[DateTimeOffset]::Parse($budget.startedUtc).AddSeconds(1800)
if([DateTimeOffset]::UtcNow -ge $deadline){throw 'Fixed 1800-second run budget exhausted.'}
$packageLink=Join-Path $project '.tools\godot\Godot_v4.7.2-stable_mono_win64\GodotSharp\Tools\nupkgs'
if(!(Test-Path -LiteralPath $packageLink)){
    New-Item -ItemType Directory -Path (Split-Path $packageLink) -Force|Out-Null
    New-Item -ItemType Junction -Path $packageLink -Target $PackageSource|Out-Null
}
$config=Join-Path $TestRoot 'offline-nuget.config'
if(!(Test-Path -LiteralPath $config)){
    $escaped=[Security.SecurityElement]::Escape($PackageSource)
    "<configuration><packageSources><clear/><add key=`"bundled`" value=`"$escaped`"/></packageSources></configuration>"|Set-Content -LiteralPath $config -Encoding utf8
}
$events=[Collections.Generic.List[object]]::new()
function Run([string]$Name,[string]$Exe,[string[]]$Arguments,[int]$Expected=0){
    $remaining=[int][Math]::Floor(($deadline-[DateTimeOffset]::UtcNow).TotalSeconds)
    if($remaining -le 0){throw 'Fixed run budget exhausted.'}
    $run=Join-Path $stageRoot $Name
    if(Test-Path -LiteralPath $run){throw "Process directory reused: $Name"}
    New-Item -ItemType Directory -Path $run|Out-Null
    $psi=[Diagnostics.ProcessStartInfo]::new($Exe);$psi.WorkingDirectory=$project;$psi.UseShellExecute=$false
    $psi.CreateNoWindow=$true;$psi.WindowStyle=[Diagnostics.ProcessWindowStyle]::Hidden
    $psi.RedirectStandardOutput=$true;$psi.RedirectStandardError=$true
    $psi.StandardOutputEncoding=[Text.Encoding]::UTF8;$psi.StandardErrorEncoding=[Text.Encoding]::UTF8
    foreach($a in $Arguments){$psi.ArgumentList.Add($a)}
    foreach($key in @('APPDATA','LOCALAPPDATA','TEMP','TMP','USERPROFILE','DOTNET_CLI_HOME')){
        $dir=Join-Path $run $key;New-Item -ItemType Directory -Path $dir|Out-Null;$psi.Environment[$key]=$dir
    }
    $psi.Environment['DOTNET_ROOT']=Split-Path $Dotnet
    $psi.Environment['PATH']=(Split-Path $Dotnet)+';'+$psi.Environment['PATH']
    $psi.Environment['DOTNET_GENERATE_ASPNET_CERTIFICATE']='false';$psi.Environment['DOTNET_SKIP_FIRST_TIME_EXPERIENCE']='1'
    $psi.Environment['DOTNET_CLI_TELEMETRY_OPTOUT']='1';$psi.Environment['MSBUILDDISABLENODEREUSE']='1'
    $psi.Environment['NUGET_PACKAGES']=Join-Path $TestRoot 'nuget-packages'
    $p=[Diagnostics.Process]::new();$p.StartInfo=$psi;[void]$p.Start();$started=$p.StartTime.ToUniversalTime();$id=$p.Id
    $stdout=$p.StandardOutput.ReadToEndAsync();$stderr=$p.StandardError.ReadToEndAsync()
    $timeout=![bool]$p.WaitForExit([Math]::Min(300,$remaining)*1000)
    if($timeout){
        if(!$p.HasExited -and $p.Id -eq $id -and $p.StartTime.ToUniversalTime() -eq $started){$p.Kill($true);$p.WaitForExit(10000)|Out-Null}
    }
    [IO.File]::WriteAllText((Join-Path $run 'stdout.log'),$stdout.GetAwaiter().GetResult())
    [IO.File]::WriteAllText((Join-Path $run 'stderr.log'),$stderr.GetAwaiter().GetResult())
    $code=if($p.HasExited){$p.ExitCode}else{$null}
    $record=[pscustomobject]@{name=$Name;pid=$id;startedUtc=$started.ToString('O');endedUtc=[DateTimeOffset]::UtcNow.ToString('O');timeout=$timeout;exitCode=$code;expected=$Expected;arguments=$Arguments}
    $events.Add($record);$events|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $stageRoot 'processes.json') -Encoding utf8
    Write-Output "$Name exit=$code timeout=$timeout"
    if($timeout -or $code -ne $Expected){throw "Unexpected process result: $Name"}
}
$game=Join-Path $project 'src\Yukkuri.Game'
if($Stage -ne 'guards'){
    Run 'restore-game' $Dotnet @('restore',(Join-Path $game 'Yukkuri.Game.csproj'),'--configfile',$config,'--disable-parallel','-p:UseSharedCompilation=false','-nodeReuse:false','-maxcpucount:1')
    Run 'build-game' $Dotnet @('build',(Join-Path $game 'Yukkuri.Game.csproj'),'--no-restore','-c','Debug','-p:UseSharedCompilation=false','-nodeReuse:false','-maxcpucount:1')
    if($firstBuild){Run 'import' $Godot @('--headless','--path',$game,'--editor','--import','--quit')}
    $out=Join-Path $stageRoot 'audit-output\result.json'
    $expect=if($Stage -eq 'baseline'){1}else{0}
    Run 'scene' $Godot @('--headless','--path',$game,'--','--shelter-unlock-audit-output',$out,'--interaction-test-profile',"shelter-unlock-$stageName") $expect
    $result=Get-Content -Raw -LiteralPath $out|ConvertFrom-Json
    Write-Output "scene checks=$($result.checks) failures=$($result.failures)"
    if($Stage -eq 'final' -and !$result.success){throw 'Final diagnostic JSON failed.'}
    if($Stage -eq 'final'){
        foreach($check in @('Interaction.Shelters','Courtyard.Economy','Input.Intent','Interaction.StateBoundaries')){
            $csproj=Join-Path $project "checks\$check\$check.csproj"
            Run "restore-$check" $Dotnet @('restore',$csproj,'--configfile',$config,'--disable-parallel','-p:UseSharedCompilation=false','-nodeReuse:false','-maxcpucount:1')
            Run "build-$check" $Dotnet @('build',$csproj,'--no-restore','-c','Debug','-p:UseSharedCompilation=false','-nodeReuse:false','-maxcpucount:1')
            Run "run-$check" $Dotnet @((Join-Path $project "checks\$check\bin\Debug\net10.0\$check.dll"))
        }
    }
}else{
    $guardCases=@(
        @{name='invalid-flag';args=@('--shelter-unlock-typo')},
        @{name='missing-profile';args=@('--shelter-unlock-audit-output',(Join-Path $stageRoot 'missing-output\result.json'))},
        @{name='direct-profile';args=@('--interaction-test-profile','shelter-unlock-direct')},
        @{name='extra-flags';args=@('--shelter-unlock-audit-output',(Join-Path $stageRoot 'extra-output\result.json'),'--interaction-test-profile','shelter-unlock-extra','--legacy')},
        @{name='reused-output';args=@('--shelter-unlock-audit-output',(Join-Path $TestRoot 'final\audit-output\result.json'),'--interaction-test-profile','shelter-unlock-reused')},
        @{name='direct-main';scene='res://Main.tscn';args=@('--shelter-unlock-audit-output',(Join-Path $stageRoot 'direct-main-output\result.json'),'--interaction-test-profile','shelter-unlock-direct-main')}
    )
    foreach($case in $guardCases){
        $engineArgs=@('--headless','--path',$game)
        if($case.scene){$engineArgs+=$case.scene}
        Run $case.name $Godot ($engineArgs+@('--')+$case.args) 2
        $stdout=Get-Content -Raw -LiteralPath (Join-Path $stageRoot "$($case.name)\stdout.log")
        if($stdout -match 'YUKKURI_VERSION|INTERACTION_ROOM_READY|SHELTER_UNLOCK_GUARD'){throw "Guard reached world initialization: $($case.name)"}
    }
}
