$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$destination = 'planning/settings/setting-catalog-v002.json'
if (Test-Path $destination) { throw 'v002 already exists; preserve it and create another version.' }
$catalog = Get-Content planning/settings/setting-catalog-v001.json -Raw | ConvertFrom-Json
$additions = @(
    @('GOV','GOV-05','完整内容与阶段验收','按完整v1.0规划，分阶段实施；每阶段设至少6小时的Goal，夜间工作、次晨用户验收、反馈修正后晚间复验。阶段可跨多个工作夜，不以时长或测试数量替代玩法验收。'),
    @('GOV','GOV-06','独立完整里程碑','里程碑记录在文件，续接和交验时核对原始范围、验收项、版本、证据、问题和未完成项；每个里程碑必须有相对独立完整的可玩闭环。'),
    @('GOV','GOV-07','开工时间','用户指定每轮开始时间；目前未给具体时间，不启动阶段Goal或固定自动日程。Goal工具无原生最小时长参数，本地流程检查不等于宿主强制保证。'),
    @('MOD','MOD-10','同一世界与空间侧重','同一世界规则，可通过预设调整；爱油以园内培养为主，虐油以野外惩戒和研究为主。园内可调教不听话者，野外可照护、保护并接纳有潜力或友善个体，不设空间硬隔离。'),
    @('MOD','MOD-11','自由模式保护配置','自由模式支持独立入侵、事故、伤病致死等开关与操作保护；剧情保留已定风险。实际规则开关与含蓄/直接画面档位分开，表现变化不改真实伤势和死亡后果。'),
    @('MOD','MOD-12','主线多种方式','支持玩家按自己的方式走完主线，包括不同管理及惩戒取向；不暗设全部任务只有爱油行为一种解法。保留已定关键损失失败、难度及分支后果，不保证任意破坏任务前提后仍可通关。'),
    @('OBS','OBS-09','自主回应','拒绝、躲避、求助、保护同伴、逃跑和反抗均可发生，受个性、能力、经历与情境影响，不统一执行或必定成功。'),
    @('OBS','OBS-10','心理维度','区分信任、恐惧、依恋、怨恨和服从，允许共存；恐惧服从不等于信任。后续照护可逐渐改善关系，不保证全部影响消除。'),
    @('OBS','OBS-11','信息传播','信息经目击、声音、交谈、事后发现传播，不全园全图自动知道；按不同认知与关系复杂度处理，不新增园外监管惩罚。'),
    @('OBS','OBS-12','归属感与惩戒认知','园内居民有归属感，认为惩戒入侵野生坏油是正义的；年长者或亲油有概率主动惩戒园内不听话者；无理由虐待会使园内及野外个体恐惧。认知受已获信息约束，不读取隐藏真值。'),
    @('DEF','DEF-08','主动护园与呼救','园内居民主动保护领地免受野生坏油入侵，抵御失败可呼喊主人，玩家可以介入驱赶或惩戒。能力不足风险继续存在，呼救不是保证无损的自动救援。'),
    @('DEF','DEF-09','评价与认可分离','群体对惩戒的认可、园区环境/管理评级、居民个人牌照分别处理；v048悬挂惩戒降低园区评分的代价不因正义感取消，不新增一般园外监管。'),
    @('UX','UX-07','互动共同因素','接触位置、力度、持续时间、工具和目标状态共同影响结果，具有基础物理效果；不自动承诺完整生物力学或任意精细局部软体。'),
    @('UX','UX-08','作品参考与可爱表现','多参考已搜集作品，整体可爱。坏油通过表情、姿态、台词与行为体现顽劣和挑衅；不把整个物种固定为坏，也不以永远准确外观标签泄露未知性格。参考须保留出处，不等于直接复制素材或自动采纳设定。'),
    @('UX','UX-09','差异反应','惩戒按具体效果呈现不同状态和反应；抚摸、抱抱、蹭蹭各有不同反应，结合个性与当前状态，不能全部用同一动画或单一好感数值替代。'),
    @('PRD','PRD-09','两类体验回报','爱油主要为培养的乐趣；虐油主要为惩戒与研究的乐趣。专门奖励、研究目录和解锁树仍由实施草案具体化，不将助手上轮全部建议视为逐项批准。')
)
foreach ($a in $additions) {
    $section = $catalog.sections | Where-Object id -eq $a[0]
    $section.rules = @($section.rules) + [pscustomobject]@{ id=$a[1]; status='confirmed'; title=$a[2]; statement=$a[3]; sources=@('planning/requirements-v049.txt') }
}
$catalog.version = 'v002'
$catalog.source_through = 'requirements-v049'
$catalog.implementation_status = 'v049确认与阶段规划已记录，新增机制尚未实施；当前无阶段Goal启动，等待用户指定时间。'
$catalog.supersessions = @($catalog.supersessions) + @(
    [pscustomobject]@{id='CHG-15'; topic='阶段时长'; historical='上一轮框架Goal最多6小时且已结束'; authority='v049'; current='新阶段至少6小时，用户指定开始，独立玩法逐阶段验收。'; rule='GOV-05'},
    [pscustomobject]@{id='CHG-16'; topic='保护开关'; historical='仅明确关闭衰老/自然死亡与无限资源'; authority='v049'; current='自由模式补充独立风险和操作保护开关；表现与规则分开。'; rule='MOD-11'}
)
$catalog | ConvertTo-Json -Depth 30 | Set-Content $destination -Encoding utf8
$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('Yukkuri 乐园：完整设定手册 v002')
$lines.Add('2026-10-27 / Asia/Shanghai；累计requirements-v001—v049。旧汇编保留。本轮记录确认与规划，不代表游戏实现。')
$lines.Add($catalog.precedence)
foreach ($s in $catalog.sections) {
    $lines.Add(''); $lines.Add("$($s.id)　$($s.title)")
    foreach ($r in $s.rules) {
        $lines.Add("$($r.id)　$($r.title)〔已确认〕")
        $lines.Add($r.statement); $lines.Add('来源：' + ($r.sources -join '；')); $lines.Add('')
    }
}
$lines.Add('历史覆盖关系')
foreach ($x in $catalog.supersessions) { $lines.Add("$($x.id)　$($x.topic)：$($x.historical) → $($x.current) 依据：$($x.authority)；规则：$($x.rule)") }
$lines.Add(''); $lines.Add('尚待设计与验证：以下内容不自动成为新问卷或确认参数')
foreach ($x in $catalog.open_items) { $lines.Add("$($x.id)　$($x.topic)：$($x.description) 关联：$($x.related_rule)") }
$lines.Add('新增实施入口：planning/milestones-v001.txt及同名JSON；当前等待用户指定开工时间。')
$lines | Set-Content planning/settings/handbook-v002.txt -Encoding utf8
$rules = @($catalog.sections | ForEach-Object rules)
$map = [ordered]@{schema_version=1; version='v002'; compiled_on='2026-10-27'; timezone='Asia/Shanghai'; note='原始证据反向索引；哈希不表示验收。最新确认优先，旧版完整保留。'; sources=@(1..49 | ForEach-Object {
    $path = 'planning/requirements-v{0:D3}.txt' -f $_
    [ordered]@{version=('v{0:D3}' -f $_); path=$path; title=(Get-Content $path -TotalCount 1); sha256=(Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant(); current_rule_ids=@($rules | Where-Object {$_.sources -contains $path} | ForEach-Object id); status='historical_evidence_latest_confirmation_wins'}
})}
$map | ConvertTo-Json -Depth 10 | Set-Content planning/settings/source-map-v002.json -Encoding utf8
$stageData = @(
    @('M01','一只油与一座小乐园',@(),@('培养与基本照护、资源补给闭环可独立游玩','抚摸抱抱蹭蹭和一种惩戒有不同反馈与基本物理','野外观察保护接纳和园内居民护园呼救可完整经历','有理由惩戒/无理由伤害/未获知者反应对照有效','跨区/特写时钟/存读档保持个体和行为记录')),
    @('M02','可持续经营的乐园与地区',@('M01'),@('收入任务与资源设施员工形成可持续经营闭环','墙门房间地形及导航排队实际可用','各类地区用途和首次旅行/快速交通可用','天气事故入侵与共同防卫产生可见后果','跨区后台及离线停止、权限与园区评分符合规则')),
    @('M03','一家油的世代与救治',@('M02'),@('茎生胎生遗传和亲缘收养完整世代可玩','生命周期和健康寿命累计有真实后果','伤病抢救时窗及昂贵转移可完整操作','身份继承/重考或冒用风险及难度死亡可验证','家庭照护与风险开关有效且保存后不重置')),
    @('M04','六种居民的培养与独立生活',@('M03'),@('首版六种及已定形态和能力差异全部可体验','研究培养路线互斥历史和装备适配有效','金银铜综合评级单项SABC费用概率和越级有效','白金管理工作迁居收益及召回协商可体验','培养投入有实际用途且不惩罚白金培养玩家')),
    @('M05','有反应有记忆的群体与研究',@('M04'),@('心理维度和个性自主反应能在行为中辨认','亲油年长者自治惩戒及群体认知按信息传播','多种工具位置力度时间状态交互有基础物理','研究观察修正及后续处置构成完整循环','理由/身份/已知事实影响反应且无全知与未授权监管')),
    @('M06','按自己方式通关的故事',@('M05'),@('固定角色随机居民和完整章节可从开局走到结局','培养取向和惩戒研究取向均有实玩通关证据','关键损失分支和失败符合规则','继续经营与继承新周目均可操作','任务地区经济生态与居民变化真正联动')),
    @('M07','完整表现自由模式与三语',@('M06'),@('六种形态及互动场景动画声音完整且样板经用户确认','简中英日实际界面教程剧情事件完整覆盖','预设与独立风险开关及操作保护可体验','新玩家教学设置音量及辅助可用','来源许可记录与作品参考用途可追溯')),
    @('M08','v1.0全流程整合验收',@('M07'),@('全部首版规则对照实际入口验收无无主遗留项','两类玩法主线结局新周目自由长期游玩完整','50/100只完整场景跨区和长期存档性能验证','用户真实试玩问题修复并回归原失败案例','完整本地Windows交付源代码说明证据三语素材齐全'))
)
$stages = @($stageData | ForEach-Object {
    $entry=$_; $checks=@(); $i=0
    foreach($text in $entry[3]) { $i++; $checks += [ordered]@{id=('{0}-C{1:D2}' -f $entry[0],$i); requirement=$text} }
    [ordered]@{id=$entry[0]; title=$entry[1]; status='planned'; dependencies=@($entry[2]); minimum_work_hours=6; scheduled_start=$null; goal_id=$null; acceptance=$checks; user_acceptance='pending'}
})
$primary = @{PRD='M08';MOD='M06';WRL='M02';OBS='M05';BIO='M03';REP='M03';EVO='M04';SPC='M04';CRT='M04';PLT='M04';MED='M03';DEF='M02';OPS='M02';UX='M07';GOV='M08'}
$constraints = @('PRD-01','PRD-03','PRD-05','PRD-06','PRD-07','PRD-08','GOV-01','GOV-02','GOV-03','GOV-04','GOV-05','GOV-06','GOV-07')
$deferred = @('SPC-08','SPC-09','EVO-15')
$overrides = @{'MOD-10'='M01';'MOD-11'='M07';'MOD-12'='M06';'OBS-09'='M01';'OBS-10'='M01';'OBS-11'='M01';'OBS-12'='M01';'DEF-08'='M01';'UX-07'='M01';'UX-09'='M01';'PRD-09'='M01'}
$mapping = @($rules | ForEach-Object {
    $id=$_.id; $section=$id.Split('-')[0]; $stage=$primary[$section]; if($overrides.ContainsKey($id)) {$stage=$overrides[$id]}
    $disposition='first_release'; if($constraints -contains $id){$disposition='project_constraint'}; if($deferred -contains $id){$disposition='deferred_scope_boundary'}
    [ordered]@{rule_id=$id; title=$_.title; primary_stage=$stage; disposition=$disposition; status='unverified'; final_verification_stage='M08'}
})
[ordered]@{schema_version=1; version='v001'; dated='2026-10-27'; authority='planning/requirements-v049.txt'; rules_catalog=$destination; plan='planning/milestones-v001.txt'; note='实施分期非新增设定；所有阶段尚未开始，开工时间由用户指定。覆盖映射不代表实现或验收。'; stages=$stages; rule_coverage=$mapping} | ConvertTo-Json -Depth 15 | Set-Content planning/milestones-v001.json -Encoding utf8
Write-Output "Recorded $($rules.Count) rules, 49 source versions, $($stages.Count) milestones."
