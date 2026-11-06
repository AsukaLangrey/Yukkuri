using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private InlineChoice _oilSelector=null!;
    private readonly List<string> _selectorIds=new();
    private string _selectorSignature="";
    private string? _familyFirstId,_familySecondId;
    private string _familyCareFeedback="";
    private string _familyCareSelection="";
    private bool _breedingInterventionExpanded;
    private readonly Dictionary<string,FamilyStatusMarker> _familyMarkers=new();

    private static string SpeciesName(string species)=>species switch
    {
        "reimu"=>"灵梦","marisa"=>"魔理沙","alice"=>"爱丽丝",
        "patchouli"=>"帕秋莉","remilia"=>"蕾米莉亚","flandre"=>"芙兰",
        _=>"普通油"
    };

    private string OilListName(Creature c)=>c.Name+" · "+SpeciesName(c.Body.SpeciesId)+
        (c.Family.Memorialized?" · 已安置":c.Family.ColdStorage.Stored?
            (c.Condition==PhysicalCondition.Dead?" · 遗体冷藏中":" · 冷藏中"):c.Condition==PhysicalCondition.Dead?" · 已离世":c.IsYoung?" · 幼体":"");

    private static bool HasVisibleFamilyBody(Creature c)=>!c.Family.ColdStorage.Stored&&!c.Family.Memorialized;

    private bool LeaveUnavailableCloseup()
    {
        if(Sim.State.Mode!=PlayMode.CloseUp||Sim.Selected is not { } selected||HasVisibleFamilyBody(selected))return false;
        // Keep the selected identity and the explicit pause setting. Only the empty
        // close-up is left; historical family information remains available.
        CancelGesture();Sim.State.Mode=PlayMode.Management;
        return true;
    }

    private void BuildOilSelector(Control parent)
    {
        _oilSelector=new InlineChoice{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,
            CustomMinimumSize=new(0,32),
            FocusMode=Control.FocusModeEnum.None};
        _oilSelector.ItemSelected+=index=>
        {
            if(index>=0&&index<_selectorIds.Count)SelectOil(_selectorIds[(int)index]);
        };
        parent.AddChild(_oilSelector);
        RefreshOilSelector(true);
    }

    private void RefreshOilSelector(bool force=false)
    {
        if(_oilSelector==null)return;
        var oils=Sim.State.Creatures.Where(c=>c.RegionId==Sim.State.ActiveRegionId).ToArray();
        string signature=string.Join("\n",oils.Select(c=>c.Id+"="+OilListName(c)));
        if(force||signature!=_selectorSignature)
        {
            _selectorSignature=signature;_oilSelector.Clear();_selectorIds.Clear();
            foreach(var c in oils){_selectorIds.Add(c.Id);_oilSelector.AddItem(OilListName(c));}
        }
        int selected=_selectorIds.IndexOf(Sim.State.SelectedId??"");
        if(selected>=0)_oilSelector.Select(selected);
        _oilSelector.Disabled=_selectorIds.Count==0;
    }

    private void SynchronizeFamilyActors()
    {
        // Birth changes the collection during simulation advancement. Add its actor before
        // presentation or pointer contact can index the actor dictionary this frame.
        int index=0;
        foreach(var c in Sim.State.Creatures)
        {
            float expectedScale=c.IsYoung?.73f:c.IsElder?1.03f:1;
            if(_actors.TryGetValue(c.Id,out var actor)&&Math.Abs(actor.BodyScale-expectedScale)>.001f)
            {
                actor.QueueFree();_actors.Remove(c.Id);_familyMarkers.Remove(c.Id);
            }
            if(!_actors.ContainsKey(c.Id))
            {
                actor=new CreatureActor{SharedEyeMeshes=_eyeReuseDisabledForCheck?null:_eyeMeshes};AddChild(actor);actor.Setup(c,index,_font);_actors[c.Id]=actor;
                // Loaded actors are created together in BuildActors. This path sees only
                // a new child, never a pregnancy failure that also entered recovery.
                if(c.IsYoung&&c.Family.BirthEventId is { } birth&&Sim.State.CompletedBirths.Contains(birth))
                {
                    actor.BeginNewbornPresentation();
                    if(!Sim.State.DetachedStemBroods.Any(s=>s.EventId==birth)&&
                        c.Family.MotherId is { } mother&&_actors.TryGetValue(mother,out var maternalActor))
                        maternalActor.BeginBirthPresentation(c.Family.BirthMethod);
                }
            }
            index++;
        }
        foreach(var id in _actors.Keys.Where(id=>!Sim.State.Creatures.Any(c=>c.Id==id)).ToArray())
        {
            _actors[id].QueueFree();_actors.Remove(id);_familyMarkers.Remove(id);_heardReactions.Remove(id);
        }
    }

    private void ResetFamilyPresentation()=>_familyMarkers.Clear();

    private void RefreshFamilyPresentation(Creature c,CreatureActor actor)
    {
        if(!_familyMarkers.TryGetValue(c.Id,out var marker))
        {
            marker=new FamilyStatusMarker();actor.AddChild(marker);marker.Build(_font);_familyMarkers[c.Id]=marker;
        }
        marker.Present(c,actor.BodyScale,actor.VisualMotionOffset,c.Id==Sim.State.SelectedId);
    }

    private void ShowFamily()
    {
        var oils=Sim.State.Creatures.Where(c=>c.RegionId==Sim.State.ActiveRegionId).ToArray();
        if(!oils.Any(c=>c.Id==_familyFirstId))_familyFirstId=Sim.Selected?.Id??oils.FirstOrDefault()?.Id;
        if(!oils.Any(c=>c.Id==_familySecondId)||_familyFirstId==_familySecondId)
            _familySecondId=oils.FirstOrDefault(c=>c.Id!=_familyFirstId)?.Id;
        OpenActivityPanel("同伴、家庭与照护",ShowFamily);
        _panelContents.AddChild(Text("相处、结伴与孕育由双方决定。先放稳、照顾好身体，再给它们安静相处的时间。",14));
        if(Sim.State.Settings.Pause)
            _panelContents.AddChild(Text("小院处于暂停中。按空格继续时间后，再安排活动。",13,"8a624e"));
        else if(Sim.State.Mode==PlayMode.CloseUp)
            _panelContents.AddChild(Text("近看时可以先提出安排，回到小院后过程才会推进。放入冷藏需要先回到小院。",13,"8a624e"));
        if(oils.Length==0){_panelContents.AddChild(PanelButton("关闭面板",CloseActivityPanel));return;}
        AddFamilyChoice("这只油",oils,_familyFirstId,id=>{_familyFirstId=id;ShowFamily();});
        var first=oils.FirstOrDefault(c=>c.Id==_familyFirstId);
        if(first==null)return;
        _panelContents.AddChild(Text(FamilySummary(first),14));
        string firstId=first.Id;
        if(first.Condition==PhysicalCondition.Dead)
        {
            if(first.Family.Memorialized)
                _panelContents.AddChild(Text("遗体已经安置。这里保留它的名字、家人和共同经历。",14));
            else if(first.Family.ColdStorage.Stored)
            {
                _panelContents.AddChild(Text("遗体仍在冷藏中，先取出，再安排告别与安置。",14));
                _panelContents.AddChild(PanelButton("取出遗体",()=>ApplyFamilyCommand(()=>Sim.SetColdStorage(firstId,false))));
            }
            else
            {
                _panelContents.AddChild(Text("安置后遗体会离开小院活动区域，家人记录与死因仍会保留。",14));
                _panelContents.AddChild(PanelButton("安置遗体",()=>ApplyFamilyCommand(()=>Sim.LayToRest(firstId))));
            }
            _panelContents.AddChild(PanelButton("关闭面板",CloseActivityPanel));return;
        }
        if(first.Family.ColdStorage.Stored)
        {
            _panelContents.AddChild(Text("它正在有限冷藏中。先取出并观察恢复，再安排相处或照护。",14));
            _panelContents.AddChild(PanelButton("取出这只油，观察复苏",()=>ApplyFamilyCommand(()=>Sim.SetColdStorage(firstId,false))));
            _panelContents.AddChild(PanelButton("关闭面板",CloseActivityPanel));return;
        }
        if(first.Family.Pregnancy.InterventionRemaining>0)
            _panelContents.AddChild(PanelButton("停止正在进行的繁育干预",()=>ApplyFamilyCommand(()=>Sim.InterruptPregnancyIntervention(firstId))));
        if(first.Family.ChildThreat.TargetId!=null)
            _panelContents.AddChild(PanelButton("制止对幼体的伤害",()=>ApplyFamilyCommand(()=>Sim.ProtectFamilyChild(firstId))));
        AddEndCompanionship(first);
        var careStops=new HashSet<string>(StringComparer.Ordinal);
        AddEndFamilyCare(first,careStops);
        bool breeding=first.Family.AllowsBreeding;
        _panelContents.AddChild(PanelButton(breeding?"繁殖许可：允许 · 点击禁止":"繁殖许可：禁止 · 点击允许",
            ()=>ApplyFamilyCommand(()=>Sim.SetBreedingPermission(firstId,!breeding))));
        AddBreedingInduction(first);

        Creature? other=null;
        var otherOils=oils.Where(c=>c.Id!=firstId&&c.Condition!=PhysicalCondition.Dead&&HasVisibleFamilyBody(c)).ToArray();
        if(otherOils.Length>0)
        {
            var second=otherOils.FirstOrDefault(c=>c.Id==_familySecondId)??otherOils[0];
            other=second;
            _familySecondId=second.Id;
            AddFamilyChoice("另一只油",otherOils,_familySecondId,id=>{_familySecondId=id;ShowFamily();});
            string secondId=second.Id;
            string careSelection=firstId+"\n"+secondId;
            if(_familyCareSelection!=careSelection){_familyCareSelection=careSelection;_familyCareFeedback="";}
            _panelContents.AddChild(Text(FamilySummary(second),14));
            if(second.Family.CompanionTargetId!=firstId)AddEndCompanionship(second);
            AddEndFamilyCare(second,careStops);
            if(_familyCareFeedback.Length>0)
                _panelContents.AddChild(Text("上次照护安排："+_familyCareFeedback,14,"8a624e"));
            if(first.IsYoung!=second.IsYoung)
                AddFamilyCareChoice(first.IsYoung?second:first,first.IsYoung?first:second);
            else if(!first.IsYoung&&!second.IsYoung)
            {
                _panelContents.AddChild(Text("伤残家人的食水照护：由有余力的一方递送实际食水。成年人接受帮助不会改变血缘或成为养子女。",13,"78816e"));
                AddFamilyCareChoice(first,second);
                AddFamilyCareChoice(second,first);
            }
            if(second.Family.Pregnancy.InterventionRemaining>0)
                _panelContents.AddChild(PanelButton("停止另一只的繁育干预",()=>ApplyFamilyCommand(()=>Sim.InterruptPregnancyIntervention(secondId))));
            if(second.Family.ChildThreat.TargetId!=null)
                _panelContents.AddChild(PanelButton("制止另一只对幼体的伤害",()=>ApplyFamilyCommand(()=>Sim.ProtectFamilyChild(secondId))));
            _panelContents.AddChild(PanelButton("邀请它们靠近相处",()=>ApplyFamilyCommand(()=>Sim.InviteCompanion(firstId,secondId))));
            bool partners=first.Family.PartnerIds.Contains(secondId)||second.Family.PartnerIds.Contains(firstId);
            if(partners)
            {
                bool firstAccepts=first.Family.Peers.TryGetValue(secondId,out var fromFirst)&&fromFirst.ConsentsToOtherPartners;
                bool secondAccepts=second.Family.Peers.TryGetValue(firstId,out var fromSecond)&&fromSecond.ConsentsToOtherPartners;
                _panelContents.AddChild(Text(first.Name+(firstAccepts?"同意":"没有同意")+second.Name+"另结伴侣。\n"+
                    second.Name+(secondAccepts?"同意":"没有同意")+first.Name+"另结伴侣。",13));
                _panelContents.AddChild(PanelButton("讨论是否接受其他伴侣",()=>ApplyFamilyCommand(()=>Sim.DiscussOpenPartnership(firstId,secondId))));
                _panelContents.AddChild(PanelButton("这只油撤回对另一方另结伴侣的许可",()=>ApplyFamilyCommand(()=>Sim.RevokeOpenPartnership(firstId,secondId))));
                _panelContents.AddChild(PanelButton("结束这对伴侣关系",()=>ApplyFamilyCommand(()=>Sim.EndPartnership(firstId,secondId))));
            }
            Creature mother=first.Body.ReproductionRole==ReproductionRole.Maternal?first:second;
            Creature father=mother==first?second:first;
            bool rolesMatch=mother.Body.ReproductionRole==ReproductionRole.Maternal&&father.Body.ReproductionRole==ReproductionRole.Paternal;
            _panelContents.AddChild(Text(rolesMatch?"安排孕育："+mother.Name+"承担母体，"+father.Name+"承担父体。成熟度、双方意愿或身体不合适时，会说明原因。":
                "这两只承担相同的繁殖角色，无法共同孕育。仍然可以相处、建立关系和照护家人。",13));
            string motherId=mother.Id,fatherId=father.Id;
            var methods=Row(_panelContents);
            methods.AddChild(PanelButton("安排茎生孕育",()=>ApplyFamilyCommand(()=>Sim.RequestPregnancy(motherId,fatherId,BirthMethod.Stem))));
            methods.AddChild(PanelButton("安排胎生孕育",()=>ApplyFamilyCommand(()=>Sim.RequestPregnancy(motherId,fatherId,BirthMethod.Live))));
            _panelContents.AddChild(Text("胎生需要更久、宝宝较少，母体也需要更多吃喝与恢复。已开始的孕育不会因为关闭繁殖许可而消失。",13,"78816e"));
            if(first.IsYoung!=second.IsYoung)
            {
                string childId=first.IsYoung?firstId:secondId,caregiverId=first.IsYoung?secondId:firstId;
                var teacher=first.IsYoung?second:first;
                var knowledge=teacher.Family.LifeKnowledge.OrderBy(k=>k.Key,StringComparer.Ordinal)
                    .Where(k=>FamilyRules.KnowledgeName(k.Key).Length>0).ToArray();
                _panelContents.AddChild(Text("长辈讲述："+teacher.Name+"。教的是它自己的判断，不保证食水安全；幼体需要靠近听完，也可能误解，之后仍会按亲身经历修正。",13,"78816e"));
                if(knowledge.Length==0)_panelContents.AddChild(Text("这位长辈还没有可以讲述的食水经验。",13));
                foreach(var belief in knowledge)
                {
                    string key=belief.Key;
                    _panelContents.AddChild(PanelButton("讲讲"+FamilyRules.KnowledgeName(key)+" · "+(belief.Value?"觉得可以吃喝":"觉得需要小心"),
                        ()=>ApplyFamilyCommand(()=>Sim.TeachFamilyKnowledge(caregiverId,childId,key))));
                }
            }
        }
        else _panelContents.AddChild(Text("这里暂时没有另一只可以邀请的油。冷藏中的个体需要先取出。",14));
        // This redesign exposes no coercive breeding action.
        _panelContents.AddChild(PanelButton("制止这只油的家庭冲突",()=>ApplyFamilyCommand(()=>Sim.InterruptFamilyConflict(firstId))));
        _panelContents.AddChild(Text("冷藏只让相关生命过程以正常速度的十分之一推进。有保存期限和复苏风险，需要及时取出照护；旧伤仍会保留，不能无限拖住濒危状态。",13,"78816e"));
        bool stored=first.Family.ColdStorage.Stored;
        _panelContents.AddChild(PanelButton(stored?(first.Condition==PhysicalCondition.Dead?"取出遗体":"取出这只油，观察复苏"):"把这只油放入冷藏",
            ()=>ApplyFamilyCommand(()=>Sim.SetColdStorage(firstId,!stored))));
        bool autonomous=Sim.State.AutonomousBreeding;
        _panelContents.AddChild(PanelButton(autonomous?"自主繁育：开 · 点击关闭":"自主繁育：关 · 点击开启",
            ()=>ApplyFamilyCommand(()=>Sim.SetAutonomousBreeding(!autonomous))));
        _panelContents.AddChild(PanelButton("关闭面板",CloseActivityPanel));
    }

    private void AddBreedingIntervention(Creature first,Creature? other)
    {
        _panelContents.AddChild(PanelButton(_breedingInterventionExpanded?"特殊繁育干预 · 收起":"特殊繁育干预 · 展开",()=>
        {_breedingInterventionExpanded=!_breedingInterventionExpanded;ShowFamily();}));
        if(!_breedingInterventionExpanded)return;
        _panelContents.AddChild(Text("强制干预会无视母体意愿并损害它对你的信任。恢复不足时身体负担更重，可能伤亡；有材料也不保证受孕或生产成功。普通安排仍尊重双方。",14,"8a624e"));
        Creature? father=first.Body.ReproductionRole==ReproductionRole.Paternal?first:
            other?.Body.ReproductionRole==ReproductionRole.Paternal?other:null;
        Creature? mother=first.Body.ReproductionRole==ReproductionRole.Maternal?first:
            other?.Body.ReproductionRole==ReproductionRole.Maternal?other:null;
        if(father!=null)
        {
            string fatherId=father.Id;
            int available=Sim.State.BreedingMaterials.Count(m=>m.FatherId==fatherId&&m.BodyId==father.Body.Id&&m.ExpiresAt>Sim.State.WorldSeconds);
            _panelContents.AddChild(Text("材料来源："+father.Name+"。尚可用的材料："+available+"份。收集仍需要它本身愿意且身体适合。",13));
            _panelContents.AddChild(PanelButton("收集所选父体的一份材料",()=>ApplyFamilyCommand(()=>Sim.CollectBreedingMaterial(fatherId))));
        }
        else _panelContents.AddChild(Text("先选择一只承担父体角色的成熟油，才能取得有明确来源的材料。",13));
        if(mother==null||father==null||mother.Id==father.Id)
        {
            _panelContents.AddChild(Text("需要分别选定母体与父体。出生角色不会因这项干预改变，幼体不能参与。",13));return;
        }
        string motherId=mother.Id,sourceFatherId=father.Id;
        _panelContents.AddChild(Text("干预对象："+mother.Name+"；材料来源："+father.Name+"。开始会消耗一份材料，停止操作也不会返还。",13));
        var methods=Row(_panelContents);
        methods.AddChild(PanelButton("强制茎生干预",()=>ApplyFamilyCommand(()=>Sim.RequestForcedPregnancy(motherId,sourceFatherId,BirthMethod.Stem))));
        methods.AddChild(PanelButton("强制胎生干预",()=>ApplyFamilyCommand(()=>Sim.RequestForcedPregnancy(motherId,sourceFatherId,BirthMethod.Live))));
    }

    private void AddFamilyChoice(string caption,Creature[] oils,string? selected,Action<string> select)
    {
        _panelContents.AddChild(Text(caption,13,"78816e"));
        var choice=new InlineChoice{CustomMinimumSize=new(0,38),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,
            FocusMode=Control.FocusModeEnum.None};
        for(int n=0;n<oils.Length;n++)
        {
            choice.AddItem(OilListName(oils[n]));
            if(oils[n].Id==selected)choice.Select(n);
        }
        choice.ItemSelected+=index=>{if(index>=0&&index<oils.Length){_familyCareFeedback="";select(oils[(int)index].Id);}};
        _panelContents.AddChild(choice);
    }

    private void AddEndCompanionship(Creature c)
    {
        if(c.Family.CompanionTargetId is not { } companionId)return;
        var companion=Sim.State.Creatures.FirstOrDefault(o=>o.Id==companionId);
        if(companion?.Family.CompanionTargetId!=c.Id)return;
        // Capture the displayed pair: a later command must not stop a new partner.
        string id=c.Id;
        _panelContents.AddChild(PanelButton("结束"+c.Name+"与"+companion.Name+"这次相处",
            ()=>ApplyFamilyCommand(()=>Sim.EndCompanionship(id,companionId))));
    }

    private void AddEndFamilyCare(Creature c,HashSet<string> shown)
    {
        foreach(var caregiver in Sim.State.Creatures.Where(o=>o.RegionId==Sim.State.ActiveRegionId&&
            o.Condition!=PhysicalCondition.Dead&&(o.Id==c.Id||o.Family.CareTargetId==c.Id)).OrderBy(o=>o.Id,StringComparer.Ordinal))
        {
            if(caregiver.Family.CareTargetId is not { } recipientId||shown.Contains(caregiver.Id))continue;
            if(shown.Count==0)
                _panelContents.AddChild(Text("需要亲自接手时，可以结束当前照护。已取出的食水会留在实际位置，仍可拿起使用。",13,"78816e"));
            shown.Add(caregiver.Id);
            string caregiverId=caregiver.Id;
            string recipientName=Sim.State.Creatures.FirstOrDefault(o=>o.Id==recipientId)?.Name??"原照护对象";
            _panelContents.AddChild(PanelButton("停止"+caregiver.Name+"对"+recipientName+"的这次照护",
                ()=>ApplyFamilyCommand(()=>Sim.EndFamilyCare(caregiverId,recipientId))));
        }
    }

    private void AddFamilyCareChoice(Creature caregiver,Creature recipient)
    {
        string caregiverId=caregiver.Id,recipientId=recipient.Id;
        _panelContents.AddChild(Text(caregiver.Name+" → 照护 → "+recipient.Name,14));
        string? reason=Sim.FamilyCareReason(caregiverId,recipientId);
        string available=recipient.IsYoung?
            (WorldSimulation.CanSelfMove(caregiver)?
                "目前可以提出照护。需要实际靠近；送来的食水也要由幼体接食，持续照顾才会留下关系。":
                "可以在已经贴近的幼体旁陪护保暖。它的身体走不开，食水仍需要你或能移动的家人递近。"):
            "目前可以请求食水帮助。照护者需要找到真实食水并送到它能接食的位置。";
        _panelContents.AddChild(Text(reason==null?
            available:
            "当前阻碍："+reason,13,reason==null?"78816e":"8a624e"));
        if(Sim.State.Settings.Pause)_panelContents.AddChild(Text("按空格继续时间后，可以在这里提出安排。",13,"8a624e"));
        var button=PanelButton("提出这项照护",()=>ApplyFamilyCareCommand(caregiverId,recipientId));
        SetInlineHint(button,"请"+caregiver.Name+"照护"+recipient.Name);
        _panelContents.AddChild(button);
    }

    private void ApplyFamilyCareCommand(string caregiverId,string recipientId)
    {
        // The world keeps running while the sidebar is open. Recheck the captured
        // pair at the moment of the command instead of trusting displayed state.
        string? reason=Sim.State.Settings.Pause?"小院处于暂停中。按空格继续时间后，再安排照护。":
            Sim.FamilyCareReason(caregiverId,recipientId);
        if(reason!=null){_familyCareFeedback=reason;ShowFamily();return;}
        string result=Sim.ArrangeCare(caregiverId,recipientId);
        var caregiver=Sim.State.Creatures.FirstOrDefault(c=>c.Id==caregiverId);
        if(caregiver?.Family.CareTargetId!=recipientId)
        {
            _familyCareFeedback=result;ShowFamily();return;
        }
        _familyCareFeedback=result;_message=result;ShowFamily();RefreshRoomUi();
    }

    private string FamilyCareSummary(Creature c)
    {
        string NameFor(string id)=>Sim.State.Creatures.FirstOrDefault(o=>o.Id==id)?.Name??"这位家人";
        string Delivery(Creature caregiver)
        {
            var parcel=Sim.State.RoomObjects.FirstOrDefault(o=>o.Id==caregiver.Family.CareParcelId&&
                o.CarrierId==caregiver.Id&&!o.Stored&&!o.HeldByHand);
            return parcel==null?"已安排照护，目前没有搬送中的食水":parcel.Kind switch
            {
                RoomObjectKind.Water=>"正在搬送一份水",
                RoomObjectKind.Sweet=>"正在搬送一份甜甜",
                _=>"正在搬送一份食物"
            };
        }
        var details=new List<string>();
        if(c.Family.CareTargetId is { } target)
        {
            string? reason=Sim.FamilyCareReason(c.Id,target);
            details.Add("当前照护："+c.Name+" → "+NameFor(target)+"；"+
                (reason==null?Delivery(c):"目前受阻："+reason));
        }
        foreach(var caregiver in Sim.State.Creatures.Where(o=>o.Id!=c.Id&&o.Family.CareTargetId==c.Id).OrderBy(o=>o.Id,StringComparer.Ordinal))
        {
            string? reason=Sim.FamilyCareReason(caregiver.Id,c.Id);
            details.Add("正在照护它："+caregiver.Name+"；"+
                (reason==null?Delivery(caregiver):"目前受阻："+reason));
        }
        return string.Join("\n",details);
    }

    private string FamilySummary(Creature c)
    {
        string NameFor(string? id)=>id==null?"未记录":Sim.State.Creatures.FirstOrDefault(o=>o.Id==id)?.Name??"不在这里的家人";
        var details=new List<string>{c.Name+" · "+(c.Body.ReproductionRole==ReproductionRole.Maternal?"母体角色":"父体角色")};
        if(c.Family.MotherId!=null||c.Family.FatherId!=null)details.Add("母亲："+NameFor(c.Family.MotherId)+"；父亲："+NameFor(c.Family.FatherId));
        if(c.Family.PartnerIds.Count>0)details.Add("伴侣："+string.Join("、",c.Family.PartnerIds.OrderBy(id=>id).Select(id=>NameFor(id))));
        if(c.Family.CaregiverIds.Count>0)
        {
            var parents=c.Family.CaregiverIds.Where(id=>id==c.Family.MotherId||id==c.Family.FatherId||id==c.ParentId).ToArray();
            var foster=c.Family.CaregiverIds.Except(parents).ToArray();
            if(parents.Length>0)details.Add("亲代照护者："+string.Join("、",parents.Select(id=>NameFor(id))));
            if(foster.Length>0)details.Add("持续照顾后认下的养亲："+string.Join("、",foster.Select(id=>NameFor(id))));
        }
        string care=FamilyCareSummary(c);
        if(care.Length>0)details.Add(care);
        if(c.Family.CareLastEvent.Length>0)details.Add("照护记录："+c.Family.CareLastEvent);
        string situation=c.Condition==PhysicalCondition.Dead?"":FamilyRules.Describe(c);
        if(situation.Length>0)details.Add(situation);
        string aftermath=FamilyRules.DescribeAftermath(c);
        if(aftermath.Length>0)details.Add(aftermath);
        string risk=c.Condition==PhysicalCondition.Dead?"":FamilyRules.DescribeRisk(c);
        if(risk.Length>0)details.Add(risk);
        if(c.Condition!=PhysicalCondition.Dead)details.Add(FamilyRules.DescribeLearning(c));
        return string.Join("\n",details);
    }

    private void ApplyFamilyCommand(Func<string> action)
    {
        // Gameplay panels never borrow or restore a different pause state.
        _message=Sim.State.Settings.Pause?"小院仍暂停着。先继续时间，再安排活动。":action();
        LeaveUnavailableCloseup();
        _familyCareFeedback=_message;ShowFamily();RefreshRoomUi();
    }

    private void AddBreedingInduction(Creature c)
    {
        _panelContents.AddChild(Text($"繁育兴趣 {c.Family.BreedingInterest:0} / 100 · 诱导用品 {Sim.State.CourtshipTools} 份",14));
        if(c.Family.InducedInterestRemaining>0)
            _panelContents.AddChild(Text($"诱导影响剩余 {c.Family.InducedInterestRemaining:0} 秒；兴趣与信任、伴侣意愿分别判断。",13));
        string? reason=Sim.BreedingInductionReason(c.Id);
        _panelContents.AddChild(Text(reason??"身体状态允许使用一份诱导用品。之后仍需安稳相处和双方接受。",13,
            reason==null?"78816e":"8a624e"));
        var row=new HBoxContainer();_panelContents.AddChild(row);
        var use=PanelButton("使用一份诱导用品",()=>ApplyFamilyCommand(()=>Sim.InduceBreedingInterest(c.Id).Message));
        use.Disabled=reason!=null||Sim.State.Settings.Pause;row.AddChild(use);
        row.AddChild(PanelButton("购买诱导用品",()=>ShowCourtshipSupplies()));
    }
}

// Small status symbols supplement actual expressions and motion. They are not an
// anatomical model or a replacement for the simulation's injury / posture priority.
internal partial class FamilyStatusMarker : Node3D
{
    private Node3D _stem=null!,_cradle=null!;
    private Label3D _label=null!;
    public void Build(Font font)
    {
        _stem=new Node3D();AddChild(_stem);
        SceneArt.Mesh(_stem,new CylinderMesh{TopRadius=.018f,BottomRadius=.025f,Height=.21f,RadialSegments=8},new(0,.1f,0),"739463");
        SceneArt.Ball(_stem,new(-.07f,.14f,0),new(.14f,.06f,.09f),"94ad73");
        SceneArt.Ball(_stem,new(.06f,.22f,0),new(.13f,.10f,.12f),"e9cba4");
        _cradle=new Node3D();AddChild(_cradle);
        SceneArt.Mesh(_cradle,new TorusMesh{InnerRadius=.08f,OuterRadius=.14f,Rings=20,RingSegments=8},new(0,.05f,0),"d5b080");
        SceneArt.Ball(_cradle,new(0,.10f,0),new(.15f,.12f,.13f),"e9cba4");
        _label=new Label3D{Font=font,FontSize=30,PixelSize=.004f,Position=new(0,.39f,0),
            Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,Modulate=new Color("fff4d7"),
            OutlineModulate=new Color("425044"),OutlineSize=9};
        AddChild(_label);
    }
    public void Present(Creature c,float bodyScale,Vector3 motion,bool selected)
    {
        var p=c.Family.Pregnancy;
        Visible=c.Condition!=PhysicalCondition.Dead&&!c.Family.ColdStorage.Stored&&!c.Family.Memorialized&&p.Phase==PregnancyPhase.Gestating;
        if(!Visible)return;
        Position=motion+new Vector3(.76f*bodyScale,.80f*bodyScale,-.10f);
        _stem.Visible=p.Method==BirthMethod.Stem;_cradle.Visible=p.Method==BirthMethod.Live;
        float progress=Mathf.Clamp((float)(p.Elapsed/FamilyRules.GestationSeconds(p.Method)),0,1);
        _label.Visible=selected;_label.Text=(p.Method==BirthMethod.Stem?"茎生孕育":"胎生孕育")+$" {progress*100:0}%";
    }
}
