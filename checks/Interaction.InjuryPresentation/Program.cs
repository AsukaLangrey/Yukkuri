using Yukkuri.Simulation;

int failures=0;
void Require(bool ok,string message){if(!ok)throw new Exception(message);}
void Check(string title,Action run)
{try{run();Console.WriteLine("PASS "+title);}catch(Exception e){failures++;Console.WriteLine("FAIL "+title+": "+e);}}

Check("淤痕和永久旧痕只属于实际受伤侧",()=>
{
    var c=new Creature();c.Interaction.Injuries.Sites.Add(new(){Part=BodyPart.Cheek,Side=SurfaceSide.Left,Damage=45,PermanentLoss=20});
    Require(ActorInjuryPresentation.MarkOpacity(c.Interaction,BodyPart.Cheek,SurfaceSide.Left,false)>0,"左伤没有淤痕");
    Require(ActorInjuryPresentation.MarkOpacity(c.Interaction,BodyPart.Cheek,SurfaceSide.Right,false)==0,"左伤串到右脸");
    c.Health=100;c.Pain=0;c.Trust=100;c.Interaction.Injuries.Sites[0].Damage=0;
    Require(ActorInjuryPresentation.MarkOpacity(c.Interaction,BodyPart.Cheek,SurfaceSide.Left,false)==0&&
        ActorInjuryPresentation.MarkOpacity(c.Interaction,BodyPart.Cheek,SurfaceSide.Left,true)>0,"总健康恢复抹掉永久旧痕或淤痕永不消失");
});
Check("敷料遮盖对应伤痕但不抹掉伤势数据",()=>
{
    var c=new Creature();var life=c.Interaction;
    life.Injuries.Sites.Add(new(){Part=BodyPart.Side,Side=SurfaceSide.Right,Damage=30,PermanentLoss=5});
    life.Dress(BodyPart.Side,SurfaceSide.Right);
    Require(ActorInjuryPresentation.MarkOpacity(life,BodyPart.Side,SurfaceSide.Right,false)==0,"淤痕穿透敷料");
    life.RemoveDressing(BodyPart.Side,SurfaceSide.Right);
    Require(ActorInjuryPresentation.MarkOpacity(life,BodyPart.Side,SurfaceSide.Right,false)>0&&life.Injuries.Sites[0].Damage==30,"移开敷料后真实伤势消失");
});
Check("剧痛濒危死亡覆盖乐观表情与亲近姿势",()=>
{
    var c=new Creature{Expression=Expression.Delighted,Pain=60,Trust=100};
    Require(ActorInjuryPresentation.ExpressionFor(c)==Expression.Hurt&&ActorInjuryPresentation.GuardPose(c,"nuzzle")=="guard","剧痛仍笑着蹭");
    Require(ActorInjuryPresentation.GuardPose(c,"pressure")=="pressure","伤势覆盖了仍在实际施力的动作");
    c.Condition=PhysicalCondition.Critical;c.Interaction.Carried=true;
    Require(ActorInjuryPresentation.ExpressionFor(c)==Expression.Exhausted&&ActorInjuryPresentation.GuardPose(c,"nuzzle")=="carry-tense","危重丢失手的支撑或还在亲昵");
    c.Condition=PhysicalCondition.Dead;
    Require(ActorInjuryPresentation.GuardPose(c,"sing")=="dead","死亡后仍唱歌");
});
Check("受损底部跳步减少抬升但不改模拟轨迹",()=>
{
    var c=new Creature();var normal=ActorHopMotion.Sample(.22f);
    var healthy=ActorInjuryPresentation.AdaptHop(c,normal);
    Require(healthy==normal,"健康时改变了既有跳步参数");
    c.Interaction.FootIntegrity=40;
    var injured=ActorInjuryPresentation.AdaptHop(c,normal);
    Require(injured.Lift<normal.Lift&&injured.Travel==normal.Travel,"伤残未改变努力程度，或错误改动水平结算");
    c.Interaction.FootIntegrity=23;c.Trust=100;c.Health=100;
    Require(ActorInjuryPresentation.StepStrength(c)==0,"高健康信任使失能者恢复跳动");
    c.Body.SpeciesId="remilia";
    Require(ActorInjuryPresentation.StepStrength(c)==1,"底部伤势错误取消其他形态的移动表现");
});
Check("身体声音优先于残留亲昵情境",()=>
{
    var c=new Creature{Expression=Expression.Delighted,Pain=65};c.Interaction.ReactionSituation="nuzzle";
    Require(InteractionPresentation.VoiceCue(c)=="coo-hurt","剧痛选择了亲昵声音");
    c.Condition=PhysicalCondition.Critical;
    Require(InteractionPresentation.VoiceCue(c)=="coo-help","危重没有使用呼救声音");
    c.Condition=PhysicalCondition.Dead;
    Require(InteractionPresentation.VoiceCue(c)=="","死亡仍发活体声音");
    c.Condition=PhysicalCondition.Active;c.Family.ColdStorage.Stored=true;
    Require(InteractionPresentation.VoiceCue(c)=="","冷藏者沿原院内坐标发声");
});
Check("新的家庭回应不继承旧吃喝或拒绝情境",()=>
{
    var sim=WorldSimulation.CreateInteractionPlaytest();var a=sim.State.Creatures[0];var b=sim.State.Creatures[1];
    a.X=-2;a.Z=0;b.X=-.4;b.Z=0;
    a.Family.Peers[b.Id]=new(){Familiarity=40,Affection=30,Fear=45};
    b.Family.Peers[a.Id]=new(){Familiarity=40,Affection=30};
    b.Interaction.ReactionSituation="drink";b.Interaction.ReactionRefused=true;b.Interaction.ReactionPose="nuzzle";b.Interaction.ReactionPoseRemaining=2;
    int serial=b.ReactionSerial;sim.InviteCompanion(a.Id,b.Id);
    Require(b.ReactionSerial>serial,"夹具未生成家庭拒绝回应");
    Require(b.Interaction.ReactionSituation==""&&!b.Interaction.ReactionRefused&&b.Interaction.ReactionPose!="nuzzle","新React沿用了旧动作元数据");
    Require(InteractionPresentation.VoiceCue(b)=="coo-refuse","家庭拒绝播了前一口饮水声音");
});
Environment.ExitCode=failures==0?0:1;
