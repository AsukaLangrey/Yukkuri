$ErrorActionPreference='Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
if(Test-Path planning/settings/setting-catalog-v004.json){throw 'Preserve existing v004.'}
$c=Get-Content planning/settings/setting-catalog-v003.json -Raw | ConvertFrom-Json
$gov=$c.sections | Where-Object id -eq 'GOV'
$r=$gov.rules | Where-Object id -eq 'GOV-05'
$r.statement='完整v1.0分里程碑验收，夜间Goal至少6小时，一轮可包含多个里程碑；不足4小时已达到交验标准的里程碑保存独立版本后直接推进下一项，不硬等6小时。次晨用户验收、反馈修正后晚间复验；时长和测试数量不替代玩法验收。'
$r.sources=@($r.sources)+@('planning/requirements-v051.txt')
$gov.rules=@($gov.rules)+[pscustomobject]@{id='GOV-08';status='confirmed';title='提前接续与验收分离';statement='里程碑实际开始到达到交验标准严格不足4小时，可在用户尚未试玩时保存版本和证据、标待验收并开始下一项；缺证据或阻塞问题不允许提前推进。各里程碑独立验收，前项失败优先修正并回归后续。至少6小时归夜间整体安排，开始时间仍由用户指定。';sources=@('planning/requirements-v051.txt')}
$c.version='v004';$c.source_through='requirements-v051';$c.implementation_status='设定和工作流已记录；当前没有启动夜间Goal，等待用户指定时间。'
$c.supersessions=@($c.supersessions)+[pscustomobject]@{id='CHG-18';topic='提前推进';historical='逐里程碑硬性6小时与先用户通过才继续实施';authority='v051';current='单项不足4小时达到交验标准就接续下一项；夜间整体至少6小时，用户验收独立保留。';rule='GOV-08'}
$c | ConvertTo-Json -Depth 30 | Set-Content planning/settings/setting-catalog-v004.json -Encoding utf8
$lines=[Collections.Generic.List[string]]::new()
$lines.Add('Yukkuri 乐园：完整设定手册 v004')
$lines.Add('2026-10-27 / Asia/Shanghai；累计v001—v051；150条规则。旧汇编保留，流程验证不表示玩法完成。')
$lines.Add($c.precedence)
foreach($s in $c.sections){$lines.Add('');$lines.Add("$($s.id)　$($s.title)");foreach($r in $s.rules){$lines.Add("$($r.id)　$($r.title)〔已确认〕");$lines.Add($r.statement);$lines.Add('来源：'+($r.sources -join '；'));$lines.Add('')}}
$lines.Add('历史覆盖关系：发生多次覆盖时以最新authority为准')
foreach($x in $c.supersessions){$lines.Add("$($x.id)　$($x.topic)：$($x.historical) → $($x.current) 依据：$($x.authority)；规则：$($x.rule)")}
$lines.Add('尚待设计与验证')
foreach($x in $c.open_items){$lines.Add("$($x.id)　$($x.topic)：$($x.description) 关联：$($x.related_rule)")}
$lines | Set-Content planning/settings/handbook-v004.txt -Encoding utf8
$rules=@($c.sections | ForEach-Object rules)
$map=Get-Content planning/settings/source-map-v003.json -Raw | ConvertFrom-Json
$map.version='v004'
$map.sources=@(1..51 | ForEach-Object {$p='planning/requirements-v{0:D3}.txt' -f $_;[ordered]@{version=('v{0:D3}' -f $_);path=$p;title=(Get-Content $p -TotalCount 1);sha256=(Get-FileHash $p).Hash.ToLowerInvariant();current_rule_ids=@($rules | Where-Object {$_.sources -contains $p} | ForEach-Object id);status='historical_evidence_latest_confirmation_wins'}})
$map | ConvertTo-Json -Depth 15 | Set-Content planning/settings/source-map-v004.json -Encoding utf8
$p=Get-Content planning/milestones-v002.json -Raw | ConvertFrom-Json
$p.version='v003';$p.authority='planning/requirements-v051.txt';$p.rules_catalog='planning/settings/setting-catalog-v004.json';$p.plan='planning/milestones-v003.txt'
$p | Add-Member night_policy ([pscustomobject]@{minimum_work_hours=6;advance_if_ready_before_hours=4;user_acceptance_required_for_advance=$false;start_time='user_specified'})
foreach($s in $p.stages){$s.minimum_work_hours=0}
$p.rule_coverage=@($p.rule_coverage)+[ordered]@{rule_id='GOV-08';title='提前接续与验收分离';primary_stage='M08';disposition='project_constraint';status='unverified';final_verification_stage='M08'}
$p | ConvertTo-Json -Depth 20 | Set-Content planning/milestones-v003.json -Encoding utf8
$text=(Get-Content planning/milestones-v002.txt -Raw).Replace('里程碑与验收流程 v002','里程碑与验收流程 v003').Replace('需求v001—v050','需求v001—v051').Replace('milestones-v002.json','milestones-v003.json')
$begin=$text.IndexOf('工作节奏');$end=$text.IndexOf('M01 一只油')
$rhythm=@'
工作节奏（v051覆盖旧版逐项6小时限制）
1. 用户指定时间后启动至少6小时夜间Goal，记录当晚起始里程碑、后续顺序、范围、真实时间和预算（如指定）。当前尚未指定时间。
2. 每里程碑独立记录开始、ready_at、产物、证据及验收项。实际不足4小时已达到全部交验标准，运行Advance检查后保存独立版本，标为待用户验收，直接启动下一项，不空等。
3. 一晚可有多个里程碑；Goal工具只能有一个活动目标时，把有序里程碑纳入同一夜间Goal，各阶段保留记录，不伪造并行Goal或用户通过状态。
4. 清晨一起交验；单项Ready不要求满6小时，NightReady检查整轮至少6小时实际工作与证据。未完成如实保留，不到时自动宣称完成。
5. 次晨用户逐项反馈，修复后晚间复验；前项被否决时优先修正并回归受影响后项。未通过仍是待验收/返工，但不足4小时达标允许先行实施后项。
6. 不足4小时为严格小于4小时；不能把完成时间回填为虚假早期时间。精确4小时不触发本条自动接续；原验收质量标准不变。
7. 不设置固定自动日程。记录检查不是宿主钩子；通过检查不证明好玩或事实真实，仍须实际试玩核对。

'@
$text=$text.Substring(0,$begin)+$rhythm+"`r`n"+$text.Substring($end)
$text | Set-Content planning/milestones-v003.txt -Encoding utf8
foreach($path in @('README.txt','planning/settings/README.txt','planning/CURRENT.txt','tools/check-milestone.ps1','tools/test-milestone-gate.ps1')){
    $t=Get-Content $path -Raw
    $t=$t.Replace('handbook-v003.txt','handbook-v004.txt').Replace('setting-catalog-v003.json','setting-catalog-v004.json').Replace('source-map-v003.json','source-map-v004.json').Replace('milestones-v002','milestones-v003').Replace('149条规则','150条规则').Replace('50份原始需求','51份原始需求').Replace('截至需求v050','截至需求v051').Replace('已按v001—v050归并','已按v001—v051归并').Replace('保留requirements-v001—v050','保留requirements-v001—v051').Replace('当前汇编：v003；原始确认截至 requirements-v050.txt。旧v001/v002保留。','当前汇编：v004；原始确认截至 requirements-v051.txt。旧汇编保留。').Replace('每阶段至少6小时Goal、独立玩法和用户验收','夜间Goal至少6小时，单项不足4小时达标自动接续；独立玩法和用户验收').Replace('8阶段可玩闭环、6小时Goal/用户反馈节奏','8阶段可玩闭环、至少6小时夜间Goal及不足4小时提前接续/用户反馈节奏')
    $t | Set-Content $path -Encoding utf8
}
$current=Get-Content planning/CURRENT.txt -Raw
$current=$current.Replace('当前需求入口（更新于 2026-10-27）',"当前需求入口（更新于 2026-10-27）`r`n最新流程：requirements-v051.txt。单里程碑不足4小时达到交验标准，保存独立版本/证据后直接开始下一项；用户验收仍待晨间，不能标已通过。至少6小时归整轮夜间Goal，不再逐项硬等。后续旧时长行由本条覆盖。开工仍由用户指定。")
$current | Set-Content planning/CURRENT.txt -Encoding utf8
