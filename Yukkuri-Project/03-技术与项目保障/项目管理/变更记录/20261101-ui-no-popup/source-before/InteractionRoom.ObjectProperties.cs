using Godot;
using System;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private void RefreshRedesignProperties()
    {
        if(_objectDetails==null)return;
        var entry=Toolbar[_toolbarCategory].Entries.FirstOrDefault(e=>e.Mode==_roomMode);
        _title.Text=Toolbar[_toolbarCategory].Name+(entry!=null&&_toolbarCategory!=0?" · "+entry.Name:"");
        if(_waterRescueToolArmed){_title.Text="水域救援用品";_hint.Text="点击落水的油，成功救援才消耗一份用品。";}
        else if(_arrivingCreature!=null)
        {_title.Text="投放新油";_hint.Text=$"点击干燥空地放下一只新油；绿色可放，红色需换位置。成功后扣 {WorldSimulation.NewCreaturePrice} 元，右键取消。";}
        else if(_roomMode==RoomMode.Observe)_hint.Text="只选中查看，不接触、不抓取。右侧显示对象属性和家庭记录。";
        else if(_roomMode==RoomMode.Grab)_hint.Text="按住抓取，松手放下；释放速度决定抛出。拖到右下收容箱可回收，资源不退款。";
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
        if(c!=null&&_actors.TryGetValue(c.Id,out var actor))
        {
            Vector2 at=_camera.UnprojectPosition(actor.GlobalPosition)+new Vector2(65,-45);
            _objectActions.Position=new(Math.Clamp(at.X,226,Math.Max(226,_ui.Size.X-470)),Math.Clamp(at.Y,90,Math.Max(90,_ui.Size.Y-480)));
        }
        _objectDetails.Visible=!independent;
        if(attached!=null)
        {
            _objectDetails.Visible=true;_who.Text="外茎与幼芽";
            _objectDetails.Text=$"完整度 {attached.Integrity:0} · 水分 {attached.Hydration:0}\n成长 {attached.Progress:P0} · 计划幼体 {attached.PlannedChildren}\n支护剩余 {attached.SupportRemaining:0}秒";
            _creatureVitalMeters.Visible=_condition.Visible=_memory.Visible=_memoryHeading.Visible=false;_closeButton.Disabled=true;
        }
        else if(obj!=null)
        {
            _objectDetails.Visible=true;_objectDetails.Text=RoomResourceText.Describe(obj,Sim.State)+$"\n实际份数 {obj.Portions}";
            _who.Text=RoomResourceText.Label(obj,Sim.State);
            _creatureVitalMeters.Visible=_condition.Visible=_memory.Visible=_memoryHeading.Visible=false;
            _familyPanelButton.Disabled=_closeButton.Disabled=true;
        }
        else if(!independent&&c!=null)
        {
            string Name(string? id)=>Sim.State.Creatures.Find(o=>o.Id==id)?.Name??"未记录";
            _objectDetails.Text=$"家庭：母亲 {Name(c.Family.MotherId)} · 父亲 {Name(c.Family.FatherId)}\n"+
                "伴侣："+(c.Family.PartnerIds.Count==0?"暂无":string.Join("、",c.Family.PartnerIds.Select(id=>Name(id))));
            if(c.Interaction.LastExaminationAt>=0)_objectDetails.Text+=$"\n检查时间 {c.Interaction.LastExaminationAt:0}秒\n"+c.Interaction.Examination;
        }
        else _objectDetails.Text="";
        string signature=independent?"object":$"{c?.Id}:{c?.Interaction.Accessory.Place}:{c?.Interaction.Accessory.Integrity<=0}:{c?.Interaction.Restraint}:{c?.Interaction.Supine}:{c?.Family.ColdStorage.Stored}:{c?.Condition}:{c?.Interaction.Water.InWater}";
        if(signature==_objectActionSignature)return;_objectActionSignature=signature;ClearChildren(_objectActions);
        if(independent||c==null)return;
        void ActionButton(string label,InteractionAction action)=>_objectActions.AddChild(Button(label,()=>
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
        if(restraint==RestraintKind.Hand)_objectActions.AddChild(Button("解除按压",()=>{_message=Sim.KeepHandPressure(c.Id,false);RefreshRoomUi();}));
        else if(restraint==RestraintKind.None&&c.Interaction.Supine)_objectActions.AddChild(Button("按住",()=>{_message=Sim.KeepHandPressure(c.Id,true);RefreshRoomUi();}));
        else if(restraint==RestraintKind.Wrap)ActionButton("解开包裹",InteractionAction.Unwrap);
        else if(restraint==RestraintKind.Frame)ActionButton("解除固定",InteractionAction.Release);
        else if(restraint==RestraintKind.Box)ActionButton("打开箱子",InteractionAction.OpenBox);
        else if(restraint==RestraintKind.Hanging){ActionButton("托住解下",InteractionAction.Lower);_objectActions.AddChild(Button("调整支点",()=>SelectAction(InteractionAction.AdjustHanging)));}
        if(c.Family.ColdStorage.Stored)_objectActions.AddChild(Button("取出复苏",()=>{_message=Sim.SetColdStorage(c.Id,false);RefreshRoomUi();}));
        if(c.Interaction.Water.InWater)_objectActions.AddChild(Button("使用水域救援用品",ArmWaterRescueTool));
        _objectActions.Visible=_objectActions.GetChildCount()>0&&!_modalOpen;
    }
    private void ShowBreedingStatus()
    {
        OpenModal("繁殖状态");
        if(Sim.Selected is { } c)
        {
            _modalContents.AddChild(Text(FamilyRules.Describe(c)));
            _modalContents.AddChild(Text($"繁育兴趣 {c.Family.BreedingInterest:0} · 诱导剩余 {c.Family.InducedInterestRemaining:0}秒"));
            var permission=new CheckButton{Text="许可这只油自主繁育",ButtonPressed=c.Family.AutonomousBreedingPermitted};
            permission.Toggled+=allowed=>{bool paused=Sim.State.Settings.Pause;Sim.State.Settings.Pause=_pauseBeforeModal;_message=Sim.SetIndividualBreedingPermission(c.Id,allowed);Sim.State.Settings.Pause=paused;};
            _modalContents.AddChild(permission);
            _modalContents.AddChild(Text("这是行为要求，教育与性格影响是否遵守。旧全局自主繁育设置仍然保留："+(Sim.State.AutonomousBreeding?"开启":"关闭")));
        }
        else _modalContents.AddChild(Text("先观察选择一只油，再查看状态。"));
        _modalContents.AddChild(Button("返回",CloseModal));
    }
    private void ShowContainer()
    {
        OpenModal("收容箱");
        foreach(var c in Sim.State.Creatures.Where(c=>c.RegionId=="courtyard-container"))
        {_modalContents.AddChild(Button(c.Name+(c.Condition==PhysicalCondition.Dead?" · 遗体":"")+" · 取回",()=>{CloseModal();_message=Sim.ReturnContainedCreature(c.Id);RefreshRoomUi();}));}
        foreach(var brood in Sim.State.DetachedStemBroods.Where(s=>s.RegionId=="courtyard-container"&&!s.Cleared))
        {_modalContents.AddChild(Button("培养瓶 / 茎窝 · 取回",()=>{CloseModal();_message=Sim.ReturnContainedBrood(brood.EventId);RefreshRoomUi();}));}
        _modalContents.AddChild(Text("物品和装置回到仓库；收容个体与茎窝保留身份和生命过程，不产生金钱。仍需及时取回照护。"));
        _modalContents.AddChild(Button("返回",CloseModal));
    }
}
