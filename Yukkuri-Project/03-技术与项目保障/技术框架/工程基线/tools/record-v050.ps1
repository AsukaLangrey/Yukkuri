$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
if(Test-Path planning/settings/setting-catalog-v003.json){throw 'Preserve existing v003; use a new version.'}
$c=Get-Content planning/settings/setting-catalog-v002.json -Raw | ConvertFrom-Json
foreach($s in $c.sections){foreach($r in $s.rules){
    if($r.statement.Contains('居民') -or $r.title.Contains('居民')){
        $r.statement=$r.statement.Replace('居民','油库里');$r.title=$r.title.Replace('居民','油库里')
        $r.sources=@($r.sources)+@('planning/requirements-v050.txt')
    }
}}
foreach($o in $c.open_items){$o.description=$o.description.Replace('居民','油库里')}
$section=$c.sections | Where-Object id -eq 'PRD'
$section.rules=@($section.rules)+[pscustomobject]@{id='PRD-10';status='confirmed';title='核心参与者';statement='主角、园区雇员、油库里是核心参与者；不另建以普通人类居民为中心的经营系统。此前泛称居民的园内培养、护园和亲缘对象明确为油库里；既有相关地区与油库里工作规则保留。';sources=@('planning/requirements-v050.txt')}
$c.version='v003';$c.source_through='requirements-v050';$c.implementation_status='v049/v050确认、分期与本地交验记录检查已整理；玩法尚未实施，未启动Goal，等待用户指定开始。'
$c.supersessions=@($c.supersessions)+[pscustomobject]@{id='CHG-17';topic='核心参与者与居民称呼';historical='文档泛称居民指园内油库里，容易与人类居民混淆';authority='v050';current='明确主角、园区雇员、油库里；不新增普通居民经营链。';rule='PRD-10'}
$c | ConvertTo-Json -Depth 30 | Set-Content planning/settings/setting-catalog-v003.json -Encoding utf8
$lines=[Collections.Generic.List[string]]::new()
$lines.Add('Yukkuri 乐园：完整设定手册 v003')
$lines.Add('2026-10-27 / Asia/Shanghai；累计v001—v050；149条规则。旧汇编保留，文档和流程检查不表示玩法已完成。')
$lines.Add($c.precedence)
foreach($s in $c.sections){$lines.Add('');$lines.Add("$($s.id)　$($s.title)");foreach($r in $s.rules){$lines.Add("$($r.id)　$($r.title)〔已确认〕");$lines.Add($r.statement);$lines.Add('来源：'+($r.sources -join '；'));$lines.Add('')}}
$lines.Add('历史覆盖关系')
foreach($x in $c.supersessions){$lines.Add("$($x.id)　$($x.topic)：$($x.historical) → $($x.current) 依据：$($x.authority)；规则：$($x.rule)")}
$lines.Add('');$lines.Add('尚待设计与验证：以下不自动成为新问卷或确认参数')
foreach($x in $c.open_items){$lines.Add("$($x.id)　$($x.topic)：$($x.description) 关联：$($x.related_rule)")}
$lines.Add('实施入口：planning/milestones-v002.txt与JSON；等待用户指定开工时间。')
$lines | Set-Content planning/settings/handbook-v003.txt -Encoding utf8
$rules=@($c.sections | ForEach-Object rules)
$map=Get-Content planning/settings/source-map-v002.json -Raw | ConvertFrom-Json
$map.version='v003'
$map.sources=@(1..50 | ForEach-Object {
    $p='planning/requirements-v{0:D3}.txt' -f $_
    [ordered]@{version=('v{0:D3}' -f $_);path=$p;title=(Get-Content $p -TotalCount 1);sha256=(Get-FileHash $p).Hash.ToLowerInvariant();current_rule_ids=@($rules | Where-Object {$_.sources -contains $p} | ForEach-Object id);status='historical_evidence_latest_confirmation_wins'}
})
$map | ConvertTo-Json -Depth 15 | Set-Content planning/settings/source-map-v003.json -Encoding utf8
$plan=(Get-Content planning/milestones-v001.json -Raw).Replace('居民','油库里') | ConvertFrom-Json
$plan.version='v002';$plan.authority='planning/requirements-v050.txt';$plan.rules_catalog='planning/settings/setting-catalog-v003.json';$plan.plan='planning/milestones-v002.txt'
$plan.rule_coverage=@($plan.rule_coverage)+[ordered]@{rule_id='PRD-10';title='核心参与者';primary_stage='M01';disposition='project_constraint';status='unverified';final_verification_stage='M08'}
$plan | ConvertTo-Json -Depth 20 | Set-Content planning/milestones-v002.json -Encoding utf8
$text=(Get-Content planning/milestones-v001.txt -Raw).Replace('里程碑与验收流程 v001','里程碑与验收流程 v002').Replace('需求v001—v049','需求v001—v050').Replace('milestones-v001.json','milestones-v002.json').Replace('居民','油库里')
$text += "`r`n本版追加（v050）：以主角、园区雇员、油库里为核心，修正原泛称居民；玩法闭环和原验收条件不删减，旧版保留。`r`n"
$text | Set-Content planning/milestones-v002.txt -Encoding utf8
foreach($path in @('README.txt','planning/settings/README.txt','planning/stages/README.txt','tools/check-milestone.ps1','tools/test-milestone-gate.ps1')){
    $text=Get-Content $path -Raw
    $text=$text.Replace('handbook-v002.txt','handbook-v003.txt').Replace('setting-catalog-v002.json','setting-catalog-v003.json').Replace('source-map-v002.json','source-map-v003.json').Replace('milestones-v001','milestones-v002').Replace('截至需求v049','截至需求v050').Replace('当前汇编：v002；原始确认截至 requirements-v049.txt。旧v001保留。','当前汇编：v003；原始确认截至 requirements-v050.txt。旧v001/v002保留。').Replace('148条规则','149条规则').Replace('49份原始需求','50份原始需求').Replace('保留requirements-v001—v049','保留requirements-v001—v050')
    $text | Set-Content $path -Encoding utf8
}
$current=Get-Content planning/CURRENT.txt -Raw
$current=$current.Replace('settings/handbook-v002.txt','settings/handbook-v003.txt').Replace('settings/setting-catalog-v002.json','settings/setting-catalog-v003.json').Replace('settings/source-map-v002.json','settings/source-map-v003.json').Replace('已按v001—v049归并','已按v001—v050归并').Replace('148条规则','149条规则').Replace('milestones-v001','milestones-v002')
$current=$current.Replace('当前需求入口（更新于 2026-10-27）',"当前需求入口（更新于 2026-10-27）`r`n最新命名澄清：requirements-v050.txt。核心参与者为主角、园区雇员、油库里；护园、培养与亲缘对象明确写油库里，不新增普通居民经营链。完整v1.0和用户指定开工时间要求继续有效。")
$current | Set-Content planning/CURRENT.txt -Encoding utf8
Write-Output 'Recorded v050, 149 rules, 8 unchanged gameplay milestones with explicit Yukkuri naming.'
