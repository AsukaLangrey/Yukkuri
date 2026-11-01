param(
    [Parameter(Mandatory=$true)][string]$ExportDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$ProfileFile='.local/m01-host-compatibility.json'
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
. "$PSScriptRoot/build-identity.ps1"
function Inside([string]$relative){
    $path=[IO.Path]::GetFullPath((Join-Path $root $relative))
    if(!$path.StartsWith($root+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Playtest paths must remain inside the workspace.'}
    return $path
}
$source=Inside $ExportDirectory
$destination=Inside $OutputDirectory
$profile=Inside $ProfileFile
if(Test-Path -LiteralPath $destination){throw 'Playtest directory exists; preserve it and choose a new name.'}
$info=Get-Content -LiteralPath (Join-Path $source 'build-info.json') -Raw|ConvertFrom-Json
if($info.stage -ne 'M01' -or $info.diagnostic_only){throw 'A non-diagnostic M01 game build is required.'}
Confirm-YukkuriPayload $source $info.payload
# Validate the host profile using the same entry point that will launch the game.
& "$PSScriptRoot/launch-m01.ps1" -ExportDirectory $ExportDirectory -ProfileFile $ProfileFile -LocalCompatibility -CheckOnly|Out-Host
New-Item -ItemType Directory -Path $destination|Out-Null
Get-ChildItem -LiteralPath $source|Copy-Item -Destination $destination -Recurse
New-Item -ItemType Directory -Path (Join-Path $destination 'tools'),(Join-Path $destination '.local')|Out-Null
foreach($name in @('launch-m01.ps1','process-affinity.ps1')){
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $destination 'tools')
}
Copy-Item -LiteralPath $profile -Destination (Join-Path $destination '.local/m01-host-compatibility.json')
@'
@echo off
cd /d "%~dp0"
powershell.exe -NoProfile -File "%~dp0tools\launch-m01.ps1" -ExportDirectory . -LocalCompatibility
if errorlevel 1 pause
'@|Set-Content -LiteralPath (Join-Path $destination 'Play This PC.cmd') -Encoding Ascii
@"
本机临时兼容启动 / $($info.build_id)

在已完整解压的目录内，双击 Play This PC.cmd。
本机曾出现与逻辑处理器4、5相关的计算异常；此入口只为新启动的游戏进程应用本机临时配置。
它检查机器名称、CPU名称和逻辑处理器数量。其他设备请直接运行 Yukkuri.exe。
不会修改BIOS、系统配置或其他程序。关闭游戏后这项进程设置结束。
直接运行 Yukkuri.exe 使用默认处理器范围，两种运行条件的验证结果分别记录。

本目录含完整游戏和启动脚本，不依赖原工程路径。不要单独移动exe或删除data、pck、tools、.local。
这不是硬件修复，也不代表用户验收通过；构建的实际验证状态请看同目录“已知限制”。
如果启动失败，保留报错；不要在不匹配的电脑上照搬此CPU配置。
"@|Set-Content -LiteralPath (Join-Path $destination '本机兼容启动说明.txt') -Encoding UTF8
$config=Get-Content -LiteralPath $profile -Raw|ConvertFrom-Json
[ordered]@{prepared_at=[DateTimeOffset]::Now.ToString('o');build_id=$info.build_id;default_executable='Yukkuri.exe';optional_local_launcher='Play This PC.cmd';requested_affinity=$config.process_affinity_mask;scope='Host-specific optional launch path. Original default-run evidence and local compatibility evidence remain distinct. No system setting changed.'}|ConvertTo-Json -Depth 5|Set-Content (Join-Path $destination 'local-launch-info.json') -Encoding UTF8
Confirm-YukkuriPayload $destination $info.payload
Write-Output "Prepared self-contained local playtest directory: $destination"
