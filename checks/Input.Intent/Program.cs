using System.Numerics;
using System.Text.Json;
using Yukkuri.Simulation;

var results=new List<object>();int failures=0;
var replays=new List<object>();
void Check(string name,Action test)
{
    try{test();results.Add(new{name,passed=true});Console.WriteLine("PASS "+name);}
    catch(Exception e){failures++;results.Add(new{name,passed=false,error=e.Message});Console.WriteLine("FAIL "+name+": "+e.Message);}
}
void Require(bool v,string why){if(!v)throw new Exception(why);}
ContactMotion Tracker(){var t=new ContactMotion();t.Sample("oil",Vector3.Zero,Vector3.UnitZ,.01);return t;}
ContactMotionSample Step(ContactMotion t,double speed,double dt=1.0/60,string? target="oil",bool engaged=true)=>
    t.Sample(target,new((float)(speed*dt),0,0),Vector3.UnitZ,dt,1,engaged);
ContactMotionSample Stop(ContactMotion t,double dt=1.0/60)
{
    ContactMotionSample result=default;
    for(int n=0;n<Math.Ceiling(.12/dt);n++)
    {
        var sample=Step(t,0,dt);
        if(sample.StopImpact>0){Require(result.StopImpact==0,"one stop produced duplicate impacts");result=sample;}
    }
    return result;
}
WorldSimulation Fresh(){var s=WorldSimulation.CreateInteractionPlaytest();s.Selected!.Trust=60;return s;}

Check("unengaged fast motion and its sudden stop never become contact",()=>
{
    var t=Tracker();for(int n=0;n<10;n++){var p=Step(t,12,engaged:false);Require(p.Distance==0&&p.StopImpact==0,"hover counted as touch");}
    Require(Step(t,0,engaged:false).StopImpact==0,"hover stop hit");
    Require(Step(t,0).StopImpact==0,"press inherited previous hover speed");
});
Check("fast engaged travel differs from an abrupt contact stop",()=>
{
    var t=Tracker();for(int n=0;n<8;n++){var p=Step(t,5);Require(p.StopImpact==0,"speed alone became strike");}
    var stop=Stop(t);Require(stop.StopImpact>0&&stop.Direction.X>.9,"stop failed to preserve incoming direction");
    for(int n=0;n<60;n++)Require(Step(t,0).StopImpact==0,"stationary hand repeated a hit");
});
Check("gradual deceleration does not become an abrupt strike",()=>
{
    var t=Tracker();for(int n=0;n<8;n++)Step(t,5);
    for(int n=1;n<=30;n++)Require(Step(t,5*(1-n/30.0)).StopImpact==0,"gentle braking became hit");
});
Check("brief pointer jitter has insufficient stroke history to strike",()=>
{
    var t=Tracker();Step(t,10);Require(Step(t,0).StopImpact==0,"single noisy sample struck");
});
Check("leaving body then stopping cannot complete the previous strike",()=>
{
    var t=Tracker();for(int n=0;n<8;n++)Step(t,5);
    Step(t,5,target:null);Require(Step(t,0).StopImpact==0,"re-entry inherited impact");
});
Check("release and focus reset discard the pending strike",()=>
{
    var t=Tracker();for(int n=0;n<8;n++)Step(t,5);t.Reset();
    Require(Step(t,0).StopImpact==0,"cancelled gesture struck");
});
Check("long stalled frame cannot turn into a strike or catch up travel",()=>
{
    var t=Tracker();for(int n=0;n<8;n++)Step(t,5);
    var stalled=Step(t,0,.4);Require(stalled.StopImpact==0&&stalled.Distance==0,"stall became strike");
    Require(Step(t,0).StopImpact==0,"stall history survived reset");
});
Check("another creature cannot inherit the preceding contact impulse",()=>
{
    var t=Tracker();for(int n=0;n<8;n++)Step(t,5);
    Require(Step(t,0,target:"other").StopImpact==0,"target switch hit new target");
});
Check("fast moving hand that stays fast only produces sustained rubbing",()=>
{
    var sim=Fresh();var c=sim.Selected!;var t=Tracker();
    sim.BeginInteractionGesture(c.Id,new(InteractionAction.Touch));double health=c.Health;
    for(int n=0;n<30;n++)
    {
        var m=Step(t,5);sim.ContinueInteractionGesture(new(InteractionAction.Touch,Force:m.Force,Duration:1.0/60,Motion:m.Distance,StopImpact:m.StopImpact),true);
    }
    Require(c.Health==health&&c.Interaction.ReactionRefused,"rubbing should be refused without inventing strike damage");
    sim.EndInteractionGesture();Require(c.Interaction.Memories.ContainsKey("rough-touch"),"roughness lost on release");
});
Check("slow and fast-then-stop contact have different persistent consequences",()=>
{
    var gentle=Fresh();var g=gentle.Selected!;gentle.BeginInteractionGesture(g.Id,new(InteractionAction.Touch));var slow=Tracker();
    for(int n=0;n<60;n++) {var m=Step(slow,.3);gentle.ContinueInteractionGesture(new(InteractionAction.Touch,Force:m.Force,Duration:1.0/60,Motion:m.Distance),true);}
    gentle.EndInteractionGesture();Require(g.Trust>60&&g.Health==100,"gentle stroke harmed instead of comforting");
    var sim=Fresh();var c=sim.Selected!;sim.BeginInteractionGesture(c.Id,new(InteractionAction.Touch));var t=Tracker();
    for(int n=0;n<8;n++){var m=Step(t,5);sim.ContinueInteractionGesture(new(InteractionAction.Touch,Force:m.Force,Duration:1.0/60,Motion:m.Distance),true);}
    for(int n=0;n<8;n++){var stop=Step(t,0);sim.ContinueInteractionGesture(new(InteractionAction.Touch,Force:stop.Force,Duration:1.0/60,StopImpact:stop.StopImpact),true);}
    Require(c.Health<100&&c.Trust<60&&c.Interaction.ReactionPose=="slap","stop failed to become distinct impact");
    double health=c.Health;sim.EndInteractionGesture();Require(c.Health==health&&c.Memories.Last().Contains("冲击"),"release replayed hit or lost memory");
});
Check("identical timed stroke at 30 60 144 fps preserves dose and strike strength",()=>
{
    var data=new List<(double dose,double impact)>();
    foreach(int fps in new[]{30,60,144})
    {
        var t=Tracker();double dose=0,dt=1.0/fps;int frames=(int)Math.Round(.2*fps);
        for(int n=0;n<frames;n++)dose+=Step(t,5,dt).Speed*dt;
        data.Add((dose,Stop(t,dt).StopImpact));
    }
    Require(data.All(d=>d.impact>0)&&data.Max(d=>d.impact)-data.Min(d=>d.impact)<.0001,"fps changed brake strength");
    Require(data.Max(d=>d.dose)-data.Min(d=>d.dose)<.025,"fps changed filtered speed integral");
});
Check("matched world-space travel at different zooms gives equal force",()=>
{
    var values=new List<double>();
    foreach(float size in new[]{3.5f,9f,23f})
    {
        var t=Tracker();var pixels=new Vector2(.08f*1080/size,0);
        var motion=InteractionCameraOrbit.ViewMotion(Vector3.UnitX,Vector3.UnitY,pixels,size,1080);
        ContactMotionSample sample=default;for(int n=0;n<8;n++)sample=t.Sample("oil",motion,Vector3.UnitZ,1.0/60);
        values.Add(sample.Force);
    }
    Require(values.Max()-values.Min()<.00001,"camera scale changed matched contact speed");
});
Check("body motion without pointer motion does not produce force or repeated affection",()=>
{
    var t=Tracker();for(int n=0;n<60;n++){var m=Step(t,0);Require(m.Distance==0&&m.StopImpact==0,"idle pointer produced work");}
    var sim=Fresh();var c=sim.Selected!;sim.BeginInteractionGesture(c.Id,new(InteractionAction.Touch));
    for(int n=0;n<60;n++)sim.ContinueInteractionGesture(new(InteractionAction.Touch,Force:.12,Duration:1.0/60),true);
    Require(c.Trust==60&&c.Health==100,"stationary hand modified relationship");
});
Check("save reload preserves injury but never resumes a pending stop gesture",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.BeginInteractionGesture(c.Id,new(InteractionAction.Touch));
    sim.ContinueInteractionGesture(new(InteractionAction.Touch,StopImpact:.6),true);sim.EndInteractionGesture();
    string json=JsonSerializer.Serialize(sim.State,SaveStore.JsonOptions);
    var state=JsonSerializer.Deserialize<WorldState>(json,SaveStore.JsonOptions)!;SaveStore.Validate(state);
    var loaded=new WorldSimulation(state);Require(loaded.Selected!.Health==c.Health&&!loaded.HasInteractionGesture,"save lost injury or replayed gesture");
});
Check("invalid impact and nonfinite motion are rejected without changing health",()=>
{
    var sim=Fresh();var c=sim.Selected!;sim.BeginInteractionGesture(c.Id,new(InteractionAction.Touch));
    Require(!sim.ContinueInteractionGesture(new(InteractionAction.Touch,StopImpact:2),true).Success&&c.Health==100,"invalid force applied");
    var t=Tracker();Require(t.Sample("oil",new(float.NaN,0,0),Vector3.UnitZ,.01)==default,"nonfinite sample accepted");
});
Check("a single missing motion packet followed by continued travel is not a stop",()=>
{
    var t=Tracker();for(int n=0;n<8;n++)Step(t,5);
    Require(Step(t,0).StopImpact==0,"unconfirmed empty packet struck");
    for(int n=0;n<8;n++)Require(Step(t,5).StopImpact==0,"resumed movement completed stale brake");
    Require(Stop(t).StopImpact>0,"real endpoint was lost after packet gap");
});
Check("sharp braking through an intermediate speed still reaches one endpoint",()=>
{
    var t=Tracker();for(int n=0;n<8;n++)Step(t,5);
    Require(Step(t,1.3).StopImpact==0,"braking struck before settling");
    Require(Stop(t).StopImpact>0,"two-step abrupt stop was missed");
});
Check("release during stop confirmation discards its pending impact",()=>
{
    var t=Tracker();for(int n=0;n<8;n++)Step(t,5);Step(t,0);t.Reset();
    Require(Stop(t).StopImpact==0,"release completed a pending strike");
});
Check("a refused touch does not shield a body that is still actually in contact",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Trust=5;
    sim.BeginInteractionGesture(c.Id,new(InteractionAction.Touch));
    sim.ContinueInteractionGesture(new(InteractionAction.Touch,Force:.2,Motion:.01),true);
    Require(c.Interaction.ReactionRefused&&c.Health==100,"setup did not refuse without injury");
    sim.ContinueInteractionGesture(new(InteractionAction.Touch,StopImpact:.6),true);
    Require(c.Health<100,"refusal swallowed later impact");double health=c.Health;
    sim.ContinueInteractionGesture(new(InteractionAction.Touch,StopImpact:.6),true);
    Require(c.Health==health,"same held gesture replayed impact");
});
Check("refusal followed by broken contact cannot apply an impact",()=>
{
    var sim=Fresh();var c=sim.Selected!;c.Trust=5;
    sim.BeginInteractionGesture(c.Id,new(InteractionAction.Touch));
    sim.ContinueInteractionGesture(new(InteractionAction.Touch,Force:.2,Motion:.01),true);
    sim.ContinueInteractionGesture(new(InteractionAction.Touch,StopImpact:.6),false);
    sim.ContinueInteractionGesture(new(InteractionAction.Touch,StopImpact:.6),true);
    Require(c.Health==100,"off-body or resumed sample dealt damage");
});
Check("inward approach tangential rubbing and outward withdrawal remain distinct",()=>
{
    ContactMotionSample Sample(Vector3 direction)
    {
        var tracker=Tracker();ContactMotionSample last=default;
        for(int n=0;n<20;n++)last=tracker.Sample("oil",direction*.025f,Vector3.UnitZ,.025);
        return last;
    }
    var along=Sample(Vector3.UnitX);var inward=Sample(-Vector3.UnitZ);var outward=Sample(Vector3.UnitZ);
    Require(along.TangentFraction>.999&&along.ApproachSpeed==0,"surface rubbing lost its tangent");
    Require(inward.TangentFraction<.001&&inward.ApproachSpeed>.99&&inward.NormalSpeed<0,"inward direction lost its sign");
    Require(inward.Force>along.Force+.04&&outward.Force<.13,"pressing and withdrawing supplied the same effective force");
});
Check("fast outward withdrawal followed by a stop does not strike",()=>
{
    foreach(int fps in new[]{30,60,144})
    {
        var t=Tracker();double dt=1.0/fps;
        for(int n=0;n<Math.Ceiling(.2/dt);n++)
            Require(t.Sample("oil",Vector3.UnitZ*(float)(5*dt),Vector3.UnitZ,dt).StopImpact==0,"withdrawal struck while moving");
        Require(Stop(t,dt).StopImpact==0,$"withdrawal endpoint struck at {fps} fps");
    }
});
Check("withdrawing after a fast rub cancels its pending strike history",()=>
{
    var t=Tracker();for(int n=0;n<8;n++)Step(t,5);
    for(int n=0;n<8;n++)t.Sample("oil",Vector3.UnitZ*(5f/60),Vector3.UnitZ,1.0/60);
    Require(Stop(t).StopImpact==0,"outward exit inherited a rubbing strike");
    for(int n=0;n<8;n++)Step(t,5);
    Require(Stop(t).StopImpact>0,"a later real contact stop was suppressed");
});
Check("a mostly outward diagonal withdrawal stays distinct from a surface stroke",()=>
{
    foreach(float yaw in new[]{0f,.7f,2f})foreach(float pitch in new[]{-.2f,.5f,1f})
    {
        var rotation=Quaternion.CreateFromYawPitchRoll(yaw,pitch,0);
        var normal=Vector3.Transform(Vector3.UnitZ,rotation);
        foreach(bool outward in new[]{true,false})
        {
            var t=new ContactMotion();t.Sample("oil",Vector3.Zero,normal,.01);
            var travel=Vector3.Transform(Vector3.Normalize(outward?new(.4f,0,1):new(1,0,.2f)),rotation)*(5f/60);
            for(int n=0;n<12;n++)t.Sample("oil",travel,normal,1.0/60);
            double impact=0;
            for(int n=0;n<8;n++)impact=Math.Max(impact,t.Sample("oil",Vector3.Zero,normal,1.0/60).StopImpact);
            Require(outward?impact==0:impact>0,"diagonal withdrawal and mostly tangential stroke were conflated");
        }
    }
});
Check("slow withdrawal after a fast rub clears both impact history and force tail",()=>
{
    foreach(int fps in new[]{30,60,144})
    {
        double dt=1.0/fps;var t=Tracker();
        for(int n=0;n<Math.Ceiling(.2/dt);n++)Step(t,5,dt);
        var withdrawn=t.Sample("oil",Vector3.UnitZ*(float)(.2*dt),Vector3.UnitZ,dt);
        Require(withdrawn.StopImpact==0&&withdrawn.Force<.13,$"slow exit inherited force at {fps} fps");
        Require(Stop(t,dt).StopImpact==0,$"slow exit inherited strike at {fps} fps");
        for(int n=0;n<Math.Ceiling(.2/dt);n++)Step(t,5,dt);
        Require(Stop(t,dt).StopImpact>0,"later deliberate contact stop was disabled");
    }
});
Check("slow reversal during braking does not complete the previous stroke",()=>
{
    foreach(int fps in new[]{30,60,144})
    {
        double dt=1.0/fps;var t=Tracker();
        for(int n=0;n<Math.Ceiling(.2/dt);n++)Step(t,5,dt);
        Require(Step(t,-.2,dt).StopImpact==0,"reversal completed the incoming hit");
        Require(Stop(t,dt).StopImpact==0,"reversed stroke left a pending hit");
    }
});
Check("pulling away during stop confirmation cancels the pending hit",()=>
{
    var t=Tracker();for(int n=0;n<8;n++)Step(t,5);
    Require(Step(t,0).StopImpact==0,"first stationary sample already struck");
    t.Sample("oil",Vector3.UnitZ/60,Vector3.UnitZ,1.0/60);
    Require(Stop(t).StopImpact==0,"withdrawal during confirmation completed a stale hit");
});
Check("gentle direct pressure cannot farm stroking affection",()=>
{
    var s=Fresh();var c=s.Selected!;var t=Tracker();s.BeginInteractionGesture(c.Id,new(InteractionAction.Touch));
    InteractionFeedback last=new(false,"","","");
    for(int n=0;n<60;n++)
    {
        var sample=t.Sample("oil",-Vector3.UnitZ*.005f,Vector3.UnitZ,1.0/60);
        last=s.ContinueInteractionGesture(new(InteractionAction.Touch,Force:sample.Force,Duration:1.0/60,Motion:sample.Distance,
            TouchTangentFraction:sample.TangentFraction,TouchNormalSpeed:sample.NormalSpeed),true);
    }
    Require(c.Trust==60&&c.Health==100&&last.Pose=="touch-press","direct pressure was counted as petting or injury");
});
Check("rotating the whole camera-contact basis preserves normal and tangent effects",()=>
{
    var values=new List<ContactMotionSample>();
    foreach(float yaw in new[]{0f,.7f,2f})foreach(float pitch in new[]{-.2f,.5f,1f})
    {
        var rotation=Quaternion.CreateFromYawPitchRoll(yaw,pitch,0);var tracker=Tracker();ContactMotionSample last=default;
        for(int n=0;n<12;n++)last=tracker.Sample("oil",Vector3.Transform(new(.025f,0,-.012f),rotation),Vector3.Transform(Vector3.UnitZ,rotation),.02);
        values.Add(last);
    }
    Require(values.Max(x=>x.Force)-values.Min(x=>x.Force)<.00001&&values.Max(x=>x.TangentFraction)-values.Min(x=>x.TangentFraction)<.00001,"camera basis changed identical geometry");
});
Check("irregular sample intervals preserve a timed stroke and its confirmed stop",()=>
{
    (double dose,double impact,int hits) Replay(double[] timing)
    {
        var t=Tracker();double time=0,dose=0,impact=0;int index=0,hits=0;
        while(time<.2-.000001)
        {double dt=Math.Min(timing[index++%timing.Length],.2-time);var p=Step(t,5,dt);dose+=p.Speed*dt;time+=dt;}
        time=0;
        while(time<.15)
        {double dt=timing[index++%timing.Length];var p=Step(t,0,dt);time+=dt;if(p.StopImpact>0){impact=p.StopImpact;hits++;}}
        return(dose,impact,hits);
    }
    var regular=Replay([1.0/60]);var irregular=Replay([.009,.028,.013,.021,.007,.034]);
    Require(regular.hits==1&&irregular.hits==1&&Math.Abs(regular.impact-irregular.impact)<.00001,"uneven sampling lost or duplicated stop");
    Require(Math.Abs(regular.dose-irregular.dose)<.00001,"uneven sampling changed integrated travel response");
});
Check("invalid surface components are rejected at the gesture entry",()=>
{
    var s=Fresh();var c=s.Selected!;
    Require(!s.BeginInteractionGesture(c.Id,new(InteractionAction.Touch,TouchTangentFraction:1.1)).Success,"impossible tangent fraction accepted");
    Require(!s.BeginInteractionGesture(c.Id,new(InteractionAction.Touch,TouchNormalSpeed:double.NaN)).Success,"nonfinite normal speed accepted");
});
Check("timestamped trajectory fixtures replay consistently at fixed and uneven intervals",()=>
{
    using var fixtures=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures/contact-traces.json")));
    var strengths=new Dictionary<string,List<double>>();
    foreach(var fixture in fixtures.RootElement.EnumerateArray())
    foreach(var schedule in new[]{new[]{1.0/30},new[]{1.0/60},new[]{1.0/144},new[]{.009,.028,.013,.021,.007,.034}})
    {
        var points=fixture.GetProperty("points").EnumerateArray().Select(p=>new{
            at=p.GetProperty("at").GetDouble(),x=p.GetProperty("x").GetDouble(),contact=p.GetProperty("contact").GetBoolean(),held=p.GetProperty("held").GetBoolean()}).ToArray();
        var tracker=new ContactMotion();var events=new List<object>();int hits=0,segment=0,index=0;double time=0,lastX=points[0].x;
        tracker.Sample(points[0].contact?"oil":null,Vector3.Zero,Vector3.UnitZ,.01,manipulating:points[0].held);
        while(time<points[^1].at-.0000001)
        {
            double dt=Math.Min(schedule[index++%schedule.Length],points[^1].at-time);time+=dt;
            while(segment+1<points.Length&&points[segment+1].at<=time+.0000001)segment++;
            var from=points[segment];var to=points[Math.Min(segment+1,points.Length-1)];
            double x=from.at==to.at?from.x:from.x+(to.x-from.x)*(time-from.at)/(to.at-from.at);
            var sample=tracker.Sample(from.contact?"oil":null,new((float)(x-lastX),0,0),Vector3.UnitZ,dt,manipulating:from.held);
            if(sample.StopImpact>0){hits++;events.Add(new{time,sample.StopImpact});}lastX=x;
        }
        string name=fixture.GetProperty("name").GetString()!;int expected=fixture.GetProperty("impacts").GetInt32();
        if(!strengths.ContainsKey(name))strengths[name]=new();
        foreach(var entry in events)
        {
            using var serialized=JsonDocument.Parse(JsonSerializer.Serialize(entry));
            strengths[name].Add(serialized.RootElement.GetProperty("StopImpact").GetDouble());
        }
        replays.Add(new{name,schedule,expected,hits,events});
        Require(hits==expected,$"{name} at {string.Join(',',schedule)}: expected {expected} impacts, got {hits}");
    }
    foreach(var (name,values) in strengths.Where(pair=>pair.Value.Count>0))
        Require(values.Max()-values.Min()<.05,$"{name}: endpoint sampling changed strength by {values.Max()-values.Min():F4}");
});
Check("finite but overflowing vector or time scale cannot poison later input",()=>
{
    var tracker=Tracker();
    Require(tracker.Sample("oil",new(float.MaxValue,0,0),Vector3.UnitZ,.01)==default,"overflowing vector was accepted");
    Step(tracker,0);
    Require(tracker.Sample("oil",Vector3.UnitX,Vector3.UnitZ,double.Epsilon)==default,"overflowing speed was accepted");
    Require(Step(tracker,0).StopImpact==0,"invalid sample survived reset");
});
Check("a stationary ungripping hand leaves urgent self care and its pose available",()=>
{
    var s=Fresh();var c=s.Selected!;s.State.Creatures.RemoveAll(o=>o.Id!=c.Id);s.State.RoomObjects.Clear();
    c.Fullness=10;c.Interaction.Hydration=90;c.X=0;c.Z=0;
    s.State.RoomObjects.Add(new(){Id="meal",Kind=RoomObjectKind.Meal,X=2,Z=0,Portions=5});
    s.BeginInteractionGesture(c.Id,new(InteractionAction.Touch));
    for(int n=0;n<80;n++)
    {
        s.ContinueInteractionGesture(new(InteractionAction.Touch,Force:.12,Duration:.05),true);
        s.AdvanceInteractionPresentation(.05);s.Advance(.05);
    }
    Require(c.X>.5&&c.Fullness>10&&!s.InteractionGestureOwnsPose,"stationary touch trapped hungry oil or kept the stroke pose");
});
Check("idle rubbing tools release autonomous needs after a brief nuzzle invitation",()=>
{
    foreach(var action in new[]{InteractionAction.Stroke,InteractionAction.Nuzzle,InteractionAction.Clean,InteractionAction.Dry,InteractionAction.CleanAccessory})
    {
        var s=Fresh();var c=s.Selected!;s.State.Creatures.RemoveAll(o=>o.Id!=c.Id);s.State.RoomObjects.Clear();
        c.Fullness=35;c.Energy=90;c.Fear=0;c.Interaction.Hydration=90;c.X=0;c.Z=0;
        c.Interaction.NextAutonomyAt=10000;
        var part=action==InteractionAction.Nuzzle?BodyPart.Cheek:action==InteractionAction.CleanAccessory?BodyPart.Accessory:BodyPart.Head;
        if(action==InteractionAction.CleanAccessory)c.Interaction.Accessory.Place=AccessoryPlace.Hand;
        s.State.RoomObjects.Add(new(){Id="meal",Kind=RoomObjectKind.Meal,X=2,Z=0,Portions=5});
        var input=new InteractionInput(action,part,Force:.2,Duration:.05,Motion:0);
        Require(s.BeginInteractionGesture(c.Id,input).Success,"could not start stationary tool fixture");
        bool sawNuzzleReply=false;
        for(int n=0;n<160;n++)
        {
            var reply=s.ContinueInteractionGesture(input,true);
            if(action==InteractionAction.Nuzzle&&!sawNuzzleReply&&reply.Pose=="nuzzle-ready")
            {
                sawNuzzleReply=true;
                Require(s.InteractionGestureOwnsPose,"first nuzzle response released movement before its body pose");
            }
            s.AdvanceInteractionPresentation(.05);s.Advance(.05);
            if(!s.InteractionGestureOwnsPose)Require(InteractionPresentation.Pose(c) is not ("stroke" or "nuzzle" or "nuzzle-ready" or "wipe"),
                $"idle {action} left an old body pose over autonomous movement");
        }
        Require(action!=InteractionAction.Nuzzle||sawNuzzleReply,"stationary invitation never received its reply");
        Require(c.X>.5&&c.Fullness>35&&!s.InteractionGestureOwnsPose,$"idle {action} trapped hungry oil");
    }
    foreach(var action in new[]{InteractionAction.Hold,InteractionAction.Press,InteractionAction.Lift})
    {
        var s=Fresh();var c=s.Selected!;
        Require(s.BeginInteractionGesture(c.Id,new(action,Force:.2)).Success&&s.InteractionGestureOwnsPose,
            $"intentional stationary {action} lost support/control");
    }
});
Check("ending rubbing pose does not erase a newer independent reaction",()=>
{
    var s=Fresh();var c=s.Selected!;c.Fear=0;c.Fullness=90;c.Interaction.Hydration=90;
    var moving=new InteractionInput(InteractionAction.Stroke,BodyPart.Head,Force:.2,Duration:.05,Motion:.01);
    s.BeginInteractionGesture(c.Id,moving);s.ContinueInteractionGesture(moving,true);
    Require(InteractionPresentation.Pose(c)=="stroke","fixture never produced the rubbing pose");
    s.ContinueInteractionGesture(moving with {Motion=0},true);
    Require(InteractionPresentation.Pose(c)!="stroke","idle tool retained the previous rubbing pose");
    s.ExecuteInteraction(c.Id,new(InteractionAction.Poke,BodyPart.Cheek,Force:.2));
    s.ContinueInteractionGesture(moving with {Motion=0},true);
    Require(InteractionPresentation.Pose(c)=="poke","idle cleanup erased a newer independent reaction");
});
Check("refusal gives pose ownership back after its actual response settles",()=>
{
    var s=Fresh();var c=s.Selected!;s.BeginInteractionGesture(c.Id,new(InteractionAction.Touch));
    s.ContinueInteractionGesture(new(InteractionAction.Touch,StopImpact:.6),true);
    Require(!s.InteractionGestureOwnsPose,"refused touch kept exclusive visual control");
    for(int n=0;n<50;n++)s.AdvanceInteractionPresentation(.1);
    Require(InteractionPresentation.Pose(c)!="slap","finished strike kept replaying while button stayed held");
});
var report=new{at=DateTimeOffset.UtcNow,scope="Pure .NET production contact math and simulation; timestamped fixtures are synthetic, not captured user input; no game or native input",success=failures==0,failures,results,replays};
if(args.Length>0){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);File.WriteAllText(args[0],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));}
Console.WriteLine($"Input intent: {results.Count-failures}/{results.Count}");return failures==0?0:1;
