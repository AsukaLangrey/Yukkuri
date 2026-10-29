using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private OptionButton _oilSelector=null!;
    private readonly List<string> _selectorIds=new();
    private string _selectorSignature="";
    private string? _familyFirstId,_familySecondId;
    private readonly Dictionary<string,FamilyStatusMarker> _familyMarkers=new();

    private static string SpeciesName(string species)=>species switch
    {
        "reimu"=>"灵梦","marisa"=>"魔理沙","alice"=>"爱丽丝",
        "patchouli"=>"帕秋莉","remilia"=>"蕾米莉亚","flandre"=>"芙兰",
        _=>"普通油"
    };

    private string OilListName(Creature c)=>c.Name+" · "+SpeciesName(c.Body.SpeciesId)+
        (c.Condition==PhysicalCondition.Dead?" · 已离世":c.Family.ColdStorage.Stored?" · 冷藏中":c.IsYoung?" · 幼体":"");

    private void BuildOilSelector(Control parent)
    {
        _oilSelector=new OptionButton{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,
            CustomMinimumSize=new(0,38),FitToLongestItem=false,ClipText=true,
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
                actor=new CreatureActor();AddChild(actor);actor.Setup(c,index,_font);_actors[c.Id]=actor;
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
        OpenModal("同伴、家庭与照护");
        _modalContents.AddChild(Text("相处、结伴与孕育由双方决定。先放稳、照顾好身体，再给它们安静相处的时间。",14));
        if(_pauseBeforeModal)
            _modalContents.AddChild(Text("小院原本处于暂停中。关掉面板会保留暂停；请继续时间后，再安排活动。",13,"8a624e"));
        else if(Sim.State.Mode==PlayMode.CloseUp)
            _modalContents.AddChild(Text("近看时可以先提出安排，回到小院后过程才会推进。放入冷藏需要先回到小院。",13,"8a624e"));
        if(oils.Length==0){_modalContents.AddChild(Button("回到小院",CloseModal));return;}
        AddFamilyChoice("这只油",oils,_familyFirstId,id=>{_familyFirstId=id;ShowFamily();});
        var first=oils.FirstOrDefault(c=>c.Id==_familyFirstId);
        if(first==null)return;
        _modalContents.AddChild(Text(FamilySummary(first),14));
        string firstId=first.Id;
        bool breeding=first.Family.AllowsBreeding;
        _modalContents.AddChild(Button(breeding?"繁殖许可：允许 · 点击禁止":"繁殖许可：禁止 · 点击允许",
            ()=>ApplyFamilyCommand(()=>Sim.SetBreedingPermission(firstId,!breeding))));

        var otherOils=oils.Where(c=>c.Id!=firstId).ToArray();
        if(otherOils.Length>0)
        {
            AddFamilyChoice("另一只油",otherOils,_familySecondId,id=>{_familySecondId=id;ShowFamily();});
            var second=otherOils.FirstOrDefault(c=>c.Id==_familySecondId)??otherOils[0];
            string secondId=second.Id;
            _modalContents.AddChild(Text(FamilySummary(second),14));
            _modalContents.AddChild(Button("邀请它们靠近相处",()=>ApplyFamilyCommand(()=>Sim.InviteCompanion(firstId,secondId))));
            bool partners=first.Family.PartnerIds.Contains(secondId)||second.Family.PartnerIds.Contains(firstId);
            if(partners)
            {
                bool firstAccepts=first.Family.Peers.TryGetValue(secondId,out var fromFirst)&&fromFirst.ConsentsToOtherPartners;
                bool secondAccepts=second.Family.Peers.TryGetValue(firstId,out var fromSecond)&&fromSecond.ConsentsToOtherPartners;
                _modalContents.AddChild(Text(first.Name+(firstAccepts?"同意":"没有同意")+second.Name+"另结伴侣。\n"+
                    second.Name+(secondAccepts?"同意":"没有同意")+first.Name+"另结伴侣。",13));
                _modalContents.AddChild(Button("讨论是否接受其他伴侣",()=>ApplyFamilyCommand(()=>Sim.DiscussOpenPartnership(firstId,secondId))));
                _modalContents.AddChild(Button("这只油撤回对另一方另结伴侣的许可",()=>ApplyFamilyCommand(()=>Sim.RevokeOpenPartnership(firstId,secondId))));
                _modalContents.AddChild(Button("结束这对伴侣关系",()=>ApplyFamilyCommand(()=>Sim.EndPartnership(firstId,secondId))));
            }
            Creature mother=first.Body.ReproductionRole==ReproductionRole.Maternal?first:second;
            Creature father=mother==first?second:first;
            bool rolesMatch=mother.Body.ReproductionRole==ReproductionRole.Maternal&&father.Body.ReproductionRole==ReproductionRole.Paternal;
            _modalContents.AddChild(Text(rolesMatch?"安排孕育："+mother.Name+"承担母体，"+father.Name+"承担父体。成熟度、双方意愿或身体不合适时，会说明原因。":
                "这两只承担相同的繁殖角色，无法共同孕育。仍然可以相处、建立关系和照护家人。",13));
            string motherId=mother.Id,fatherId=father.Id;
            var methods=Row(_modalContents);
            methods.AddChild(Button("安排茎生孕育",()=>ApplyFamilyCommand(()=>Sim.RequestPregnancy(motherId,fatherId,BirthMethod.Stem))));
            methods.AddChild(Button("安排胎生孕育",()=>ApplyFamilyCommand(()=>Sim.RequestPregnancy(motherId,fatherId,BirthMethod.Live))));
            _modalContents.AddChild(Text("胎生需要更久、宝宝较少，母体也需要更多吃喝与恢复。已开始的孕育不会因为关闭繁殖许可而消失。",13,"78816e"));
            if(first.IsYoung!=second.IsYoung)
            {
                string childId=first.IsYoung?firstId:secondId,caregiverId=first.IsYoung?secondId:firstId;
                _modalContents.AddChild(Button("请成年一方照护幼体",()=>ApplyFamilyCommand(()=>Sim.ArrangeCare(caregiverId,childId))));
            }
        }
        else _modalContents.AddChild(Text("这里还没有另一只油可以邀请。",14));
        _modalContents.AddChild(Button("制止这只油的家庭冲突",()=>ApplyFamilyCommand(()=>Sim.InterruptFamilyConflict(firstId))));
        _modalContents.AddChild(Text("冷藏只让相关生命过程以正常速度的十分之一推进。有保存期限和复苏风险，需要及时取出照护；旧伤仍会保留，不能无限拖住濒危状态。",13,"78816e"));
        bool stored=first.Family.ColdStorage.Stored;
        _modalContents.AddChild(Button(stored?(first.Condition==PhysicalCondition.Dead?"取出遗体":"取出这只油，观察复苏"):"把这只油放入冷藏",
            ()=>ApplyFamilyCommand(()=>Sim.SetColdStorage(firstId,!stored))));
        bool autonomous=Sim.State.AutonomousBreeding;
        _modalContents.AddChild(Button(autonomous?"自主繁育：开 · 点击关闭":"自主繁育：关 · 点击开启",
            ()=>ApplyFamilyCommand(()=>Sim.SetAutonomousBreeding(!autonomous))));
        _modalContents.AddChild(Button("回到小院",CloseModal));
    }

    private void AddFamilyChoice(string caption,Creature[] oils,string? selected,Action<string> select)
    {
        _modalContents.AddChild(Text(caption,13,"78816e"));
        var choice=new OptionButton{CustomMinimumSize=new(0,38),SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,
            FitToLongestItem=false,ClipText=true,FocusMode=Control.FocusModeEnum.None};
        for(int n=0;n<oils.Length;n++)
        {
            choice.AddItem(OilListName(oils[n]));
            if(oils[n].Id==selected)choice.Select(n);
        }
        choice.ItemSelected+=index=>{if(index>=0&&index<oils.Length)select(oils[(int)index].Id);};
        _modalContents.AddChild(choice);
    }

    private string FamilySummary(Creature c)
    {
        string NameFor(string? id)=>id==null?"未记录":Sim.State.Creatures.FirstOrDefault(o=>o.Id==id)?.Name??"不在这里的家人";
        var details=new List<string>{c.Name+" · "+(c.Body.ReproductionRole==ReproductionRole.Maternal?"母体角色":"父体角色")};
        if(c.Family.MotherId!=null||c.Family.FatherId!=null)details.Add("母亲："+NameFor(c.Family.MotherId)+"；父亲："+NameFor(c.Family.FatherId));
        if(c.Family.PartnerIds.Count>0)details.Add("伴侣："+string.Join("、",c.Family.PartnerIds.OrderBy(id=>id).Select(id=>NameFor(id))));
        if(c.Family.CaregiverIds.Count>0)details.Add("照护者："+string.Join("、",c.Family.CaregiverIds.Select(id=>NameFor(id))));
        string situation=FamilyRules.Describe(c);
        if(situation.Length>0)details.Add(situation);
        return string.Join("\n",details);
    }

    private void ApplyFamilyCommand(Func<string> action)
    {
        // Modal pause is temporary; the simulation must see the original pause state.
        CloseModal();
        _message=Sim.State.Settings.Pause?"小院仍暂停着。先继续时间，再安排活动。":action();
        RefreshRoomUi();
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
        Visible=c.Condition!=PhysicalCondition.Dead&&!c.Family.ColdStorage.Stored&&p.Phase==PregnancyPhase.Gestating;
        if(!Visible)return;
        Position=motion+new Vector3(.76f*bodyScale,.80f*bodyScale,-.10f);
        _stem.Visible=p.Method==BirthMethod.Stem;_cradle.Visible=p.Method==BirthMethod.Live;
        _label.Visible=selected;_label.Text=p.Method==BirthMethod.Stem?"茎生孕育":"胎生孕育";
    }
}
