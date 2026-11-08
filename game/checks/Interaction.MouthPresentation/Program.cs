using System.Text.Json;
using Yukkuri.Simulation;

// Remote-only checks. Local source inspection does not establish an audio or UI result.
int failures=0;
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string name,Action action)
{try{action();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
WorldSimulation Room()
{
    var sim=WorldSimulation.CreateInteractionPlaytest(301030);
    var c=sim.Selected!;c.Fullness=100;c.Energy=100;c.Interaction.Hydration=100;
    c.Trust=80;c.Fear=0;c.Pain=0;c.Interaction.NextAutonomyAt=10000;
    return sim;
}
void Mouth(Creature c,double damage,double permanent=0)
{
    c.Interaction.Injuries.Sites.RemoveAll(s=>s.Part==BodyPart.Mouth);
    c.Interaction.Injuries.Sites.Add(new(){Part=BodyPart.Mouth,Damage=damage,PermanentLoss=permanent,Cause="已有嘴边伤势"});
}
WorldSimulation Reload(WorldSimulation sim)
{
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
(WorldSimulation Sim,Creature Speaker,Creature Listener,string Fact) Telling()
{
    var sim=WorldSimulation.CreateMilestone(seed:301030);var s=sim.State;
    s.Creatures.RemoveRange(2,s.Creatures.Count-2);s.Settings.Invasions=false;s.Settings.Accidents=false;
    foreach(var region in s.Regions){region.Facilities.Clear();region.StaffEnabled=false;}
    for(int n=0;n<s.Creatures.Count;n++)
    {
        var c=s.Creatures[n];c.X=n*1.5;c.Z=0;c.TargetX=c.X;c.TargetZ=c.Z;
        c.Fullness=100;c.Energy=100;c.Pain=0;c.Fear=0;c.DecisionIn=10000;c.BehaviorCooldown=10000;
        c.Knowledge.Clear();c.PersonalMemories.Clear();
    }
    var speaker=s.Creatures[0];var listener=s.Creatures[1];string fact="event-000001";
    s.IncidentSequence=1;s.Incidents.Clear();
    s.Incidents.Add(new(){Id=fact,Kind=IncidentKind.Care,TargetId=speaker.Id,ActorId="player",RegionId="home",Description="主人给讲述者送来食物。"});
    speaker.Knowledge.Add(new(){IncidentId=fact,KnowsActor=true,Source=KnowledgeSource.Witness});
    s.SocialTimer=12-sim.Config.TickSeconds;
    return(sim,speaker,listener,fact);
}

Check("普通动作仍可接受，最终台词按嘴部能力表达",()=>
{
    var normal=Room();var limited=Room();Mouth(limited.Selected!,60);
    var a=normal.ExecuteInteraction(normal.Selected!.Id,new(InteractionAction.Stretch));
    var b=limited.ExecuteInteraction(limited.Selected!.Id,new(InteractionAction.Stretch));
    Require(a.Success&&b.Success&&!b.Refused,"受伤把仍能完成的伸展误作拒绝");
    Require(a.Speech.Length>0&&a.Speech!=b.Speech,"正常句子与含混发声未区分");
    Require(b.Speech==MouthFunction.FormatSpeech(limited.Selected!,a.Speech)&&b.Speech==limited.Selected!.Speech,"反馈与角色最终台词不一致");
    Require(b.Pose=="stretch"&&limited.Selected!.Interaction.ReactionSituation=="stretch","说话受限吞掉真实身体动作");
});
Check("唱不清不能结算唱歌成功或增加亲近",()=>
{
    foreach(var wound in new[]{(Damage:60d,Permanent:0d),(Damage:0d,Permanent:70d)})
    {
        var sim=Room();var c=sim.Selected!;Mouth(c,wound.Damage,wound.Permanent);
        double trust=c.Trust,attachment=c.Attachment,budget=c.Interaction.HumanBond.CareBudget;
        var answer=sim.ExecuteInteraction(c.Id,new(InteractionAction.Sing));
        Require(!answer.Success&&answer.Refused&&!answer.Changed&&answer.Message.Contains("唱不清"),"口部受限没有真实做不到反馈");
        Require(answer.Pose!="sing"&&c.Interaction.ReactionSituation!="sing","拒绝仍留下唱歌成功表现");
        Require(c.Trust==trust&&c.Attachment==attachment&&c.Interaction.HumanBond.CareBudget==budget,"失败唱歌结算了成功奖励");
    }
});
Check("未达发音受限程度仍可唱歌，不把当前与永久损失相加",()=>
{
    foreach(var wound in new[]{(Damage:0d,Permanent:0d),(Damage:59d,Permanent:0d),(Damage:35d,Permanent:40d)})
    {
        var sim=Room();var c=sim.Selected!;Mouth(c,wound.Damage,wound.Permanent);
        var answer=sim.ExecuteInteraction(c.Id,new(InteractionAction.Sing));
        Require(answer.Success&&answer.Pose=="sing"&&answer.Speech!="唔……嗯……","仍能说清楚的个体被禁止唱歌");
    }
});
Check("口部含混的普通发声不沿用亲昵旋律，吃喝声与情绪事实保留",()=>
{
    var sim=Room();var c=sim.Selected!;Mouth(c,70);c.Expression=Expression.Content;
    foreach(string situation in new[]{"stretch","nuzzle","lift","rock"})
    {
        c.Interaction.ReactionSituation=situation;c.Interaction.ReactionRefused=false;
        string cue=InteractionPresentation.VoiceCue(c);
        Require(cue=="coo-refuse","严重口伤仍选择普通亲昵声音");
        Require(!c.Interaction.ReactionRefused&&c.Expression==Expression.Content,"音效选择改写接受或情绪事实");
    }
    c.Interaction.ReactionSituation="drink";Require(InteractionPresentation.VoiceCue(c)=="feed","咽水声被错误当作清晰台词禁止");
});
Check("求救痛呼优先于发音限制，死亡与冷藏没有回应声",()=>
{
    var c=Room().Selected!;Mouth(c,80);c.Interaction.ReactionRefused=true;
    c.Expression=Expression.Calling;Require(InteractionPresentation.VoiceCue(c)=="coo-help","含混或拒绝盖过求救");
    c.Expression=Expression.Hurt;Require(InteractionPresentation.VoiceCue(c)=="coo-hurt","含混或拒绝盖过痛呼");
    c.Expression=Expression.Content;c.Pain=50;Require(InteractionPresentation.VoiceCue(c)=="coo-hurt","剧痛播放普通回应");
    c.Condition=PhysicalCondition.Critical;Require(InteractionPresentation.VoiceCue(c)=="coo-help","濒危求救被剧痛盖过");
    c.Condition=PhysicalCondition.Dead;Require(InteractionPresentation.VoiceCue(c)=="","死者仍发声");
    c.Condition=PhysicalCondition.Active;c.Family.ColdStorage.Stored=true;Require(InteractionPresentation.VoiceCue(c)=="","冷藏中仍发声");
});
Check("真实查看伤势入口仍保留剧痛与濒危表达优先",()=>
{
    var sim=Room();var c=sim.Selected!;Mouth(c,75);c.Pain=55;
    var painful=sim.ExecuteInteraction(c.Id,new(InteractionAction.Inspect));
    Require(painful.Success&&c.Expression==Expression.Hurt&&InteractionPresentation.VoiceCue(c)=="coo-hurt","实际伤势查看被含混反馈盖过痛苦");
    c.Condition=PhysicalCondition.Critical;c.CriticalRemaining=30;c.Health=5;
    var critical=sim.ExecuteInteraction(c.Id,new(InteractionAction.Inspect));
    Require(critical.Success&&critical.Speech=="……"&&critical.Pose=="help"&&InteractionPresentation.VoiceCue(c)=="coo-help","实际濒危入口失去微弱回应或求救");
});
Check("清楚的讲者能把事实告诉口伤听者，不能把口伤当失聪",()=>
{
    var f=Telling();Mouth(f.Listener,75);f.Listener.Interaction.EatingRemaining=3;
    f.Sim.Advance(f.Sim.Config.TickSeconds);
    var learned=f.Listener.Knowledge.SingleOrDefault(k=>k.IncidentId==f.Fact);
    Require(learned is {KnowsActor:true,Source:KnowledgeSource.Told}&&learned.InformantId==f.Speaker.Id,"受伤或吃东西的听者被禁止理解事实");
    Require(f.Listener.PersonalMemories.Any(m=>m.IncidentId==f.Fact),"理解的事实没有留下来源记忆");
});
Check("口伤或忙嘴的讲者不能转述，伤情改善后原有事实仍可传递",()=>
{
    foreach(bool mouthful in new[]{false,true})
    {
        var f=Telling();
        if(mouthful)f.Speaker.Interaction.EatingRemaining=3;else Mouth(f.Speaker,70);
        f.Sim.Advance(f.Sim.Config.TickSeconds);
        Require(!f.Sim.Knows(f.Listener,f.Fact),"说不清或忙嘴时仍完整传播事实");
        Require(f.Sim.Knows(f.Speaker,f.Fact),"发音受限清除了原有知识");
        // A later recovered/finished state is a fixture transition, not a healing test.
        f.Speaker.Interaction.EatingRemaining=0;Mouth(f.Speaker,0);
        f.Sim.State.SocialTimer=12-f.Sim.Config.TickSeconds;f.Sim.Advance(f.Sim.Config.TickSeconds);
        Require(f.Sim.Knows(f.Listener,f.Fact),"恢复讲述能力后仍无法传递原有事实");
    }
});
Check("持久口伤存读后仍限制唱歌和完整讲述，知识本身保存",()=>
{
    var f=Telling();Mouth(f.Speaker,0,70);var copy=Reload(f.Sim);var c=copy.Selected!;
    Require(!MouthFunction.CanSpeakClearly(c)&&copy.Knows(c,f.Fact),"读档丢失发音限制或原有知识");
    copy.Advance(copy.Config.TickSeconds);
    Require(!copy.Knows(copy.State.Creatures.Single(x=>x.Id==f.Listener.Id),f.Fact),"读档后绕过转述限制");
    var sim=Room();Mouth(sim.Selected!,0,70);var roomCopy=Reload(sim);
    Require(!roomCopy.ExecuteInteraction(roomCopy.Selected!.Id,new(InteractionAction.Sing)).Success,"读档后持久口伤仍能成功唱歌");
});
Check("暂停和近看不推进旧事实转述",()=>
{
    foreach(bool closeUp in new[]{false,true})
    {
        var f=Telling();if(closeUp)f.Sim.State.Mode=PlayMode.CloseUp;else f.Sim.State.Settings.Pause=true;
        double clock=f.Sim.State.WorldSeconds;f.Sim.Advance(24);
        Require(f.Sim.State.WorldSeconds==clock&&!f.Sim.Knows(f.Listener,f.Fact),"自然时间停止时发生转述");
    }
});
Environment.ExitCode=failures==0?0:1;
