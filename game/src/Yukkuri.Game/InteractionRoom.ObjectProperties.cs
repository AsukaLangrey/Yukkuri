using Godot;
using System;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private void RefreshRedesignProperties()
    {
        RefreshDevelopmentUi();
        if(_objectDetails==null)return;
        var activeCategory=Toolbar.First(t=>t.Entries.Any(e=>e.Mode==_roomMode));
        var entry=activeCategory.Entries.First(e=>e.Mode==_roomMode);
        _title.Text=activeCategory.Name+(_roomMode!=RoomMode.Observe?" · "+entry.Name:"");
        if(_waterRescueToolArmed){_title.Text="水域救援用品";_hint.Text="点击落水的油，成功救援才消耗一份用品。";}
        else if(_arrivingCreature!=null)
        {_title.Text="投放新油";_hint.Text=$"点击干燥空地放下一只新油；绿色可放，红色需换位置。成功后扣 {WorldSimulation.NewCreaturePrice} 元，右键取消。";}
        else if(_roomMode==RoomMode.Observe)_hint.Text="只选中查看，不接触、不抓取。右侧显示对象属性和家庭记录。";
        else if(_roomMode==RoomMode.Grab)_hint.Text="按住抓取，松手放下；释放速度决定抛出。拖到画布左下回收图标可收纳，资源不退款。";
        else if(_roomMode==RoomMode.Pet)_hint.Text="停在头顶或脸侧即可接触；按住轻拖贴地挪动，障碍仍阻挡。";
        else if(_roomMode==RoomMode.Poke)_hint.Text="轻点部位；空地按住后挥向身体，速度决定掌掴或拳击。长按部位拖动可拉拽。";
        else if(_roomMode==RoomMode.Pressure)_hint.Text="按住部位按压；从下半部向上推可推翻。身旁可保持或解除按压。";
        else if(_roomMode==RoomMode.Teaching)_hint.Text="点击脏处附近的油，示范自己打扫卫生。";
        else if(_roomMode==RoomMode.Shelter)_hint.Text=ToolHint(_action);
        var c=Sim.Selected;
        var obj=Sim.State.RoomObjects.Find(o=>o.Id==_selectedRoomObjectId&&!o.Stored);
        var attached=_observedStemMotherId==null?null:Sim.StemCareStatus(_observedStemMotherId);
        bool independent=obj!=null||attached!=null||_selectedSupplyStationId!=null||_selectedDetachedStemEventId!=null||_selectedShelterId!=null;
        _objectActions.Visible=!independent&&c!=null&&!_modalOpen&&_objectActions.GetChildCount()>0;
        _objectDetails.Visible=!independent;
        if(attached!=null)
        {
            _objectDetails.Visible=true;_who.Text="外茎与幼芽";
            _objectDetails.Text=$"完整度 {attached.Integrity:0} · 水分 {attached.Hydration:0}\n成长 {attached.Progress:P0} · 计划幼体 {attached.PlannedChildren}\n支护剩余 {attached.SupportRemaining:0}秒";
            _creatureVitalMeters.Visible=_condition.Visible=_memory.Visible=_memoryHeading.Visible=false;_closeButton.Disabled=true;
        }
        else if(obj!=null)
        {
            _speechLabel.Text="";
            _objectDetails.Visible=true;_objectDetails.Text=RoomResourceText.Describe(obj,Sim.State)+$"\n实际份数 {obj.Portions}";
            _who.Text=RoomResourceText.Label(obj,Sim.State);
            _creatureVitalMeters.Visible=_condition.Visible=_memory.Visible=_memoryHeading.Visible=false;
            _familyPanelButton.Disabled=_closeButton.Disabled=true;
        }
        else if(!independent&&c!=null)
        {
            string Name(string? id)=>Sim.State.Creatures.Find(o=>o.Id==id)?.Name??"未记录";
            var relations=new System.Collections.Generic.List<string>();
            if(c.Family.MotherId!=null)relations.Add("母亲："+Name(c.Family.MotherId));
            if(c.Family.FatherId!=null)relations.Add("父亲："+Name(c.Family.FatherId));
            if(c.Family.PartnerIds.Count>0)relations.Add("伴侣："+string.Join("、",c.Family.PartnerIds.Select(id=>Name(id))));
            _objectDetails.Text=Sim.EnvironmentSummary(c)+"\n"+GrowthRules.Name(c)+$" · 年龄 {c.Body.AgeSeconds/GrowthRules.Day:0.0} 天 · 健壮度 {c.Robustness:P0}\n"+string.Join("\n",relations);
            if(c.Interaction.LastExaminationAt>=0)_objectDetails.Text+=$"\n检查时间 {c.Interaction.LastExaminationAt:0}秒\n"+c.Interaction.Examination;
        }
        else _objectDetails.Text="";
        _objectDetails.Visible=_objectDetails.Visible&&_objectDetails.Text.Length>0;
        string signature=independent?"object":$"{c?.Id}:{c?.Interaction.Accessory.Place}:{c?.Interaction.Accessory.Integrity<=0}:{c?.Interaction.Restraint}:{c?.Interaction.Supine}:{c?.Family.ColdStorage.Stored}:{c?.Condition}:{c?.Interaction.Water.InWater}:{(c==null?false:Sim.IsShelteredHidden(c))}";
        if(signature==_objectActionSignature)return;_objectActionSignature=signature;ClearChildren(_objectActions);
        if(independent||c==null)return;
        void ActionButton(string label,InteractionAction action)=>_objectActions.AddChild(PanelButton(label,()=>
        {
            CancelGesture();
            if(InteractionCatalog.Choice(action).Continuous){SelectAction(action);_message="对准取下的饰品按住操作。";}
            else AcceptFeedback(Sim.ExecuteInteraction(c.Id,new(action,BodyPart.Accessory,Force:.25)),c.Id);
            RefreshRoomUi();
        }));
        if(c.Interaction.Accessory.Place!=AccessoryPlace.Worn)
        {
            foreach(var item in new[]{("清洗饰品",InteractionAction.CleanAccessory),("缝补饰品",InteractionAction.RepairAccessory),("损坏饰品",InteractionAction.DamageAccessory),("扣留饰品",InteractionAction.StoreAccessory),("挂高饰品",InteractionAction.HangAccessory)})ActionButton(item.Item1,item.Item2);
            if(c.Interaction.Accessory.Integrity<=0)ActionButton("更换饰品",InteractionAction.ReplaceAccessory);
            _objectActions.AddChild(Text("拖回原主身体可归还",12));
        }
        var restraint=c.Interaction.Restraint;
        if(restraint==RestraintKind.Hand)_objectActions.AddChild(PanelButton("解除按压",()=>{_message=Sim.KeepHandPressure(c.Id,false);RefreshRoomUi();}));
        else if(restraint==RestraintKind.None&&c.Interaction.Supine)_objectActions.AddChild(PanelButton("按住",()=>{_message=Sim.KeepHandPressure(c.Id,true);RefreshRoomUi();}));
        else if(restraint==RestraintKind.Wrap)ActionButton("解开包裹",InteractionAction.Unwrap);
        else if(restraint==RestraintKind.Frame)ActionButton("解除固定",InteractionAction.Release);
        else if(restraint==RestraintKind.Box)ActionButton("打开箱子",InteractionAction.OpenBox);
        else if(restraint==RestraintKind.Hanging){ActionButton("托住解下",InteractionAction.Lower);_objectActions.AddChild(PanelButton("调整支点",()=>SelectAction(InteractionAction.AdjustHanging)));}
        if(Sim.IsShelteredHidden(c))_objectActions.AddChild(PanelButton("请它出屋",()=>{_message=Sim.RequestShelterResidentExit(c.Id);RefreshRoomUi();}));
        if(c.Family.ColdStorage.Stored)_objectActions.AddChild(PanelButton("取出复苏",()=>{_message=Sim.SetColdStorage(c.Id,false);RefreshRoomUi();}));
        if(c.Interaction.Water.InWater)_objectActions.AddChild(PanelButton("使用水域救援用品",ArmWaterRescueTool));
        _objectActions.Visible=_objectActions.GetChildCount()>0&&!_modalOpen;
    }
    private void ShowBreedingStatus()
    {
        OpenActivityPanel("繁殖状态",ShowBreedingStatus);
        if(Sim.Selected is { } c)
        {
            _panelContents.AddChild(Text(FamilyRules.Describe(c)));
            _panelContents.AddChild(Text($"繁育兴趣 {c.Family.BreedingInterest:0} · 诱导剩余 {c.Family.InducedInterestRemaining:0}秒"));
            var permission=new CheckButton{Text="许可这只油自主繁育",ButtonPressed=c.Family.AutonomousBreedingPermitted};
            permission.Toggled+=allowed=>
            {
                _message=Sim.SetIndividualBreedingPermission(c.Id,allowed);
                permission.SetPressedNoSignal(c.Family.AutonomousBreedingPermitted);RefreshRoomUi();
            };
            _panelContents.AddChild(permission);
            _panelContents.AddChild(Text("这是行为要求，教育与性格影响是否遵守。旧全局自主繁育设置仍然保留："+(Sim.State.AutonomousBreeding?"开启":"关闭")));
        }
        else _panelContents.AddChild(Text("先观察选择一只油，再查看状态。"));
        _panelContents.AddChild(PanelButton("返回",CloseActivityPanel));
    }
    private void ShowContainer(){DeselectInspector();ShowColdStorage();RefreshRoomUi();}
    private void ShowColdStorage(string? result=null,string? selectedId=null,string? confirm=null,int price=0)
    {
        OpenActivityPanel("回收箱",()=>ShowColdStorage(result,selectedId,confirm,price));
        var stored=Sim.State.Creatures.Where(c=>c.Family.ColdStorage.Stored).ToArray();
        _panelContents.AddChild(Text($"已使用 {stored.Length}/12 格",18));
        _panelContents.AddChild(Text("箱内生理时间暂停。卖出、放生与销毁会离场，亲缘档案保留。",13));
        if(result!=null)_panelContents.AddChild(Text(result,14));
        var slots=new GridContainer{Columns=3,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        slots.AddThemeConstantOverride("h_separation",4);slots.AddThemeConstantOverride("v_separation",4);_panelContents.AddChild(slots);
        for(int n=0;n<WorldSimulation.ColdStorageCapacity;n++)
        {
            var oil=n<stored.Length?stored[n]:null;string? id=oil?.Id;
            var slot=PanelButton($"{n+1:00}\n{(oil==null?"空位":oil.Name)}",()=>ShowColdStorage(selectedId:id),id==selectedId&&id!=null);
            slot.CustomMinimumSize=new(0,54);slot.Disabled=oil==null;slots.AddChild(slot);
        }
        var c=stored.FirstOrDefault(c=>c.Id==selectedId)??stored.FirstOrDefault();
        if(c!=null)
        {
            string id=c.Id;
            _panelContents.AddChild(Text(c.Name+(c.Condition==PhysicalCondition.Dead?" · 遗体":""),18));
            _panelContents.AddChild(PanelButton("拿回庭院",()=>{string message=Sim.SetColdStorage(id,false);ShowColdStorage(message);RefreshRoomUi();}));
            var quote=CourtyardEconomy.Quote(c);
            var saleReason=Sim.CreatureSaleReason(id,true);
            if(saleReason!=null)_panelContents.AddChild(Text(saleReason,13));
            else _panelContents.AddChild(PanelButton($"卖出 · {quote.Price}元",()=>ShowColdStorage(selectedId:id,confirm:"sale",price:quote.Price)));
            foreach(bool destroy in new[]{false,true})
            {
                string kind=destroy?"destroy":"release";
                string label=destroy?$"销毁 · 回收{WorldSimulation.ColdStorageFoodRecovery}份食物":"放生";
                var reason=Sim.ColdDepartureReason(id,destroy);
                if(reason!=null)_panelContents.AddChild(Text(label+"："+reason,13));
                else _panelContents.AddChild(PanelButton(label,()=>ShowColdStorage(selectedId:id,confirm:kind)));
            }
            if(confirm!=null&&selectedId==id)
            {
                string action=confirm=="sale"?$"卖出，到账{price}元":confirm=="destroy"?$"销毁，回收{WorldSimulation.ColdStorageFoodRecovery}份食物":"放生";
                _panelContents.AddChild(Text($"确认将 {c.Name} {action}？离场后不能取回。",14));
                bool submitted=false;
                _panelContents.AddChild(PanelButton("确认"+action,()=>
                {
                    if(submitted)return;submitted=true;
                    string message=confirm=="sale"?Sim.SellCreatureToShop(id,price,true):Sim.DepartColdCreature(id,confirm=="destroy");
                    ShowColdStorage(message);RefreshRoomUi();
                }));
                _panelContents.AddChild(PanelButton("取消",()=>ShowColdStorage(selectedId:id)));
            }
        }
        foreach(var legacy in Sim.State.Creatures.Where(c=>c.RegionId=="courtyard-container"&&!c.Family.ColdStorage.Stored))
        {string id=legacy.Id;_panelContents.AddChild(PanelButton(legacy.Name+" · 旧箱溢出，取回",()=>{ShowColdStorage(Sim.ReturnContainedCreature(id));RefreshRoomUi();}));}
        foreach(var brood in Sim.State.DetachedStemBroods.Where(s=>s.RegionId=="courtyard-container"&&!s.Cleared))
        {string id=brood.EventId;_panelContents.AddChild(PanelButton("培养瓶 / 茎窝 · 取回",()=>{ShowColdStorage(Sim.ReturnContainedBrood(id));RefreshRoomUi();}));}
        _panelContents.AddChild(Text("物品与装置回到仓库，不占制冷格；培养瓶保留原有生命过程。",13));
        _panelContents.AddChild(PanelButton("返回",CloseActivityPanel));
    }
}
