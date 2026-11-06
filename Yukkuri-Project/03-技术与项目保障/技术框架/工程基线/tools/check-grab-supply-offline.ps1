# Requires PowerShell 7, an authorized disposable checkout, and the exact installed offline toolchain.
param([Parameter(Mandatory=$true)][ValidateSet('build','import','run','checkbuild','checkrun','guard')][string]$Stage,[ValidatePattern('^[a-zA-Z0-9_-]{1,40}$')][string]$Attempt='01',[ValidateSet('Input.Intent','Interaction.StateBoundaries')][string]$CheckName='Input.Intent',[ValidateSet('unknown','legacy','direct','duplicate','relative','missing','reuse-output','reuse-profile')][string]$GuardCase='unknown',[Parameter(Mandatory=$true)][string]$ToolRoot)
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new()
$root=Split-Path -Parent $PSScriptRoot
$testing=Join-Path $root '.local\testing'
$tools=$ToolRoot
$dotnet=Join-Path $tools 'dotnet\dotnet.exe'
$godot=Join-Path $tools 'godot\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe'
$nupkg=Join-Path $tools 'godot\Godot_v4.7.2-stable_mono_win64\GodotSharp\Tools\nupkgs'
foreach($inputPath in @($dotnet,$godot,$nupkg)){if(!(Test-Path -LiteralPath $inputPath)){throw "Missing installed offline toolchain input: $inputPath"}}
New-Item -ItemType Directory -Path $testing -Force|Out-Null
$budgetPath=Join-Path $testing 'fixed-budget.json'
if(!(Test-Path -LiteralPath $budgetPath)){
    if($Stage -ne 'build'){throw 'Budget must originate from first build.'}
    @{startedUtc=[DateTime]::UtcNow.ToString('o');maximumSeconds=1800;perProcessSeconds=300}|ConvertTo-Json|Set-Content -LiteralPath $budgetPath -Encoding utf8
}
$budget=Get-Content -Raw -LiteralPath $budgetPath|ConvertFrom-Json
$deadline=([DateTime]$budget.startedUtc).ToUniversalTime().AddSeconds(1800)
$seconds=[Math]::Min(300,[Math]::Floor(($deadline-[DateTime]::UtcNow).TotalSeconds))
if($seconds -le 0){throw 'Fixed 1800 second budget exhausted; do not reset.'}
$profile=Join-Path $testing ('environment-'+$Stage+'-'+$Attempt)
if(Test-Path -LiteralPath $profile){throw 'Fresh environment required; do not reuse earlier attempt.'}
foreach($d in @('appdata','localappdata','temp','userprofile','cli','nuget')){New-Item -ItemType Directory -Path (Join-Path $profile $d)|Out-Null}
$env:APPDATA=Join-Path $profile 'appdata';$env:LOCALAPPDATA=Join-Path $profile 'localappdata';$env:TEMP=Join-Path $profile 'temp';$env:TMP=$env:TEMP
$env:USERPROFILE=Join-Path $profile 'userprofile';$env:HOME=$env:USERPROFILE;$env:DOTNET_CLI_HOME=Join-Path $profile 'cli'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false';$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1';$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_ROOT=Split-Path $dotnet;$env:PATH=$env:DOTNET_ROOT+';'+$env:PATH
$env:MSBUILDDISABLENODEREUSE='1'
# Retain build dependencies only inside this isolated project; no user NuGet cache.
$env:NUGET_PACKAGES=Join-Path $testing 'nuget-packages';$env:NUGET_HTTP_CACHE_PATH=Join-Path $profile 'nuget'
Set-Location -LiteralPath $root
# Run only in an authorized disposable checkout. Never reset its fixed budget.
# Temporarily route restore to approved installed packages; always restore exact config bytes.
$config=Join-Path $root 'NuGet.Config'
$originalConfig=[IO.File]::ReadAllBytes($config)
try {
    if($Stage -in @('build','checkbuild')){
        $escaped=[Security.SecurityElement]::Escape($nupkg)
        "<?xml version=`"1.0`" encoding=`"utf-8`"?><configuration><packageSources><clear/><add key=`"Godot offline`" value=`"$escaped`"/></packageSources></configuration>"|Set-Content -LiteralPath $config -Encoding utf8
    }
if($Stage -eq 'build'){
    $exe=$dotnet;$arguments=@('build','src/Yukkuri.Game/Yukkuri.Game.csproj','--configfile','NuGet.Config','--disable-build-servers','-p:UseSharedCompilation=false','-nodeReuse:false','-maxcpucount:1','-p:NuGetAudit=false')
} elseif($Stage -eq 'checkbuild'){
    $exe=$dotnet;$arguments=@('build',("checks/"+$CheckName+"/"+$CheckName+".csproj"),'--configfile','NuGet.Config','--disable-build-servers','-p:UseSharedCompilation=false','-nodeReuse:false','-maxcpucount:1','-p:NuGetAudit=false')
} elseif($Stage -eq 'checkrun'){
    $exe=$dotnet;$arguments=@(('"'+(Join-Path $root ("checks/"+$CheckName+"/bin/Debug/net10.0/"+$CheckName+".dll"))+'"'))
    if($CheckName -eq 'Input.Intent'){$arguments+=('"'+(Join-Path $testing ('input-intent-'+$Attempt+'.json'))+'"')}
} elseif($Stage -eq 'guard'){
    $out=Join-Path $testing ('guard-result-'+$Attempt+'\result.json')
    $profileName='guard-'+$Attempt
    $userArgs=@('--interaction-test-profile',$profileName,'--grab-supply-check-output',('"'+$out+'"'))
    $scene=@()
    switch($GuardCase){
        'unknown' {$userArgs+= '--grab-supply-unknown'}
        'legacy' {$userArgs+= '--legacy'}
        'direct' {$scene=@('res://Main.tscn')}
        'duplicate' {$userArgs+=@('--grab-supply-check-output',('"'+$out+'"'))}
        'relative' {$userArgs=@('--interaction-test-profile',$profileName,'--grab-supply-check-output','relative.json')}
        'missing' {$userArgs=@('--interaction-test-profile',$profileName,'--grab-supply-check-output')}
        'reuse-output' {New-Item -ItemType Directory -Path (Split-Path $out)|Out-Null;Set-Content -LiteralPath $out -Value 'preserve-existing-evidence' -Encoding utf8}
        'reuse-profile' {
            $config=Get-Content -LiteralPath (Join-Path $root 'src/Yukkuri.Game/project.godot') -Encoding utf8
            $appName=($config | Where-Object {$_ -match '^config/name='}) -replace '^config/name="(.*)"$','$1'
            $save=Join-Path $env:APPDATA ('Godot/app_userdata/'+$appName+'/interaction-test-saves/'+$profileName)
            New-Item -ItemType Directory -Path $save -Force|Out-Null
        }
    }
    $exe=$godot;$arguments=@('--headless','--path',('"'+(Join-Path $root 'src\Yukkuri.Game')+'"'),'--audio-driver','Dummy','--quit-after','60')+$scene+@('--')+$userArgs
} elseif($Stage -eq 'import'){
    $exe=$godot;$arguments=@('--headless','--path',('"'+(Join-Path $root 'src\Yukkuri.Game')+'"'),'--editor','--import','--quit')
} else {
    $out=Join-Path $testing ('result-'+$Attempt+'\result.json')
    $exe=$godot;$arguments=@('--headless','--path',('"'+(Join-Path $root 'src\Yukkuri.Game')+'"'),'--audio-driver','Dummy','--quit-after','600','--','--interaction-test-profile',('grab-'+$Attempt),'--grab-supply-check-output',('"'+$out+'"'))
}
$name=$Stage+'-'+$Attempt
$stdout=Join-Path $testing ($name+'.stdout.log');$stderr=Join-Path $testing ($name+'.stderr.log')
if((Test-Path -LiteralPath $stdout) -or (Test-Path -LiteralPath $stderr)){throw 'Fresh log names required.'}
$started=[DateTime]::UtcNow
$p=Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
$ownedStart=$p.StartTime;$ownedId=$p.Id;$ownedPath=$p.Path
@{stage=$Stage;attempt=$Attempt;pid=$ownedId;ownedStart=$ownedStart.ToUniversalTime().ToString('o');executable=$exe;ownedPath=$ownedPath;args=$arguments;deadlineUtc=$deadline.ToString('o');timeoutSeconds=$seconds;profile=$profile}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $testing ($name+'.process.json')) -Encoding utf8
$timedOut=!$p.WaitForExit([int]($seconds*1000))
if($timedOut){
    $p.Refresh()
    if(!$p.HasExited -and $p.Id -eq $ownedId -and $p.StartTime -eq $ownedStart -and $p.Path -eq $ownedPath){$p.Kill($true);$p.WaitForExit(10000)|Out-Null}
}
$p.Refresh()
@{stage=$Stage;attempt=$Attempt;pid=$ownedId;exited=$p.HasExited;timedOut=$timedOut;exitCode=if($p.HasExited){$p.ExitCode}else{$null};seconds=([DateTime]::UtcNow-$started).TotalSeconds}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $testing ($name+'.completion.json')) -Encoding utf8
Get-Content -LiteralPath $stdout -Tail 30;Get-Content -LiteralPath $stderr -Tail 25
if($timedOut){throw 'Owned process tree terminated on timeout.'}
$expected=if($Stage -eq 'guard'){2}else{0}
if($p.ExitCode -ne $expected){throw "Stage exited $($p.ExitCode), expected $expected; evidence preserved."}
if($Stage -eq 'guard'){
    $log=Get-Content -Raw -LiteralPath $stdout
    $ok=$log.Contains('GRAB_SUPPLY_GUARD_REJECTED_BEFORE_WORLD') -and !$log.Contains('YUKKURI_VERSION') -and !$log.Contains('INTERACTION_ROOM_READY') -and !$log.Contains('PIPELINE_READY')
    $saves=@(Get-ChildItem -LiteralPath $env:APPDATA -Directory -Recurse | Where-Object {$_.Name -in @('interaction-saves','m01-saves','saves')})
    $ok=$ok -and $saves.Count -eq 0
    if($GuardCase -eq 'reuse-output'){$ok=$ok -and ((Get-Content -LiteralPath $out -Raw).Trim() -eq 'preserve-existing-evidence')}
    elseif(Test-Path -LiteralPath $out){$ok=$false}
    @{passed=$ok;case=$GuardCase;exitCode=$p.ExitCode;rejectedBeforeWorld=$log.Contains('GRAB_SUPPLY_GUARD_REJECTED_BEFORE_WORLD');ordinarySaveDirectories=$saves.Count}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $testing ($name+'.guard.json')) -Encoding utf8
    if(!$ok){throw 'Early guard regression failed; evidence preserved.'}
}


if($Stage -eq 'run'){
    $result=Get-Content -Raw -LiteralPath $out|ConvertFrom-Json
    if(!$result.success -or $result.failures -ne 0 -or $result.fixtureFailures -ne 0){throw 'Diagnostic JSON reports a failure; evidence preserved.'}
}
if($Stage -eq 'checkrun' -and $CheckName -eq 'Input.Intent'){
    $result=Get-Content -Raw -LiteralPath (Join-Path $testing ('input-intent-'+$Attempt+'.json'))|ConvertFrom-Json
    if(!$result.success -or $result.failures -ne 0){throw 'Input.Intent JSON reports a failure; evidence preserved.'}
}
} finally { [IO.File]::WriteAllBytes($config,$originalConfig) }
