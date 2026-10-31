using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private PanelContainer _debugPanel=null!;
    private OptionButton _debugScope=null!,_debugCreature=null!;
    private LineEdit _debugSearch=null!;
    private TextEdit _debugValues=null!;
    private Label _debugSummary=null!,_debugPageLabel=null!;
    private Button _debugFreezeButton=null!,_debugPrevious=null!,_debugNext=null!;
    private bool _debugFrozen;
    private int _debugPage;
    private ulong _debugCapturedAt;
    private string _debugSnapshotJson="",_debugCreatureList="";
    private string? _debugSourceId;
    private WorldState? _debugWorldSource;
    private double _debugWorldSeconds;
    private readonly List<string> _debugCreatureIds=new();
    private readonly List<string> _debugRows=new();
    private const int DebugPageSize=160;

    // Debugging reads state. It never runs a simulation step, spends resources,
    // changes a creature, or persists a private second copy of gameplay state.
    private bool DebugPanelVisible=>_debugPanel!=null&&_debugPanel.Visible;
    private bool DebugContainsPoint(Vector2 point)=>DebugPanelVisible&&_debugPanel.GetGlobalRect().HasPoint(point);

    private void BuildDebugPanel()
    {
        _debugPanel=Panel(_ui,"f3f0e6");
        _debugPanel.Name="LiveDebugPanel";
        _debugPanel.AnchorLeft=1;_debugPanel.AnchorRight=1;_debugPanel.AnchorBottom=1;
        _debugPanel.OffsetLeft=-490;_debugPanel.OffsetRight=-14;
        _debugPanel.OffsetTop=88;_debugPanel.OffsetBottom=-218;_debugPanel.Visible=false;
        var scroll=new ScrollContainer{HorizontalScrollMode=ScrollContainer.ScrollMode.Disabled,
            SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,SizeFlagsVertical=Control.SizeFlags.ExpandFill};
        _debugPanel.AddChild(scroll);
        var column=Column(scroll,7);column.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        column.SizeFlagsVertical=Control.SizeFlags.ExpandFill;
        var heading=Row(column,6);
        var title=Text("调试 · 实时参数",18);title.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;heading.AddChild(title);
        heading.AddChild(Button("关闭 · F3",ToggleDebugPanel));
        column.AddChild(Text("只读观测，不暂停小院。自然时间仍遵守暂停和近看规则。",12,"78816e"));
        _debugCreature=new OptionButton{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};column.AddChild(_debugCreature);
        _debugCreature.ItemSelected+=index=>
        {
            if(index<0||index>=_debugCreatureIds.Count)return;
            _debugFrozen=false;_debugPage=0;
            string id=_debugCreatureIds[(int)index];
            if(id.StartsWith("stem:",StringComparison.Ordinal))SelectDetachedStem(id[5..]);
            else if(id.StartsWith("station:",StringComparison.Ordinal))SelectSupplyStation(id[8..]);
            else SelectOil(id[9..]);
            RefreshDebugPanel(true);
        };
        _debugSummary=Text("",13);column.AddChild(_debugSummary);
        var tools=Row(column,5);
        _debugScope=new OptionButton{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        _debugScope.AddItem("选中对象 · 全部状态");_debugScope.AddItem("世界 · 全部个体与资源");_debugScope.AddItem("模拟配置与当前输入");
        tools.AddChild(_debugScope);
        _debugScope.ItemSelected+=_=>{_debugPage=0;_debugFrozen=false;ClearDebugComparison();RefreshDebugPanel(true);};
        _debugFreezeButton=Button("冻结读数",()=>
        {
            _debugFrozen=!_debugFrozen;
            if(!_debugFrozen)RefreshDebugPanel(true);
            else {_debugFreezeButton.Text="恢复读数";RenderDebugRows();}
        });tools.AddChild(_debugFreezeButton);
        _debugSearch=new LineEdit{PlaceholderText="筛选中文名称、字段路径或当前值",ClearButtonEnabled=true};column.AddChild(_debugSearch);
        _debugSearch.TextChanged+=_=>{_debugPage=0;RenderDebugRows();};
        BuildDebugComparisonControls(column);
        _debugValues=new TextEdit{Editable=false,SizeFlagsVertical=Control.SizeFlags.ExpandFill,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,
            CustomMinimumSize=new(0,110)};
        _debugValues.AddThemeFontSizeOverride("font_size",13);column.AddChild(_debugValues);
        var pages=Row(column,5);
        _debugPrevious=Button("上一页",()=>{_debugPage=Math.Max(0,_debugPage-1);RenderDebugRows();});pages.AddChild(_debugPrevious);
        _debugPageLabel=Text("",12);_debugPageLabel.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;pages.AddChild(_debugPageLabel);
        _debugNext=Button("下一页",()=>{_debugPage++;RenderDebugRows();});pages.AddChild(_debugNext);
        column.AddChild(Button("复制此范围的完整参数",()=>
        {
            if(!_debugFrozen)RefreshDebugPanel(true);
            if(_debugSnapshotJson.Length>0)DisplayServer.ClipboardSet(_debugSnapshotJson);
        }));
    }

    private void ToggleDebugPanel()
    {
        if(_debugPanel==null||_modalOpen)return;
        CancelGesture();
        bool show=!_debugPanel.Visible;
        _debugPanel.Visible=show;_rightPanel.Visible=!show;
        if(show){_debugFrozen=false;RefreshDebugPanel(true);}
        else GetViewport().GuiGetFocusOwner()?.ReleaseFocus();
    }

    private void RefreshDebugPanel(bool force=false)
    {
        if(!DebugPanelVisible||Sim==null)return;
        NormalizeDetachedStemSelection();
        NormalizeSupplyStationSelection();
        float available=Math.Max(200,_ui.Size.X-28);
        _debugPanel.OffsetLeft=-Math.Min(available,Math.Clamp(_ui.Size.X*.38f,330,520))-14;
        RefreshDebugCreatureSelector();
        string sourceId=_selectedSupplyStationId is { } stationId?"station:"+stationId:
            _selectedDetachedStemEventId is { } stemId?"stem:"+stemId:"creature:"+Sim.State.SelectedId;
        if(_debugSourceId!=sourceId||!ReferenceEquals(_debugWorldSource,Sim.State))
        {
            // Selecting in the scene must not relabel a frozen old creature's
            // values as the newly selected creature.
            _debugFrozen=false;_debugPage=0;force=true;
            ClearDebugComparison();
        }
        ulong now=Time.GetTicksMsec();
        if(_debugFrozen||!force&&now-_debugCapturedAt<250)return;
        _debugCapturedAt=now;_debugFreezeButton.Text="冻结读数";
        _debugSourceId=sourceId;_debugWorldSource=Sim.State;_debugWorldSeconds=Sim.State.WorldSeconds;
        var c=Sim.Selected;
        var brood=_selectedDetachedStemEventId is { } selectedStem?Sim.State.DetachedStemBroods.Find(s=>s.EventId==selectedStem):null;
        var station=_selectedSupplyStationId is { } selectedStation?Sim.State.SupplyStations.Find(s=>s.Id==selectedStation):null;
        var stationView=station==null?null:Sim.SupplyStationStatus(station.Id);
        _debugSummary.Text=stationView!=null?
            $"{SupplyStationName(stationView.Kind)}\n仓内 {stationView.StoredPortions} · 出口 {stationView.OutletPortions} · 总容量 {stationView.Capacity}\n{stationView.LastEvent}":brood!=null?
            $"离体茎 · {brood.Mother.Name}\n完整度 {brood.Integrity:0.0}　水分 {brood.Hydration:0.0}　营养 {brood.Nutrition:0.0}\n"+
            $"瓶体完整 {brood.VesselIntegrity:0.0}　发育 {brood.Elapsed:0.0} 秒\n{brood.LastEvent}":
            c==null?"当前未选中对象，可切换到世界范围。":
            $"{c.Name} · {c.Activity}\n生命 {c.Health:0.0}　信任 {c.Trust:0.0}　恐惧 {c.Fear:0.0}　疼痛 {c.Pain:0.0}\n"+
            $"馒馒来度 {c.Interaction.Emotion.Manmanlai:0.0}　惊惧余波 {c.Interaction.Emotion.Alarm:0.0}\n"+
            $"饱食 {c.Fullness:0.0}　水分 {c.Interaction.Hydration:0.0}　精力 {c.Energy:0.0}\n"+
            $"对人安全感 {c.Interaction.HumanBond.Safety:0.0}　性格 {c.TrueDisposition}";
        try
        {
            // The world includes newly added state automatically, rather than a
            // hand-maintained whitelist silently hiding future gameplay fields.
            object? source=_debugScope.Selected switch
            {
                1=>Sim.State,
                2=>new { Configuration=Sim.Config, Sim.State.Settings, Tool=_action, ToolArmed=_toolArmed,
                    BaseForce=_force, AppliedForce=_appliedForce, PointerHeld=_mouseHeld, CarryHeight=_height,
                    RescueToolArmed=_waterRescueToolArmed, ClickOfferTarget=_clickOfferId,
                    SelectedDetachedStem=_selectedDetachedStemEventId, HeldSupply=_heldSupplyId,
                    SelectedSupplyStation=_selectedSupplyStationId, PlacingSupplyStation=_placingSupplyStationKind,
                    Sim.CurrentInteractionTargetKind, GestureObject=Sim.InteractionGestureObject,
                    Sim.InteractionCompletionProgress, Sim.UsesConsumableStock,
                    SelectedCreatureWaterSupport=_selectedDetachedStemEventId==null&&_selectedSupplyStationId==null&&c!=null?Sim.WaterSupportStatus(c):null,
                    SelectedCreatureWaterAttempt=_selectedDetachedStemEventId==null&&_selectedSupplyStationId==null&&c!=null?Sim.WaterAttemptStatus(c):null,
                    SelectedCreatureHungerFeeding=_selectedDetachedStemEventId==null&&_selectedSupplyStationId==null&&c!=null?Sim.HungerFeedingStatus(c):null,
                    Sim.HasInteractionGesture, GestureTarget=Sim.InteractionGestureTarget, Sim.CurrentInteractionAction,
                    Sim.InteractionIntakeAccepted, Sim.State.Mode, Sim.State.WorldSeconds },
                _=>station is not null?(object)new { State=station, Interaction=stationView }:
                    brood is not null?(object)new { State=brood, Interaction=Sim.DetachedStemStatus(brood.EventId) }:c
            };
            JsonElement snapshot=DebugStateSnapshot.Capture(source);
            _debugSnapshotJson=snapshot.GetRawText();
            _debugParameters.Clear();CollectDebugParameters(snapshot,"","",_debugParameters);
            _debugRows.Clear();_debugRows.AddRange(_debugParameters.Values.Select(parameter=>FormatDebugParameter(parameter.Path,parameter.Value)));
            RefreshDebugComparisonControls();
            RenderDebugRows();
        }
        catch(Exception error) when(error is JsonException or NotSupportedException or InvalidOperationException or System.Reflection.TargetInvocationException)
        {
            _debugRows.Clear();_debugParameters.Clear();_debugSnapshotJson="";
            RefreshDebugComparisonControls();
            _debugValues.Text="本次参数快照读取失败："+error.Message;
            _debugPageLabel.Text="未显示旧快照；小院继续运行";
            _debugPrevious.Disabled=_debugNext.Disabled=true;
        }
    }

    private void RefreshDebugCreatureSelector()
    {
        var stems=Sim.State.DetachedStemBroods.Where(s=>s.RegionId==Sim.State.ActiveRegionId).ToArray();
        var stations=Sim.State.SupplyStations.Where(s=>s.RegionId==Sim.State.ActiveRegionId).ToArray();
        string key=string.Join("\n",Sim.State.Creatures.Select(c=>c.Id+"="+c.Name))+"\nSTEMS\n"+
            string.Join("\n",stems.Select(s=>s.EventId+"="+s.Mother.Name))+"\nSTATIONS\n"+
            string.Join("\n",stations.Select(s=>s.Id+"="+s.Kind));
        if(key!=_debugCreatureList)
        {
            _debugCreatureList=key;_debugCreature.Clear();_debugCreatureIds.Clear();
            foreach(var c in Sim.State.Creatures){_debugCreature.AddItem(c.Name);_debugCreatureIds.Add("creature:"+c.Id);}
            for(int n=0;n<stems.Length;n++)
            {_debugCreature.AddItem($"离体茎 {n+1} · {stems[n].Mother.Name}");_debugCreatureIds.Add("stem:"+stems[n].EventId);}
            for(int n=0;n<stations.Length;n++)
            {_debugCreature.AddItem($"{SupplyStationName(stations[n].Kind)} {n+1}");_debugCreatureIds.Add("station:"+stations[n].Id);}
        }
        int selected=_debugCreatureIds.IndexOf(_selectedSupplyStationId is { } sid?"station:"+sid:
            _selectedDetachedStemEventId is { } id?"stem:"+id:"creature:"+Sim.State.SelectedId);
        if(_debugCreature.Selected!=selected)_debugCreature.Select(selected);
    }

    private void RenderDebugRows()
    {
        if(_debugValues==null||_debugSnapshotJson.Length==0)return;
        string search=_debugSearch.Text.Trim();
        var sourceRows=_debugChangesOnly?DebugChangedRows():_debugRows;
        var rows=search.Length==0?sourceRows:sourceRows.Where(row=>row.Contains(search,StringComparison.OrdinalIgnoreCase)).ToList();
        int pages=Math.Max(1,(rows.Count+DebugPageSize-1)/DebugPageSize);
        _debugPage=Math.Clamp(_debugPage,0,pages-1);
        string content=rows.Count==0?(_debugChangesOnly&&search.Length==0?"与记下的读数相比，当前没有变化。":"没有匹配参数。清空筛选可查看全部。"):string.Join("\n",rows.Skip(_debugPage*DebugPageSize).Take(DebugPageSize));
        if(_debugValues.Text!=content)
        {
            double vertical=_debugValues.ScrollVertical;int horizontal=_debugValues.ScrollHorizontal;
            _debugValues.Text=content;_debugValues.ScrollVertical=vertical;_debugValues.ScrollHorizontal=horizontal;
        }
        _debugPageLabel.Text=$"{_debugPage+1}/{pages} 页 · {rows.Count} 项"+(_debugFrozen?$" · 冻结于 {_debugWorldSeconds:0.0}秒":" · 实时");
        _debugPrevious.Disabled=_debugPage==0;_debugNext.Disabled=_debugPage==pages-1;
    }

    private static string DebugParameterLabel(string name)=>name switch
    {
        "SupplyStations"=>"食水供给器","SupplyStationSequence"=>"供给器身份序号",
        "FoodStationStock"=>"空饭饭供给器库存","WaterStationStock"=>"空饮水器库存","StationWaterPortions"=>"储备清水份数",
        "StoredPortions"=>"仓内份数","OutletPortions"=>"出口实际份数","OutletObjectId"=>"出口物品身份",
        "OutputSequence"=>"出口物品创建序号","OutletX"=>"出口横坐标","OutletZ"=>"出口纵坐标",
        "SelectedSupplyStation"=>"选中供给器","PlacingSupplyStation"=>"待放置供给器种类",
        "CanRefill"=>"目前可以补充","CanMove"=>"目前可以搬动",
        "Health"=>"生命值","Trust"=>"信任度","Fear"=>"恐惧值","Pain"=>"疼痛","Resentment"=>"不满",
        "Fullness"=>"饱食度","Hydration"=>"水分","Energy"=>"精力","TrueDisposition"=>"性格",
        "Comfort"=>"舒适度","Manmanlai"=>"馒馒来度","Safety"=>"对人安全感","Familiarity"=>"熟悉度",
        "Alarm"=>"惊惧余波","SafeSeconds"=>"持续安全时间","LastThreatAt"=>"最近威胁时间",
        "Emotion"=>"情绪","HumanBond"=>"对人的记忆","Personality"=>"性格特征","Kindness"=>"善意",
        "Pride"=>"傲慢","Carelessness"=>"粗心","Relationships"=>"关系","PersonalMemories"=>"个人记忆",
        "LastHarmAt"=>"最近受伤时间","LastProtectionAt"=>"最近保护收益时间","Caution"=>"部位警戒","Condition"=>"身体状态","HealthStatus"=>"伤势状态",
        "Activity"=>"当前活动","Expression"=>"表情","Speech"=>"台词","ReactionRemaining"=>"反应剩余时间",
        "FootIntegrity"=>"足底功能","FootRecovery"=>"足底恢复","Wetness"=>"潮湿","Damage"=>"损伤",
        "Carried"=>"被托举","Falling"=>"下落中","Restraint"=>"固定方式","Height"=>"高度",
        "InWater"=>"落水","ExposureSeconds"=>"泡水时间","Struggle"=>"挣扎","RecoverySeconds"=>"恢复时间",
        "WaterAftercare"=>"离水后守候","WaterConcernEventId"=>"关切的落水事件","EnteredAt"=>"本次入水时间",
        "PlayerCaused"=>"本次落水由人类造成","HumanRescueCredited"=>"本次救助已结算",
        "AttemptSequence"=>"实际挣扎尝试序号","AttemptGeneration"=>"挣扎尝试换代标识",
        "AttemptAt"=>"实际尝试时间","AttemptOutcome"=>"实际尝试结果","NextAttemptAt"=>"下次尝试资格检查时间",
        "ContactSeconds"=>"有效接触累计秒数","Remembered"=>"已留下关系记忆",
        "HungerFeeding"=>"饥饿袭咬摄食","HungerTargetId"=>"当前饥饿目标","RemainsNutrition"=>"个体剩余可摄食储备",
        "TakenNutrition"=>"本次实际摄食量","Committed"=>"本次已入口结算","ContactProgress"=>"实际接触准备进度",
        "ContactStartedAt"=>"本次接触开始时间","LastContactAt"=>"上次有效接触时间","EntryAt"=>"实际入口时间",
        "IntakeSequence"=>"入口序号","IntakeGeneration"=>"入口序号换代标识",
        "Money"=>"金钱","Food"=>"食物库存","Medicine"=>"护理包库存","CriticalRemaining"=>"救治剩余时间","UnlimitedResources"=>"免费购买（旧沙盒也不限物资）","UsesConsumableStock"=>"实际消耗食物与护理库存","WorldSeconds"=>"世界时间",
        "WaterRescueTools"=>"救援用品库存","WaterRescuePurchases"=>"救援用品购买次数",
        "CourtshipTools"=>"诱导用品库存","CourtshipToolPurchases"=>"诱导用品购买次数",
        "BreedingInterest"=>"繁育兴趣","InducedInterestRemaining"=>"诱导剩余时间","NextInductionAt"=>"下次可诱导时间",
        "Pregnancy"=>"孕育状态","PlannedChildren"=>"本窝计划数量","Burden"=>"孕育负担",
        "RecoveryRemaining"=>"恢复剩余时间","Warmth"=>"幼体保暖","CareSeconds"=>"稳定照护累计",
        "CareTargetId"=>"当前照护对象","CareParcelId"=>"当前递送份额",
        "HeatTension"=>"灼烧紧绷","HitCount"=>"连续命中","HitRemaining"=>"连击剩余时间",
        "Pause"=>"暂停","LethalInjuries"=>"致命后果","AgeSeconds"=>"身体年龄","InnerAgeSeconds"=>"内在年龄",
        "CareRemaining"=>"护理剩余时间","CareLastEvent"=>"照护记录","EatingRemaining"=>"吞咽剩余时间",
        "Stem"=>"外茎幼芽","Integrity"=>"完整度","SupportRemaining"=>"支护剩余时间",
        "LastCareAt"=>"最近支护时间","LastDamageAt"=>"最近受损时间","LastDamageCause"=>"最近受损原因","LastDamageActorId"=>"最近施害者",
        "DetachedStemBroods"=>"离体茎记录","Nutrition"=>"营养","VesselIntegrity"=>"瓶体完整度",
        "StemVessels"=>"空瓶库存","StemVesselPurchases"=>"空瓶购买次数","DetachedAt"=>"离体时间",
        "Cleared"=>"残留已收拾","CanClear"=>"当前可收拾","CanUnbottle"=>"当前可撤出活窝",
        "HeldByHand"=>"正被手持","Lowering"=>"正在慢慢放下","Rolling"=>"正在翻滚","RollOrientation"=>"翻滚方向",
        "InteractionHandPresent"=>"人类正在场中陪伴","InteractionHandUntil"=>"当前在场有效截止时间","InteractionHandRegionId"=>"人类在场区域",
        "ReactionPose"=>"即时反应姿态","ReactionPoseRemaining"=>"即时姿态剩余时间","ReactionSituation"=>"即时反应情境",
        "ReactionRefused"=>"本次反应为拒绝","ReactionDirectionX"=>"反应方向X","ReactionDirectionZ"=>"反应方向Z",
        "BallContactCooldown"=>"软球接触间隔","HarshResponseRemaining"=>"严厉言语回应剩余时间",
        "PendingConversation"=>"等待回应的话语","ConversationPraise"=>"等待回应的夸奖","ConversationReactionSerial"=>"对话对应反应序号","ConversationDelay"=>"对话等待时间",
        _=>""
    };
}
