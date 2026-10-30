using System.Text.Json;

if(args.Contains("--intake-combinations"))return IntakeCombinationAudit.Run(args[0],args.Contains("--motion"));
if(args.Contains("--intake-perf"))
{
    var timings=new List<object>();float checksum=0;const int count=200;
    foreach(string environment in new[]{"ground","cushion","cushion-wrap"})foreach(string species in new[]{"reimu","marisa"})
    {
        var state=Yukkuri.Simulation.WorldSimulation.CreateInteractionPlaytest().State;var c=state.Creatures[0];c.X=c.Z=0;c.Body.SpeciesId=species;
        state.RoomObjects.Clear();c.Interaction.Restraint=environment=="cushion-wrap"?Yukkuri.Simulation.RestraintKind.Wrap:Yukkuri.Simulation.RestraintKind.None;
        if(environment.Contains("cushion"))state.RoomObjects.Add(new(){Kind=Yukkuri.Simulation.RoomObjectKind.Cushion});
        float height=(float)Yukkuri.Simulation.RoomSupplySupport.Height(state,c.RegionId,0,.55);
        var support=new ActorNaturalSupport(c,state.RoomObjects,1);
        void Sample(int n)
        {
            float age=.45f+n%20*.01f;var pose=ActorNaturalPose.Sample("eat",age,groundIntake:true,
                remaining:Yukkuri.Simulation.InteractionLife.IntakeDuration(Yukkuri.Simulation.IntakeKind.Meal)-age,
                sourceKind:Yukkuri.Simulation.RoomObjectKind.Meal,sourceDistance:.55,sourceHeight:height,support:support)!.Value;
            checksum+=pose.Offset.Y+pose.RotationDegrees.X;
        }
        for(int n=0;n<60;n++)Sample(n);
        long allocated=GC.GetAllocatedBytesForCurrentThread();var watch=System.Diagnostics.Stopwatch.StartNew();
        for(int n=0;n<count;n++)Sample(n);
        watch.Stop();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
        timings.Add(new{environment,species,samples=count,milliseconds=watch.Elapsed.TotalMilliseconds,meanMilliseconds=watch.Elapsed.TotalMilliseconds/count,allocatedBytes=allocated});
    }
    var measurement=new{at=DateTimeOffset.UtcNow,scope="Offline shared intake pose calculation only; warm CPU batch timing, not Godot frame rate or native performance acceptance.",runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,checksum,timings};
    File.WriteAllText(args[0],JsonSerializer.Serialize(measurement,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(timings));return 0;
}
var checks=new List<object>();
bool all=true;
void Check(string name,Func<bool> run){bool ok=run();all&=ok;checks.Add(new{name,passed=ok});Console.WriteLine((ok?"PASS ":"FAIL ")+name);}
Check("old flat eye cannot preserve face clearance across its whole shape",()=>
{
    float centerGap=.541f-ActorFaceSurface.Height(.23f,.04f);
    float edgeGap=.541f-ActorFaceSurface.Height(.31f,.04f);
    return edgeGap-centerGap>.02f;
});
Check("new eye surface maintains clearance through blinks",()=>
{
    foreach(float open in new[]{.08f,.1f,.12f,.2f,.65f,1f})foreach(float side in new[]{-1f,1f})
        for(int ring=0;ring<=5;ring++)for(int n=0;n<48;n++)
        {
            var point=ActorFaceSurface.Point(side*.23f,.04f,.16f,.24f*open,ring/5f,MathF.Tau*n/48,.009f);
            float gap=point.Z-ActorFaceSurface.Height(point.X,point.Y);
            if(MathF.Abs(gap-.009f)>.00001f)return false;
            if(!float.IsFinite(point.Z)||MathF.Abs(ActorFaceSurface.Normal(point.X,point.Y).Length()-1)>.00001f)return false;
        }
    return true;
});
Check("mirrored eyes have matched depth",()=>MathF.Abs(ActorFaceSurface.Height(-.23f,.04f)-ActorFaceSurface.Height(.23f,.04f))<.00001f);
Check("brow relief preserves the original outline and stays between skin clearance and old front",()=>
{
    bool recessed=false;
    foreach(float side in new[]{-1f,1f})foreach(float angle in new[]{-30f,-24f,-17f,0f,17f,24f,30f})
    foreach(float height in new[]{0f,.005f,ActorBrowSurface.FrightHeight})
        for(int row=0;row<=ActorBrowSurface.Segments;row++)for(int col=0;col<=ActorBrowSurface.Across;col++)
        {
            float u=row/(float)ActorBrowSurface.Segments,v=col/(float)ActorBrowSurface.Across;
            float radians=angle*MathF.PI/180,sine=MathF.Sin(MathF.PI*u);
            var along=new System.Numerics.Vector2(MathF.Cos(radians),MathF.Sin(radians));
            var across=new System.Numerics.Vector2(-along.Y,along.X);
            var original=new System.Numerics.Vector2(side*.235f,ActorBrowSurface.CenterY+height)+along*((u-.5f)*.18f)+
                across*((v*2-1)*(.009f*MathF.Sqrt(MathF.Max(0,sine))))+System.Numerics.Vector2.UnitY*(.004f*sine);
            var p=ActorBrowSurface.Point(side,angle,height,u,v);float depth=ActorBrowSurface.Recess(u,v);
            float skin=ActorFaceSurface.Height(p.X,p.Y),oldFront=ActorFaceSurface.Height(original.X,original.Y,.011f);
            if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z)||!float.IsFinite(depth))return false;
            if(MathF.Abs(p.X-original.X)>1e-7f||MathF.Abs(p.Y-original.Y)>1e-7f)return false;
            if(depth<0||depth>.0015f+1e-7f||p.Z>oldFront+1e-7f||p.Z-skin<.0095f-1e-7f)return false;
            if(MathF.Abs(oldFront-p.Z-depth)>1e-7f)return false;
            recessed|=depth>.0014f;
        }
    return recessed;
});
Check("brow center ridge and both closed tips retain the old surface with symmetric side relief",()=>
{
    foreach(float side in new[]{-1f,1f})foreach(float angle in new[]{-24f,0f,24f})
        for(int n=0;n<=96;n++)
        {
            float u=n/96f,v=n/96f;
            var center=ActorBrowSurface.Point(side,angle,.005f,u,.5f);
            if(ActorBrowSurface.Recess(u,.5f)!=0||MathF.Abs(center.Z-ActorFaceSurface.Height(center.X,center.Y,.011f))>1e-7f)return false;
            if(MathF.Abs(ActorBrowSurface.Recess(u,v)-ActorBrowSurface.Recess(u,1-v))>1e-7f)return false;
            foreach(float tip in new[]{0f,1f})
            {
                var p=ActorBrowSurface.Point(side,angle,.005f,tip,v);
                var closed=ActorBrowSurface.Point(side,angle,.005f,tip,.5f);
                if(ActorBrowSurface.Recess(tip,v)!=0||System.Numerics.Vector3.Distance(p,closed)>1e-6f||
                    MathF.Abs(p.Z-ActorFaceSurface.Height(p.X,p.Y,.011f))>1e-7f)return false;
            }
        }
    return ActorBrowSurface.Segments==24&&ActorBrowSurface.Across==4;
});
Check("actual brow mesh normals remain finite unit and forward through the full expression angle range",()=>
{
    foreach(float side in new[]{-1f,1f})for(int angle=-30;angle<=30;angle+=2)
    foreach(float height in new[]{0f,.005f,ActorBrowSurface.FrightHeight})
        for(int row=0;row<=ActorBrowSurface.Segments;row++)for(int col=0;col<=ActorBrowSurface.Across;col++)
        {
            float u=row/(float)ActorBrowSurface.Segments,v=col/(float)ActorBrowSurface.Across;
            var point=ActorBrowSurface.Point(side,angle,height,u,v);
            var normal=ActorBrowSurface.Normal(side,angle,height,u,v);
            var support=ActorFaceSurface.Normal(point.X,point.Y);
            if(!float.IsFinite(normal.X)||!float.IsFinite(normal.Y)||!float.IsFinite(normal.Z)||
                MathF.Abs(normal.Length()-1)>1e-5f||normal.Z<=0||System.Numerics.Vector3.Dot(normal,support)<=0)return false;
            if((row==0||row==ActorBrowSurface.Segments)&&System.Numerics.Vector3.Distance(normal,support)>1e-6f)return false;
            if(row>0&&row<ActorBrowSurface.Segments)
            {
                var tangent=ActorBrowSurface.Point(side,angle,height,u,MathF.Min(1,v+.0005f))-
                    ActorBrowSurface.Point(side,angle,height,u,MathF.Max(0,v-.0005f));
                if(MathF.Abs(System.Numerics.Vector3.Dot(normal,System.Numerics.Vector3.Normalize(tangent)))>1e-4f)return false;
            }
        }
    return true;
});
Check("old underside spot intersects skin instead of remaining outside",()=>
{
    float OldSpotY(float x,float z)=>-.45f-.009f*MathF.Sqrt(MathF.Max(0,1-x*x/(.15f*.15f)-z*z/(.11f*.11f)));
    return OldSpotY(0,.04f)>ActorFaceSurface.Bottom(0,.04f)
        &&OldSpotY(.12f,0)<ActorFaceSurface.Bottom(.12f,0);
});
Check("underside markings follow the same continuous skin with positive clearance",()=>
{
    for(int ring=0;ring<=8;ring++)for(int n=0;n<64;n++)
    {
        float x=MathF.Cos(MathF.Tau*n/64)*.36f*ring/8;
        float z=ActorFaceSurface.CenterZ+MathF.Sin(MathF.Tau*n/64)*.325f*ring/8;
        float y=ActorFaceSurface.Bottom(x,z,.01f);
        if(!float.IsFinite(y)||MathF.Abs(ActorFaceSurface.Bottom(x,z)-y-.01f)>.00001f)return false;
    }
    return true;
});
Check("bow inset trim follows the actual folded cloth without sinking into it",()=>
{
    foreach(float side in new[]{-1f,1f})foreach(float edge in new[]{.07f,.93f})for(int i=0;i<=64;i++)
    {
        var cloth=ActorAccessorySurface.Bow(side,i/64f,edge);
        var trim=ActorAccessorySurface.BowTrim(side,i/64f,edge);
        if(MathF.Abs(trim.X-cloth.X)>1e-6f||MathF.Abs(trim.Y-cloth.Y)>1e-6f||MathF.Abs(trim.Z-cloth.Z-.005f)>1e-6f)return false;
    }
    return true;
});
Check("hat crown closes to a single point rather than an open ring",()=>
{
    var tip=ActorAccessorySurface.HatCone(1,0);
    for(int n=1;n<=96;n++)if(System.Numerics.Vector3.Distance(tip,ActorAccessorySurface.HatCone(1,n*MathF.Tau/96))>1e-6f)return false;
    return true;
});
Check("hat brim closes its seam and keeps the rolled edge inside its thickness",()=>
{
    for(int i=0;i<=96;i++)
    {
        float angle=i*MathF.Tau/96;
        var top=ActorAccessorySurface.HatBrim(1,angle);
        var bottom=ActorAccessorySurface.HatBrimUnderside(1,angle);
        var edge=ActorAccessorySurface.HatBrimRim(angle);
        if(!float.IsFinite(edge.X)||!(top.Y>edge.Y&&edge.Y>bottom.Y))return false;
        if(MathF.Abs((top.Y-edge.Y)-(edge.Y-bottom.Y))>1e-6f)return false;
        if(MathF.Abs(top.X-bottom.X)>1e-6f||MathF.Abs(top.Z-bottom.Z)>1e-6f)return false;
        if(MathF.Abs(top.X-edge.X)>1e-6f||MathF.Abs(top.Z-edge.Z)>1e-6f)return false;
    }
    return System.Numerics.Vector3.Distance(ActorAccessorySurface.HatBrim(1,0),ActorAccessorySurface.HatBrim(1,MathF.Tau))<1e-6f;
});
Check("hat brim folds remain finite inside the original shell without changing its lining or projection",()=>
{
    bool recessed=false;
    for(int row=0;row<=120;row++)for(int n=0;n<=384;n++)
    {
        float u=row/120f,angle=n*MathF.Tau/384;
        float radius=.035f+.65f*u;
        var original=new System.Numerics.Vector3(MathF.Cos(angle)*radius,
            u*u*(.018f+MathF.Sin(angle*2+.4f)*.025f),MathF.Sin(angle)*radius);
        var basis=ActorAccessorySurface.HatBrimBase(u,angle);
        var top=ActorAccessorySurface.HatBrim(u,angle);
        var bottom=ActorAccessorySurface.HatBrimUnderside(u,angle);
        float depth=ActorAccessorySurface.HatBrimRecess(u,angle);
        foreach(var p in new[]{basis,top,bottom})
            if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z))return false;
        if(!float.IsFinite(depth)||depth<0||depth>ActorAccessorySurface.BrimPleatDepth+1e-7f)return false;
        if(System.Numerics.Vector3.Distance(basis,original)>1e-6f||
            System.Numerics.Vector3.Distance(bottom,original-System.Numerics.Vector3.UnitY*.016f)>1e-6f)return false;
        if(top.X!=basis.X||top.Z!=basis.Z||MathF.Abs(basis.Y-top.Y-depth)>1e-7f)return false;
        if(top.Y>basis.Y+1e-7f||top.Y-bottom.Y<.0135f-1e-7f||top.Y-bottom.Y>.016f+1e-7f)return false;
        if((u<=.66f||u>=1)&&depth!=0)return false;
        recessed|=depth>.0024f;
    }
    return recessed;
});
Check("eight brim folds join smoothly at radial boundaries and preserve the periodic seam",()=>
{
    const float epsilon=.0001f;
    for(int n=0;n<=192;n++)
    {
        float angle=n*MathF.Tau/192;
        foreach(float edge in new[]{0,.66f,1f})
            if(ActorAccessorySurface.HatBrimRecess(edge,angle)!=0)return false;
        if(ActorAccessorySurface.HatBrimRecess(.66f+epsilon,angle)/epsilon>.001f||
            ActorAccessorySurface.HatBrimRecess(1-epsilon,angle)/epsilon>.001f)return false;
        for(int row=0;row<=48;row++)
        {
            float u=row/48f;
            float depth=ActorAccessorySurface.HatBrimRecess(u,angle);
            if(MathF.Abs(depth-ActorAccessorySurface.HatBrimRecess(u,angle+MathF.PI/4))>1e-7f)return false;
            if(System.Numerics.Vector3.Distance(ActorAccessorySurface.HatBrim(u,angle),
                ActorAccessorySurface.HatBrim(u,angle+MathF.Tau))>2e-6f)return false;
        }
    }
    // Distinct troughs and untouched cloth between them, rather than a uniformly lowered brim.
    return ActorAccessorySurface.HatBrimRecess(.83f,MathF.PI/8)>.0024f&&
        ActorAccessorySurface.HatBrimRecess(.83f,0)==0;
});
Check("hat support samples the exact authored folded brim and unchanged lining grid",()=>
{
    var points=ActorAccessorySupport.Points(true);
    int index=25*64; // Existing crown samples precede the two brim surfaces.
    int count=(ActorAccessorySurface.BrimRows+1)*(ActorAccessorySurface.BrimSides+1)*2;
    if(points.Length<index+count)return false;
    bool foldedSample=false;
    for(int row=0;row<=ActorAccessorySurface.BrimRows;row++)for(int n=0;n<=ActorAccessorySurface.BrimSides;n++)
    {
        float u=row/(float)ActorAccessorySurface.BrimRows,angle=n/(float)ActorAccessorySurface.BrimSides*MathF.Tau;
        if(System.Numerics.Vector3.Distance(points[index++],ActorAccessorySurface.HatBrim(u,angle))>1e-7f||
            System.Numerics.Vector3.Distance(points[index++],ActorAccessorySurface.HatBrimUnderside(u,angle))>1e-7f)return false;
        foldedSample|=ActorAccessorySurface.HatBrimRecess(u,angle)>.002f;
    }
    return foldedSample;
});
Check("press and squeeze deform opposite axes without inversion at any force",()=>
{
    foreach(float size in new[]{.73f,1f,1.03f})for(int n=1;n<=100;n++)
    {
        float force=n/100f;var p=ActorTouchShape.Press(force)*size;var s=ActorTouchShape.Squeeze(force)*size;
        if(!(p.Y<size&&p.X>size&&s.X<size&&s.Y>size))return false;
        foreach(var v in new[]{p,s})
            if(v.X*v.Y*v.Z/(size*size*size)<.9f||v.X*v.Y*v.Z/(size*size*size)>1.1f)return false;
    }
    return ActorTouchShape.Press(0)==System.Numerics.Vector3.One&&ActorTouchShape.Squeeze(0)==System.Numerics.Vector3.One;
});
Check("intake stages distinguish offer chewing sipping swallowing and finished",()=>
{
    var meal=Yukkuri.Simulation.IntakeKind.Meal;var water=Yukkuri.Simulation.IntakeKind.Water;
    if(ActorIntakePresentation.Sample(meal,0,false).Active||ActorIntakePresentation.Sample(meal,0,true).MouthFrame!=0)return false;
    if(ActorIntakePresentation.Sample(water,1,false).MouthFrame!=3||ActorIntakePresentation.Sample(water,.1,false).MouthFrame!=4)return false;
    var frames=new HashSet<int>();
    for(int n=3;n<160;n++)
    {
        var sample=ActorIntakePresentation.Sample(meal,n/100.0,false);
        frames.Add(sample.MouthFrame);
        if(!sample.Active||sample.Width<.9f||sample.Width>1.1f||sample.Height<.9f||sample.Height>1.1f)return false;
    }
    return frames.SetEquals(new[]{1,2,4});
});
Check("repair progressively closes tears and lays stitches instead of showing every stitch at once",()=>
{
    var item=new Yukkuri.Simulation.AccessoryState{Integrity=30,RepairProgress=0};
    float previousTear=1,previousStitches=0;
    for(int n=0;n<=70;n++)
    {
        item.Integrity=30+n;item.RepairProgress=n/.75;
        float tear=ActorAccessoryWear.TearOpacity(item,0),stitches=0;
        for(int i=0;i<8;i++)stitches+=ActorAccessoryWear.StitchOpacity(item,i);
        if(tear>previousTear+1e-5||stitches<previousStitches-1e-5||stitches>8)return false;
        if(n==10&&stitches>=2)return false;
        previousTear=tear;previousStitches=stitches;
    }
    if(previousTear!=0||previousStitches!=8)return false;
    item.Integrity=70;item.RepairProgress=0;
    return ActorAccessoryWear.TearOpacity(item,0)>0&&Enumerable.Range(0,8).All(i=>ActorAccessoryWear.StitchOpacity(item,i)==0);
});
Check("every cleanable surface has marks whose entire footprint matches the real anatomical hit region",()=>
{
    var expected=new[]{Yukkuri.Simulation.BodyPart.Head,Yukkuri.Simulation.BodyPart.Cheek,Yukkuri.Simulation.BodyPart.Mouth,Yukkuri.Simulation.BodyPart.Side,Yukkuri.Simulation.BodyPart.Bottom};
    foreach(var set in new[]{ActorSurfaceMarks.Dirt,ActorSurfaceMarks.Wet,ActorSurfaceMarks.Dressing})
    {
        if(!set.Select(m=>m.Part).ToHashSet().SetEquals(expected))return false;
        foreach(var mark in set)for(int ring=0;ring<=5;ring++)for(int n=0;n<32;n++)
        {
            var point=ActorSurfaceMarks.Point(mark,ring/5f,n*MathF.Tau/32,0);
            if(!mark.OnHair&&ActorSurfaceMarks.ClassifySkin(point)!=mark.Part)
            {Console.WriteLine($"Misplaced {mark.Part} patch at {point}: {ActorSurfaceMarks.ClassifySkin(point)}");return false;}
            var raised=ActorSurfaceMarks.Point(mark,ring/5f,n*MathF.Tau/32,.009f);
            if(MathF.Abs(System.Numerics.Vector3.Distance(point,raised)-.009f)>.00001f)return false;
        }
    }
    return true;
});
Check("injury detail remains inside the original patches with bounded colour and unchanged edge fading",()=>
{
    foreach(bool scar in new[]{false,true})for(int row=0;row<=ActorInjurySurface.Rows;row++)for(int side=0;side<=ActorInjurySurface.Sides;side++)
    {
        float r=(float)row/ActorInjurySurface.Rows,a=side*MathF.Tau/ActorInjurySurface.Sides;
        var q=ActorInjurySurface.Surface(r,a,scar);var tint=ActorInjurySurface.Tint(r,a,scar);
        if(!float.IsFinite(q.LengthSquared())||q.LengthSquared()>r*r+.000001f||
            !float.IsFinite(tint.LengthSquared())||tint.X is <0 or >1||tint.Y is <0 or >1||tint.Z is <0 or >1)return false;
        if(scar&&(MathF.Abs(q.X-r*MathF.Cos(a))>.000001f||MathF.Abs(q.Y)>MathF.Abs(r*MathF.Sin(a))+.000001f))return false;
        if(!scar&&q.Length()<r*.88f-.000001f)return false;
        if(row==0&&(q!=System.Numerics.Vector2.Zero||tint!=ActorInjurySurface.Tint(0,0,scar)))return false;
        if(side==ActorInjurySurface.Sides&&
            (System.Numerics.Vector2.Distance(q,ActorInjurySurface.Surface(r,0,scar))>.00001f||
             System.Numerics.Vector3.Distance(tint,ActorInjurySurface.Tint(r,0,scar))>.00001f))return false;
        float alpha=ActorInjurySurface.EdgeOpacity(r);
        if(!float.IsFinite(alpha)||alpha is <0 or >1||MathF.Abs(alpha-MathF.Sqrt(MathF.Max(0,1-r*r)))>.000001f)return false;
        foreach(var source in ActorSurfaceMarks.Dressing)
        {
            var site=source with {Size=source.Size*.82f};
            var mark=scar?site with {Size=site.Size*new System.Numerics.Vector2(.7f,.3f)}:site;
            var point=ActorInjurySurface.Point(site,r,a,scar);
            var skin=ActorSurfaceMarks.Project(mark,q,0);
            if(!float.IsFinite(point.LengthSquared())||
                MathF.Abs(System.Numerics.Vector3.Distance(point,skin)-(scar?.012f:.011f))>.00001f)return false;
        }
    }
    return ActorInjurySurface.EdgeOpacity(0)==1&&ActorInjurySurface.EdgeOpacity(1)==0;
});
Check("localized water and dirt stay visible until that region is actually dried or cleaned",()=>
{
    var life=new Yukkuri.Simulation.InteractionLife{Dirt=0,Wetness=0};
    life.SurfaceWetness[Yukkuri.Simulation.BodyPart.Side]=20;
    life.SurfaceDirt[Yukkuri.Simulation.BodyPart.Mouth]=8;
    life.WipeSurface(Yukkuri.Simulation.BodyPart.Cheek,100,true);
    if(life.Wetness>=5||ActorSurfaceMarks.Opacity(life,Yukkuri.Simulation.BodyPart.Side,true)<.8f)return false;
    if(ActorSurfaceMarks.Opacity(life,Yukkuri.Simulation.BodyPart.Cheek,true)!=0||ActorSurfaceMarks.Opacity(life,Yukkuri.Simulation.BodyPart.Mouth,false)<=0)return false;
    float previous=1;
    for(int n=0;n<20;n++)
    {
        life.WipeSurface(Yukkuri.Simulation.BodyPart.Side,1,true);
        float opacity=ActorSurfaceMarks.Opacity(life,Yukkuri.Simulation.BodyPart.Side,true);
        if(opacity>previous)return false;previous=opacity;
    }
    return previous==0&&ActorSurfaceMarks.Opacity(life,Yukkuri.Simulation.BodyPart.Mouth,false)>0;
});
Check("landing absorbs impact and settles without inversion or runaway motion",()=>
{
    var initial=ActorReactionMotion.Sample("land",0)!.Value;
    var last=ActorReactionMotion.Sample("land",2.2f)!.Value;
    if(initial.Scale.Y>=1||initial.Scale.X<=1||System.Numerics.Vector3.Distance(last.Scale,System.Numerics.Vector3.One)>.001f)return false;
    foreach(string pose in new[]{"greet","comfort","poke","land","settle","ball-bump","help","thirsty","careful-step","nursing","confused","inspect","blocked","held","box","frame","hanging","accessory-watch","rest-invite"})
    for(int n=0;n<=440;n++)
    {
        var m=ActorReactionMotion.Sample(pose,n/100f)!.Value;
        if(m.Scale.X<.8f||m.Scale.Y<.8f||m.Scale.Z<.8f||m.Scale.X*m.Scale.Y*m.Scale.Z<.9f||m.Scale.X*m.Scale.Y*m.Scale.Z>1.1f)return false;
        if(m.Offset.Length()>.05f||m.RotationDegrees.Length()>16)return false;
    }
    return true;
});
Check("guarded impacts stay bounded without travel and resolve continuously into the protective posture",()=>
{
    foreach(string pose in new[]{"slap","flick","poke"})foreach(bool restrained in new[]{false,true})
    {
        for(int n=0;n<220;n++)
        {
            var m=ActorReactionMotion.SampleGuardedImpact(pose,n/1000f,new(1,2),restrained)!.Value;
            if(!float.IsFinite(m.Scale.LengthSquared())||!float.IsFinite(m.RotationDegrees.LengthSquared())||m.Offset!=System.Numerics.Vector3.Zero||
                m.Scale.X<.98f||m.Scale.X>1.02f||m.Scale.Y<.95f||m.Scale.Y>1.05f||m.Scale.Z<.98f||m.Scale.Z>1.02f||
                m.RotationDegrees.Length()>5.00001f||restrained&&m.RotationDegrees!=System.Numerics.Vector3.Zero)return false;
        }
        var end=ActorReactionMotion.SampleGuardedImpact(pose,ActorReactionMotion.GuardedImpactDuration-.00001f,new(1,2),restrained)!.Value;
        if(System.Numerics.Vector3.Distance(end.Scale,System.Numerics.Vector3.One)>.000001f||end.RotationDegrees.Length()>.000001f||
            ActorReactionMotion.SampleGuardedImpact(pose,ActorReactionMotion.GuardedImpactDuration,new(1,2),restrained)!=null)return false;
    }
    return true;
});
Check("guarded impact direction changes only recoil and unrelated or invalid events produce no overlay",()=>
{
    foreach(string pose in new[]{"slap","flick","poke"})
    {
        var right=ActorReactionMotion.SampleGuardedImpact(pose,.03f,new(1,0))!.Value;
        var left=ActorReactionMotion.SampleGuardedImpact(pose,.03f,new(-1,0))!.Value;
        if(right.Scale!=left.Scale||right.Offset!=left.Offset||right.RotationDegrees!=-left.RotationDegrees)return false;
        foreach(float age in new[]{-.1f,float.NaN,float.PositiveInfinity,1f})
            if(ActorReactionMotion.SampleGuardedImpact(pose,age,new(1,0))!=null)return false;
        foreach(var direction in new[]{System.Numerics.Vector2.Zero,new System.Numerics.Vector2(float.NaN,0),new System.Numerics.Vector2(0,float.PositiveInfinity)})
        {
            var m=ActorReactionMotion.SampleGuardedImpact(pose,0,direction)!.Value;
            if(!float.IsFinite(m.Scale.LengthSquared())||!float.IsFinite(m.RotationDegrees.LengthSquared()))return false;
        }
    }
    foreach(string pose in new[]{"guard","care","burn-tense","dead","swallow",""})
        if(ActorReactionMotion.SampleGuardedImpact(pose,0,new(1,0))!=null)return false;
    return true;
});
Check("guarded landings compress and release continuously without a rebound or invented displacement",()=>
{
    foreach(bool restrained in new[]{false,true})
    {
        float previousY=0,previousX=float.PositiveInfinity;
        for(int n=0;n<220;n++)
        {
            var frame=ActorReactionMotion.SampleGuardedImpact("land",n/1000f,new(1,2),restrained)!.Value;
            var scale=frame.Scale;
            if(!float.IsFinite(scale.LengthSquared())||scale.Y<(restrained?.969999f:.939999f)||scale.Y>1||
                scale.X<1||scale.X>1.021001f||scale.Z!=scale.X||scale.Y<previousY||scale.X>previousX||
                frame.Offset!=System.Numerics.Vector3.Zero||frame.RotationDegrees!=System.Numerics.Vector3.Zero)return false;
            previousY=scale.Y;previousX=scale.X;
        }
        var end=ActorReactionMotion.SampleGuardedImpact("land",ActorReactionMotion.GuardedImpactDuration-.00001f,new(1,2),restrained)!.Value;
        if(System.Numerics.Vector3.Distance(end.Scale,System.Numerics.Vector3.One)>1e-6f||
            ActorReactionMotion.SampleGuardedImpact("land",ActorReactionMotion.GuardedImpactDuration,new(1,2),restrained)!=null)return false;
    }
    return ActorReactionMotion.SampleGuardedImpact("land",0,new(1,2))!.Value.Scale.Y<
        ActorReactionMotion.SampleGuardedImpact("land",0,new(1,2),true)!.Value.Scale.Y;
});
Check("guarded landing ignores travel direction and rejects stale or invalid event ages",()=>
{
    foreach(bool restrained in new[]{false,true})
    {
        var sample=ActorReactionMotion.SampleGuardedImpact("land",.07f,System.Numerics.Vector2.Zero,restrained);
        foreach(var direction in new[]{new System.Numerics.Vector2(1,2),new System.Numerics.Vector2(-1,-2),
            new System.Numerics.Vector2(float.NaN,0),new System.Numerics.Vector2(0,float.PositiveInfinity),
            new System.Numerics.Vector2(float.MaxValue,float.MaxValue)})
            if(ActorReactionMotion.SampleGuardedImpact("land",.07f,direction,restrained)!=sample)return false;
        foreach(float age in new[]{-.001f,float.NaN,float.NegativeInfinity,float.PositiveInfinity,1f})
            if(ActorReactionMotion.SampleGuardedImpact("land",age,System.Numerics.Vector2.Zero,restrained)!=null)return false;
    }
    return true;
});
Check("heat tension is bounded and fades to neutral without inventing translation or a timer",()=>
{
    foreach(double heat in new[]{-1d,0,double.NaN,double.NegativeInfinity,double.PositiveInfinity})
        if(ActorHeatPresentation.Sample(heat,false,true,new(1,0))!=null)return false;
    foreach(bool restrained in new[]{false,true})foreach(bool fleeing in new[]{false,true})
    {
        float previousY=1;
        for(int n=1;n<=200;n++)
        {
            var frame=ActorHeatPresentation.Sample(n,restrained,fleeing,new(1,2))!.Value;
            var scale=frame.Scale;
            if(!float.IsFinite(scale.LengthSquared())||!float.IsFinite(frame.Pitch)||
                scale.X<1||scale.X>1.025001f||scale.Y<.934999f||scale.Y>1||scale.Z!=scale.X||
                frame.Pitch< -4.000001f||frame.Pitch>0||scale.Y>previousY+1e-7f)return false;
            if(frame.Heading.HasValue&&!float.IsFinite(frame.Heading.Value))return false;
            previousY=scale.Y;
        }
        if(ActorHeatPresentation.Sample(100,restrained,fleeing,new(1,2))!=
            ActorHeatPresentation.Sample(double.MaxValue,restrained,fleeing,new(1,2)))return false;
        var cooled=ActorHeatPresentation.Sample(.0001,restrained,fleeing,new(1,2))!.Value;
        if(System.Numerics.Vector3.Distance(cooled.Scale,System.Numerics.Vector3.One)>1e-6f||MathF.Abs(cooled.Pitch)>1e-5f)return false;
    }
    return true;
});
Check("blocked or invalid heat travel never creates a flee heading and residual heat is not a new escape",()=>
{
    var idle=ActorHeatPresentation.Sample(65,false,false,System.Numerics.Vector2.Zero)!.Value;
    foreach(var travel in new[]{System.Numerics.Vector2.Zero,new System.Numerics.Vector2(1e-6f,0),
        new System.Numerics.Vector2(float.NaN,0),new System.Numerics.Vector2(0,float.PositiveInfinity),
        new System.Numerics.Vector2(float.MaxValue,float.MaxValue)})
    {
        var blocked=ActorHeatPresentation.Sample(65,false,true,travel)!.Value;
        if(blocked!=idle||blocked.Heading!=null||blocked.Pitch!=0)return false;
    }
    foreach(var travel in new[]{new System.Numerics.Vector2(1,0),new System.Numerics.Vector2(-1,2)})
        if(ActorHeatPresentation.Sample(65,false,false,travel)!.Value!=idle)return false;
    var restrained=ActorHeatPresentation.Sample(65,true,false,System.Numerics.Vector2.Zero)!.Value;
    if(restrained.Heading!=null||restrained.Pitch!=0||restrained.Scale.Y>=idle.Scale.Y)return false;
    return ActorHeatPresentation.Sample(65,true,true,new(1,2))!.Value==restrained&&
        ActorHeatPresentation.Sample(65,true,true,new(float.NaN,float.PositiveInfinity))!.Value==restrained;
});
Check("a heat escape heading follows actual planar travel while speed and reversal cannot change tension",()=>
{
    foreach(var travel in new[]{new System.Numerics.Vector2(1,0),new System.Numerics.Vector2(0,1),
        new System.Numerics.Vector2(-1,0),new System.Numerics.Vector2(0,-1),new System.Numerics.Vector2(2,-3)})
    {
        var frame=ActorHeatPresentation.Sample(80,false,true,travel)!.Value;
        var reverse=ActorHeatPresentation.Sample(80,false,true,-travel)!.Value;
        var fast=ActorHeatPresentation.Sample(80,false,true,travel*5)!.Value;
        if(frame.Heading is not float heading||reverse.Heading is not float reverseHeading||fast.Heading is not float fastHeading||
            frame.Pitch>=0||frame.Scale!=reverse.Scale||frame.Pitch!=reverse.Pitch||frame.Scale!=fast.Scale||frame.Pitch!=fast.Pitch)return false;
        var direction=System.Numerics.Vector2.Normalize(travel);
        if(MathF.Abs(MathF.Sin(heading)-direction.X)>1e-6f||MathF.Abs(MathF.Cos(heading)-direction.Y)>1e-6f||
            MathF.Abs(MathF.Sin(reverseHeading)+direction.X)>1e-6f||MathF.Abs(MathF.Cos(reverseHeading)+direction.Y)>1e-6f||
            MathF.Abs(heading-fastHeading)>1e-6f)return false;
    }
    return true;
});
Check("repeated impacts replay their response while speech during a held motion does not restart its cycle",()=>
{
    var clock=new ActorPoseClock();clock.Advance("flick",1,.1f,false);
    for(int n=0;n<10;n++)clock.Advance("flick",1,.1f,false);
    if(clock.Age<.9f)return false;
    clock.Advance("flick",2,.1f,false);if(clock.Age!=0)return false;
    clock.Advance("roll",3,.1f,false);clock.Advance("roll",3,.1f,false);
    clock.Advance("roll",4,.1f,false);if(clock.Age<.19f)return false;
    float age=clock.Age;clock.Advance("roll",4,1,true);
    return clock.Age==age;
});
Check("fringe tips close without burying the visible end in the skin",()=>
{
    for(int strand=0;strand<5;strand++)
    {
        var shape=ActorHairSurface.Fringe(strand);
        var tip=ActorHairSurface.Point(shape,1,0);
        for(int n=0;n<64;n++)
            if(System.Numerics.Vector3.Distance(tip,ActorHairSurface.Point(shape,1,n/64f))>1e-6f)return false;
        for(int row=80;row<=100;row++)for(int n=0;n<64;n++)
        {
            var p=ActorHairSurface.Point(shape,row/100f,n/64f);
            float gap=p.Z-ActorFaceSurface.Height(p.X,p.Y);
            if(gap<.01f){Console.WriteLine($"Fringe {strand} u={row/100f}: skin clearance {gap:F5}");return false;}
        }
    }
    return true;
});
Check("hair bundle relief stays inside the old section and preserves roots tips backs and seams",()=>
{
    var shapes=Enumerable.Range(0,5).Select(ActorHairSurface.Fringe).Concat(new[]{ActorHairSurface.Side(-1),ActorHairSurface.Side(1)});
    bool hasRelief=false;
    foreach(var shape in shapes)for(int row=0;row<=100;row++)for(int n=0;n<=128;n++)
    {
        float u=row/100f,v=n/128f;var section=ActorHairSurface.Section(shape,u);
        var smooth=ActorHairSurface.SmoothPoint(section,v);var p=ActorHairSurface.Point(section,v);
        float depth=ActorHairSurface.Recess(section,v);
        if(!float.IsFinite(p.LengthSquared())||!float.IsFinite(depth)||depth<0||p.X!=smooth.X||p.Y!=smooth.Y||p.Z>smooth.Z)return false;
        if(depth>0)hasRelief=true;
        if((row==0||row==100||n==0||n>=64)&&(depth!=0||p!=smooth))return false;
        if(depth>(shape.Fringe?ActorHairSurface.FringeRecess:ActorHairSurface.SideRecess)+.000001f)return false;
        if(depth>0&&depth>section.Depth*section.Front.Z*MathF.Sin(v*MathF.Tau)*.18f+.000001f)return false;
        if(shape.Fringe)
        {
            float clearance=MathF.Max(0,smooth.Z-ActorFaceSurface.Height(smooth.X,smooth.Y,.012f));
            if(depth>clearance*.5f+.000001f)return false;
        }
        if(System.Numerics.Vector3.Distance(ActorHairSurface.Point(section,0),ActorHairSurface.Point(section,1))>.000001f)return false;
    }
    return hasRelief;
});
Check("authored hair relief has finite nondegenerate surface tangents at the actual mesh rows",()=>
{
    var shapes=Enumerable.Range(0,5).Select(ActorHairSurface.Fringe).Concat(new[]{ActorHairSurface.Side(-1),ActorHairSurface.Side(1)});
    foreach(var shape in shapes)for(int row=1;row<20;row++)for(int n=0;n<32;n++)
    {
        float u=row/20f,v=n/32f,step=.0005f;
        var down=ActorHairSurface.Point(shape,u+step,v)-ActorHairSurface.Point(shape,u-step,v);
        var across=ActorHairSurface.Point(shape,u,(v+step)%1)-ActorHairSurface.Point(shape,u,(v-step+1)%1);
        var normal=System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(across,down));
        if(!float.IsFinite(normal.LengthSquared())||MathF.Abs(normal.LengthSquared()-1)>.00001f)return false;
    }
    return true;
});
Check("close-up orbit reaches front side and back while staying above the floor at every supported height",()=>
{
    foreach(float height in new[]{.62f,.8f,1.1f,2.1f,3.62f})for(int yaw=-180;yaw<=180;yaw+=10)for(int pitch=-25;pitch<=65;pitch+=5)
    {
        var offset=InteractionCameraOrbit.Offset(yaw,pitch,height);
        if(!float.IsFinite(offset.Length())||height+offset.Y<.1499f||offset.Length()<2.4f)return false;
    }
    return InteractionCameraOrbit.Offset(0,14.5f,.62f).Z>5&&InteractionCameraOrbit.Offset(180,14.5f,.62f).Z< -5&&
        InteractionCameraOrbit.Size(3.5f,.65f)<3.5f&&InteractionCameraOrbit.Size(3.5f,1.6f)>3.5f;
});
Check("touch motion measures the contacted surface and cannot be supplied by idle animation or a gap between regions",()=>
{
    var tracker=new ContactStrokeTracker();var cheek=Yukkuri.Simulation.BodyPart.Cheek;
    tracker.Sample("a",cheek,new(100,100),new(0,0,.5f),1);
    if(tracker.Sample("a",cheek,new(100,100),new(.1f,.1f,.5f),1).Distance!=0)return false;
    var stroke=tracker.Sample("a",cheek,new(102,100),new(.12f,.1f,.5f),1);
    if(MathF.Abs(stroke.Distance-.02f)>.00001f||stroke.Direction.X<=0)return false;
    if(tracker.Sample("a",Yukkuri.Simulation.BodyPart.Head,new(105,100),new(0,.4f,.3f),1).Distance!=0)return false;
    tracker.Reset();if(tracker.Sample("a",cheek,new(107,100),new(.12f,0,.5f),1).Distance!=0)return false;
    return tracker.Sample("b",cheek,new(109,100),new(-.3f,0,.5f),1).Distance==0;
});
Check("reprojecting a moving pointer separates rubbing from the body's own travel",()=>
{
    var tracker=new ContactStrokeTracker();var cheek=Yukkuri.Simulation.BodyPart.Cheek;
    tracker.Sample("oil",cheek,new(100,100),new(0,0,.5f),1);
    // The same old pointer now lands at x=.3 because the body/camera moved;
    // the user's two-pixel motion contributes only the further .02.
    var stroke=tracker.Sample("oil",cheek,new(102,100),new(.32f,0,.5f),1,
        previousLocalPoint:new(.3f,0,.5f));
    if(MathF.Abs(stroke.Distance-.02f)>.00001f)return false;
    if(tracker.Sample("oil",cheek,new(float.NaN,100),new(.4f,0,.5f),1).Distance!=0)return false;
    // Invalid input must reseed the next frame, not span the missing sample.
    return tracker.Sample("oil",cheek,new(104,100),new(.4f,0,.5f),1).Distance==0;
});
Check("a camera looking above the horizon cannot place objects behind its ray",()=>
{
    var origin=new System.Numerics.Vector3(0,1,5);
    if(InteractionCameraOrbit.TryPlaneHit(origin,new(0,.1f,-1),0,out _))return false;
    if(InteractionCameraOrbit.TryPlaneHit(origin,new(1,0,0),0,out _))return false;
    if(!InteractionCameraOrbit.TryPlaneHit(origin,new(0,-.2f,-1),0,out var hit)||MathF.Abs(hit.Y)>.00001f)return false;
    var drag=InteractionCameraOrbit.ViewMotion(System.Numerics.Vector3.UnitX,System.Numerics.Vector3.UnitY,new(20,0),3.5f,700);
    return MathF.Abs(drag.X-.1f)<.00001f&&drag.Y==0&&drag.Z==0;
});
Check("hanging support follows the outer skin and hair rather than ending behind an unsupported body",()=>
{
    float DistanceToEllipsoid(System.Numerics.Vector3 point,System.Numerics.Vector3 center,System.Numerics.Vector3 radii)
    {
        var p=point-center;var r2=radii*radii;
        float Metric(float lambda){var q=radii*p/(r2+new System.Numerics.Vector3(lambda));return q.LengthSquared();}
        if(Metric(0)<=1)return 0;
        float lo=0,hi=1;while(Metric(hi)>1)hi*=2;
        for(int n=0;n<40;n++){float mid=(lo+hi)*.5f;if(Metric(mid)>1)lo=mid;else hi=mid;}
        return System.Numerics.Vector3.Distance(p,r2*p/(r2+new System.Numerics.Vector3(hi)));
    }
    bool Clear(System.Numerics.Vector3 p,float radius)
    {
        float skin=DistanceToEllipsoid(p,new(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ),new(ActorFaceSurface.RadiusX,ActorFaceSurface.RadiusY,ActorFaceSurface.RadiusZ));
        float hair=float.PositiveInfinity;
        foreach(var vertex in ActorHairSurface.BackSupport)hair=MathF.Min(hair,System.Numerics.Vector3.Distance(p,vertex));
        if(ActorHairSurface.Contains(p))hair=0;
        if(skin<radius||hair<radius){Console.WriteLine($"Support at {p}: skin {skin:F5}, hair {hair:F5}, radius {radius}");return false;}
        return true;
    }
    for(int n=0;n<=200;n++)
    {
        float u=n/200f;
        if(!Clear(ActorHarnessSurface.Belt(u*MathF.Tau),.01f)||!Clear(ActorHarnessSurface.Yoke(u),.012f))return false;
        foreach(float side in new[]{-1f,1f})if(!Clear(ActorHarnessSurface.Strap(side,u),.012f))return false;
    }
    foreach(float size in new[]{.73f,1,1.03f})
    {
        float top=2.31f*size,bottom=1.55f*size+(.46f+ActorHarnessSurface.Anchor.Y)*size;
        if(top-bottom<.07f*size)return false;
    }
    return true;
});
Check("the former stretched hat could enter the former box roof",()=>
{
    var deform=ActorTouchShape.Squeeze(1);
    var old=System.Numerics.Vector3.Transform(ActorAccessorySurface.HatCone(1,0),ActorAccessoryPlacement.Rotation(ActorAccessoryPlacement.HatRotation))+ActorAccessoryPlacement.HatOrigin;
    old*=deform;old.Y+=.08f+(deform.Y-1)*.46f;
    return old.Y>1.26f-.05f;
});
Check("hat shape remains rigid and fits the box through pressure, damaged tilt and small reactions",()=>
{
    float side=Yukkuri.Simulation.InteractionFixtureGeometry.BoxSideX-.05f;
    float back=Yukkuri.Simulation.InteractionFixtureGeometry.BoxBackZ+.04f;
    float front=Yukkuri.Simulation.InteractionFixtureGeometry.BoxFrontZ-.0175f;
    float roof=Yukkuri.Simulation.InteractionFixtureGeometry.BoxWallTop;
    foreach(float force in new[]{0,.25f,.65f,1})foreach(var deform in new[]{ActorTouchShape.Press(force),ActorTouchShape.Squeeze(force)})
    foreach(float damage in new[]{0,12f})foreach(float yaw in new[]{-15f,0,15f})foreach(float roll in new[]{-3f,0,3f})foreach(float pitch in new[]{-5f,0,5f})
    {
        var rotation=ActorAccessoryPlacement.Rotation(new(pitch,yaw,roll));
        var center=System.Numerics.Vector3.Transform(new System.Numerics.Vector3(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ)*deform,rotation);
        var radii=new System.Numerics.Vector3(ActorFaceSurface.RadiusX,ActorFaceSurface.RadiusY,ActorFaceSurface.RadiusZ)*deform;
        float extent=MathF.Sqrt(MathF.Pow(rotation.M12*radii.X,2)+MathF.Pow(rotation.M22*radii.Y,2)+MathF.Pow(rotation.M32*radii.Z,2));
        float lift=-.38f+.025f-(center.Y-extent);
        for(int row=0;row<=20;row++)for(int n=0;n<48;n++)foreach(bool brim in new[]{true,false})
        {
            var local=brim?ActorAccessorySurface.HatBrim(row/20f,n*MathF.Tau/48):ActorAccessorySurface.HatCone(row/20f,n*MathF.Tau/48);
            var p=System.Numerics.Vector3.Transform(ActorAccessoryPlacement.Point(local,true,deform,damage),rotation);p.Y+=lift;
            if(MathF.Abs(p.X)>side-.005f||p.Z<back+.005f||p.Z>front-.005f||p.Y>roof-.005f)
            {Console.WriteLine($"Hat out of box: {p}, deform {deform}, damage {damage}, rotation {pitch}/{yaw}/{roll}");return false;}
        }
    }
    return true;
});
Check("carry support tracks the deformed and turned underside instead of using the idle body's height",()=>
{
    foreach(float ageScale in new[]{.73f,1,1.03f})foreach(var deform in new[]{System.Numerics.Vector3.One,ActorTouchShape.Squeeze(1),ActorTouchShape.Press(1)})
    foreach(float angle in new[]{-.15f,0,.15f})
    {
        var scale=deform*ageScale;var rotation=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitZ,angle);
        float bottom=ActorBodyBounds.LowestSkinY(scale,rotation,System.Numerics.Vector3.Zero);
        float sampleMin=float.PositiveInfinity;
        for(int latitude=0;latitude<=60;latitude++)for(int n=0;n<80;n++)
        {
            float a=latitude*MathF.PI/60,b=n*MathF.Tau/80;
            var p=new System.Numerics.Vector3(ActorFaceSurface.RadiusX*MathF.Sin(a)*MathF.Cos(b),ActorFaceSurface.CenterY+ActorFaceSurface.RadiusY*MathF.Cos(a),ActorFaceSurface.CenterZ+ActorFaceSurface.RadiusZ*MathF.Sin(a)*MathF.Sin(b));
            sampleMin=MathF.Min(sampleMin,System.Numerics.Vector3.Transform(p*scale,rotation).Y);
        }
        if(bottom>sampleMin+.00001f||sampleMin-bottom>.001f)return false;
        float palmTop=(bottom/ageScale+.462f-.465f)*ageScale;
        if(palmTop>=bottom||bottom-palmTop>.004f)return false;
    }
    return true;
});
Check("left and right visual patches use the corresponding saved surface state",()=>
{
    var life=new Yukkuri.Simulation.InteractionLife{Dirt=18,Wetness=0};
    life.WipeSurface(Yukkuri.Simulation.BodyPart.Cheek,30,false,Yukkuri.Simulation.SurfaceSide.Left);
    var cheeks=ActorSurfaceMarks.Dirt.Where(m=>m.Part==Yukkuri.Simulation.BodyPart.Cheek).ToArray();
    return cheeks.Any(m=>m.Side==Yukkuri.Simulation.SurfaceSide.Left)&&cheeks.Any(m=>m.Side==Yukkuri.Simulation.SurfaceSide.Right)&&
        cheeks.All(m=>m.Side==Yukkuri.Simulation.SurfaceSide.Left?ActorSurfaceMarks.Opacity(life,m.Part,false,m.Side)==0:ActorSurfaceMarks.Opacity(life,m.Part,false,m.Side)>.5f);
});
Check("towel thickness closes continuously and the drinking water stays inside the open cup",()=>
{
    for(int row=0;row<=10;row++)for(int n=0;n<=128;n++)
    {
        var top=InteractionToolSurface.Cloth(row/10f,n*MathF.Tau/128);var bottom=InteractionToolSurface.Cloth(row/10f,n*MathF.Tau/128,true);
        if(MathF.Abs(top.Y-bottom.Y-.022f)>.00001f||MathF.Abs(top.X)>.141f||MathF.Abs(top.Z)>.111f)return false;
    }
    if(System.Numerics.Vector3.Distance(InteractionToolSurface.Cloth(1,0),InteractionToolSurface.Cloth(1,MathF.Tau))>.0001f)return false;
    float inner=.064f+.04f*(InteractionToolSurface.WaterHeight+.057f)/.132f;
    if(InteractionToolSurface.WaterRadius>inner||InteractionToolSurface.WaterHeight+.001f>=InteractionToolSurface.CupTop)return false;
    for(int sip=0;sip<=20;sip++)
    {
        float height=InteractionToolSurface.SipWaterHeight(sip/20f),radius=InteractionToolSurface.SipWaterRadius(height);
        float wall=.064f+.04f*(height+.057f)/.132f;
        if(radius>=wall||height<-.05f||height+radius*MathF.Sin(5*MathF.PI/180)>.070f)return false;
    }
    foreach(float u in new[]{0f,1f})
    {
        var p=InteractionToolSurface.CupHandle(u);float wall=.08f+.04f*(p.Y+.075f)/.15f;
        if(MathF.Abs(p.X-wall)>.00001f)return false;
    }
    return true;
});
Check("a supine hat extends beyond the former standing-only footprint",()=>
{
    float maximum=0;
    var turn=ActorAccessoryPlacement.Rotation(new(-105,0,0));
    for(int r=0;r<=30;r++)for(int n=0;n<96;n++)foreach(bool brim in new[]{false,true})
    {
        var local=brim?ActorAccessorySurface.HatBrim(r/30f,n*MathF.Tau/96):ActorAccessorySurface.HatCone(r/30f,n*MathF.Tau/96);
        var p=System.Numerics.Vector3.Transform(ActorAccessoryPlacement.Point(local,true,System.Numerics.Vector3.One),turn)+new System.Numerics.Vector3(0,.04f,-.08f);
        maximum=MathF.Max(maximum,MathF.Sqrt(p.X*p.X+p.Z*p.Z));
    }
    Console.WriteLine($"Supine hat maximum sampled horizontal radius: {maximum}");
    return maximum>.98f;
});
Check("former absolute carry projection shifts the oil when only height changes",()=>
{
    var origin=new System.Numerics.Vector3(0,2,5);
    var direction=System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(0,-.25f,-1));
    return InteractionCameraOrbit.TryPlaneHit(origin,direction,.8f,out var low)&&InteractionCameraOrbit.TryPlaneHit(origin,direction,.95f,out var high)&&MathF.Abs(high.Z-low.Z)>.5f;
});
Check("standing and turned placement envelopes contain the hat through deformation and its turning path",()=>
{
    float standing=0,turned=0;
    foreach(var deform in new[]{System.Numerics.Vector3.One,ActorTouchShape.Press(1),ActorTouchShape.Squeeze(1)})foreach(float damage in new[]{0f,12})for(int step=0;step<=15;step++)
    {
        float progress=step/15f;
        var turn=ActorAccessoryPlacement.Rotation(new(Yukkuri.Simulation.InteractionFixtureGeometry.SupineAngle*progress,0,0));
        for(int row=0;row<=20;row++)for(int n=0;n<64;n++)foreach(bool brim in new[]{false,true})
        {
            var local=brim?ActorAccessorySurface.HatBrim(row/20f,n*MathF.Tau/64):ActorAccessorySurface.HatCone(row/20f,n*MathF.Tau/64);
            var p=System.Numerics.Vector3.Transform(ActorAccessoryPlacement.Point(local,true,deform,damage),turn)+new System.Numerics.Vector3(0,0,Yukkuri.Simulation.InteractionFixtureGeometry.SupineOffsetZ*progress);
            float radius=MathF.Sqrt(p.X*p.X+p.Z*p.Z);
            if(step==0)standing=MathF.Max(standing,radius);turned=MathF.Max(turned,radius);
        }
    }
    Console.WriteLine($"Placement hat envelopes: standing {standing}, turning {turned}");
    return standing<Yukkuri.Simulation.InteractionFixtureGeometry.StandingFootprint-.01f&&turned<Yukkuri.Simulation.InteractionFixtureGeometry.SupineFootprint-.01f;
});
Check("relative support drag stays still under reframing and remains bounded at shallow and upward angles",()=>
{
    foreach(float size in new[]{2.8f,4.3f,12.3f})foreach(float yaw in new[]{-180f,-90,0,45,90,180})foreach(float pitch in new[]{-25f,0,1,14.5f,45,65})
    {
        var offset=InteractionCameraOrbit.Offset(yaw,pitch,3.62f);
        var forward=-System.Numerics.Vector3.Normalize(offset);
        var right=System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(forward,System.Numerics.Vector3.UnitY));
        if(InteractionCameraOrbit.GroundDrag(right,forward,System.Numerics.Vector2.Zero,size,900)!=System.Numerics.Vector3.Zero)return false;
        var drag=InteractionCameraOrbit.GroundDrag(right,forward,new(30,-30),size,900);
        var ahead=System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(forward.X,0,forward.Z));
        if(!float.IsFinite(drag.Length())||drag.Y!=0||System.Numerics.Vector3.Dot(drag,ahead)<=0||System.Numerics.Vector3.Dot(drag,right)<=0||drag.Length()>size*30/900*4.13f)return false;
        var one=InteractionCameraOrbit.GroundDrag(right,forward,new(1,-1),size,900);
        if(System.Numerics.Vector3.Distance(one*30,drag)>.00001f)return false;
    }
    return true;
});
Check("support drag matches orthographic ground projection at ordinary downward viewing angles",()=>
{
    foreach(float pitch in new[]{20f,35,65})foreach(float yaw in new[]{-90f,0,45,180})
    {
        var offset=InteractionCameraOrbit.Offset(yaw,pitch,1);
        var forward=-System.Numerics.Vector3.Normalize(offset);
        var right=System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(forward,System.Numerics.Vector3.UnitY));
        var up=System.Numerics.Vector3.Cross(right,forward);
        var view=InteractionCameraOrbit.ViewMotion(right,up,new(24,-32),4.3f,900);
        var origin=offset+new System.Numerics.Vector3(0,1,0);
        if(!InteractionCameraOrbit.TryPlaneHit(origin,forward,.8f,out var before)||!InteractionCameraOrbit.TryPlaneHit(origin+view,forward,.8f,out var after))return false;
        var drag=InteractionCameraOrbit.GroundDrag(right,forward,new(24,-32),4.3f,900);
        if(System.Numerics.Vector3.Distance(drag,after-before)>.00001f)return false;
    }
    return true;
});
Check("eye color layers and highlights remain inside the actual face-conforming lid at every supported expression",()=>
{
    foreach(float openness in new[]{.08f,.1f,.12f,.2f,.42f,.65f,1})foreach(var curve in Enum.GetValues<EyeCurve>())
    foreach(var layer in Enum.GetValues<EyeLayer>())foreach(float side in new[]{-1f,1f})
    {
        if(!ActorEyeSurface.Visible(layer,openness))continue;
        for(int ring=0;ring<=5;ring++)for(int n=0;n<48;n++)
        {
            var normalized=ActorEyeSurface.Coordinate(layer,ring/5f,n*MathF.Tau/48);
            if(normalized.LengthSquared()>1.00001f)return false;
            var p=ActorEyeSurface.Point(side,openness,curve,layer,ring/5f,n*MathF.Tau/48);
            if(!float.IsFinite(p.Z)||MathF.Abs(p.Z-ActorFaceSurface.Height(p.X,p.Y)-ActorEyeSurface.Offset(layer))>.00001f)return false;
            if(layer!=EyeLayer.Outline&&ActorEyeSurface.Offset(layer)<=ActorEyeSurface.Offset(EyeLayer.Outline))return false;
        }
    }
    return true;
});
Check("relaxed joy and sleep eyelids have distinct readable curves without moving the eye corners",()=>
{
    var relaxed=ActorEyeSurface.Point(1,.12f,EyeCurve.Relaxed,EyeLayer.Outline,0,0);
    var joy=ActorEyeSurface.Point(1,.20f,EyeCurve.Joy,EyeLayer.Outline,0,0);
    var sleep=ActorEyeSurface.Point(1,.12f,EyeCurve.Sleep,EyeLayer.Outline,0,0);
    if(!(joy.Y>relaxed.Y+.015f&&relaxed.Y>sleep.Y+.03f))return false;
    foreach(float angle in new[]{0,MathF.PI})
    {
        var a=ActorEyeSurface.Point(1,.12f,EyeCurve.Relaxed,EyeLayer.Outline,1,angle);
        var b=ActorEyeSurface.Point(1,.12f,EyeCurve.Sleep,EyeLayer.Outline,1,angle);
        if(System.Numerics.Vector3.Distance(a,b)>.00001f)return false;
    }
    return true;
});
Check("continuous eyelid reveal stays bounded and inner geometry retracts smoothly through closure",()=>
{
    foreach(var layer in Enum.GetValues<EyeLayer>())
    {
        float previous=-1;
        for(int step=0;step<=1000;step++)
        {
            float open=step/1000f,reveal=ActorEyeSurface.Reveal(layer,open);
            if(reveal<previous||reveal<0||reveal>1)return false;
            if(layer!=EyeLayer.Outline&&open<=.20f&&(reveal!=0||ActorEyeSurface.Visible(layer,open)))return false;
            if(step>0&&layer!=EyeLayer.Outline&&reveal-previous>.008f)return false;
            previous=reveal;
            for(int n=0;n<24;n++)
            {
                var p=ActorEyeSurface.ProjectedCoordinate(layer,open,1,n*MathF.Tau/24);
                if(!float.IsFinite(p.LengthSquared())||p.LengthSquared()>1.00001f)return false;
            }
        }
    }
    return true;
});
Check("interpolated eye arcs retain original expression endpoints and fixed outer corners",()=>
{
    foreach(float side in new[]{-1f,1f})foreach(var curve in Enum.GetValues<EyeCurve>())
    {
        var old=ActorEyeSurface.Point(side,.6f,curve,EyeLayer.Pupil,.8f,1.2f);
        var continuous=ActorEyeSurface.Point(side,.6f,ActorEyeSurface.Arc(curve),EyeLayer.Pupil,.8f,1.2f);
        if(System.Numerics.Vector3.Distance(old,continuous)>.000001f)return false;
    }
    for(int n=0;n<=100;n++)foreach(float angle in new[]{0f,MathF.PI})
    {
        float arc=-.018f+n*.051f/100;
        var p=ActorEyeSurface.Point(1,.3f,arc,EyeLayer.Outline,1,angle);
        var endpoint=ActorEyeSurface.Point(1,.3f,0f,EyeLayer.Outline,1,angle);
        if(System.Numerics.Vector3.Distance(p,endpoint)>.000001f)return false;
    }
    return true;
});
Check("the lined cradle closes underneath and stays outside the skin and hair envelope",()=>
{
    var skinCenter=new System.Numerics.Vector3(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ);
    var skinRadii=new System.Numerics.Vector3(ActorFaceSurface.RadiusX,ActorFaceSurface.RadiusY,ActorFaceSurface.RadiusZ);
    foreach(bool inner in new[]{false,true})for(int row=0;row<=40;row++)for(int n=0;n<=128;n++)
    {
        var point=ActorCradleSurface.Point(row/40f,n*MathF.Tau/128,inner);
        if(!float.IsFinite(point.Length())||(point-skinCenter)/skinRadii is var relative&&relative.LengthSquared()<1)
        {Console.WriteLine($"Cradle enters skin at {point}");return false;}
        if(ActorHairSurface.Contains(point))
        {Console.WriteLine($"Cradle enters hair at {point}");return false;}
        if(row==0&&System.Numerics.Vector3.Distance(point,ActorCradleSurface.Point(0,0,inner))>.00001f)return false;
    }
    return true;
});
Check("wrapped support stays above the floor while rocking at every body size",()=>
{
    foreach(float size in new[]{.73f,1,1.03f})foreach(float angle in new[]{-.18f,-.05f,0,.05f,.18f})foreach(var shape in new[]{System.Numerics.Vector3.One,ActorTouchShape.Press(.5f)})
    {
        var scale=shape*size;var rotation=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitZ,angle);
        float min=ActorCradleSurface.LowestY(scale,rotation,System.Numerics.Vector3.Zero);
        float lift=MathF.Max(0,.003f-.46f*size-min);
        for(int row=0;row<=ActorCradleSurface.Rows;row++)for(int n=0;n<ActorCradleSurface.Sides;n++)
        {
            var p=System.Numerics.Vector3.Transform(ActorCradleSurface.Point(row/(float)ActorCradleSurface.Rows,n*MathF.Tau/ActorCradleSurface.Sides)*scale,rotation);
            if(p.Y+.46f*size+lift<0)return false;
        }
    }
    return true;
});
Check("the tufted cushion is closed and its support height matches its authored top surface",()=>
{
    for(int row=0;row<=64;row++)for(int n=0;n<=192;n++)
    {
        var p=ActorCushionSurface.Point(row/64f,n/192f);
        if(!float.IsFinite(p.Length())||p.Y<.009f||p.Y>.231f||MathF.Abs(p.X)>.9001f||MathF.Abs(p.Z)>.6751f)return false;
        if(row>=32&&MathF.Abs(p.Y-ActorCushionSurface.TopHeight(p.X,p.Z))>.00001f)return false;
        if(row is 0 or 64&&System.Numerics.Vector3.Distance(p,ActorCushionSurface.Point(row/64f,0))>.00001f)return false;
    }
    return ActorCushionSurface.TopHeight(1,0)==0&&ActorCushionSurface.TopHeight(0,.8f)==0;
});
Check("cushion support lifts the posed body clear instead of intersecting it during rest or a turn",()=>
{
    foreach(float size in new[]{.73f,1.03f})foreach(var position in new[]{new System.Numerics.Vector3(0,0,0),new(.55f,0,.25f),new(.95f,0,0)})foreach(float pitch in new[]{0f,-105})
    {
        var scale=new System.Numerics.Vector3(1.07f,.88f,1.04f)*size;
        var rotation=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitX,pitch*MathF.PI/180);
        var origin=position+System.Numerics.Vector3.UnitY*(.46f*size);
        origin.Y+=ActorCushionSurface.SkinLift(System.Numerics.Vector3.Zero,origin,scale,rotation);
        var inverse=System.Numerics.Quaternion.Inverse(rotation);
        for(int row=32;row<=64;row++)for(int n=0;n<192;n++)
        {
            var p=ActorCushionSurface.Point(row/64f,n/192f);
            var local=System.Numerics.Vector3.Transform(p-origin,inverse)/scale-new System.Numerics.Vector3(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ);
            local/=new System.Numerics.Vector3(ActorFaceSurface.RadiusX,ActorFaceSurface.RadiusY,ActorFaceSurface.RadiusZ);
            if(local.LengthSquared()<.99999f){Console.WriteLine($"Cushion enters posed skin at {p}; origin {origin}, pitch {pitch}");return false;}
        }
    }
    return true;
});
Check("cushion support does not allocate managed objects during repeated body and cradle calculations",()=>
{
    var zero=System.Numerics.Vector3.Zero;var origin=new System.Numerics.Vector3(0,.46f,0);var scale=System.Numerics.Vector3.One;var rotation=System.Numerics.Quaternion.Identity;
    ActorCushionSurface.SkinLift(zero,origin,scale,rotation);ActorCushionSurface.CradleLift(zero,origin,scale,rotation);
    long before=GC.GetAllocatedBytesForCurrentThread();
    for(int n=0;n<20;n++){ActorCushionSurface.SkinLift(zero,origin,scale,rotation);ActorCushionSurface.CradleLift(zero,origin,scale,rotation);}
    return GC.GetAllocatedBytesForCurrentThread()==before;
});
Check("the soft wrapping also clears the cushion instead of hiding its lower cloth inside it",()=>
{
    foreach(float size in new[]{.73f,1.03f})foreach(float angle in new[]{-.17f,0,.17f})
    {
        var scale=System.Numerics.Vector3.One*size;var rotation=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitZ,angle);
        var origin=new System.Numerics.Vector3(.18f,.46f*size,.05f);
        origin.Y+=ActorCushionSurface.CradleLift(System.Numerics.Vector3.Zero,origin,scale,rotation);
        for(int row=0;row<=40;row++)for(int n=0;n<192;n++)
        {
            var p=origin+System.Numerics.Vector3.Transform(ActorCradleSurface.Point(row/40f,n*MathF.Tau/192)*scale,rotation);
            float height=ActorCushionSurface.TopHeight(p.X,p.Z);
            if(height>0&&p.Y<height){Console.WriteLine($"Cradle intersects cushion at {p}, surface {height}");return false;}
        }
    }
    return true;
});
Check("a turned hat and its hair cap clear the cushion as well as the skin",()=>
{
    foreach(float size in new[]{.73f,1.03f})foreach(float damage in new[]{0f,12})foreach(float pitch in new[]{-65f,-105})
    {
        var scale=ActorTouchShape.Squeeze(.7f)*size;
        var rotation=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitX,pitch*MathF.PI/180);
        var origin=new System.Numerics.Vector3(.15f,.46f*size,.05f);
        var transform=ActorAccessorySupport.Transform(true,origin,scale,size,rotation,damage);
        float lift=MathF.Max(ActorCushionSurface.SkinLift(System.Numerics.Vector3.Zero,origin,scale,rotation),ActorCushionSurface.HairLift(System.Numerics.Vector3.Zero,origin,scale,rotation));
        lift=MathF.Max(lift,ActorCushionSurface.PointLift(ActorAccessorySupport.Points(true),transform,System.Numerics.Vector3.Zero,.012f*size));
        origin.Y+=lift;transform=ActorAccessorySupport.Transform(true,origin,scale,size,rotation,damage);
        for(int row=0;row<=48;row++)for(int n=0;n<128;n++)foreach(bool brim in new[]{true,false})
        {
            var local=brim?ActorAccessorySurface.HatBrimUnderside(row/48f,n*MathF.Tau/128):ActorAccessorySurface.HatCone(row/48f,n*MathF.Tau/128);
            var p=System.Numerics.Vector3.Transform(local,transform);float top=ActorCushionSurface.TopHeight(p.X,p.Z);
            if(top>0&&p.Y<top){Console.WriteLine($"Hat enters cushion at {p}; top {top}");return false;}
        }
        var inverse=System.Numerics.Quaternion.Inverse(rotation);
        for(int row=16;row<=32;row++)for(int n=0;n<96;n++)
        {
            var p=ActorCushionSurface.Point(row/32f,n/96f);
            var local=System.Numerics.Vector3.Transform(p-origin,inverse)/scale;
            if(ActorHairSurface.Contains(local))return false;
        }
    }
    return true;
});
Check("a fingertip contact leaves the palm above the surface instead of burying its larger heel",()=>
{
    float oldPalmClearance=-InteractionToolSurface.FingerPad.Y+.003f-.06f;
    if(oldPalmClearance>=0)return false;
    var rotation=System.Numerics.Matrix4x4.CreateRotationX(InteractionToolSurface.FingerTiltDegrees*MathF.PI/180);
    float originY=-System.Numerics.Vector3.Transform(InteractionToolSurface.FingerPad,rotation).Y+.003f;
    foreach(var piece in new[]{(center:System.Numerics.Vector3.Zero,radii:new System.Numerics.Vector3(.085f,.06f,.10f)),(center:new System.Numerics.Vector3(0,0,-.12f),radii:new System.Numerics.Vector3(.035f,.04f,.10f))})
    {
        float extent=MathF.Sqrt(MathF.Pow(rotation.M12*piece.radii.X,2)+MathF.Pow(rotation.M22*piece.radii.Y,2)+MathF.Pow(rotation.M32*piece.radii.Z,2));
        float bottom=originY+System.Numerics.Vector3.Transform(piece.center,rotation).Y-extent;
        if(bottom<0){Console.WriteLine($"Hand plane penetration: {bottom}");return false;}
    }
    return true;
});
Check("the central ribbon knot is readable above the hair instead of burying its front surface",()=>
{
    int oldHidden=0;float minimum=float.PositiveInfinity;
    foreach(var deform in new[]{System.Numerics.Vector3.One,ActorTouchShape.Press(1),ActorTouchShape.Squeeze(1)})
    for(int row=0;row<=24;row++)for(int n=0;n<=48;n++)
    {
        var local=ActorAccessorySurface.BowKnot(row/24f,n/96f);
        var p=ActorAccessoryPlacement.Point(ActorAccessorySurface.HeadBow(local),false,deform);
        float q=((p-ActorSurfaceMarks.HairCenter*deform)/(ActorSurfaceMarks.HairRadii*deform)).LengthSquared();minimum=MathF.Min(minimum,q);
        var old=System.Numerics.Vector3.Transform(local,ActorAccessoryPlacement.Rotation(ActorAccessoryPlacement.BowRotation))+new System.Numerics.Vector3(0,.50f,.10f)*deform;
        if(((old-ActorSurfaceMarks.HairCenter*deform)/(ActorSurfaceMarks.HairRadii*deform)).LengthSquared()<1)oldHidden++;
    }
    Console.WriteLine($"Bow knot: former hidden front samples {oldHidden}, current minimum hair-normalized radius squared {minimum}");
    return oldHidden>0&&minimum>1;
});
Check("dressing tape and gauze follow the selected skin or hair with a closed taper at each edge",()=>
{
    foreach(var site in ActorSurfaceMarks.Dressing)foreach(bool pad in new[]{false,true})
    {
        var center=site.OnHair?ActorSurfaceMarks.HairCenter:new System.Numerics.Vector3(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ);
        var radii=site.OnHair?ActorSurfaceMarks.HairRadii:new System.Numerics.Vector3(ActorFaceSurface.RadiusX,ActorFaceSurface.RadiusY,ActorFaceSurface.RadiusZ);
        for(int row=0;row<=20;row++)for(int n=0;n<96;n++)
        {
            var p=ActorDressingSurface.Point(site,row/20f,n*MathF.Tau/96,pad);
            if(!float.IsFinite(p.Length())||((p-center)/radii).LengthSquared()<=1)return false;
            if(row==0&&System.Numerics.Vector3.Distance(p,ActorDressingSurface.Point(site,0,0,pad))>.000001)return false;
        }
        var outer=ActorDressingSurface.Point(site,1,0,pad);
        var sameSite=pad?site with {Size=site.Size*new System.Numerics.Vector2(.46f,.76f)}:site;
        var under=ActorSurfaceMarks.Project(sameSite,System.Numerics.Vector2.UnitX,pad?.019f:.007f);
        if(System.Numerics.Vector3.Distance(outer,under)>.000001)return false;
    }
    return true;
});
Check("backing folds stay within the old shell while preserving the gauze center and outer bevel",()=>
{
    bool folded=false;
    foreach(var site in ActorSurfaceMarks.Dressing)for(int row=0;row<=50;row++)for(int n=0;n<96;n++)
    {
        float radius=row/50f,angle=n*MathF.Tau/96;
        float depth=ActorDressingSurface.BackingFoldDepth(radius,angle);
        if(!float.IsFinite(depth)||depth<0||depth>ActorDressingSurface.BackingFoldMaximum+.0000001f)return false;
        if(radius<=.56f||radius>=.88f){if(depth!=0)return false;}
        if(MathF.Abs(depth-ActorDressingSurface.BackingFoldDepth(radius,-angle))>.000001f||
            MathF.Abs(depth-ActorDressingSurface.BackingFoldDepth(radius,MathF.PI-angle))>.000001f)return false;
        folded|=depth>.0001f;
        float Round(float value)=>MathF.CopySign(MathF.Pow(MathF.Abs(value),.48f),value);
        var outline=new System.Numerics.Vector2(Round(MathF.Cos(angle)),Round(MathF.Sin(angle)))*radius;
        float bevel=Math.Clamp((radius-.88f)/.12f,0,1);bevel=bevel*bevel*(3-2*bevel);
        foreach(bool pad in new[]{false,true})
        {
            var sampled=pad?site with {Size=site.Size*new System.Numerics.Vector2(.46f,.76f)}:site;
            float oldOffset=pad?.023f-.004f*bevel:.019f-.012f*bevel;
            var original=ActorSurfaceMarks.Project(sampled,outline,oldOffset);
            var basePoint=ActorSurfaceMarks.Project(sampled,outline,0);
            var current=ActorDressingSurface.Point(site,radius,angle,pad);
            float displacement=System.Numerics.Vector3.Distance(current,original);
            if(!float.IsFinite(current.Length())||MathF.Abs(displacement-(pad?0:depth))>.000001f)return false;
            // Projection offsets are along the same unit surface normal; the
            // fold removes height without moving sideways or entering skin.
            var expected=ActorSurfaceMarks.Project(sampled,outline,oldOffset-(pad?0:depth));
            if(System.Numerics.Vector3.Distance(current,expected)>.000001f||
                System.Numerics.Vector3.Distance(current,basePoint)<(pad?.019f:.007f)-.000001f)return false;
        }
    }
    foreach(float boundary in new[]{.56f,.88f})for(int n=0;n<96;n++)
    {
        float angle=n*MathF.Tau/96;
        if(ActorDressingSurface.BackingFoldDepth(boundary,angle)!=0||
            MathF.Abs(ActorDressingSurface.BackingFoldDepth(boundary-.00001f,angle)-
                ActorDressingSurface.BackingFoldDepth(boundary+.00001f,angle))>.000001f)return false;
    }
    return folded;
});
Check("dressing support uses the actual folded backing and gauze mesh samples",()=>
{
    var life=new Yukkuri.Simulation.InteractionLife();
    foreach(var site in ActorSurfaceMarks.Dressing)life.Dress(site.Part,site.Side);
    foreach(float pitch in new[]{0f,-105,125})foreach(float roll in new[]{0f,70})
    {
        var scale=new System.Numerics.Vector3(.89f,.73f,.92f);
        var rotation=System.Numerics.Quaternion.CreateFromYawPitchRoll(.3f,pitch*MathF.PI/180,roll*MathF.PI/180);
        var origin=new System.Numerics.Vector3(.1f,.37f,.05f);
        var transform=System.Numerics.Matrix4x4.CreateScale(scale)*System.Numerics.Matrix4x4.CreateFromQuaternion(rotation)*
            System.Numerics.Matrix4x4.CreateTranslation(origin);
        float lowest=float.PositiveInfinity,lift=0;
        foreach(var site in ActorSurfaceMarks.Dressing)foreach(bool pad in new[]{false,true})
            for(int row=0;row<=ActorDressingSurface.Rows;row++)for(int n=0;n<ActorDressingSurface.Sides;n++)
            {
                var p=ActorDressingSurface.Point(site,row/(float)ActorDressingSurface.Rows,n*MathF.Tau/ActorDressingSurface.Sides,pad);
                lowest=MathF.Min(lowest,System.Numerics.Vector3.Transform(p,transform).Y);
                lift=MathF.Max(lift,ActorCushionSurface.PointLift(new[]{p},transform,System.Numerics.Vector3.Zero,.004f));
            }
        if(MathF.Abs(ActorDressingSurface.LowestY(life,transform)-lowest)>.000001f||
            MathF.Abs(ActorDressingSurface.CushionLift(life,System.Numerics.Vector3.Zero,origin,scale,rotation)-lift)>.000001f)return false;
    }
    return true;
});
Check("visible care dressings clear the cushion under a resting or rolled body",()=>
{
    int oldPenetrations=0;
    var life=new Yukkuri.Simulation.InteractionLife();
    foreach(var site in ActorSurfaceMarks.Dressing)life.Dress(site.Part,site.Side);
    foreach(float size in new[]{.73f,1.03f})foreach(float pitch in new[]{0f,-105,125})foreach(float roll in new[]{0f,70})
    {
        var scale=new System.Numerics.Vector3(1.07f,.88f,1.04f)*size;
        var rotation=System.Numerics.Quaternion.CreateFromYawPitchRoll(0,pitch*MathF.PI/180,roll*MathF.PI/180);
        var origin=new System.Numerics.Vector3(.1f,.46f*size,.05f);
        float skin=MathF.Max(ActorCushionSurface.SkinLift(System.Numerics.Vector3.Zero,origin,scale,rotation),ActorCushionSurface.HairLift(System.Numerics.Vector3.Zero,origin,scale,rotation));
        float withDressing=MathF.Max(skin,ActorDressingSurface.CushionLift(life,System.Numerics.Vector3.Zero,origin,scale,rotation));
        foreach(var site in ActorSurfaceMarks.Dressing)for(int row=0;row<=20;row++)for(int n=0;n<96;n++)foreach(bool pad in new[]{false,true})
        {
            var p=origin+System.Numerics.Vector3.Transform(ActorDressingSurface.Point(site,row/20f,n*MathF.Tau/96,pad)*scale,rotation);
            float top=ActorCushionSurface.TopHeight(p.X,p.Z);
            if(top<=0)continue;if(p.Y+skin<top)oldPenetrations++;
            if(p.Y+withDressing<top-.00001f){Console.WriteLine($"Dressing inside cushion: {site.Part}, {site.Side}, {p.Y+withDressing-top}");return false;}
        }
    }
    Console.WriteLine($"Dressing samples that skin-only support missed: {oldPenetrations}");return oldPenetrations>0;
});
Check("tap response moves away from the touched surface and mirrors with the contact direction",()=>
{
    foreach(var normal in new[]{new System.Numerics.Vector3(1,0,0),new(-1,0,0),new(0,0,1),new(0,0,-1)})
    {
        var direction=InteractionCameraOrbit.ContactImpulse(normal,new(0,-.3f,-1));
        if(System.Numerics.Vector2.Dot(direction,new(normal.X,normal.Z))>-.999f)return false;
        var a=ActorReactionMotion.Sample("poke",0,direction)!.Value;
        var b=ActorReactionMotion.Sample("poke",0,-direction)!.Value;
        if(System.Numerics.Vector3.Distance(a.Offset,-b.Offset)>.000001||System.Numerics.Vector3.Distance(a.RotationDegrees,-b.RotationDegrees)>.000001)return false;
        if(a.Offset.X*direction.X+a.Offset.Z*direction.Y<=0)return false;
    }
    var vertical=InteractionCameraOrbit.ContactImpulse(System.Numerics.Vector3.UnitY,new(-1,-.3f,0));
    return vertical==new System.Numerics.Vector2(-1,0)&&float.IsFinite(InteractionCameraOrbit.ContactImpulse(System.Numerics.Vector3.UnitY,-System.Numerics.Vector3.UnitY).Length());
});
Check("lighter drop stays inside the old sphere with closed tips seam and finite outward normals",()=>
{
    for(int row=0;row<=128;row++)for(int side=0;side<=128;side++)
    {
        float u=row/128f,v=side/128f;
        var p=InteractionLighterSurface.Point(u,v);var n=InteractionLighterSurface.Normal(u,v);
        if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z)||p.LengthSquared()>.250001f||
            MathF.Abs(p.Y-(u-.5f))>1e-7f||!float.IsFinite(n.X)||!float.IsFinite(n.Y)||!float.IsFinite(n.Z)||
            MathF.Abs(n.Length()-1)>1e-5f||System.Numerics.Vector3.Dot(p,n)<=0)return false;
        if(row==0&&(p!=new System.Numerics.Vector3(0,-.5f,0)||n!=-System.Numerics.Vector3.UnitY))return false;
        if(row==128&&(p!=new System.Numerics.Vector3(0,.5f,0)||n!=System.Numerics.Vector3.UnitY))return false;
        if(side==128&&(p!=InteractionLighterSurface.Point(u,0)||n!=InteractionLighterSurface.Normal(u,0)))return false;
        if(u>0&&u<.5f)
        {
            var upper=InteractionLighterSurface.Point(1-u,v);
            if(p.X*p.X+p.Z*p.Z<=upper.X*upper.X+upper.Z*upper.Z)return false;
        }
    }
    return true;
});
Check("actual lighter mesh has closed edges and only nondegenerate outward clockwise triangles",()=>
{
    var edges=new Dictionary<(int,int),int>();var vertices=new HashSet<int>();int triangles=0;
    int Key(int row,int side)=>row is 0?0:row==InteractionLighterSurface.Rows?
        InteractionLighterSurface.Rows*InteractionLighterSurface.Sides:row*InteractionLighterSurface.Sides+side%InteractionLighterSurface.Sides;
    bool Triangle((int Row,int Side) a,(int Row,int Side) b,(int Row,int Side) c)
    {
        System.Numerics.Vector3 Point((int Row,int Side) q)=>InteractionLighterSurface.Point(q.Row/(float)InteractionLighterSurface.Rows,q.Side/(float)InteractionLighterSurface.Sides);
        System.Numerics.Vector3 Normal((int Row,int Side) q)=>InteractionLighterSurface.Normal(q.Row/(float)InteractionLighterSurface.Rows,q.Side/(float)InteractionLighterSurface.Sides);
        var pa=Point(a);var pb=Point(b);var pc=Point(c);
        // Godot's exterior winding is clockwise, opposite the usual cross(b-a,c-a).
        var exterior=System.Numerics.Vector3.Cross(pc-pa,pb-pa);
        if(!float.IsFinite(exterior.LengthSquared())||exterior.LengthSquared()<1e-12f||
            System.Numerics.Vector3.Dot(exterior,Normal(a)+Normal(b)+Normal(c))<=0)return false;
        int ka=Key(a.Row,a.Side),kb=Key(b.Row,b.Side),kc=Key(c.Row,c.Side);
        foreach(int vertex in new[]{ka,kb,kc})vertices.Add(vertex);
        foreach(var edge in new[]{(ka,kb),(kb,kc),(kc,ka)})
        {var key=(Math.Min(edge.Item1,edge.Item2),Math.Max(edge.Item1,edge.Item2));edges[key]=edges.GetValueOrDefault(key)+1;}
        triangles++;return true;
    }
    for(int row=0;row<InteractionLighterSurface.Rows;row++)for(int side=0;side<InteractionLighterSurface.Sides;side++)
    {
        if(row<InteractionLighterSurface.Rows-1&&!Triangle((row,side),(row+1,side+1),(row+1,side)))return false;
        if(row>0&&!Triangle((row,side),(row,side+1),(row+1,side+1)))return false;
    }
    return triangles==2*(InteractionLighterSurface.Rows-1)*InteractionLighterSurface.Sides&&
        edges.Values.All(count=>count==2)&&vertices.Count-edges.Count+triangles==2;
});
Check("tear shallow cap preserves original projection and glints stay above their actual underlying coordinate",()=>
{
    foreach(bool glint in new[]{false,true})foreach(float side in new[]{-1f,1f})
        for(int ring=0;ring<=24;ring++)for(int n=0;n<=96;n++)
        {
            float radius=ring/24f,angle=n*MathF.Tau/96;
            float x=MathF.Cos(angle)*radius,y=MathF.Sin(angle)*radius;
            if(glint){x=x*.16f-.28f;y=y*.34f-.30f;}
            float originalX=side*(.27f+.006f*(1-y))+x*.028f*(.55f-.45f*y),originalY=-.16f+y*.085f;
            var q=ActorTearSurface.SurfaceCoordinate(radius,angle,glint);float offset=ActorTearSurface.SurfaceOffset(q);
            var p=ActorTearSurface.Point(side,radius,angle,glint);
            if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z)||q.LengthSquared()>1.000001f||
                offset<.015f-1e-7f||offset>.017f+1e-7f||MathF.Abs(p.X-originalX)>1e-7f||MathF.Abs(p.Y-originalY)>1e-7f)return false;
            if(MathF.Abs(p.Z-ActorFaceSurface.Height(p.X,p.Y)-offset-(glint?.002f:0))>1e-7f)return false;
            if(glint)
            {
                var underlying=ActorTearSurface.Point(side,q.Length(),MathF.Atan2(q.Y,q.X));
                if(MathF.Abs(p.X-underlying.X)>1e-7f||MathF.Abs(p.Y-underlying.Y)>1e-7f||
                    MathF.Abs(p.Z-underlying.Z-.002f)>1e-7f)return false;
            }
        }
    return true;
});
Check("tear edge alpha fades monotonically to zero independently from the paused whole-trace amount",()=>
{
    var trace=new ActorTearPresentation();trace.Initialize(.65f);
    foreach(bool glint in new[]{false,true})
    {
        float previous=1,start=glint?.60f:.70f;
        if(ActorTearSurface.EdgeOpacity(0,glint)!=1||ActorTearSurface.EdgeOpacity(start,glint)!=1||
            ActorTearSurface.EdgeOpacity(1,glint)!=0)return false;
        for(int n=0;n<=200;n++)
        {
            float radius=n/200f,alpha=ActorTearSurface.EdgeOpacity(radius,glint);
            if(!float.IsFinite(alpha)||alpha<0||alpha>1||alpha>previous+1e-7f)return false;
            float amount=trace.Amount;trace.Advance(0f,0);
            if(trace.Amount!=amount||alpha*trace.Amount<0||alpha*trace.Amount>trace.Amount)return false;
            if(radius<=start&&alpha!=1)return false;
            previous=alpha;
        }
        if(MathF.Abs(ActorTearSurface.EdgeOpacity(start+.0001f,glint)-1)>1e-6f||
            ActorTearSurface.EdgeOpacity(1-.0001f,glint)>1e-6f)return false;
        float edge=ActorTearSurface.EdgeOpacity(.85f,glint);trace.Advance(0f,.1f);
        if(ActorTearSurface.EdgeOpacity(.85f,glint)!=edge||trace.Amount>=.65f)return false;
    }
    return true;
});
Check("tear tracks conform to the cheeks and remain below the eyes",()=>
{
    foreach(float side in new[]{-1f,1f})foreach(bool glint in new[]{false,true})for(int row=0;row<=24;row++)for(int n=0;n<96;n++)
    {
        var p=ActorTearSurface.Point(side,row/24f,n*MathF.Tau/96,glint);
        float gap=p.Z-ActorFaceSurface.Height(p.X,p.Y);
        if(!float.IsFinite(p.Length())||gap<.0149f||gap>.0191f||p.Y>-.074f||p.Y<-.246f)return false;
        if(row==0&&System.Numerics.Vector3.Distance(p,ActorTearSurface.Point(side,0,0,glint))>.000001)return false;
    }
    return true;
});
Check("tear traces build and fade continuously and freeze when presentation is paused",()=>
{
    var tears=new ActorTearPresentation();tears.Advance(true,.1f);float first=tears.Amount;
    if(first<=0||first>=1)return false;for(int n=0;n<10;n++)tears.Advance(true,.1f);
    if(tears.Amount!=1)return false;tears.Advance(false,.1f);float remaining=tears.Amount;
    if(remaining<=0||remaining>=1)return false;for(int n=0;n<10;n++)tears.Advance(false,0);
    if(tears.Amount!=remaining)return false;for(int n=0;n<30;n++)tears.Advance(false,.1f);
    return tears.Amount==0&&ActorReactionMotion.GuardYaw(70,0)==-ActorReactionMotion.GuardYaw(0,70);
});
Check("graded tears preserve lingering alarm, loaded amount and paused time without snapping",()=>
{
    float lingering=ActorTearPresentation.Target(0,0,60,false);
    if(lingering<=0||lingering>=.55f||ActorTearPresentation.Target(100,100,100,true,dead:true)!=0)return false;
    if(ActorTearPresentation.Target(100,100,100,true,critical:true)>.55f)return false;
    float previous=0;
    for(int n=0;n<=100;n++)
    {
        float target=ActorTearPresentation.Target(n,0,0,false);
        if(target<previous||target<0||target>1)return false;previous=target;
    }
    var coarse=new ActorTearPresentation();var fine=new ActorTearPresentation();
    coarse.Initialize(.8f);fine.Initialize(.8f);
    for(int n=0;n<10;n++)coarse.Advance(lingering,.1f);
    for(int n=0;n<50;n++)fine.Advance(lingering,.02f);
    if(MathF.Abs(coarse.Amount-fine.Amount)>.00001f||coarse.Amount<=lingering||coarse.Amount>=.8f)return false;
    float saved=coarse.Amount;coarse.Advance(0f,0);
    return coarse.Amount==saved;
});
Check("soft ball panels stay inside their contact radius with outward front faces",()=>
{
    for(int row=0;row<=48;row++)for(int n=0;n<144;n++)
    {
        float u=row/48f,angle=n*MathF.Tau/144;var p=InteractionBallSurface.Point(u,angle);
        if(!float.IsFinite(p.Length())||p.Length()>.24001f||p.Length()<.238f)return false;
        if(row>0&&row<48)
        {
            var du=InteractionBallSurface.Point(u+.0001f,angle)-p;var dv=InteractionBallSurface.Point(u,angle+.0001f)-p;
            if(System.Numerics.Vector3.Dot(-System.Numerics.Vector3.Cross(du,dv),p)<=0)return false;
        }
    }
    return true;
});
Check("ball rotation follows displacement and reverses without spinning during a hold",()=>
{
    var ball=new InteractionBallMotion();ball.Present(3,2,false);ball.Present(3.12,2,false);var moved=ball.Rotation;
    if(MathF.Abs(moved.Z)<.2f)return false;for(int n=0;n<100;n++)ball.Present(3.12,2,false);
    if(ball.Rotation!=moved)return false;ball.Present(3.12,2,true);if(ball.Rotation!=moved)return false;
    ball.Present(3,2,false);return MathF.Abs(ball.Rotation.W-1)<.00001f;
});
Check("ball picking follows the visible sphere instead of a fixed screen-distance shortcut",()=>
{
    var zero=System.Numerics.Vector3.Zero;var ray=-System.Numerics.Vector3.UnitZ;
    bool hit=InteractionPropContact.RayHit(Yukkuri.Simulation.RoomObjectKind.Ball,zero,new(0,.24f,2),ray,out float distance);
    bool outside=InteractionPropContact.RayHit(Yukkuri.Simulation.RoomObjectKind.Ball,zero,new(.26f,.24f,2),ray,out _);
    bool behind=InteractionPropContact.RayHit(Yukkuri.Simulation.RoomObjectKind.Ball,zero,new(0,.24f,2),-ray,out _);
    return hit&&MathF.Abs(distance-1.76f)<.00001f&&!outside&&!behind;
});
Check("body water droplets preserve the old sphere envelope and smooth outward surface normals",()=>
{
    for(int row=0;row<=96;row++)for(int side=0;side<=96;side++)
    {
        float u=row/96f,v=side/96f;
        var p=ActorWaterDropletSurface.Point(u,v);var n=ActorWaterDropletSurface.Normal(u,v);
        if(!float.IsFinite(p.LengthSquared())||p.LengthSquared()>.250001f||
            !float.IsFinite(n.LengthSquared())||MathF.Abs(n.LengthSquared()-1)>.00001f||
            System.Numerics.Vector3.Dot(p,n)<=0)return false;
        if(side==96&&(p!=ActorWaterDropletSurface.Point(u,0)||n!=ActorWaterDropletSurface.Normal(u,0)))return false;
        if(row==0&&(p!=new System.Numerics.Vector3(0,-.5f,0)||n!=-System.Numerics.Vector3.UnitY))return false;
        if(row==96&&(p!=new System.Numerics.Vector3(0,.5f,0)||n!=System.Numerics.Vector3.UnitY))return false;
        if(row>0&&row<48&&ActorWaterDropletSurface.Point(u,0).X<=ActorWaterDropletSurface.Point(1-u,0).X)return false;
    }
    return true;
});
Check("rendered water droplet triangles are closed nondegenerate shells using the sampled surface",()=>
{
    var samples=new Dictionary<System.Numerics.Vector3,System.Numerics.Vector3>();
    for(int row=0;row<=ActorWaterDropletSurface.Rows;row++)for(int side=0;side<ActorWaterDropletSurface.Sides;side++)
    {
        float u=(float)row/ActorWaterDropletSurface.Rows,v=(float)side/ActorWaterDropletSurface.Sides;
        samples[ActorWaterDropletSurface.Point(u,v)]=ActorWaterDropletSurface.Normal(u,v);
    }
    var vertices=new Dictionary<System.Numerics.Vector3,int>();
    var edges=new Dictionary<(int,int),(int Count,int Direction)>();
    int Key(System.Numerics.Vector3 p)
    {if(!vertices.TryGetValue(p,out int id)){id=vertices.Count;vertices.Add(p,id);}return id;}
    int count=0;
    foreach(var face in ActorWaterDropletSurface.Triangles())
    {
        var a=face.A.Position;var b=face.B.Position;var c=face.C.Position;
        var cross=System.Numerics.Vector3.Cross(c-a,b-a);
        if(!float.IsFinite(cross.LengthSquared())||cross.LengthSquared()<=1e-12f||
            System.Numerics.Vector3.Dot(cross,(a+b+c)/3)<=0)return false;
        foreach(var vertex in new[]{face.A,face.B,face.C})
            if(!samples.TryGetValue(vertex.Position,out var normal)||vertex.Normal!=normal||
                System.Numerics.Vector3.Dot(cross,vertex.Normal)<=0)return false;
        int ia=Key(a),ib=Key(b),ic=Key(c);
        foreach(var edge in new[]{(ia,ib),(ib,ic),(ic,ia)})
        {
            var key=(Math.Min(edge.Item1,edge.Item2),Math.Max(edge.Item1,edge.Item2));
            var old=edges.GetValueOrDefault(key);
            edges[key]=(old.Count+1,old.Direction+(edge.Item1<edge.Item2?1:-1));
        }
        count++;
    }
    return count==352&&vertices.Count==samples.Count&&vertices.Count-edges.Count+count==2&&
        edges.Values.All(edge=>edge.Count==2&&edge.Direction==0);
});
Check("actual chocolate facets remain inside their original oriented crumb envelope",()=>
{
    for(int index=0;index<InteractionFoodSurface.ChipCount;index++)
    {
        // Derive the old ellipsoid from its public surface, independently of
        // the new ring construction. Game and this check use the same faces.
        var top=InteractionFoodSurface.CrumbPoint(index,0,0,true);
        var bottom=InteractionFoodSurface.CrumbPoint(index,MathF.PI,0,true);
        var center=(top+bottom)*.5f;
        var x=InteractionFoodSurface.CrumbPoint(index,MathF.PI*.5f,0,true)-center;
        var y=top-center;
        var z=InteractionFoodSurface.CrumbPoint(index,MathF.PI*.5f,MathF.PI*.5f,true)-center;
        bool Inside(System.Numerics.Vector3 p)
        {
            var q=p-center;
            float u=System.Numerics.Vector3.Dot(q,x)/x.LengthSquared();
            float v=System.Numerics.Vector3.Dot(q,y)/y.LengthSquared();
            float w=System.Numerics.Vector3.Dot(q,z)/z.LengthSquared();
            float squared=u*u+v*v+w*w;
            return float.IsFinite(squared)&&squared<=1.00001f;
        }
        foreach(var face in InteractionFoodSurface.ChipTriangles(index))
            if(!Inside(face.A)||!Inside(face.B)||!Inside(face.C)||!Inside((face.A+face.B+face.C)/3))return false;
    }
    return true;
});
Check("rendered chocolate facets form closed shells with actual flat outward normals",()=>
{
    for(int index=0;index<InteractionFoodSurface.ChipCount;index++)
    {
        var center=(InteractionFoodSurface.CrumbPoint(index,0,0,true)+
            InteractionFoodSurface.CrumbPoint(index,MathF.PI,0,true))*.5f;
        var vertices=new Dictionary<System.Numerics.Vector3,int>();
        var edges=new Dictionary<(int,int),(int Count,int Direction)>();
        int Key(System.Numerics.Vector3 point)
        {if(!vertices.TryGetValue(point,out int id)){id=vertices.Count;vertices.Add(point,id);}return id;}
        int faces=0;
        foreach(var face in InteractionFoodSurface.ChipTriangles(index))
        {
            var cross=System.Numerics.Vector3.Cross(face.C-face.A,face.B-face.A);
            float area=cross.Length();
            if(!float.IsFinite(area)||area<=.0000001f||!float.IsFinite(face.Normal.Length())||
                MathF.Abs(face.Normal.Length()-1)>.00001f||
                System.Numerics.Vector3.Distance(cross/area,face.Normal)>.00001f||
                System.Numerics.Vector3.Dot(face.Normal,(face.A+face.B+face.C)/3-center)<=0)return false;
            int a=Key(face.A),b=Key(face.B),c=Key(face.C);
            if(a==b||b==c||c==a)return false;
            foreach(var edge in new[]{(a,b),(b,c),(c,a)})
            {
                var key=(Math.Min(edge.Item1,edge.Item2),Math.Max(edge.Item1,edge.Item2));
                var old=edges.GetValueOrDefault(key);
                edges[key]=(old.Count+1,old.Direction+(edge.Item1<edge.Item2?1:-1));
            }
            faces++;
        }
        if(faces!=64||vertices.Count-edges.Count+faces!=2||
            edges.Values.Any(edge=>edge.Count!=2||edge.Direction!=0))return false;
    }
    return true;
});
Check("cookie edges close and the hand portion rests above the spoon's inner surface",()=>
{
    if(MathF.Abs(InteractionFoodSurface.Plate(0,0,false).Y+InteractionFoodSurface.MealGroundOffset)>.000001f||MathF.Abs(InteractionFoodSurface.CookieTop(0,0,true).Y+InteractionFoodSurface.SweetGroundOffset)>.000001f)return false;
    for(int n=0;n<144;n++)
    {
        float angle=n*MathF.Tau/144;
        if(System.Numerics.Vector3.Distance(InteractionFoodSurface.CookieTop(1,angle),InteractionFoodSurface.CookieSide(1,angle))>.000001f||
            System.Numerics.Vector3.Distance(InteractionFoodSurface.CookieTop(1,angle,true),InteractionFoodSurface.CookieSide(0,angle))>.000001f)return false;
        for(int row=0;row<=24;row++)
        {
            var p=InteractionFoodSurface.HandMeal(row/24f,angle,true);float r2=p.X*p.X/.01f+p.Z*p.Z/.0049f;
            float bowl=.008f+.022f*Math.Clamp(r2,0,1);
            if(!float.IsFinite(p.Length())||p.Y<bowl+.0009f)return false;
        }
    }
    return true;
});
Check("offered spoon and cookie meet the mouth without burying their rims in the face",()=>
{
    foreach(float size in new[]{.73f,1,1.03f})foreach(float x in new[]{-.08f,0,.08f})foreach(bool sweet in new[]{false,true})
    {
        var normal=ActorFaceSurface.Normal(x,-.175f);var forward=System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(normal.X,0,normal.Z));
        var right=System.Numerics.Vector3.Cross(System.Numerics.Vector3.UnitY,forward);
        System.Numerics.Vector3 Transform(System.Numerics.Vector3 p)=>right*p.X+System.Numerics.Vector3.UnitY*p.Y+forward*p.Z;
        var contact=new System.Numerics.Vector3(x,-.175f,ActorFaceSurface.Height(x,-.175f))*size;
        var offer=sweet?InteractionFoodSurface.SweetOffer:InteractionFoodSurface.MealOffer;
        var origin=contact-Transform(offer)+normal*(sweet?.003f:.005f);
        bool Outside(System.Numerics.Vector3 p)
        {
            var world=origin+Transform(p);var q=(world/size-new System.Numerics.Vector3(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ))/new System.Numerics.Vector3(ActorFaceSurface.RadiusX,ActorFaceSurface.RadiusY,ActorFaceSurface.RadiusZ);
            if(q.LengthSquared()<.9999f){Console.WriteLine($"Offering intersects face: sweet {sweet}, size {size}, contact x {x}, point {p}, normalized radius {q.LengthSquared()}");return false;}
            return true;
        }
        for(int row=0;row<=24;row++)for(int n=0;n<144;n++)
        {
            float u=row/24f,a=n*MathF.Tau/144;
            if(sweet)
            {
                if(!Outside(InteractionFoodSurface.CookieTop(u,a)*InteractionFoodSurface.SweetHandScale)||!Outside(InteractionFoodSurface.CookieTop(u,a,true)*InteractionFoodSurface.SweetHandScale)||!Outside(InteractionFoodSurface.CookieSide(u,a)*InteractionFoodSurface.SweetHandScale))return false;
            }
            else if(!Outside(InteractionFoodSurface.HandMeal(u,a))||!Outside(InteractionFoodSurface.SpoonBowl(u,a,true))||!Outside(InteractionFoodSurface.SpoonBowl(u,a,false)))return false;
        }
        if(!sweet)for(int n=0;n<96;n++)
        {
            var rim=InteractionFoodSurface.SpoonRim(n*MathF.Tau/96);
            foreach(var padding in new[]{System.Numerics.Vector3.UnitX,-System.Numerics.Vector3.UnitX,System.Numerics.Vector3.UnitY,-System.Numerics.Vector3.UnitY,System.Numerics.Vector3.UnitZ,-System.Numerics.Vector3.UnitZ})
                if(!Outside(rim+padding*.0035f)||!Outside(InteractionFoodSurface.SpoonHandle(n/95f)+padding*.014f))return false;
        }
    }
    return true;
});
Check("care cloth is closed and its underside stays above the contact plane",()=>
{
    bool folded=false;
    foreach(bool gauze in new[]{false,true})for(int r=0;r<=32;r++)for(int n=0;n<=96;n++)
    {
        var p=InteractionCareSurface.Pad(r/32f,n/96f,gauze);
        if(!float.IsFinite(p.LengthSquared())||p.Y<InteractionCareSurface.PadBottom-.000001f)return false;
        if(r is 0 or 32&&MathF.Abs(p.X)+MathF.Abs(p.Z)>.00001f)return false;
        if(System.Numerics.Vector3.Distance(InteractionCareSurface.Pad(r/32f,0,gauze),InteractionCareSurface.Pad(r/32f,1,gauze))>.00001f)return false;
        if(gauze)
        {
            // Recover the original smaller gauze shell from the unchanged base
            // cloth. Only the top Y may recede; lower mesh and poles stay put.
            var basis=InteractionCareSurface.Pad(r/32f,n/96f);
            float oldY=.024f+basis.Y*(.006f/.022f);
            float depth=InteractionCareSurface.GauzeFoldDepth(r/32f,n/96f);
            if(!float.IsFinite(depth)||depth<0||depth>InteractionCareSurface.GauzeFoldMaximum||
                MathF.Abs(p.X-basis.X*(.048f/.115f))>.000001f||MathF.Abs(p.Z-basis.Z*(.042f/.075f))>.000001f||
                MathF.Abs(p.Y-(oldY-depth))>.000001f||p.Y>oldY+.000001f)return false;
            if((r==0||r>=16)&&depth!=0)return false;
            if(r==0&&MathF.Abs(p.Y-.03f)>.000001f)return false;
            folded|=depth>.000001f;
        }
    }
    return folded&&MathF.Abs(InteractionCareSurface.Pad(1,0).Y-InteractionCareSurface.PadBottom)<.000001f;
});
Check("repair needle reaches the exact tip while its shaft eye and thread clear the cloth plane",()=>
{
    var rotation=System.Numerics.Matrix4x4.CreateRotationX(InteractionCareSurface.NeedleTiltDegrees*MathF.PI/180);
    bool Clear(System.Numerics.Vector3 p,float radius=0)=>System.Numerics.Vector3.Transform(p-InteractionCareSurface.NeedleTip,rotation).Y+.004f-radius>=-.000001f;
    for(int r=0;r<=100;r++)
    {
        float u=r/100f;
        for(int n=0;n<=32;n++)if(!Clear(InteractionCareSurface.Needle(u,n/32f)))return false;
        if(!Clear(InteractionCareSurface.NeedleEye(u),.0017f)||!Clear(InteractionCareSurface.Thread(u),.0015f))return false;
    }
    return System.Numerics.Vector3.Distance(InteractionCareSurface.Needle(0,0),InteractionCareSurface.NeedleTip)<.000001f;
});
Check("pebble and its mineral vein stay above ground inside the prop pick bounds",()=>
{
    bool Inside(System.Numerics.Vector3 p)
    {
        var q=(p-new System.Numerics.Vector3(0,.08f,0))/new System.Numerics.Vector3(.11f,.08f,.09f);
        return p.Y>=0&&q.LengthSquared()<=1.00001f;
    }
    for(int r=0;r<=32;r++)for(int n=0;n<=128;n++)
        if(!Inside(InteractionTreasureSurface.Point(r*MathF.PI/32,n*MathF.Tau/128))||!Inside(InteractionTreasureSurface.Vein(r/32f,n/128f)))return false;
    return true;
});
Check("beveled fixture panels preserve their authored bounds and outward unit normals",()=>
{
    foreach(var size in new[]{new System.Numerics.Vector3(1.8f,.1f,1.72f),new(.1f,1.93f,1.72f),new(.08f,2.3f,.08f),new(.2f,.018f,.14f)})
    for(int face=0;face<6;face++)for(int r=0;r<=14;r++)for(int n=0;n<=14;n++)
    {
        var sample=InteractionFixtureSurface.Sample(face,r/14f,n/14f,size,.018f);var p=sample.Point;
        if(!float.IsFinite(p.LengthSquared())||MathF.Abs(sample.Normal.Length()-1)>.00001f||System.Numerics.Vector3.Dot(p,sample.Normal)<=0)return false;
        if(MathF.Abs(p.X)>size.X*.5f+.000001f||MathF.Abs(p.Y)>size.Y*.5f+.000001f||MathF.Abs(p.Z)>size.Z*.5f+.000001f)return false;
    }
    return true;
});
Check("rounded fixture faces share closed edges and use Godot clockwise outward winding",()=>
{
    var size=new System.Numerics.Vector3(1.8f,.1f,1.72f);
    for(int face=0;face<6;face++)for(int n=0;n<=14;n++)foreach(bool atEnd in new[]{false,true})
    {
        var p=InteractionFixtureSurface.Sample(face,atEnd?1:0,n/14f,size,.018f).Point;bool match=false;
        for(int other=0;other<6;other++)if(other!=face)
            for(int k=0;k<=14;k++)foreach(var uv in new[]{(0f,k/14f),(1f,k/14f),(k/14f,0f),(k/14f,1f)})
                if(System.Numerics.Vector3.Distance(p,InteractionFixtureSurface.Sample(other,uv.Item1,uv.Item2,size,.018f).Point)<.00001f)match=true;
        if(!match)return false;
    }
    for(int face=0;face<6;face++)
    {
        var a=InteractionFixtureSurface.Sample(face,.4f,.4f,size,.018f);
        var b=InteractionFixtureSurface.Sample(face,.6f,.4f,size,.018f);
        var c=InteractionFixtureSurface.Sample(face,.6f,.6f,size,.018f);
        if(System.Numerics.Vector3.Dot(System.Numerics.Vector3.Cross(b.Point-a.Point,c.Point-a.Point),a.Normal)>=0)return false;
    }
    return true;
});
Check("box panels meet without overlapping coplanar strips at posts back or floor",()=>
{
    float panelFront=Yukkuri.Simulation.InteractionFixtureGeometry.BoxSidePanelZ+Yukkuri.Simulation.InteractionFixtureGeometry.BoxSidePanelDepth*.5f;
    float postBack=Yukkuri.Simulation.InteractionFixtureGeometry.BoxFrontZ-.05f;
    float sideInside=Yukkuri.Simulation.InteractionFixtureGeometry.BoxSideX-.05f;
    float floorTop=Yukkuri.Simulation.InteractionFixtureGeometry.BoxFloorY+Yukkuri.Simulation.InteractionFixtureGeometry.BoxFloorThickness*.5f;
    return MathF.Abs(panelFront-postBack)<.000001f&&MathF.Abs(sideInside-Yukkuri.Simulation.InteractionFixtureGeometry.BoxBackWidth*.5f)<.000001f&&MathF.Abs(floorTop-Yukkuri.Simulation.InteractionFixtureGeometry.BoxWallBottom)<.000001f;
});
Check("hop has a visible ballistic clearance and plants its underside through squash",()=>
{
    var apex=ActorHopMotion.Sample((ActorHopMotion.Launch+ActorHopMotion.Touchdown)*.5f);
    if(apex.Lift-.46f*(apex.Scale.Y-1)<.26f)return false;
    for(int n=0;n<=1000;n++)
    {
        float age=ActorHopMotion.Duration*n/1000;
        var f=ActorHopMotion.Sample(age);
        if(MathF.Abs(f.Scale.X*f.Scale.Y*f.Scale.Z-1)>.00001f)return false;
        float clearance=.46f+f.Lift-.46f*f.Scale.Y;
        if(clearance<-.00001f)return false;
        if(age<ActorHopMotion.Launch&&(f.Travel!=0||MathF.Abs(clearance)>.00001f))return false;
        if(age>=ActorHopMotion.Touchdown&&(f.Travel!=1||MathF.Abs(clearance)>.00001f))return false;
        if(n>0)
        {
            var previous=ActorHopMotion.Sample(age-ActorHopMotion.Duration/1000);
            if(MathF.Abs(previous.Lift-f.Lift)>.006f||MathF.Abs(previous.Scale.Y-f.Scale.Y)>.012f)return false;
        }
    }
    return ActorHopMotion.Sample(0).Scale==System.Numerics.Vector3.One&&ActorHopMotion.Sample(ActorHopMotion.Duration).Scale==System.Numerics.Vector3.One;
});
Check("hop finishes on stopping, never bobs at rest, and supports quarter-second simulation updates",()=>
{
    foreach(int fps in new[]{30,60,144})
    {
        var hop=new ActorHopMotion();hop.Reset(System.Numerics.Vector2.Zero);
        var target=System.Numerics.Vector2.Zero;
        for(int frame=1;frame<=fps*5;frame++)
        {
            float time=(float)frame/fps;
            target=new(MathF.Floor(MathF.Min(time,3)*4)*.2f,0);
            var before=hop.Position;float previousTravel=hop.Frame.Travel;
            bool active=hop.Active;
            hop.Advance(target,1f/fps,true,false);
            if(System.Numerics.Vector2.Distance(hop.Position,target)>.85f)return false;
            if(active&&hop.Active&&previousTravel==hop.Frame.Travel&&hop.Position!=before)return false;
        }
        if(hop.Active||System.Numerics.Vector2.Distance(hop.Position,target)>.00001f)return false;
        for(int frame=0;frame<fps;frame++){hop.Advance(target,1f/fps,true,false);if(hop.Active||hop.Frame.Lift!=0)return false;}
    }
    return true;
});
Check("hop freezes on pause and resets cleanly on carry restraint or teleport",()=>
{
    var hop=new ActorHopMotion();hop.Reset(System.Numerics.Vector2.Zero);
    var target=new System.Numerics.Vector2(.4f,0);
    hop.Advance(target,.1f,true,false);hop.Advance(target,.1f,true,false);
    var position=hop.Position;var pose=hop.Frame;
    for(int n=0;n<20;n++)hop.Advance(target,.1f,true,true);
    if(hop.Position!=position||hop.Frame!=pose)return false;
    hop.Advance(target,.016f,false,false);
    if(hop.Active||hop.Position!=target||hop.Frame.Lift!=0)return false;
    target=new(5,-3);hop.Advance(target,.016f,true,false);
    return !hop.Active&&hop.Position==target;
});
Check("paused camera changes preserve an airborne hop but not a real support loss",()=>
{
    var hop=new ActorHopMotion();hop.Reset(System.Numerics.Vector2.Zero);
    var target=new System.Numerics.Vector2(0,.4f);
    hop.Advance(target,.1f,true,false);hop.Advance(target,.1f,true,false);
    var position=hop.Position;var frame=hop.Frame;float heading=hop.Heading;
    if(!hop.Active||frame.Lift<=.1f||frame.Travel<=0||frame.Travel>=1)return false;
    // Repeated view toggles must preserve travel, squash, lift, pitch and heading.
    foreach(bool close in new[]{true,false,true,true,false,true})
    {
        hop.Advance(target,.1f,ActorHopMotion.AllowsView(close,true,true),true);
        if(!hop.Active||hop.Position!=position||hop.Frame!=frame||hop.Heading!=heading)return false;
    }
    // The actor hands the final body transform to its existing smoother before
    // resuming in close-up; the hop must then stop contributing a second offset.
    var offset=new System.Numerics.Vector3(position.X-target.X,frame.Lift,position.Y-target.Y);
    var rotation=System.Numerics.Quaternion.CreateFromYawPitchRoll(heading,frame.Pitch*MathF.PI/180,0);
    var smoothing=new ActorPoseSmoothing();smoothing.BeginFrom(frame.Scale,offset,rotation);
    hop.Advance(target,.016f,ActorHopMotion.AllowsView(true,false,true),false);
    if(hop.Active||hop.Position!=target||smoothing.Offset!=offset||smoothing.Scale!=frame.Scale)return false;
    for(int n=0;n<90;n++)smoothing.Advance(System.Numerics.Vector3.One,System.Numerics.Vector3.Zero,System.Numerics.Quaternion.Identity,1f/60,false);
    if(smoothing.Offset.Length()>.00001f||System.Numerics.Vector3.Distance(smoothing.Scale,System.Numerics.Vector3.One)>.00001f)return false;
    // A freshly created close-up actor cannot manufacture a paused hop.
    if(ActorHopMotion.AllowsView(true,true,false))return false;
    // Death/carry/restraint still pass allowed=false, even in a paused view.
    hop.Reset(System.Numerics.Vector2.Zero);
    hop.Advance(target,.1f,true,false);hop.Advance(target,.1f,true,false);
    hop.Advance(target,.1f,false,true);
    if(hop.Active||hop.Position!=target)return false;
    // Large world-position changes must also retain the original reset rule.
    hop.Reset(System.Numerics.Vector2.Zero);
    hop.Advance(target,.1f,true,false);hop.Advance(target,.1f,true,false);
    var relocated=new System.Numerics.Vector2(5,-3);
    hop.Advance(relocated,.1f,ActorHopMotion.AllowsView(true,true,true),true);
    return !hop.Active&&hop.Position==relocated;
});
Check("hop faces every actual flight direction and turns only while planted",()=>
{
    foreach(int fps in new[]{30,60,144})for(int direction=0;direction<8;direction++)
    {
        var hop=new ActorHopMotion();hop.Reset(System.Numerics.Vector2.Zero);
        float angle=direction*MathF.Tau/8;
        var target=new System.Numerics.Vector2(MathF.Sin(angle),MathF.Cos(angle))*.4f;
        bool moved=false;
        for(int frame=0;frame<fps*2;frame++)
        {
            var previous=hop.Position;float oldHeading=hop.Heading;
            hop.Advance(target,1f/fps,true,false);
            var travel=hop.Position-previous;
            if(travel.LengthSquared()>.00000001f)
            {
                moved=true;
                var forward=new System.Numerics.Vector2(MathF.Sin(hop.Heading),MathF.Cos(hop.Heading));
                if(System.Numerics.Vector2.Dot(System.Numerics.Vector2.Normalize(travel),forward)<.99999f)return false;
            }
            if(hop.Turning&&(travel.LengthSquared()>0||hop.Frame.Lift!=0))return false;
            if(MathF.Abs(MathF.IEEERemainder(hop.Heading-oldHeading,MathF.Tau))>MathF.Tau*1.5f/fps+.00001f)return false;
        }
        if(!moved||hop.Active||MathF.Abs(MathF.IEEERemainder(hop.Heading-angle,MathF.Tau))>.00001f)return false;
    }
    return true;
});
Check("midair target changes wait until landing and paused turns keep their heading",()=>
{
    var hop=new ActorHopMotion();hop.Reset(System.Numerics.Vector2.Zero);
    var forward=new System.Numerics.Vector2(0,.4f);var right=new System.Numerics.Vector2(.4f,.4f);
    hop.Advance(forward,.1f,true,false);
    for(int n=0;n<3;n++)
    {
        hop.Advance(right,.1f,true,false);
        if(hop.Heading!=0||hop.Position.X!=0)return false;
    }
    hop.Advance(right,.1f,true,false);hop.Advance(right,.016f,true,false);
    if(!hop.Turning||hop.Position!=forward)return false;
    float heading=hop.Heading;
    for(int n=0;n<20;n++)hop.Advance(right,.1f,true,true);
    return hop.Turning&&hop.Heading==heading&&hop.Position==forward;
});
Check("rear hair covers the skin continuously and closes its lower hem inside the body",()=>
{
    var skinCenter=new System.Numerics.Vector3(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ);
    var skinRadii=new System.Numerics.Vector3(ActorFaceSurface.RadiusX,ActorFaceSurface.RadiusY,ActorFaceSurface.RadiusZ);
    for(int row=0;row<=100;row++)for(int side=0;side<=160;side++)
    {
        float u=row/100f,v=side/160f;
        var p=ActorHairSurface.Back(u,v);var normal=ActorHairSurface.BackNormal(p);
        if(!float.IsFinite(p.LengthSquared())||((p-skinCenter)/skinRadii).LengthSquared()<=1)return false;
        if(!float.IsFinite(normal.LengthSquared())||System.Numerics.Vector3.Dot(normal,new(p.X,0,p.Z-ActorFaceSurface.CenterZ))<=0)return false;
        if(row==100&&((ActorHairSurface.Back(1,v,true)-skinCenter)/skinRadii).LengthSquared()>=1)return false;
        if(row>0&&System.Numerics.Vector3.Distance(p,ActorHairSurface.Back(u-.01f,v))>.024f)return false;
    }
    return true;
});
Check("hat bow front backing tails and knot clear the crown rather than intersecting it",()=>
{
    bool Clear(System.Numerics.Vector3 p)
    {
        p=ActorAccessorySurface.HatBow(p*ActorAccessorySurface.HatBowScale);
        float t=(p.Y-.028f)/ActorAccessorySurface.HatHeight;
        if(t<0||t>1)return false;
        float x=.025f+.13f*t*t,z=-.02f-.08f*t*t;
        float radius=ActorAccessorySurface.HatCone(t,0).X-x;
        return MathF.Sqrt(MathF.Pow(p.X-x,2)+MathF.Pow(p.Z-z,2))>=radius+.008f;
    }
    foreach(float sign in new[]{-1f,1f})for(int r=0;r<=32;r++)for(int n=0;n<=48;n++)
    {
        float u=r/32f,v=n/48f;
        foreach(var p in new[]{ActorAccessorySurface.Bow(sign,u,v),ActorAccessorySurface.BowTail(sign,u,v)})
            if(!Clear(p)||!Clear(p-System.Numerics.Vector3.UnitZ*ActorAccessorySurface.ClothThickness))return false;
        var knot=ActorAccessorySurface.BowKnot(u,v);
        if(!Clear(knot))return false;
    }
    return true;
});
Check("authored cloth knot closes both poles and its seam inside the original envelope",()=>
{
    var center=new System.Numerics.Vector3(0,0,.045f);var radii=new System.Numerics.Vector3(.08f,.07f,.065f);
    for(int row=0;row<=96;row++)for(int n=0;n<=256;n++)
    {
        float u=row/96f,v=n/256f;var p=ActorAccessorySurface.BowKnot(u,v);
        if(!float.IsFinite(p.LengthSquared())||((p-center)/radii).LengthSquared()>1.00001f)return false;
        if(row==0&&p!=new System.Numerics.Vector3(0,.07f,.045f))return false;
        if(row==96&&p!=new System.Numerics.Vector3(0,-.07f,.045f))return false;
        if(row>0&&p.Y>ActorAccessorySurface.BowKnot((row-1)/96f,v).Y)return false;
        if(ActorAccessorySurface.BowKnot(u,0)!=ActorAccessorySurface.BowKnot(u,1))return false;
        float pleat=ActorAccessorySurface.BowKnotPleat(u,v);
        if(!float.IsFinite(pleat)||pleat<0||pleat>1.00001f||v>.5f&&pleat!=0)return false;
        if(v>0&&v<.5f&&p.Z<center.Z||v>.5f&&v<1&&p.Z>center.Z+.000001f)return false;
    }
    return ActorAccessorySurface.BowKnotPleat(.5f,1f/12)>0;
});
Check("head and hat supports sample every authored knot vertex through the same placement",()=>
{
    var bow=ActorAccessorySupport.Points(false);var hat=ActorAccessorySupport.Points(true);
    int count=(ActorAccessorySurface.BowKnotRows+1)*(ActorAccessorySurface.BowKnotSides+1);
    int index=0;
    for(int row=0;row<=ActorAccessorySurface.BowKnotRows;row++)for(int n=0;n<=ActorAccessorySurface.BowKnotSides;n++)
    {
        var p=ActorAccessorySurface.BowKnot(row/(float)ActorAccessorySurface.BowKnotRows,n/(float)ActorAccessorySurface.BowKnotSides);
        if(System.Numerics.Vector3.Distance(bow[bow.Length-count+index],ActorAccessorySurface.HeadBow(p))>.000001f||
            System.Numerics.Vector3.Distance(hat[hat.Length-count+index],ActorAccessorySurface.HatBow(p*ActorAccessorySurface.HatBowScale))>.000001f)return false;
        index++;
    }
    return true;
});
Check("visible hat brim clears the crown hair during hops pressure and damaged tilt",()=>
{
    var shapes=new[]{System.Numerics.Vector3.One,ActorTouchShape.Press(1),ActorTouchShape.Squeeze(1),ActorHopMotion.Sample(.052f).Scale,ActorHopMotion.Sample(.4175f).Scale,ActorHopMotion.Sample(.08f).Scale};
    foreach(var scale in shapes)foreach(float damage in new[]{0f,12f})
    for(int row=0;row<=36;row++)for(int n=0;n<192;n++)
    {
        var local=ActorAccessorySurface.HatBrimUnderside(.55f+.45f*row/36,n*MathF.Tau/192);
        if(MathF.Pow(local.X-.025f,2)+MathF.Pow(local.Z+.02f,2)<.416f*.416f)continue;
        var p=ActorAccessoryPlacement.Point(local,true,scale,damage);
        var center=ActorSurfaceMarks.HairCenter*scale;var radii=ActorSurfaceMarks.HairRadii*scale;
        float q=MathF.Pow((p.X-center.X)/radii.X,2)+MathF.Pow((p.Z-center.Z)/radii.Z,2);
        if(q<1&&p.Y<center.Y+radii.Y*MathF.Sqrt(1-q)+.001f)return false;
    }
    return true;
});
Check("new rear hair remains clear of the cushion when tilted or rolled",()=>
{
    var bed=new System.Numerics.Vector3(0,-.46f,0);
    foreach(float size in new[]{.73f,1,1.03f})foreach(float angle in new[]{0f,.6f,1.8f,3.14f})
    {
        var rotation=System.Numerics.Quaternion.CreateFromYawPitchRoll(.4f,angle,.2f);
        var scale=new System.Numerics.Vector3(size);
        float lift=ActorCushionSurface.HairLift(bed,System.Numerics.Vector3.Zero,scale,rotation);
        foreach(var p in ActorHairSurface.BackSupport)
        {
            var world=System.Numerics.Vector3.Transform(p*scale,rotation)+System.Numerics.Vector3.UnitY*lift;
            float top=ActorCushionSurface.TopHeight(world.X,world.Z);
            if(top>0&&world.Y<bed.Y+top+.006f)return false;
        }
    }
    return true;
});
Check("head ribbon wings and tails clear the hair without collapsing their front and backing",()=>
{
    var rotation=ActorAccessoryPlacement.Rotation(ActorAccessoryPlacement.BowRotation);
    foreach(float sign in new[]{-1f,1f})for(int row=0;row<=32;row++)for(int n=0;n<=48;n++)
    foreach(var cloth in new[]{ActorAccessorySurface.Bow(sign,row/32f,n/48f),ActorAccessorySurface.BowTail(sign,row/32f,n/48f)})
    {
        var front=ActorAccessorySurface.HeadBow(cloth);
        var back=ActorAccessorySurface.HeadBow(cloth-System.Numerics.Vector3.UnitZ*ActorAccessorySurface.ClothThickness);
        if(System.Numerics.Vector3.Distance(front,back)<.003f){Console.WriteLine($"Ribbon thickness collapsed at {cloth}: {System.Numerics.Vector3.Distance(front,back)}");return false;}
        foreach(var p in new[]{front,back})
        {
            var placed=System.Numerics.Vector3.Transform(p,rotation)+ActorAccessoryPlacement.BowOrigin;
            if(((placed-ActorSurfaceMarks.HairCenter)/ActorSurfaceMarks.HairRadii).LengthSquared()<=1){Console.WriteLine($"Ribbon enters hair at {placed} from {cloth}");return false;}
        }
    }
    return true;
});
Check("crown and rear form a single round profile without a pinched horizontal join",()=>
{
    foreach(float v in new[]{.1f,.3f,.5f,.7f,.9f})for(int row=1;row<100;row++)
    {
        float Radius(float u)
        {
            var p=ActorHairSurface.Back(u,v)-ActorSurfaceMarks.HairCenter;
            return MathF.Sqrt(p.X*p.X+p.Z*p.Z);
        }
        float u=row/100f;
        if(Radius(u)+.00001f<(Radius(u-.01f)+Radius(u+.01f))*.5f)return false;
    }
    for(int row=0;row<=32;row++)
        if(System.Numerics.Vector3.Distance(ActorHairSurface.Shell(row/32f,0),ActorHairSurface.Shell(row/32f,1))>.00001f)return false;
    return true;
});
Check("ground eating and drinking lower the mouth toward the actual source heading",()=>
{
    foreach(string pose in new[]{"eat","sip"})foreach(float heading in new[]{0f,MathF.PI*.5f,MathF.PI,-MathF.PI*.5f})
    {
        var motion=ActorNaturalPose.Sample(pose,.5f,heading,true,pose=="sip"?Yukkuri.Simulation.IntakeKind.Water:Yukkuri.Simulation.IntakeKind.Meal,.9)!.Value;
        var rotation=System.Numerics.Quaternion.CreateFromYawPitchRoll(motion.RotationDegrees.Y*MathF.PI/180,motion.RotationDegrees.X*MathF.PI/180,0);
        var mouth=new System.Numerics.Vector3(0,-.175f,ActorFaceSurface.Height(0,-.175f));
        var lower=System.Numerics.Vector3.Transform(mouth*motion.Scale,rotation)+motion.Offset;
        if(lower.Y>=mouth.Y-.03f||System.Numerics.Vector3.Dot(motion.Offset,new(MathF.Sin(heading),0,MathF.Cos(heading)))<.035f)return false;
        if(Math.Abs(Math.IEEERemainder(motion.RotationDegrees.Y*Math.PI/180-heading,Math.Tau))>.0001)return false;
    }
    return true;
});
Check("sickness and resting poses stay bounded and have continuous gentle breathing",()=>
{
    foreach(string pose in new[]{"rest","stomach"})foreach(double severity in new[]{0,40,85})
    {
        var previous=ActorNaturalPose.Sample(pose,0,severity:severity)!.Value;
        for(int n=1;n<=760;n++)
        {
            var next=ActorNaturalPose.Sample(pose,n*.01f,severity:severity)!.Value;
            if(next.Scale.Y<.85f||next.Scale.Y>1||next.Scale.X>1.1f||System.Numerics.Vector3.Distance(previous.Scale,next.Scale)>.001f)return false;
            previous=next;
        }
        var start=ActorNaturalPose.Sample(pose,0,severity:severity)!.Value;
        var end=ActorNaturalPose.Sample(pose,3.8f,severity:severity)!.Value;
        if(System.Numerics.Vector3.Distance(start.Scale,end.Scale)>.00001f)return false;
    }
    return ActorNaturalPose.Sample("slap",0)==null;
});
Check("direct contact deformation follows force without turning a soft touch into collapse",()=>
{
    foreach(bool inward in new[]{false,true})
    {
        float previous=1;
        for(int n=0;n<=100;n++)
        {
            var shape=ActorTouchShape.DirectContact(n/100f,inward);
            if(shape.Y>previous+.00001||shape.Y<.9f||shape.X>1.06f||shape.Z>1.03f)return false;
            previous=shape.Y;
        }
    }
    return ActorTouchShape.DirectContact(.6f,true)!=ActorTouchShape.DirectContact(.6f,false);
});
Check("accessory attention cannot turn the mouth away during intake or direct manipulation",()=>
{
    foreach(string pose in new[]{"eat","sip","rest","stomach","stroke","touch-press","slap","carry","offer"})
        if(ActorNaturalPose.AllowsAccessoryAttention(pose,0))return false;
    foreach(string pose in new[]{"idle","accessory-watch","reach","guard"})
        if(!ActorNaturalPose.AllowsAccessoryAttention(pose,0)||ActorNaturalPose.AllowsAccessoryAttention(pose,.1))return false;
    return true;
});
Check("ground preparation opens a steady mouth before the portion enters and then starts chewing",()=>
{
    var kind=Yukkuri.Simulation.IntakeKind.Meal;double duration=Yukkuri.Simulation.InteractionLife.IntakeDuration(kind);
    for(double age=0;age<=.44;age+=.02)
    {
        var sample=ActorIntakePresentation.Sample(kind,duration-age,false,true);
        if(sample.MouthFrame!=0||sample.Width!=1||sample.Height!=1)return false;
    }
    var eating=ActorIntakePresentation.Sample(kind,duration-.6,false,true);
    return eating.MouthFrame is 1 or 2&&eating.Width>1;
});
Check("every live intake mouth frame follows the continuous face surface",()=>
{
    for(int frame=0;frame<ActorIntakeMouthSurface.FrameCount;frame++)
    foreach(var layer in ActorIntakeMouthSurface.Layers(frame))
    for(int row=0;row<=layer.Rows;row++)for(int column=0;column<=layer.Columns;column++)
    {
        var p=layer.Point(row/(float)layer.Rows,column/(float)layer.Columns);
        if(!float.IsFinite(p.LengthSquared())||p.Y<-.23f||p.Y>-.12f)return false;
        float clearance=p.Z-ActorFaceSurface.Height(p.X,p.Y);
        if(clearance<.011f||clearance>.019f)return false;
    }
    return true;
});
Check("continuous intake keeps preparation still and reads existing mouthful progress without replaying",()=>
{
    foreach(var kind in Enum.GetValues<Yukkuri.Simulation.IntakeKind>())
    {
        double duration=Yukkuri.Simulation.InteractionLife.IntakeDuration(kind);
        var ready=ActorIntakeMouthSurface.ShapeForFrame(kind==Yukkuri.Simulation.IntakeKind.Water?3:0);
        for(int n=0;n<=40;n++)
        {
            var sample=ActorIntakePresentation.SampleMouth(kind,duration-n*.01,false,true);
            if(sample!=ready)return false;
        }
        if(ActorIntakePresentation.SampleMouth(kind,0,false).Active||!ActorIntakePresentation.SampleMouth(kind,0,true).Active)return false;
        var saved=ActorIntakePresentation.SampleMouth(kind,.63,false,true);
        for(int n=0;n<100;n++)if(ActorIntakePresentation.SampleMouth(kind,.63,false,true)!=saved)return false;
    }
    return true;
});
Check("mouth geometry changes continuously through actual intake and swallowing boundaries",()=>
{
    foreach(var kind in Enum.GetValues<Yukkuri.Simulation.IntakeKind>())foreach(bool ground in new[]{false,true})
    {
        double duration=Yukkuri.Simulation.InteractionLife.IntakeDuration(kind);
        var previous=ActorIntakePresentation.SampleMouth(kind,duration,false,ground);
        for(int n=1;n<duration/.002;n++)
        {
            var next=ActorIntakePresentation.SampleMouth(kind,duration-n*.002,false,ground);
            if(!next.Active||next.Width<.0319f||next.Width>.1101f||next.Height<.0119f||next.Height>.0901f||next.Tongue<0||next.Tongue>1)return false;
            if(MathF.Abs(previous.Width-next.Width)>.002f||MathF.Abs(previous.Height-next.Height)>.002f||MathF.Abs(previous.Tongue-next.Tongue)>.06f)return false;
            previous=next;
        }
        foreach(double boundary in new[]{duration-Yukkuri.Simulation.NaturalResources.BiteContactSeconds,.25})
        {
            var before=ActorIntakePresentation.SampleMouth(kind,boundary+.000001,false,ground);
            var after=ActorIntakePresentation.SampleMouth(kind,boundary-.000001,false,ground);
            if(MathF.Abs(before.Width-after.Width)>.00001f||MathF.Abs(before.Height-after.Height)>.00001f||MathF.Abs(before.Tongue-after.Tongue)>.0001f)return false;
        }
    }
    return true;
});
Check("interpolated intake cavity and tongue remain on the face and inside the same changing aperture",()=>
{
    for(int from=0;from<ActorIntakeMouthSurface.FrameCount;from++)for(int to=0;to<ActorIntakeMouthSurface.FrameCount;to++)
    for(int step=0;step<=12;step++)
    {
        var shape=ActorIntakeMouthSurface.Blend(ActorIntakeMouthSurface.ShapeForFrame(from),ActorIntakeMouthSurface.ShapeForFrame(to),step/12f);
        foreach(bool tongue in new[]{false,true})for(int ring=0;ring<=3;ring++)for(int n=0;n<48;n++)
        {
            var p=ActorIntakeMouthSurface.Point(shape,tongue,ring/3f,n*MathF.Tau/48);
            if(!float.IsFinite(p.LengthSquared())||p.Y<-.2201f||p.Y>-.1299f)return false;
            float expected=tongue?.018f:.012f;
            if(MathF.Abs(p.Z-ActorFaceSurface.Height(p.X,p.Y)-expected)>.00001f)return false;
            float x=p.X/(shape.Width*.5f),y=(p.Y+.175f)/(shape.Height*.5f);
            if(x*x+y*y>(tongue?.88f*.88f:1)+.0001f)return false;
        }
    }
    return true;
});
Check("supported mouth reaches leaves river meals sweets and cup rims across body sizes",()=>
{
    var hair=new List<System.Numerics.Vector3>(ActorHairSurface.BackSupport.ToArray());
    foreach(float side in new[]{-1f,1f})for(int row=0;row<=20;row++)for(int col=0;col<=32;col++)
        hair.Add(ActorHairSurface.Point(ActorHairSurface.Side(side),row/20f,col/32f));
    foreach(float size in new[]{.73f,1,1.03f})
    foreach(var kind in new[]{Yukkuri.Simulation.RoomObjectKind.Leaves,Yukkuri.Simulation.RoomObjectKind.RiverWater,
        Yukkuri.Simulation.RoomObjectKind.Meal,Yukkuri.Simulation.RoomObjectKind.Sweet,Yukkuri.Simulation.RoomObjectKind.Water})
    foreach(float heading in new[]{0f,1.2f,MathF.PI,-2.4f})
    foreach(float elapsed in new[]{.35f,.55f,.65f})
    {
        bool water=Yukkuri.Simulation.NaturalResources.IsWater(kind);
        var intake=water?Yukkuri.Simulation.IntakeKind.Water:kind==Yukkuri.Simulation.RoomObjectKind.Sweet?Yukkuri.Simulation.IntakeKind.Sweet:Yukkuri.Simulation.IntakeKind.Meal;
        double remaining=Yukkuri.Simulation.InteractionLife.IntakeDuration(intake)-elapsed;
        float distance=.792f*size;
        var pose=ActorNaturalPose.Sample(water?"sip":"eat",elapsed,heading,true,intake,remaining,
            sourceKind:kind,sourceDistance:distance,bodyScale:size)!.Value;
        var rotation=System.Numerics.Quaternion.CreateFromYawPitchRoll(heading,pose.RotationDegrees.X*MathF.PI/180,0);
        var scale=pose.Scale*size;var offset=pose.Offset*size+System.Numerics.Vector3.UnitY*(.46f*size);
        float bottom=ActorBodyBounds.LowestSkinY(scale,rotation,offset);
        foreach(var p in hair)bottom=MathF.Min(bottom,(System.Numerics.Vector3.Transform(p*scale,rotation)+offset).Y);
        float correction=bottom<0?-bottom+.003f:0;
        var mouth=System.Numerics.Vector3.Transform(new System.Numerics.Vector3(0,-.175f,ActorFaceSurface.Height(0,-.175f,.012f))*scale,rotation)+offset+System.Numerics.Vector3.UnitY*correction;
        var target=ActorNaturalPose.SourceMouthTarget(kind,distance);
        var expected=new System.Numerics.Vector3(MathF.Sin(heading)*target.X,target.Y,MathF.Cos(heading)*target.X);
        if(System.Numerics.Vector3.Distance(mouth,expected)>.005f)
        {Console.WriteLine($"Mouth gap {kind}/{size}/{elapsed}: {mouth} vs {expected}");return false;}
        var support=ActorBodyBounds.LowestSkinPoint(scale,rotation,offset);
        if(MathF.Abs(support.Y-ActorBodyBounds.LowestSkinY(scale,rotation,offset))>.00001f)return false;
        if(kind==Yukkuri.Simulation.RoomObjectKind.RiverWater&&
            support.X*MathF.Sin(heading)+support.Z*MathF.Cos(heading)>distance-.04f)return false;
    }
    return true;
});
Check("intake bends into contact and returns smoothly using its saved mouthful progress",()=>
{
    foreach(var kind in new[]{Yukkuri.Simulation.IntakeKind.Meal,Yukkuri.Simulation.IntakeKind.Water})
    {
        string name=kind==Yukkuri.Simulation.IntakeKind.Water?"sip":"eat";
        double duration=Yukkuri.Simulation.InteractionLife.IntakeDuration(kind);
        var previous=ActorNaturalPose.Sample(name,0,groundIntake:true,intakeKind:kind,remaining:duration)!.Value;
        if(previous.Offset.Length()>.00001f||MathF.Abs(previous.RotationDegrees.X)>.00001f)return false;
        for(int n=1;n<=(int)(duration*500);n++)
        {
            double remaining=Math.Max(0,duration-n*.002);
            var next=ActorNaturalPose.Sample(name,n*.002f,groundIntake:true,intakeKind:kind,remaining:remaining)!.Value;
            if(System.Numerics.Vector3.Distance(previous.Offset,next.Offset)>.012f||MathF.Abs(previous.RotationDegrees.X-next.RotationDegrees.X)>1.1f)return false;
            if(ActorNaturalPose.Sample(name,100,groundIntake:true,intakeKind:kind,remaining:remaining)!.Value!=next)return false;
            previous=next;
        }
        if(previous.Offset.Length()>.00001f)return false;
    }
    return true;
});
Check("rotated hair support uses actual vertices instead of empty bounding box corners",()=>
{
    var points=ActorHairSurface.BackSupport.ToArray();
    var rotation=System.Numerics.Matrix4x4.CreateRotationX(MathF.PI/4);
    var row=new System.Numerics.Vector3(rotation.M12,rotation.M22,rotation.M32);
    float exact=ActorMeshSupport.LowestY(points,row,0);
    float expected=points.Min(p=>System.Numerics.Vector3.Transform(p,rotation).Y);
    var min=new System.Numerics.Vector3(points.Min(p=>p.X),points.Min(p=>p.Y),points.Min(p=>p.Z));
    var max=new System.Numerics.Vector3(points.Max(p=>p.X),points.Max(p=>p.Y),points.Max(p=>p.Z));
    float box=float.PositiveInfinity;
    for(int n=0;n<8;n++)box=MathF.Min(box,System.Numerics.Vector3.Transform(new System.Numerics.Vector3((n&1)==0?min.X:max.X,(n&2)==0?min.Y:max.Y,(n&4)==0?min.Z:max.Z),rotation).Y);
    return MathF.Abs(exact-expected)<.00001f&&exact>box+.15f;
});
Check("a natural appeal turns toward the perceived hand and stays gently raised",()=>
{
    for(int n=0;n<16;n++)
    {
        float heading=n*MathF.Tau/16;
        var pose=ActorNaturalPose.Sample("help-look",.5f,heading)!.Value;
        float yaw=pose.RotationDegrees.Y*MathF.PI/180;
        if(MathF.Abs(MathF.IEEERemainder(yaw-heading,MathF.Tau))>.00001f||pose.RotationDegrees.X>=0||pose.Scale.Y<1)return false;
    }
    return true;
});
Check("airborne and planted hop phases hand over to intake without discarding their visible pose",HopIntakeAudit.Run);
Check("live pose smoothing reaches intake contact consistently across frame schedules",()=>
{
    foreach(var kind in new[]{Yukkuri.Simulation.IntakeKind.Meal,Yukkuri.Simulation.IntakeKind.Water})
    {
        var ends=new List<System.Numerics.Vector3>();
        foreach(var schedule in new[]{new[]{1/30f},new[]{1/60f},new[]{1/144f},new[]{.01f,.035f,.008f,.019f,.041f}})
        {
            var smoothing=new ActorPoseSmoothing();smoothing.Advance(System.Numerics.Vector3.One,System.Numerics.Vector3.Zero,System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY,.5f),0,false);
            float time=0;int frame=0;
            float contactTime=(float)Yukkuri.Simulation.NaturalResources.BiteContactSeconds;
            while(time<contactTime)
            {
                float dt=MathF.Min(schedule[frame++%schedule.Length],contactTime-time);time+=dt;
                var motion=ActorNaturalPose.Sample(kind==Yukkuri.Simulation.IntakeKind.Water?"sip":"eat",time,
                    groundIntake:true,intakeKind:kind,remaining:Yukkuri.Simulation.InteractionLife.IntakeDuration(kind)-time)!.Value;
                smoothing.Advance(motion.Scale,motion.Offset,System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitX,motion.RotationDegrees.X*MathF.PI/180),dt,false,true);
            }
            var offset=smoothing.Offset+System.Numerics.Vector3.UnitY*.46f;
            var mouth=System.Numerics.Vector3.Transform(new System.Numerics.Vector3(0,-.175f,ActorFaceSurface.Height(0,-.175f,.012f))*smoothing.Scale,smoothing.Rotation)+offset;
            mouth.Y+=MathF.Max(0,-ActorBodyBounds.LowestSkinY(smoothing.Scale,smoothing.Rotation,offset));
            var target=ActorNaturalPose.SourceMouthTarget(kind==Yukkuri.Simulation.IntakeKind.Water?Yukkuri.Simulation.RoomObjectKind.RiverWater:Yukkuri.Simulation.RoomObjectKind.Leaves,.792f);
            if(System.Numerics.Vector3.Distance(mouth,new(0,target.Y,target.X))>.005f)
            {Console.WriteLine($"Smoothed contact gap {kind}/{schedule[0]}: {mouth} vs {target}");return false;}
            ends.Add(mouth);
            var before=(smoothing.Scale,smoothing.Offset,smoothing.Rotation);
            smoothing.Advance(System.Numerics.Vector3.One*2,System.Numerics.Vector3.One,System.Numerics.Quaternion.Identity,1,true);
            if(before!=(smoothing.Scale,smoothing.Offset,smoothing.Rotation))
            {Console.WriteLine($"Paused smoothing changed {kind}/{schedule[0]}: {before} to {(smoothing.Scale,smoothing.Offset,smoothing.Rotation)}");return false;}
        }
        foreach(var end in ends)if(System.Numerics.Vector3.Distance(end,ends[0])>.003f)
        {Console.WriteLine($"Frame schedule gap {kind}: {end} vs {ends[0]}");return false;}
    }
    return true;
});
Check("supported intake reaches raised supplies on box floors cushions and soft wrapping",()=>
{
    bool ok=true;float worst=0;
    foreach(string environment in new[]{"ground","box","wrap","cushion","cushion-wrap"})
    foreach(float size in new[]{.73f,1f,1.03f})
    foreach(var kind in new[]{Yukkuri.Simulation.RoomObjectKind.Meal,Yukkuri.Simulation.RoomObjectKind.Sweet,Yukkuri.Simulation.RoomObjectKind.Water})
    foreach(int fps in new[]{0,30,144})
    {
        var state=Yukkuri.Simulation.WorldSimulation.CreateInteractionPlaytest().State;var c=state.Creatures[0];
        state.Creatures.RemoveAll(o=>o.Id!=c.Id);state.RoomObjects.Clear();c.X=0;c.Z=0;
        c.IsYoung=size<1;c.IsElder=size>1;
        c.Interaction.Restraint=environment=="box"?Yukkuri.Simulation.RestraintKind.Box:environment.Contains("wrap")?Yukkuri.Simulation.RestraintKind.Wrap:Yukkuri.Simulation.RestraintKind.None;
        if(environment.Contains("cushion"))state.RoomObjects.Add(new(){Kind=Yukkuri.Simulation.RoomObjectKind.Cushion,X=0,Z=0});
        float distance=.55f*size,height=(float)Yukkuri.Simulation.RoomSupplySupport.Height(state,c.RegionId,0,distance,kind);
        var intake=kind==Yukkuri.Simulation.RoomObjectKind.Water?Yukkuri.Simulation.IntakeKind.Water:kind==Yukkuri.Simulation.RoomObjectKind.Sweet?Yukkuri.Simulation.IntakeKind.Sweet:Yukkuri.Simulation.IntakeKind.Meal;
        var support=new ActorNaturalSupport(c,state.RoomObjects,size);
        var motion=ActorNaturalPose.Sample(intake==Yukkuri.Simulation.IntakeKind.Water?"sip":"eat",.5f,groundIntake:true,
            intakeKind:intake,remaining:Yukkuri.Simulation.InteractionLife.IntakeDuration(intake)-.5,sourceKind:kind,sourceDistance:distance,sourceHeight:height,bodyScale:size,support:support)!.Value;
        var rotation=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitX,motion.RotationDegrees.X*MathF.PI/180);
        if(fps>0)
        {
            var transition=new ActorPoseSmoothing();transition.Advance(System.Numerics.Vector3.One,System.Numerics.Vector3.Zero,System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY,MathF.PI),0,false);
            float end=(float)Yukkuri.Simulation.NaturalResources.BiteContactSeconds;
            for(float time=0;time<end;)
            {
                float dt=MathF.Min(1f/fps,end-time);time+=dt;
                var p=ActorNaturalPose.Sample(intake==Yukkuri.Simulation.IntakeKind.Water?"sip":"eat",time,groundIntake:true,
                    intakeKind:intake,remaining:Yukkuri.Simulation.InteractionLife.IntakeDuration(intake)-time,sourceKind:kind,sourceDistance:distance,sourceHeight:height,bodyScale:size,support:support)!.Value;
                transition.Advance(p.Scale,p.Offset,System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitX,p.RotationDegrees.X*MathF.PI/180),dt,false,true);
            }
            motion=motion with{Scale=transition.Scale,Offset=transition.Offset};rotation=transition.Rotation;
        }
        float lift=support.Lift(motion.Scale,rotation,motion.Offset);
        var mouth=(System.Numerics.Vector3.Transform(new System.Numerics.Vector3(0,-.175f,ActorFaceSurface.Height(0,-.175f,.012f))*motion.Scale,rotation)+motion.Offset+System.Numerics.Vector3.UnitY*(.46f+lift))*size;
        var target=ActorNaturalPose.SourceMouthTarget(kind,distance,height);float gap=System.Numerics.Vector3.Distance(mouth,new(0,target.Y,target.X));
        worst=MathF.Max(worst,gap);if(gap>.005f){ok=false;Console.WriteLine($"Supported intake gap {environment}/{size}/{kind}/{fps}: {gap:F6}");}
    }
    Console.WriteLine($"Maximum supported intake gap {worst:F6}");return ok;
});
Check("simulation source contacts match the actual leaves food and cup geometry",()=>
{
    var pairs=new[]{
        (Yukkuri.Simulation.RoomObjectKind.Leaves,WoodlandSurface.LeafBiteHeight),
        (Yukkuri.Simulation.RoomObjectKind.RiverWater,WoodlandSurface.WaterHeight),
        (Yukkuri.Simulation.RoomObjectKind.Meal,InteractionFoodSurface.Meal(0,0).Y+InteractionFoodSurface.MealGroundOffset),
        (Yukkuri.Simulation.RoomObjectKind.Sweet,InteractionFoodSurface.CookieTopHeight(0)+InteractionFoodSurface.SweetGroundOffset),
        (Yukkuri.Simulation.RoomObjectKind.Water,InteractionToolSurface.GroundCupLift+InteractionToolSurface.CupTop)};
    foreach(var (kind,height) in pairs)
        if(MathF.Abs(Yukkuri.Simulation.NaturalIntakeGeometry.SourceTarget(kind,.8f).Y-height)>.000001f)return false;
    return Yukkuri.Simulation.NaturalIntakeGeometry.CupRimRadius==InteractionToolSurface.CupRimRadius;
});
Check("permitted wrapped intake reaches its source while low river water remains physically out of reach",()=>
{
    foreach(float size in new[]{.73f,1f,1.03f})
    foreach(var kind in new[]{Yukkuri.Simulation.RoomObjectKind.Leaves,Yukkuri.Simulation.RoomObjectKind.RiverWater,
        Yukkuri.Simulation.RoomObjectKind.Meal,Yukkuri.Simulation.RoomObjectKind.Sweet,Yukkuri.Simulation.RoomObjectKind.Water})
    {
        var state=Yukkuri.Simulation.WorldSimulation.CreateInteractionPlaytest().State;var c=state.Creatures[0];
        state.RoomObjects.Clear();c.X=0;c.Z=0;c.IsYoung=size<1;c.IsElder=size>1;c.Interaction.Restraint=Yukkuri.Simulation.RestraintKind.Wrap;
        var source=new Yukkuri.Simulation.RoomObject{Kind=kind,X=0,Z=.792f*size};
        var target=ActorNaturalPose.SourceMouthTarget(kind,(float)source.Z);
        bool allowed=Yukkuri.Simulation.NaturalIntakeGeometry.ClearsBodySupport(c,source);
        var intake=Yukkuri.Simulation.NaturalResources.IsWater(kind)?Yukkuri.Simulation.IntakeKind.Water:Yukkuri.Simulation.IntakeKind.Meal;
        var support=new ActorNaturalSupport(c,state.RoomObjects,size);float maximumGap=0;
        foreach(float end in new[]{.45f,.55f,.65f})
        {
            var transition=new ActorPoseSmoothing();transition.Advance(System.Numerics.Vector3.One,System.Numerics.Vector3.Zero,System.Numerics.Quaternion.Identity,0,false);
            for(float time=0;time<end;)
            {
                float dt=MathF.Min(1/60f,end-time);time+=dt;
                var motion=ActorNaturalPose.Sample(intake==Yukkuri.Simulation.IntakeKind.Water?"sip":"eat",time,groundIntake:true,intakeKind:intake,
                    remaining:Yukkuri.Simulation.InteractionLife.IntakeDuration(intake)-time,sourceKind:kind,sourceDistance:source.Z,bodyScale:size,support:support)!.Value;
                transition.Advance(motion.Scale,motion.Offset,System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitX,motion.RotationDegrees.X*MathF.PI/180),dt,false,true);
            }
            float lift=support.Lift(transition.Scale,transition.Rotation,transition.Offset);
            var mouth=(System.Numerics.Vector3.Transform(new System.Numerics.Vector3(0,-.175f,ActorFaceSurface.Height(0,-.175f,.012f))*transition.Scale,transition.Rotation)+transition.Offset+System.Numerics.Vector3.UnitY*(.46f+lift))*size;
            maximumGap=MathF.Max(maximumGap,System.Numerics.Vector3.Distance(mouth,new(0,target.Y,target.X)));
        }
        Console.WriteLine($"Wrapped reach {size}/{kind}: allowed={allowed}, gap={maximumGap:F6}, lowerBound={Yukkuri.Simulation.NaturalIntakeGeometry.LowestSupportedMouth(c,kind):F6}");
        if(allowed&&maximumGap>.005f)return false;
        if(kind==Yukkuri.Simulation.RoomObjectKind.RiverWater&&(allowed||maximumGap<.02f))return false;
    }
    return true;
});
Check("tree picking blocks actual trunks and crowns but leaves their empty corners open",WoodlandOcclusionAudit.Rays);
Check("tree picking preserves props in front and occludes props behind the visible trunk",WoodlandOcclusionAudit.DepthOrder);
Check("cached tree picking performs repeated rays without per-query allocations",WoodlandOcclusionAudit.Allocation);
Check("woodland water banks and flow traces face upward while tree trunks face outward",()=>
{
    System.Numerics.Vector3 Normal(WoodlandPatch p,float u,float v)
    {
        const float h=.0005f;
        var du=p.Point(u+h,v)-p.Point(u-h,v);var dv=p.Point(u,v+h)-p.Point(u,v-h);
        return System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(dv,du))*(p.Flip?-1:1);
    }
    foreach(var patch in WoodlandSurface.River().Where(p=>p.Name is "west river bank" or "river surface"||p.Name.StartsWith("flow trace")))
        for(int n=1;n<10;n++)if(Normal(patch,n/10f,.5f).Y<.999f)return false;
    foreach(var patch in WoodlandSurface.Tree(System.Numerics.Vector3.Zero).Where(p=>p.Name=="tree trunk"))
        for(int n=0;n<16;n++)
        {
            float v=n/16f;var p=patch.Point(.5f,v);var trunk=new Yukkuri.Simulation.WoodlandTree(0,0,1).TrunkAt(p.Y);
            if(System.Numerics.Vector3.Dot(Normal(patch,.5f,v),System.Numerics.Vector3.Normalize(new(p.X-(float)trunk.X,0,p.Z-(float)trunk.Z)))<.95f)return false;
        }
    return true;
});
Check("river flow ribbons have a consistent cross-stream width instead of folding along themselves",()=>
{
    foreach(var patch in WoodlandSurface.River().Where(p=>p.Name.StartsWith("flow trace")))
        for(int n=0;n<=12;n++)
        {
            var width=patch.Point(n/12f,1)-patch.Point(n/12f,0);
            if(MathF.Abs(width.X-.016f)>.000002f||MathF.Abs(width.Z)>.000002f)return false;
        }
    return true;
});
Check("tree ball collisions cover the actual leaning trunk at every sphere cross section",()=>
{
    float radius=InteractionBallSurface.Radius;
    foreach(var tree in Yukkuri.Simulation.WoodlandLayout.Trees)
    {
        var middle=tree.TrunkAt(radius);double centerX=middle.X-middle.Radius-radius-.006;
        var patch=WoodlandSurface.Tree(new((float)tree.X,0,(float)tree.Z),(float)tree.Scale).First();
        for(int n=0;n<=100;n++)
        {
            double height=radius*2*n/100,sphereRadius=Math.Sqrt(Math.Max(0,radius*radius-Math.Pow(height-radius,2)));
            var trunk=tree.TrunkAt(height);
            if(centerX+sphereRadius>trunk.X-trunk.Radius+.00001)return false;
            var visible=patch.Point((float)(height/(1.6*tree.Scale)),.5f);
            if(Math.Abs(visible.X-(trunk.X-trunk.Radius))>.00001||Math.Abs(visible.Y-height)>.00001)return false;
        }
    }
    return true;
});
Check("a floating ball stays supported by the curved bank and its picking sphere follows its visible height",()=>
{
    var state=Yukkuri.Simulation.WorldSimulation.CreateInteractionPlaytest().State;float radius=InteractionBallSurface.Radius;
    foreach(double z in new[]{-4.0,0,2.3,5})
    for(int n=0;n<=32;n++)
    {
        var ball=new Yukkuri.Simulation.RoomObject{Kind=Yukkuri.Simulation.RoomObjectKind.Ball,X=Yukkuri.Simulation.WoodlandLayout.RiverEdge(z)-.1+n*.015,Z=z};
        float center=Yukkuri.Simulation.RoomBallEnvironment.CenterHeight(state,ball);
        for(int row=0;row<=24;row++)for(int col=0;col<48;col++)
        {
            var p=InteractionBallSurface.Point(row/24f,col*MathF.Tau/48);
            if(ball.X+p.X<Yukkuri.Simulation.WoodlandLayout.RiverEdge(ball.Z+p.Z)&&center+p.Y<-.00001)return false;
        }
        if(!InteractionPropContact.RayHit(ball.Kind,System.Numerics.Vector3.Zero,new(0,1,0),-System.Numerics.Vector3.UnitY,out float distance,center)||
            MathF.Abs(distance-(1-center-radius))>.00001)return false;
    }
    return true;
});
Check("optimized cushion support matches the full original surface scan across varied body poses",()=>
{
    var random=new Random(20261029);float maximum=0;
    for(int sample=0;sample<160;sample++)
    {
        float Rand(float lo,float hi)=>lo+(hi-lo)*(float)random.NextDouble();
        var bed=new System.Numerics.Vector3(.3f,.05f,-.2f);
        var origin=bed+new System.Numerics.Vector3(Rand(-1.3f,1.3f),Rand(.25f,.9f),Rand(-1.2f,1.2f));
        var scale=new System.Numerics.Vector3(Rand(.65f,1.2f),Rand(.64f,1.2f),Rand(.7f,1.2f));
        var rotation=System.Numerics.Quaternion.CreateFromYawPitchRoll(Rand(-MathF.PI,MathF.PI),Rand(-1.85f,1.15f),Rand(-.8f,.8f));
        var inverse=System.Numerics.Quaternion.Inverse(rotation);var radii=new System.Numerics.Vector3(ActorFaceSurface.RadiusX,ActorFaceSurface.RadiusY,ActorFaceSurface.RadiusZ);
        var center=origin+System.Numerics.Vector3.Transform(new System.Numerics.Vector3(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ)*scale,rotation);
        var vertical=System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitY,inverse)/scale/radii;
        float a=vertical.LengthSquared(),reference=0;
        for(int x=0;x<=48;x++)for(int z=0;z<=36;z++)
        {
            float px=ActorCushionSurface.RadiusX*(x*2f/48-1),pz=ActorCushionSurface.RadiusZ*(z*2f/36-1),height=ActorCushionSurface.TopHeight(px,pz);
            if(height<=0)continue;var point=bed+new System.Numerics.Vector3(px,height,pz);
            var flat=System.Numerics.Vector3.Transform(new System.Numerics.Vector3(point.X-center.X,0,point.Z-center.Z),inverse)/scale/radii;
            float b=2*System.Numerics.Vector3.Dot(vertical,flat),c=flat.LengthSquared()-1,discriminant=b*b-4*a*c;
            if(discriminant<0)continue;
            float bottom=center.Y+(-b-MathF.Sqrt(discriminant))/(2*a);reference=MathF.Max(reference,point.Y-bottom+.004f);
        }
        float actual=ActorCushionSurface.SkinLift(bed,origin,scale,rotation),difference=MathF.Abs(actual-reference);maximum=MathF.Max(maximum,difference);
        if(difference>.00005f){Console.WriteLine($"Support scan mismatch {sample}: {actual} vs {reference}");return false;}
    }
    Console.WriteLine($"Maximum original/optimized cushion support difference {maximum:E3}");return true;
});
var report=new{at=DateTimeOffset.UtcNow,scope="Offline surface geometry and motion only; visual/performance acceptance pending",success=all,checks};
if(args.Length>0){Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);File.WriteAllText(args[0],JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));}
return all?0:1;
