using Godot;
using System;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private VBoxContainer _creatureVitalMeters=null!;
    private Label _fullnessLabel=null!,_thirstLabel=null!,_healthLabel=null!;
    private ProgressBar _fullnessMeter=null!,_thirstMeter=null!;
    private StyleBoxFlat _fullnessFill=null!,_thirstFill=null!;
    private Label _comfortLabel=null!,_trustLabel=null!,_lifeLabel=null!,_fearLabel=null!,_continuityLabel=null!,_personalityLabel=null!;
    private Label _waterSupportLabel=null!,_hungerFeedingLabel=null!,_familyNeedsLabel=null!;
    private ProgressBar _comfortMeter=null!,_trustMeter=null!,_lifeMeter=null!,_fearMeter=null!;
    private StyleBoxFlat _comfortFill=null!,_trustFill=null!,_lifeFill=null!,_fearFill=null!;
    private void BuildVitalMeters(Control parent)
    {
        var meters=Column(parent,5);_creatureVitalMeters=meters;
        _fullnessLabel=Text("饱食度",13);meters.AddChild(_fullnessLabel);
        _fullnessMeter=NeedMeter(meters,"8aa576",out _fullnessFill);
        _thirstLabel=Text("口渴度",13);meters.AddChild(_thirstLabel);
        _thirstMeter=NeedMeter(meters,"7cabc0",out _thirstFill);
        _healthLabel=Text("身体状态：正常",14);meters.AddChild(_healthLabel);
        BuildImmediateInterventions(meters);
        _personalityLabel=Text("",12,"78816e");meters.AddChild(_personalityLabel);
        var overview=new GridContainer{Columns=2,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
        overview.AddThemeConstantOverride("h_separation",10);overview.AddThemeConstantOverride("v_separation",7);meters.AddChild(overview);
        BuildRelationshipMeter(overview,"馒馒来", "91aa79",out _comfortLabel,out _comfortMeter,out _comfortFill);
        BuildRelationshipMeter(overview,"信任", "779c9c",out _trustLabel,out _trustMeter,out _trustFill);
        BuildRelationshipMeter(overview,"生命", "88a877",out _lifeLabel,out _lifeMeter,out _lifeFill);
        BuildRelationshipMeter(overview,"恐惧", "ae8c74",out _fearLabel,out _fearMeter,out _fearFill);
        _continuityLabel=Text("",12,"78816e");meters.AddChild(_continuityLabel);
        _waterSupportLabel=Text("",12,"78816e");meters.AddChild(_waterSupportLabel);
        _hungerFeedingLabel=Text("",12,"a56b55");meters.AddChild(_hungerFeedingLabel);
        _familyNeedsLabel=Text("",12,"78816e");meters.AddChild(_familyNeedsLabel);
    }
    private void BuildRelationshipMeter(Control parent,string name,string color,out Label label,out ProgressBar bar,out StyleBoxFlat fill)
    {
        var column=Column(parent,3);column.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;
        label=Text(name,12);column.AddChild(label);bar=NeedMeter(column,color,out fill);
        bar.CustomMinimumSize=new(0,7);
    }
    private ProgressBar NeedMeter(Control parent,string color,out StyleBoxFlat fill)
    {
        var bar=new ProgressBar{MinValue=0,MaxValue=100,ShowPercentage=false,CustomMinimumSize=new(0,12),MouseFilter=Control.MouseFilterEnum.Ignore};
        bar.AddThemeStyleboxOverride("background",RoomStyle("e2e3d5",5,0));
        fill=RoomStyle(color,5,0);bar.AddThemeStyleboxOverride("fill",fill);parent.AddChild(bar);return bar;
    }
    private void RefreshVitalMeters(Creature c)
    {
        double full=Math.Clamp(c.Fullness,0,100),thirst=100-Math.Clamp(c.Interaction.Hydration,0,100);
        _fullnessMeter.Value=full;_thirstMeter.Value=thirst;
        _fullnessLabel.Text=$"饱食度  {Math.Round(full):0}/100";
        _thirstLabel.Text=$"口渴度  {Math.Round(thirst):0}/100 · 越高越渴";
        _fullnessFill.BgColor=new Color(full<25?"bd795d":"8aa576");
        _thirstFill.BgColor=new Color(thirst>75?"bd795d":"7cabc0");
        _healthLabel.Text="身体状态："+(c.HealthStatus switch
        {
            HealthStatus.Dead=>"死亡",HealthStatus.Critical=>"重伤 · 急需救治",
            HealthStatus.Recovering=>"恢复中",HealthStatus.Injured=>"受伤",_=>"正常"
        });
        if(c.Condition==PhysicalCondition.Critical)
            _healthLabel.Text+=$" · 救治余下约 {Math.Ceiling(Math.Max(0,c.CriticalRemaining)):0} 秒";
        var personality=c.Personality;
        _personalityLabel.Text="性情："+(personality.Kindness>=.65?"友善":personality.Kindness<.25?"自私":"随和")+
            (personality.Carelessness>=.5?" · 粗心":personality.Carelessness<.2?" · 谨慎":"")+
            (personality.Pride>=.75?" · 傲慢":"");
        double comfort=Math.Clamp(c.Interaction.Emotion.Manmanlai,0,100),trust=Math.Clamp(c.Trust,0,100);
        double health=Math.Clamp(c.Health,0,100),fear=Math.Clamp(c.Fear,0,100);
        _comfortLabel.Text=$"馒馒来 {comfort:0}";_comfortMeter.Value=comfort;
        _trustLabel.Text=$"信任 {trust:0}";_trustMeter.Value=trust;
        _lifeLabel.Text=$"生命 {health:0}";_lifeMeter.Value=health;
        _fearLabel.Text=$"恐惧 {fear:0}";_fearMeter.Value=fear;
        _comfortFill.BgColor=new Color(comfort<30?"b88a71":"91aa79");
        _trustFill.BgColor=new Color(trust<20?"a79b87":"779c9c");
        _lifeFill.BgColor=new Color(health<35?"b86e61":"88a877");
        _fearFill.BgColor=new Color(fear>50?"b86e61":"ae8c74");
        var emotion=c.Interaction.Emotion;var water=c.Interaction.Water;
        _continuityLabel.Text=c.Condition==PhysicalCondition.Dead?"生命已经结束，经历和关系记录仍保留。":
            WaterEnvironment.IsInWater(Sim.State,c)?$"落水 {water.ExposureSeconds:0} 秒 · 挣扎 {water.Struggle:0}\n拿救援用品点击它，可尝试带回岸上。":
            water.RecoverySeconds>0?$"已经离水，仍在恢复 · 余下 {water.RecoverySeconds:0} 秒\n先擦干并检查伤势。":
            emotion.Alarm>0?$"惊惧余波 {emotion.Alarm:0.0} · 需要持续安全\n愿意接近食物，也可能仍然害怕；先给它一点空间。":
            !EmotionalReadiness.Settled(c)?$"疼痛 {c.Pain:0} · 恐惧 {fear:0}\n还没有安定下来，先照顾身体并留出空间。":
            "0—100：馒馒来是当前心情；信任取决于相处经历。";
        RefreshImmediateInterventions(c);
        RefreshHungerFeedingStatus(c);
        RefreshFamilyNeedsStatus(c);
        var support=Sim.WaterSupportStatus(c);
        var supported=support==null?null:Sim.State.Creatures.Find(other=>other.Id==support.TargetId);
        _waterSupportLabel.Visible=support!=null&&supported!=null;
        if(support!=null&&supported!=null)
            _waterSupportLabel.Text=support.Phase switch
            {
                WaterSupportPhase.Calling=>$"正在为{supported.Name}呼救。",
                WaterSupportPhase.Responding=>$"正在岸边尝试接应{supported.Name}。",
                WaterSupportPhase.Approaching=>$"正在靠近刚上岸的{supported.Name}，查看情况。",
                _=>$"在{supported.Name}身边守候 · "+(support.ContactSeconds>=6?"已经陪伴一会儿":$"持续 {support.ContactSeconds:0} 秒")+
                    "\n守候不能代替实际食水和伤势护理。"
            };
    }
    private void RefreshFamilyNeedsStatus(Creature c)
    {
        var pregnancy=c.Family.Pregnancy;
        _familyNeedsLabel.Visible=c.Condition!=PhysicalCondition.Dead&&!c.Family.ColdStorage.Stored&&
            (c.IsYoung||pregnancy.Phase==PregnancyPhase.Recovering);
        if(!_familyNeedsLabel.Visible)return;
        _familyNeedsLabel.Text=c.IsYoung?
            $"幼体保暖 {Math.Clamp(c.Family.Warmth,0,100):0}/100"+
                (c.Family.Warmth<35?" · 正在发冷":"")+"\n吃喝、保暖和陪伴都需要持续照看。":
            $"孕育恢复尚需 {Math.Ceiling(Math.Max(0,pregnancy.RecoveryRemaining)):0} 秒\n吃喝不足或疼痛时，恢复进度会停住。";
    }

    private void RefreshHungerFeedingStatus(Creature c)
    {
        var feeding=Sim.HungerFeedingStatus(c);
        var target=feeding==null?null:Sim.State.Creatures.Find(other=>other.Id==feeding.TargetId);
        _hungerFeedingLabel.Visible=feeding!=null&&target!=null;
        if(feeding==null||target==null)return;
        bool remains=target.Condition==PhysicalCondition.Dead;
        _hungerFeedingLabel.Text=feeding.Phase switch
        {
            HungerFeedingPhase.Pursuing=>remains?$"正在靠近{target.Name}留下的遗体。":$"饥饿中盯上了{target.Name}。\n及时隔开，或提供能实际吃到的食物。",
            HungerFeedingPhase.Preparing=>remains?$"正准备取食遗体 · {feeding.ContactProgress*100:0}%":$"正在准备袭咬{target.Name} · {feeding.ContactProgress*100:0}%\n现在仍可隔开制止。",
            _=>$"已经入口，正在吞咽 · 摄食量 {feeding.TakenNutrition:0.#}\n先检查被袭击者的伤势，并避免再次接触。"
        };
        if(remains&&feeding.Phase==HungerFeedingPhase.Swallowing)
            _hungerFeedingLabel.Text=$"已经入口，正在吞咽 · 摄食量 {feeding.TakenNutrition:0.#}";
    }
    private static string WasteLabel(RoomObject obj)=>obj.Kind==RoomObjectKind.Urine?"尿液 · Del 清理":"粪便 · Del 清理";
    private static void BuildWaste(Node3D root,RoomObjectKind kind)
    {
        if(kind==RoomObjectKind.Urine)
        {
            var puddle=SceneArt.Ball(root,new(0,.014f,0),new(.60f,.024f,.46f),"c5b565");
            SceneArt.EnableSurfaceFade(puddle);
            SceneArt.SetSurfaceOpacity(puddle,.65f);
        }
        else
        {
            SceneArt.Ball(root,new(0,.07f,0),new(.26f,.14f,.23f),"71513a");
            SceneArt.Ball(root,new(.08f,.05f,.09f),new(.17f,.10f,.16f),"806044");
        }
    }
}
