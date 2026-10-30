using System.Text.Json;
using Yukkuri.Simulation;

// Source only. Execution belongs to the independently authorised remote machine.
int failures=0;
void Require(bool ok,string why){if(!ok)throw new Exception(why);}
void Check(string name,Action body)
{try{body();Console.WriteLine("PASS "+name);}catch(Exception e){failures++;Console.WriteLine("FAIL "+name+": "+e);}}
string Snapshot(WorldSimulation s)=>JsonSerializer.Serialize(s.State,SaveStore.JsonOptions);
void Step(WorldSimulation s){s.AdvanceInteractionPresentation(.1);s.Advance(.1);}
void Tick(WorldSimulation s,double seconds){for(int n=0;n<(int)Math.Ceiling(seconds*10);n++)Step(s);}
double Care(Creature a,Creature b)=>a.Family.Peers.TryGetValue(b.Id,out var bond)?bond.CareSeconds:0;
WorldSimulation Fresh(string relationship="parent")
{
    var s=WorldSimulation.CreateInteractionPlaytest(20261031);s.State.RoomObjects.Clear();
    s.State.PlayerX=0;s.State.PlayerZ=7;
    for(int n=0;n<s.State.Creatures.Count;n++)
    {
        var c=s.State.Creatures[n];c.IsYoung=false;c.Body.AgeSeconds=c.InnerAgeSeconds=2000;
        c.Fullness=c.Energy=c.Interaction.Hydration=90;c.Pain=c.Fear=0;
        c.TrueDisposition="gentle";c.Personality.Initialized=true;c.Personality.Kindness=.8;c.Aggression=.1;c.Patience=.8;
        c.Family.NextCareAt=c.Family.NextSocialAt=c.Interaction.NextAutonomyAt=10000;c.Interaction.ToyInterest=0;
        c.Family.LifeKnowledge.Clear();c.Family.LearningSources.Clear();c.Family.Peers.Clear();
        c.X=c.TargetX=n==0?0:-6;c.Z=c.TargetZ=n==0?0:-6;
    }
    var a=s.State.Creatures[0];var child=s.State.Creatures[2];
    child.IsYoung=true;child.Body.AgeSeconds=child.InnerAgeSeconds=0;child.Family.Warmth=40;
    child.Interaction.FootIntegrity=0; // Initial dependent child, not an asserted injury journey.
    child.X=child.TargetX=WorldSimulation.InteractionFootprint(a)+WorldSimulation.InteractionFootprint(child)+.1;
    child.Z=child.TargetZ=0;child.ParentId=null;child.Family.MotherId=null;child.Family.FatherId=null;
    child.Family.CaregiverIds.Clear();
    if(relationship=="parent")child.ParentId=child.Family.MotherId=a.Id;
    if(relationship=="caregiver")child.Family.CaregiverIds.Add(a.Id); // Existing upbringing, not newly earned.
    return s;
}
void Invite(WorldSimulation s,Creature a,Creature b)
{
    string message=s.InviteCompanion(a.Id,b.Id);
    Require(a.Family.CompanionTargetId==b.Id&&b.Family.CompanionTargetId==a.Id,"real invitation failed: "+message);
}

Check("亲代与幼体真实相处仍提供贴身温暖，不冒充递送或照护收益",()=>
{
    var s=Fresh();var a=s.State.Creatures[0];var child=s.State.Creatures[2];Invite(s,a,child);
    string before=Snapshot(s);Require(s.ArrangeCare(a.Id,child.Id).Contains("相处"),"occupied caregiver accepted a second task");
    Require(Snapshot(s)==before,"refused delivery changed state");
    double warmth=child.Family.Warmth;Tick(s,2);
    Require(child.Family.Warmth>warmth&&a.Family.CompanionTargetId==child.Id,"real child companionship did not share warmth");
    Require(Care(a,child)==0&&Care(child,a)==0&&a.Family.CareTargetId==null,"passive warmth created care work or care credit");
});

Check("只对真实亲缘或既有养亲保暖，不把普通相处直接变养亲",()=>
{
    foreach(string relation in new[]{"caregiver","unrelated"})
    {
        var s=Fresh(relation);var a=s.State.Creatures[0];var child=s.State.Creatures[2];Invite(s,a,child);
        double warmth=child.Family.Warmth;Tick(s,1);
        Require(relation=="caregiver"?child.Family.Warmth>warmth:child.Family.Warmth<warmth,"family warmth eligibility ignored relationship");
        Require(Care(a,child)==0&&Care(child,a)==0,"passive warmth earned stable care qualification");
        Require(child.Family.CaregiverIds.Contains(a.Id)==(relation=="caregiver"),"companionship rewrote upbringing");
    }
});

Check("保暖继续要求身体余力、真实接触与当前准确同伴",()=>
{
    foreach(string boundary in new[]{"hungry","thirsty","pain","far","other-companion","one-sided"})
    {
        var s=Fresh();var a=s.State.Creatures[0];var child=s.State.Creatures[2];var other=s.State.Creatures[1];
        if(boundary=="hungry")a.Fullness=40;
        if(boundary=="thirsty")a.Interaction.Hydration=40;
        if(boundary=="far"){child.X=child.TargetX=3;a.Interaction.FootIntegrity=0;}
        if(boundary=="other-companion")
        {other.X=other.TargetX=-1;other.Z=other.TargetZ=0;Invite(s,a,other);}
        else Invite(s,a,child);
        if(boundary=="pain")a.Pain=30; // Existing body-state boundary; no claim of a player injury journey.
        // One-sided references are an old-save boundary, not a gameplay action.
        if(boundary=="one-sided")child.Family.CompanionTargetId=null;
        double warmth=child.Family.Warmth;Step(s);
        Require(child.Family.Warmth<warmth,"invalid warmth source warmed child: "+boundary);
    }
});

Check("真实结束相处后能够安排照护，原伴侣亲缘和关系收益不被改写",()=>
{
    var s=Fresh();var a=s.State.Creatures[0];var child=s.State.Creatures[2];var partner=s.State.Creatures[1];
    // Explicit pre-existing partnership is preserved; no claim of earning it here.
    a.Family.PartnerIds.Add(partner.Id);partner.Family.PartnerIds.Add(a.Id);
    Invite(s,a,child);Tick(s,2);
    string bonds=JsonSerializer.Serialize(new[]{a.Family.Peers,child.Family.Peers},SaveStore.JsonOptions);
    Require(s.EndCompanionship(a.Id,child.Id).StartsWith("已结束"),"current pair could not stop");
    Require(a.Family.CompanionTargetId==null&&child.Family.CompanionTargetId==null,"pair did not stop symmetrically");
    Require(JsonSerializer.Serialize(new[]{a.Family.Peers,child.Family.Peers},SaveStore.JsonOptions)==bonds,"stopping changed relationship rewards/history");
    Require(a.Family.PartnerIds.Contains(partner.Id)&&partner.Family.PartnerIds.Contains(a.Id)&&child.ParentId==a.Id&&child.Family.MotherId==a.Id,"stopping dissolved family identity");
    s.ArrangeCare(a.Id,child.Id);Require(a.Family.CareTargetId==child.Id,"released caregiver still cannot accept care");
    double warmth=child.Family.Warmth;Tick(s,2);
    Require(child.Family.Warmth>warmth&&Care(a,child)>0,"real care did not follow ending companionship");
    SaveStore.Validate(s.State);
});

Check("重复停止、旧对象和对方新相处不受旧按钮影响",()=>
{
    var s=Fresh();var a=s.State.Creatures[0];var child=s.State.Creatures[2];var other=s.State.Creatures[1];
    Invite(s,a,child);s.EndCompanionship(a.Id,child.Id);
    string stopped=Snapshot(s);s.EndCompanionship(a.Id,child.Id);Require(Snapshot(s)==stopped,"repeat stop changed state");
    other.X=other.TargetX=-1;other.Z=other.TargetZ=0;Invite(s,a,other);
    string current=Snapshot(s);s.EndCompanionship(a.Id,child.Id);s.EndCompanionship(child.Id,a.Id);
    Require(Snapshot(s)==current&&a.Family.CompanionTargetId==other.Id&&other.Family.CompanionTargetId==a.Id,"stale pair command cancelled newer companionship");
    // A one-sided persisted reference must not clear the other person's new pair either.
    child.Family.CompanionTargetId=a.Id;child.Family.CompanionUntil=a.Family.CompanionUntil;
    current=Snapshot(s);s.EndCompanionship(child.Id,a.Id);Require(Snapshot(s)==current,"stale one-sided reference cancelled new pair");
});

Check("暂停和错误入口不变，保存后相处保暖与停止仍可用",()=>
{
    var s=Fresh();var a=s.State.Creatures[0];var child=s.State.Creatures[2];Invite(s,a,child);Tick(s,1);
    s.State.Settings.Pause=true;string paused=Snapshot(s);
    Require(s.EndCompanionship(a.Id,child.Id).Contains("恢复时间"),"paused stop was not rejected");
    Tick(s,1);Require(Snapshot(s)==paused,"pause changed world or companionship");
    s.State.Settings.Pause=false;string before=Snapshot(s);
    s.EndCompanionship("missing",child.Id);s.EndCompanionship(a.Id,a.Id);
    Require(Snapshot(s)==before,"invalid command changed world");
    SaveStore.Validate(s.State);var loaded=JsonSerializer.Deserialize<WorldState>(Snapshot(s),SaveStore.JsonOptions)!;
    SaveStore.Validate(loaded);s=new(loaded);a=s.State.Creatures[0];child=s.State.Creatures[2];
    double warmth=child.Family.Warmth;Tick(s,1);Require(child.Family.Warmth>warmth,"loaded real companion lost warmth");
    Require(s.EndCompanionship(child.Id,a.Id).StartsWith("已结束"),"either current participant should be able to stop");
});

Check("实际相处建立教学信任，未听完的真实教导可停止且不会伪造知识",()=>
{
    var s=Fresh();var a=s.State.Creatures[0];var child=s.State.Creatures[2];
    a.Family.LifeKnowledge["safe:Meal"]=true; // Existing parental knowledge; child starts without it or earned trust.
    for(int n=0;n<3000&&a.Family.Lesson==null;n++)
    {
        if(a.Family.CompanionTargetId==null)s.InviteCompanion(a.Id,child.Id);
        Step(s);
    }
    Require(a.Family.Lesson is {Key:"safe:Meal"}&&a.Family.Lesson.Elapsed<6,"real companionship did not reach actual lesson");
    Require(child.Family.Peers[a.Id].Affection>=10&&!child.Family.LifeKnowledge.ContainsKey("safe:Meal"),"lesson prerequisites or incomplete knowledge incorrect");
    double warmth=child.Family.Warmth;Tick(s,.5);
    Require(a.Family.Lesson!=null&&child.Family.Warmth>warmth&&Care(a,child)==0,"ongoing real lesson blocked warmth or invented care");
    Require(s.EndCompanionship(child.Id,a.Id).StartsWith("已结束"),"learner could not end current lesson/company");
    Require(a.Family.Lesson==null&&!child.Family.LifeKnowledge.ContainsKey("safe:Meal")&&a.Family.NextLessonAt>=s.State.WorldSeconds+29.9,"unfinished teaching produced knowledge or skipped existing interruption cooldown");
    SaveStore.Validate(s.State);
});

return failures==0?0:1;
