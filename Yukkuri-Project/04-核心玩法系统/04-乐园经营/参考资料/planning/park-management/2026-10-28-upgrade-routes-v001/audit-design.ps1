$ErrorActionPreference = 'Stop'
$routeFolder = $PSScriptRoot
$routeData = Get-Content -LiteralPath (Join-Path $routeFolder 'upgrade-catalog.json') -Raw | ConvertFrom-Json
$balanceData = Get-Content -LiteralPath (Join-Path $routeFolder 'balance-assumptions.json') -Raw | ConvertFrom-Json
$routeChecks = [System.Collections.Generic.List[object]]::new()
function Add-DesignCheck([string]$CheckName, [bool]$Passed, [string]$Detail) {
 $routeChecks.Add([pscustomobject]@{name=$CheckName;passed=$Passed;detail=$Detail})
}
$routeNodes = @($routeData.levels)
Add-DesignCheck 'node_count' ($routeNodes.Count -eq 36) 'Expected 15 area levels and 21 house levels.'
Add-DesignCheck 'unique_ids' ((@($routeNodes.id | Select-Object -Unique)).Count -eq 36) 'IDs must be unique.'
Add-DesignCheck 'kind_counts' ((@($routeNodes | Where-Object kind -eq '区域')).Count -eq 15 -and (@($routeNodes | Where-Object kind -eq '房屋')).Count -eq 21) 'Area and building levels checked independently.'
$routeLookup = @{}
foreach($routeNode in $routeNodes) { $routeLookup[$routeNode.id]=$routeNode }
$familyOk=$true
foreach($routeGroup in ($routeNodes | Group-Object family)) {
 if ((($routeGroup.Group.level | Sort-Object) -join ',') -ne '1,2,3') { $familyOk=$false }
}
Add-DesignCheck 'family_levels' ($familyOk -and (@($routeNodes | Group-Object family)).Count -eq 12) 'Every family has levels 1,2,3.'
$refsOk=$true; $fieldsOk=$true; $costsOk=$true; $parentsOk=$true
foreach($routeNode in $routeNodes) {
 foreach($routeDep in $routeNode.prerequisites) { if(-not $routeLookup.ContainsKey($routeDep)) {$refsOk=$false} }
 foreach($routeField in @('name','new_gameplay','autonomous_work','exception_handling','visible_change','player_choice','capacity_draft')) {
  if([string]::IsNullOrWhiteSpace([string]$routeNode.$routeField)) {$fieldsOk=$false}
 }
 foreach($routeField in @('staff','space','evidence')) {
  if([string]::IsNullOrWhiteSpace([string]$routeNode.conditions.$routeField)) {$fieldsOk=$false}
 }
 if($routeNode.cost_funds -le 0 -or $routeNode.construction_shifts -le 0 -or $null -eq $routeNode.maintenance_funds_per_cycle -or $routeNode.maintenance_funds_per_cycle -lt 0) {$costsOk=$false}
 if($routeNode.level -gt 1 -and ($routeNode.family+'-'+($routeNode.level-1)) -notin $routeNode.prerequisites) {$parentsOk=$false}
}
Add-DesignCheck 'dependencies_exist' $refsOk 'All prerequisites resolve to a node in this design.'
Add-DesignCheck 'required_design_fields' $fieldsOk 'Each level has conditions, gameplay, autonomy, recovery, visible effects and player decisions.'
Add-DesignCheck 'cost_fields' $costsOk 'Draft costs and construction shifts positive; maintenance nonnegative.'
Add-DesignCheck 'previous_level_required' $parentsOk 'Levels 2 and 3 include the preceding tier.'
$routeDone=@{}; $routeOrder=[System.Collections.Generic.List[string]]::new()
while($routeDone.Count -lt $routeNodes.Count) {
 $madeProgress=$false
 foreach($routeNode in $routeNodes) {
  if($routeDone.ContainsKey($routeNode.id)){continue}
  $ready=$true
  foreach($routeDep in $routeNode.prerequisites){if(-not $routeDone.ContainsKey($routeDep)){$ready=$false}}
  if($ready){$routeDone[$routeNode.id]=$true;$routeOrder.Add($routeNode.id);$madeProgress=$true}
 }
 if(-not $madeProgress){break}
}
Add-DesignCheck 'acyclic_and_reachable' ($routeDone.Count -eq $routeNodes.Count) 'Topological reference check only; actor learning and economic feasibility require future gameplay tests.'
$levelCases = @(Import-Csv -LiteralPath (Join-Path $routeFolder 'level-acceptance.tsv') -Delimiter ([char]9))
Add-DesignCheck 'planned_cases_cover_nodes' ($levelCases.Count -eq 36 -and (@($levelCases.node_id | Sort-Object) -join ',') -eq (@($routeNodes.id | Sort-Object) -join ',')) 'One future acceptance entry per node.'
Add-DesignCheck 'no_false_test_status' ((@($levelCases | Where-Object status -ne 'planned_not_run')).Count -eq 0 -and (@($routeNodes | Where-Object implementation_status -ne 'design_only')).Count -eq 0) 'Game behaviors remain untested.'
$routeRequiredFiles=@('TASK.txt','STATUS.txt','README.txt','01-route-overview.txt','02-rules-and-economy.txt','03-area-routes.txt','04-house-routes.txt','05-autonomy-and-workload.txt','06-acceptance-and-failures.txt','07-implementation-handoff.txt','upgrade-catalog.json','balance-assumptions.json','level-acceptance.tsv')
$fileOk=$true
foreach($routeFile in $routeRequiredFiles){if(-not(Test-Path -LiteralPath (Join-Path $routeFolder $routeFile))){$fileOk=$false}}
Add-DesignCheck 'handoff_files_exist' $fileOk 'Reader-facing design and handoff files exist.'
$areaText=Get-Content -LiteralPath (Join-Path $routeFolder '03-area-routes.txt') -Raw
$houseText=Get-Content -LiteralPath (Join-Path $routeFolder '04-house-routes.txt') -Raw
$textOk=$true
foreach($routeNode in $routeNodes) {
 $routeText=if($routeNode.kind -eq '区域'){$areaText}else{$houseText}
 if(-not $routeText.Contains($routeNode.id+'  '+$routeNode.name) -or -not $routeText.Contains($routeNode.new_gameplay) -or -not $routeText.Contains($routeNode.autonomous_work) -or -not $routeText.Contains(([string]$routeNode.cost_funds)+'资金；')){$textOk=$false}
}
Add-DesignCheck 'text_catalog_alignment' $textOk 'Node names, new gameplay, autonomy and costs agree with readable route documents.'
$maintenance=0
foreach($initialAsset in $balanceData.initial.facility_instances){$maintenance += $routeLookup[$initialAsset].maintenance_funds_per_cycle}
$fixed=$maintenance+$balanceData.initial.food_cost+$balanceData.initial.human_support_cost+$balanceData.initial.other_routine_allowance_cost
$subsidy=$balanceData.initial.conditional_care_contract_income
$service=$balanceData.services.basic_boarding
$netBase=$subsidy-$fixed
$netService=$subsidy+$service.income-$fixed-$service.direct_cost
$reserve=$balanceData.reserve_cycles*$fixed
$reserveCommitted=$balanceData.reserve_cycles*($fixed+$service.direct_cost)
$ordinaryBeds=@($balanceData.initial.facility_instances | Where-Object {$_ -eq 'HL-1'}).Count*$balanceData.initial.ordinary_bed_capacity_per_HL1
$load=0
foreach($loadProperty in $balanceData.initial_task_load.PSObject.Properties){$load += [int]$loadProperty.Value}
Add-DesignCheck 'initial_example_arithmetic' ($maintenance -eq 9 -and $fixed -eq 97 -and $netBase -eq 3 -and $netService -eq 103 -and $reserve -eq 291 -and $reserveCommitted -eq 531) 'Arithmetic only, not economy simulation.'
Add-DesignCheck 'initial_beds_and_draft_load' ($ordinaryBeds -eq 8 -and $ordinaryBeds -ge $balanceData.initial.resident_count -and $load -eq 10 -and $load -le ($balanceData.initial.workers*$balanceData.initial.worker_fixed_load_budget)) 'Template has 8 ordinary beds for 6 residents and draft fixed work 10 within 12; real time/physical feasibility untested.'
$allOk=(@($routeChecks | Where-Object {-not $_.passed})).Count -eq 0
$report=[ordered]@{report_type='design_structure_and_arithmetic_only';date='2026-10-28';passed=$allOk;checks=$routeChecks;topological_order=$routeOrder;example=[ordered]@{maintenance=$maintenance;fixed_expense=$fixed;base_net=$netBase;service_net=$netService;reserve=$reserve;reserve_with_service=$reserveCommitted;ordinary_beds=$ordinaryBeds;fixed_work_load=$load};not_tested=@('game implementation','economic balance','actual actor autonomy','construction navigation','GUI','player enjoyment','50-100 resident performance');}
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $routeFolder 'design-audit.json') -Encoding utf8
[pscustomobject]@{passed=$allOk;checks=$routeChecks.Count;nodes=$routeNodes.Count;report=(Join-Path $routeFolder 'design-audit.json')}
if(-not $allOk){$routeChecks | Where-Object {-not $_.passed} | Format-List;exit 1}

