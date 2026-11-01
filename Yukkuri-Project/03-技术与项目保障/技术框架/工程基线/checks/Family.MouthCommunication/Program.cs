using System.Text.Json;
using Yukkuri.Simulation;

// Authorised independent test host only. Never run on the development workstation.
int failures=0;
void Require(bool condition,string reason){if(!condition)throw new Exception(reason);}
void Check(string name,Action action)
{try{action();Console.WriteLine("PASS "+name);}catch(Exception error){failures++;Console.WriteLine("FAIL "+name+": "+error);}}
void Tick(WorldSimulation sim,double seconds)
{for(int n=0;n<(int)Math.Round(seconds*20);n++){sim.AdvanceInteractionPresentation(.05);sim.Advance(.05);}}
WorldSimulation Fixture()
{
    var sim=WorldSimulation.CreateInteractionPlaytest(20261102);sim.State.RoomObjects.Clear();
    sim.State.PlayerX=0;sim.State.PlayerZ=7;
    for(int n=0;n<3;n++)
    {
        var c=sim.State.Creatures[n];c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
        c.Fullness=c.Interaction.Hydration=c.Energy=c.Health=100;c.Fear=c.Pain=0;c.Trust=80;
        c.Aggression=.05;c.Patience=.8;c.Courage=.2;c.TrueDisposition="gentle";
        c.X=c.TargetX=n==0?0:n==1?1.65:-5.5;c.Z=c.TargetZ=n==2?-5.5:0;
        c.Family.NextSocialAt=c.Family.NextCareAt=10000;c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;
    }
    return sim;
}
void Mouth(Creature c,double damage=0,double lasting=75)
{
    c.Interaction.Injuries.Sites.RemoveAll(w=>w.Part==BodyPart.Mouth);
    c.Interaction.Injuries.Sites.Add(new(){Part=BodyPart.Mouth,Damage=damage,PermanentLoss=lasting,Cause="口部功能边界样本"});
}
WorldSimulation Reload(WorldSimulation sim)
{
    var state=JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions),SaveStore.JsonOptions)!;
    SaveStore.Validate(state);return new(state);
}
WorldSimulation LossFixture()
{
    var sim=Fixture();var listener=sim.State.Creatures[1];var deceased=sim.State.Creatures[2];
    listener.X=listener.TargetX=6;listener.Z=listener.TargetZ=6;
    deceased.X=deceased.TargetX=1.65;deceased.Z=deceased.TargetZ=0;
    sim.Hurt(deceased,200,"讲述用死亡事实",null);sim.LayToRest(deceased.Id);
    Require(sim.State.Creatures[0].Family.KnownLosses.ContainsKey(deceased.Id)&&!listener.Family.KnownLosses.ContainsKey(deceased.Id),"离世知情前置错误");
    listener.X=listener.TargetX=1.65;listener.Z=listener.TargetZ=0;return sim;
}
WorldSimulation RelationshipFixture()
{
    var sim=Fixture();var speaker=sim.State.Creatures[0];var partner=sim.State.Creatures[2];
    speaker.Family.PartnerIds.Add(partner.Id);partner.Family.PartnerIds.Add(speaker.Id);
    sim.State.FamilyRelationshipEvents.Add(new(){Id="mouth-relationship",FirstId=speaker.Id,SecondId=partner.Id,
        KnownBy=new(){[speaker.Id]=KnowledgeSource.Witness}});return sim;
}
WorldSimulation TeachingFixture()
{
    var sim=Fixture();var teacher=sim.State.Creatures[0];var child=sim.State.Creatures[1];
    child.IsYoung=true;child.Body.AgeSeconds=child.InnerAgeSeconds=10;child.X=child.TargetX=1.45;
    child.Family.MotherId=child.ParentId=teacher.Id;
    teacher.Family.Peers[child.Id]=new(){Familiarity=70,Affection=70};child.Family.Peers[teacher.Id]=new(){Familiarity=70,Affection=70};
    teacher.Family.LifeKnowledge["safe:Meal"]=false;child.Family.LifeKnowledge["safe:Water"]=true;
    return sim;
}
void StartLesson(WorldSimulation sim)
{
    var teacher=sim.State.Creatures[0];var child=sim.State.Creatures[1];
    string result=sim.TeachFamilyKnowledge(teacher.Id,child.Id,"safe:Meal");
    Require(teacher.Family.Lesson!=null,"未开始正常教导："+result);
}

Check("关系转述要求说话者能清楚说话，正常与受伤听者均可听懂",()=>
{
    foreach(string mode in new[]{"normal","speaker","listener"})
    {
        var sim=RelationshipFixture();var speaker=sim.State.Creatures[0];var listener=sim.State.Creatures[1];
        if(mode=="speaker")Mouth(speaker);if(mode=="listener")Mouth(listener);
        sim.TellFamilyRelationship(speaker.Id,listener.Id,"mouth-relationship");
        var fact=sim.State.FamilyRelationshipEvents[0];
        Require(fact.KnownBy.ContainsKey(listener.Id)==(mode!="speaker"),"关系讲述能力方向错误："+mode);
        if(mode!="speaker")Require(fact.KnownBy[listener.Id]==KnowledgeSource.Told,"听者口伤使转述成为目击");
        Require(fact.KnownBy[speaker.Id]==KnowledgeSource.Witness&&speaker.Family.PartnerIds.Contains(sim.State.Creatures[2].Id),"口伤删除已有知识或关系");
    }
});
Check("离世手动转述只限制说话者，不限制受伤听者的理解",()=>
{
    foreach(string mode in new[]{"normal","speaker","listener"})
    {
        var sim=LossFixture();var speaker=sim.State.Creatures[0];var listener=sim.State.Creatures[1];var deceased=sim.State.Creatures[2];
        if(mode=="speaker")Mouth(speaker);if(mode=="listener")Mouth(listener);
        sim.TellFamilyLoss(speaker.Id,listener.Id,deceased.Id);
        Require(listener.Family.KnownLosses.ContainsKey(deceased.Id)==(mode!="speaker"),"离世转述能力方向错误："+mode);
        if(mode!="speaker")Require(listener.Family.KnownLosses[deceased.Id].Source==KnowledgeSource.Told,"实际听说来源错误");
        Require(speaker.Family.KnownLosses[deceased.Id].Source==KnowledgeSource.Witness,"不能说话就丢失已有见证");
    }
});
Check("自主陪伴中的离世转述使用同一口部条件，安静陪伴仍可发生",()=>
{
    foreach(string mode in new[]{"normal","speaker","listener"})
    {
        var sim=LossFixture();var speaker=sim.State.Creatures[0];var listener=sim.State.Creatures[1];var deceased=sim.State.Creatures[2];
        if(mode=="speaker")Mouth(speaker);if(mode=="listener")Mouth(listener);
        sim.InviteCompanion(speaker.Id,listener.Id);Tick(sim,3);
        Require(speaker.Family.LastCompanionActivityAt>0&&listener.Family.LastCompanionActivityAt>0,"口部限制一刀切取消普通陪伴："+mode);
        Require(listener.Family.KnownLosses.ContainsKey(deceased.Id)==(mode!="speaker"),"自主转述绕过口部限制或拒绝受伤听者："+mode);
    }
});
Check("伤势与持续功能损失都在60边界限制完整讲述",()=>
{
    foreach(bool lasting in new[]{false,true})
    foreach(double level in new[]{59.99,60.0,80.0})
    {
        var sim=RelationshipFixture();var speaker=sim.State.Creatures[0];var listener=sim.State.Creatures[1];
        Mouth(speaker,lasting?0:level,lasting?level:0);
        sim.TellFamilyRelationship(speaker.Id,listener.Id,"mouth-relationship");
        Require(sim.State.FamilyRelationshipEvents[0].KnownBy.ContainsKey(listener.Id)==(level<60),"口部边界未按共同功能规则："+lasting+"/"+level);
    }
});
Check("教师口部严重受限不能新教导，但保留知识与陪伴",()=>
{
    var sim=TeachingFixture();var teacher=sim.State.Creatures[0];var child=sim.State.Creatures[1];Mouth(teacher);
    sim.TeachFamilyKnowledge(teacher.Id,child.Id,"safe:Meal");
    Require(teacher.Family.Lesson==null&&!child.Family.LifeKnowledge.ContainsKey("safe:Meal"),"口部不能清晰讲述仍教出知识");
    Require(teacher.Family.LifeKnowledge.ContainsKey("safe:Meal")&&child.Family.MotherId==teacher.Id,"受伤删除常识或亲缘");
    sim.InviteCompanion(teacher.Id,child.Id);Tick(sim,8);
    Require(teacher.Family.LastCompanionActivityAt>0&&child.Family.LastCompanionActivityAt>0&&teacher.Family.Lesson==null&&
        !child.Family.LifeKnowledge.ContainsKey("safe:Meal"),"自动教导绕过限制或普通陪伴被禁止");
});
Check("正常教师能完整教导，幼体口伤不妨碍听懂并记知识",()=>
{
    foreach(bool listenerInjured in new[]{false,true})
    {
        var sim=TeachingFixture();var teacher=sim.State.Creatures[0];var child=sim.State.Creatures[1];
        if(listenerInjured)Mouth(child);StartLesson(sim);Tick(sim,6.5);
        Require(teacher.Family.Lesson==null&&child.Family.LearningSources.TryGetValue("safe:Meal",out var source)&&
            source.Source==LifeKnowledgeSource.Taught&&source.FromId==teacher.Id,"听者口伤被误当成不能理解："+listenerInjured);
        Require(child.Family.LifeKnowledge["safe:Water"]&&child.Family.MotherId==teacher.Id,"学习覆盖已有无关知识或亲缘");
    }
});
Check("课程中途严重口伤及读档会停止未讲完内容，不重抽也不删已有知识",()=>
{
    var sim=TeachingFixture();var teacher=sim.State.Creatures[0];var child=sim.State.Creatures[1];
    StartLesson(sim);Tick(sim,2);Require(teacher.Family.Lesson is {Elapsed:>0 and <6},"中途样本没有实际教导进度");
    Mouth(teacher,80,0);var restored=Reload(sim);Tick(sim,.25);Tick(restored,.25);
    Require(teacher.Family.Lesson==null&&!child.Family.LifeKnowledge.ContainsKey("safe:Meal")&&child.Family.LifeKnowledge["safe:Water"],"受伤中断写入半节课或删旧知识");
    Require(teacher.Family.NextLessonAt>sim.State.WorldSeconds&&teacher.Family.CompanionTargetId==child.Id,"中断缺冷却或取消了可继续的安静陪伴");
    Require(JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==JsonSerializer.Serialize(restored.State,SaveStore.JsonOptions),"读档重新结算口伤中断");
});
Check("受伤听者的中途课程可读档续接，完成理解不重新抽取",()=>
{
    var sim=TeachingFixture();Mouth(sim.State.Creatures[1]);StartLesson(sim);Tick(sim,3);var restored=Reload(sim);
    Tick(sim,4);Tick(restored,4);
    Require(sim.State.Creatures[1].Family.LifeKnowledge.ContainsKey("safe:Meal")&&
        JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions)==JsonSerializer.Serialize(restored.State,SaveStore.JsonOptions),"口伤听者丢失课程或读档改变理解");
});
Check("暂停与近看不暗中推进或结算口伤课程",()=>
{
    var sim=TeachingFixture();var teacher=sim.State.Creatures[0];StartLesson(sim);Tick(sim,2);Mouth(teacher);
    double elapsed=teacher.Family.Lesson!.Elapsed,time=sim.State.WorldSeconds;
    sim.State.Settings.Pause=true;Tick(sim,10);sim.State.Settings.Pause=false;sim.State.Mode=PlayMode.CloseUp;Tick(sim,10);
    Require(teacher.Family.Lesson?.Elapsed==elapsed&&sim.State.WorldSeconds==time,"暂停近看推进课程");
    sim.State.Mode=PlayMode.Management;Tick(sim,.25);
    Require(teacher.Family.Lesson==null&&!sim.State.Creatures[1].Family.LifeKnowledge.ContainsKey("safe:Meal"),"恢复后严重口伤仍结算未完成教导");
});

Environment.ExitCode=failures==0?0:1;
