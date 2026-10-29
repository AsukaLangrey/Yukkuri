using System.Numerics;
using System.Text.Json;
using Yukkuri.Simulation;

if(args.Length<1)throw new ArgumentException("Output geometry JSON path required; this never launches the game.");
bool harness=args.Contains("--harness"),wrapped=args.Contains("--wrapped"),cushion=args.Contains("--cushion");
bool marisa=args.Contains("--marisa");
string? naturalPose=args.FirstOrDefault(a=>a.StartsWith("--pose="))?[7..];
if(naturalPose!=null&&naturalPose is not ("rest" or "stomach" or "eat" or "sip" or "help-look"))throw new ArgumentException("Supported source poses: rest, stomach, eat, sip, help-look.");
if(naturalPose!=null&&(harness||args.Contains("--compressed")))throw new ArgumentException("Natural pose preview cannot combine hanging or compression overrides.");
bool groundIntake=naturalPose is "eat" or "sip";
float naturalFacingDegrees=float.Parse(args.FirstOrDefault(a=>a.StartsWith("--facing-deg="))?[13..]??"0",System.Globalization.CultureInfo.InvariantCulture);
if(!float.IsFinite(naturalFacingDegrees)||naturalFacingDegrees!=0&&(groundIntake||naturalPose==null))
    throw new ArgumentException("A facing override is only supported for rest, stomach and help-look previews.");
var sourceKind=args.Contains("--source=meal")?RoomObjectKind.Meal:naturalPose=="sip"?RoomObjectKind.RiverWater:RoomObjectKind.Leaves;
if(sourceKind==RoomObjectKind.Meal&&naturalPose!="eat")throw new ArgumentException("Meal source requires eating pose.");
float sourceDistance=sourceKind==RoomObjectKind.Meal?.55f:.792f;
var previewState=WorldSimulation.CreateInteractionPlaytest().State;var previewCreature=previewState.Creatures[0];
previewState.Creatures.RemoveAll(c=>c.Id!=previewCreature.Id);previewState.RoomObjects.Clear();previewCreature.X=0;previewCreature.Z=0;
previewCreature.Interaction.Restraint=wrapped?RestraintKind.Wrap:RestraintKind.None;
if(cushion)previewState.RoomObjects.Add(new(){Kind=RoomObjectKind.Cushion,X=0,Z=0});
float sourceHeight=sourceKind==RoomObjectKind.Meal?(float)RoomSupplySupport.Height(previewState,previewCreature.RegionId,0,sourceDistance):0;
var previewSupport=new ActorNaturalSupport(previewCreature,previewState.RoomObjects,1,marisa);
var intakeKind=naturalPose=="sip"?IntakeKind.Water:IntakeKind.Meal;
float intakeAge=float.Parse(args.FirstOrDefault(a=>a.StartsWith("--intake-age="))?[13..]??"0.65",System.Globalization.CultureInfo.InvariantCulture);
if(!float.IsFinite(intakeAge)||intakeAge<0)throw new ArgumentException("Intake age must be finite and nonnegative.");
double intakeRemaining=groundIntake?Math.Max(0,InteractionLife.IntakeDuration(intakeKind)-intakeAge):0;
var intakeAppearance=ActorIntakePresentation.Sample(intakeKind,intakeRemaining,false,groundIntake);
var naturalFrame=naturalPose==null?(ReactionMotion?)null:ActorNaturalPose.Sample(naturalPose,intakeAge,facing:naturalFacingDegrees*MathF.PI/180,groundIntake:groundIntake,intakeKind:intakeKind,remaining:intakeRemaining,severity:55,
    sourceKind:sourceKind,sourceDistance:sourceDistance,sourceHeight:sourceHeight,support:previewSupport);
var naturalRotation=naturalFrame is { } frame?Quaternion.CreateFromYawPitchRoll(frame.RotationDegrees.Y*MathF.PI/180,frame.RotationDegrees.X*MathF.PI/180,frame.RotationDegrees.Z*MathF.PI/180):Quaternion.Identity;
bool smoothedIntake=groundIntake&&!args.Contains("--settled");
string? hopStartText=args.FirstOrDefault(a=>a.StartsWith("--from-hop="))?[11..];
float? hopStart=hopStartText==null?null:float.Parse(hopStartText,System.Globalization.CultureInfo.InvariantCulture);
if(hopStart is { } start&&(!float.IsFinite(start)||start<0||start>ActorHopMotion.Duration||!smoothedIntake||wrapped||harness))
    throw new ArgumentException("Hop handoff requires a free, smoothed ground intake and a hop age between 0 and 0.5 seconds.");
if(smoothedIntake)
{
    var transition=new ActorPoseSmoothing();transition.Advance(Vector3.One,Vector3.Zero,Quaternion.Identity,0,false);
    if(hopStart is { } age)
    {
        var hop=ActorHopMotion.Sample(age);
        var rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,hop.Pitch*MathF.PI/180);
        var offset=new Vector3(0,hop.Lift,-.35f);
        offset.Y+=previewSupport.Lift(hop.Scale,rotation,offset);
        transition.BeginFrom(hop.Scale,offset,rotation);
    }
    for(float time=0;time<intakeAge;)
    {
        float step=MathF.Min(1/60f,intakeAge-time);time+=step;
        var pose=ActorNaturalPose.Sample(naturalPose!,time,groundIntake:true,intakeKind:intakeKind,
            remaining:Math.Max(0,InteractionLife.IntakeDuration(intakeKind)-time),sourceKind:sourceKind,sourceDistance:sourceDistance,sourceHeight:sourceHeight,support:previewSupport)!.Value;
        var rotation=Quaternion.CreateFromYawPitchRoll(pose.RotationDegrees.Y*MathF.PI/180,pose.RotationDegrees.X*MathF.PI/180,pose.RotationDegrees.Z*MathF.PI/180);
        transition.Advance(pose.Scale,pose.Offset,rotation,step,false,true);
    }
    naturalFrame=naturalFrame!.Value with{Scale=transition.Scale,Offset=transition.Offset};naturalRotation=transition.Rotation;
}
string hairColor=marisa?"d8ad60":"493330";
var previewDeformation=naturalFrame?.Scale??(args.Contains("--compressed")?ActorHopMotion.Sample(.4175f).Scale:Vector3.One);
float naturalLift=0;
if(naturalFrame is { } supported)
{
    naturalLift=previewSupport.Lift(supported.Scale,naturalRotation,supported.Offset);
}
var pillowOrigin=new Vector3(0,-.46f,0);
float bodyLift=wrapped?MathF.Max(0,.003f-.46f-ActorCradleSurface.LowestY(Vector3.One,Quaternion.Identity,Vector3.Zero)):0;
if(cushion)
{
    bodyLift=MathF.Max(bodyLift,ActorCushionSurface.SkinLift(pillowOrigin,Vector3.Zero,Vector3.One,Quaternion.Identity));
    if(wrapped)bodyLift=MathF.Max(bodyLift,ActorCushionSurface.CradleLift(pillowOrigin,Vector3.Zero,Vector3.One,Quaternion.Identity));
}
if(naturalFrame!=null)bodyLift=0;
bool bodyCoordinates=true,actorCoordinates=true;
EyeCurve eyeCurve=args.Contains("--joy")?EyeCurve.Joy:args.Contains("--relaxed")?EyeCurve.Relaxed:args.Contains("--sleep")||naturalPose=="rest"?EyeCurve.Sleep:EyeCurve.Open;
float eyeOpen=eyeCurve==EyeCurve.Open?1:eyeCurve==EyeCurve.Joy?.20f:.12f;
var meshes=new List<SurfaceMesh>();
float lowestActorVertex=float.PositiveInfinity;
void Surface(string name,string color,int rows,int columns,Func<float,float,Vector3> point)
{
    var vertices=new List<float[]>();var faces=new List<int[]>();
    for(int r=0;r<=rows;r++)for(int c=0;c<=columns;c++)
    {
        var p=point((float)r/rows,(float)c/columns);
        if(bodyCoordinates)
        {
            if(naturalFrame!=null&&name.StartsWith("bow "))p+=ActorAccessoryPlacement.BowOrigin*(previewDeformation-Vector3.One);
            else p*=previewDeformation;
            p.Y+=bodyLift+(naturalFrame==null?.46f*(previewDeformation.Y-1):0);
        }
        if(actorCoordinates&&naturalFrame is { } poseFrame&&!name.StartsWith("cushion"))p=Vector3.Transform(p,naturalRotation)+poseFrame.Offset+Vector3.UnitY*naturalLift;
        if(actorCoordinates)lowestActorVertex=MathF.Min(lowestActorVertex,p.Y);
        vertices.Add([p.X,p.Y,p.Z]);
    }
    for(int r=0;r<rows;r++)for(int c=0;c<columns;c++)
    {int a=r*(columns+1)+c,b=a+columns+1;faces.Add([a,b,b+1]);faces.Add([a,b+1,a+1]);}
    meshes.Add(new(name,color,vertices,faces));
}
void Ellipsoid(string name,string color,Vector3 center,Vector3 radii)=>Surface(name,color,32,64,(u,v)=>
    center+new Vector3(MathF.Sin(u*MathF.PI)*MathF.Cos(v*MathF.Tau),MathF.Cos(u*MathF.PI),MathF.Sin(u*MathF.PI)*MathF.Sin(v*MathF.Tau))*radii);
void Face(string name,string color,float x,float y,float width,float height,float offset=.01f)=>Surface(name,color,5,48,(u,v)=>ActorFaceSurface.Point(x,y,width,height,u,v*MathF.Tau,offset));
Ellipsoid("skin","ffeaca",new(0,ActorFaceSurface.CenterY,ActorFaceSurface.CenterZ),new(ActorFaceSurface.RadiusX,ActorFaceSurface.RadiusY,ActorFaceSurface.RadiusZ));
Surface("hair shell",hairColor,ActorHairSurface.BackRows,ActorHairSurface.BackSides,(u,v)=>ActorHairSurface.Shell(u,v));
Surface("hair hem",hairColor,2,ActorHairSurface.BackSides,(u,v)=>Vector3.Lerp(ActorHairSurface.Shell(1,v),ActorHairSurface.Shell(1,v,true),u));
for(int i=0;i<5;i++){var shape=ActorHairSurface.Fringe(i);Surface("fringe "+i,hairColor,20,32,(u,v)=>ActorHairSurface.Point(shape,u,v));}
foreach(float sign in new[]{-1f,1f})
{
    var shape=ActorHairSurface.Side(sign);Surface("side hair "+sign,hairColor,20,32,(u,v)=>ActorHairSurface.Point(shape,u,v));
    foreach(EyeLayer layer in Enum.GetValues<EyeLayer>())
    {
        if(!ActorEyeSurface.Visible(layer,eyeOpen))continue;
        string color=layer switch{EyeLayer.Iris=>"70503e",EyeLayer.MainLight=>"fff7e7",EyeLayer.SmallLight=>"d8c4a4",_=>"352b2a"};
        Surface("eye "+layer,color,5,48,(u,v)=>ActorEyeSurface.Point(sign,eyeOpen,eyeCurve,layer,u,v*MathF.Tau));
    }
    if(!args.Contains("--cry")&&naturalPose is not ("stomach" or "rest"))Face("cheek","e4a39b",sign*.36f,-.10f,.19f,.075f,.007f);
    float browAngle=args.Contains("--cry")||naturalPose=="stomach"?-sign*17:0,browHeight=args.Contains("--cry")?ActorBrowSurface.FrightHeight:0;
    Surface("brow",hairColor,ActorBrowSurface.Segments,ActorBrowSurface.Across,(u,v)=>ActorBrowSurface.Point(sign,browAngle,browHeight,u,v));
    if(marisa)continue;
    var transform=ActorAccessoryPlacement.Rotation(ActorAccessoryPlacement.BowRotation)*Matrix4x4.CreateTranslation(ActorAccessoryPlacement.BowOrigin);
    Surface("bow wing","b84e51",22,24,(u,v)=>Vector3.Transform(ActorAccessorySurface.HeadBow(ActorAccessorySurface.Bow(sign,u,v)),transform));
    Surface("bow backing","a34347",12,24,(u,v)=>Vector3.Transform(ActorAccessorySurface.HeadBow(ActorAccessorySurface.Bow(sign,u,v)-Vector3.UnitZ*ActorAccessorySurface.ClothThickness),transform));
    Surface("bow tail","b84e51",18,16,(u,v)=>Vector3.Transform(ActorAccessorySurface.HeadBow(ActorAccessorySurface.BowTail(sign,u,v)),transform));
    Surface("bow tail backing","a34347",18,16,(u,v)=>Vector3.Transform(ActorAccessorySurface.HeadBow(ActorAccessorySurface.BowTail(sign,u,v)-Vector3.UnitZ*ActorAccessorySurface.ClothThickness),transform));
}
var bowTransform=ActorAccessoryPlacement.Rotation(ActorAccessoryPlacement.BowRotation)*Matrix4x4.CreateTranslation(ActorAccessoryPlacement.BowOrigin);
if(!marisa)Surface("bow knot","b84e51",24,48,(u,v)=>Vector3.Transform(ActorAccessorySurface.HeadBow(new Vector3(0,0,.045f)+new Vector3(.08f*MathF.Sin(u*MathF.PI)*MathF.Cos(v*MathF.Tau),.07f*MathF.Cos(u*MathF.PI),.065f*MathF.Sin(u*MathF.PI)*MathF.Sin(v*MathF.Tau))),bowTransform));
if(marisa)
{
    bodyCoordinates=false;
    Vector3 Hat(Vector3 p)=>ActorAccessoryPlacement.Point(p,true,previewDeformation)+Vector3.UnitY*(bodyLift+(naturalFrame==null?.46f*(previewDeformation.Y-1):0));
    Surface("hat brim","3e3c46",14,96,(u,v)=>Hat(ActorAccessorySurface.HatBrim(u,v*MathF.Tau)));
    Surface("hat underside","34323c",14,96,(u,v)=>Hat(ActorAccessorySurface.HatBrimUnderside(u,v*MathF.Tau)));
    Surface("hat crown","45434e",36,96,(u,v)=>Hat(ActorAccessorySurface.HatCone(u,v*MathF.Tau)));
    Surface("hat band","e9dfc5",6,96,(u,v)=>Hat(ActorAccessorySurface.HatCone(.045f+u*.16f,v*MathF.Tau)+new Vector3(MathF.Cos(v*MathF.Tau),0,MathF.Sin(v*MathF.Tau))*.006f));
    Vector3 SmallBow(Vector3 p)=>Hat(ActorAccessorySurface.HatBow(p*ActorAccessorySurface.HatBowScale));
    foreach(float sign in new[]{-1f,1f})
    {
        Surface("hat bow wing","f0e8d6",22,24,(u,v)=>SmallBow(ActorAccessorySurface.Bow(sign,u,v)));
        Surface("hat bow backing","f0e8d6",12,24,(u,v)=>SmallBow(ActorAccessorySurface.Bow(sign,u,v)-Vector3.UnitZ*ActorAccessorySurface.ClothThickness));
        Surface("hat bow tail","f0e8d6",18,16,(u,v)=>SmallBow(ActorAccessorySurface.BowTail(sign,u,v)));
        Surface("hat bow tail backing","f0e8d6",18,16,(u,v)=>SmallBow(ActorAccessorySurface.BowTail(sign,u,v)-Vector3.UnitZ*ActorAccessorySurface.ClothThickness));
    }
    Surface("hat bow knot","f0e8d6",16,32,(u,v)=>SmallBow(new Vector3(0,0,.045f)+new Vector3(.08f*MathF.Sin(u*MathF.PI)*MathF.Cos(v*MathF.Tau),.07f*MathF.Cos(u*MathF.PI),.065f*MathF.Sin(u*MathF.PI)*MathF.Sin(v*MathF.Tau))));
    bodyCoordinates=true;
}
if(groundIntake&&intakeAppearance.Active)
{
    foreach(var layer in ActorIntakeMouthSurface.Layers(intakeAppearance.MouthFrame))
        Surface("intake mouth",layer.Color,layer.Rows,layer.Columns,layer.Point);
}
else if(eyeCurve==EyeCurve.Joy||args.Contains("--cry")||naturalPose=="help-look")
{
    Face("mouth","7e4543",0,-.178f,eyeCurve==EyeCurve.Joy?.18f:.12f,eyeCurve==EyeCurve.Joy?.14f:.18f);
    if(eyeCurve==EyeCurve.Joy)Face("tongue","dc8d85",0,-.211f,.115f,.045f,.015f);
}
else Surface("mouth","9b5c4d",40,4,(u,v)=>ActorMouthSurface.Point(eyeCurve==EyeCurve.Sleep?MouthCurve.Flat:naturalPose=="stomach"?MouthCurve.Worry:MouthCurve.Smile,u,v));
void Tube(string name,Func<float,Vector3> path,float radius,string color="9e846b")
{
    Surface(name,color,64,8,(u,v)=>
    {
        var tangent=Vector3.Normalize(path(MathF.Min(1,u+.001f))-path(MathF.Max(0,u-.001f)));
        var axis=MathF.Abs(tangent.Y)>.95f?Vector3.UnitX:Vector3.UnitY;
        var right=Vector3.Normalize(Vector3.Cross(tangent,axis));var up=Vector3.Normalize(Vector3.Cross(tangent,right));
        return path(u)+radius*(right*MathF.Cos(v*MathF.Tau)+up*MathF.Sin(v*MathF.Tau));
    });
}
if(args.Contains("--cry"))
{
    foreach(float side in new[]{-1f,1f})foreach(bool glint in new[]{false,true})
        Surface("tear"+(glint?" glint":""),glint?"e7f2ee":"94c4d1",8,48,(u,v)=>ActorTearSurface.Point(side,u,v*MathF.Tau,glint));
}
if(args.Contains("--dressings"))
{
    foreach(var site in ActorSurfaceMarks.Dressing)foreach(bool pad in new[]{false,true})
        Surface("dressing "+site.Part+" "+site.Side+(pad?" gauze":" tape"),pad?"f3ebd8":"e8ddbf",ActorDressingSurface.Rows,ActorDressingSurface.Sides,(u,v)=>ActorDressingSurface.Point(site,u,v*MathF.Tau,pad));
}
if(harness)
{
    Tube("support belt",u=>ActorHarnessSurface.Belt(u*MathF.Tau),.010f);
    foreach(float side in new[]{-1f,1f})Tube("support strap",u=>ActorHarnessSurface.Strap(side,u),.012f);
    Tube("support yoke",ActorHarnessSurface.Yoke,.012f);
    Ellipsoid("support clasp","726d62",ActorHarnessSurface.Anchor,new(.0325f,.0275f,.0125f));
    Tube("rope attachment",u=>Vector3.Lerp(ActorHarnessSurface.Anchor,new(-.07f,.55f,-.55f),u),.0125f);
}
if(wrapped)
{
    Surface("wrap outer","c3d1c1",ActorCradleSurface.Rows,ActorCradleSurface.Sides,(u,v)=>ActorCradleSurface.Point(u,v*MathF.Tau));
    Surface("wrap lining","e1e6d6",ActorCradleSurface.Rows,ActorCradleSurface.Sides,(u,v)=>ActorCradleSurface.Point(u,v*MathF.Tau,true));
    Tube("wrap hem",u=>ActorCradleSurface.Hem(u*MathF.Tau),ActorCradleSurface.HemRadius,"e8e9d5");
}
if(cushion)
{
    bodyCoordinates=false;actorCoordinates=false;
    Surface("cushion","b8c4a8",ActorCushionSurface.Rows,ActorCushionSurface.Sides,(u,v)=>ActorCushionSurface.Point(u,v)+pillowOrigin);
    Tube("cushion seam",u=>ActorCushionSurface.Seam(u)+pillowOrigin,.007f,"e1dfc2");
}
object? intakeContact=null;
if(groundIntake)
{
    float supportCorrection=MathF.Max(0,-.46f-lowestActorVertex);
    if(supportCorrection>0)
    {
        supportCorrection+=.003f;
        foreach(var mesh in meshes.Where(m=>!m.name.StartsWith("cushion")))foreach(var p in mesh.vertices)p[1]+=supportCorrection;
        naturalLift+=supportCorrection;
    }
    bodyCoordinates=false;actorCoordinates=false;
    if(sourceKind==RoomObjectKind.Meal)
    {
        var origin=new Vector3(0,sourceHeight-.46f+InteractionFoodSurface.MealGroundOffset,sourceDistance);
        Surface("plate inside","eae4cf",12,64,(u,v)=>InteractionFoodSurface.Plate(u,v*MathF.Tau,true)+origin);
        Surface("plate outside","d8d5c4",12,64,(u,v)=>InteractionFoodSurface.Plate(u,v*MathF.Tau,false)+origin);
        Tube("plate rim",u=>InteractionFoodSurface.PlateRim(u*MathF.Tau)+origin,.008f,"f2ecda");
        Surface("meal","e6d3a8",14,64,(u,v)=>InteractionFoodSurface.Meal(u,v*MathF.Tau)+origin);
        Surface("meal bottom","d7c297",8,48,(u,v)=>InteractionFoodSurface.Meal(u,v*MathF.Tau,true)+origin);
        Surface("meal edge","dfcaa0",2,64,(u,v)=>InteractionFoodSurface.MealEdge(u,v*MathF.Tau)+origin);
        Surface("ground","c6c99b",1,1,(u,v)=>new(-1+u*2,-.472f,-1+v*2.5f));
    }
    else if(naturalPose=="eat")
    {
        foreach(var patch in WoodlandSurface.Leaves())
            Surface(patch.Name,patch.Color,patch.Rows,patch.Columns,(u,v)=>patch.Point(u,v)+new Vector3(0,-.46f,sourceDistance));
        Surface("ground","c6c99b",1,1,(u,v)=>new(-1+u*2,-.472f,-1+v*2.5f));
    }
    else
    {
        float bankZ=(float)WoodlandLayout.BankZ,centerX=(float)WoodlandLayout.DrinkX-sourceDistance;
        foreach(var patch in WoodlandSurface.River().Where(p=>p.Name is "west river bank" or "river surface"))
            Surface(patch.Name,patch.Color,24,patch.Columns,(u,v)=>
            {
                var p=patch.Point((bankZ-1+2*u+8)/16,v);
                return new(bankZ-p.Z,p.Y-.46f,p.X-centerX);
            });
        Surface("dry ground","c6c99b",16,1,(u,v)=>
        {
            float x=-1+2*u,edge=(float)WoodlandLayout.RiverEdge(bankZ-x)-centerX;
            return new(x,-.472f,-1+v*(edge+1));
        });
    }
    var mouth=Vector3.Transform(new Vector3(0,-.175f,ActorFaceSurface.Height(0,-.175f,.012f))*previewDeformation,naturalRotation)
        +naturalFrame!.Value.Offset+new Vector3(0,naturalLift+.46f,0);
    var sourceTarget=ActorNaturalPose.SourceMouthTarget(sourceKind,sourceDistance,sourceHeight);
    intakeContact=new{pose=naturalPose,age=intakeAge,remaining=intakeRemaining,frame=intakeAppearance.MouthFrame,smoothedAt60Fps=smoothedIntake,hopStart,
        mouth=new[]{mouth.X,mouth.Y,mouth.Z},sourceCenterDistance=sourceDistance,source=sourceKind.ToString(),wrapped,cushion,
        sourceSurfaceHeight=sourceTarget.Y,gapToSource=Vector3.Distance(mouth,new(0,sourceTarget.Y,sourceTarget.X)),
        note="Production pose, shared smoothing and source geometry. Partial model/support surfaces; no native rendering."};
}
var stats=new List<object>();
for(int i=0;i<5;i++)
{
    var shape=ActorHairSurface.Fringe(i);float minimum=float.PositiveInfinity;int penetrating=0;
    for(int row=80;row<=100;row++)for(int n=0;n<64;n++)
    {var p=ActorHairSurface.Point(shape,row/100f,n/64f);float gap=p.Z-ActorFaceSurface.Height(p.X,p.Y);minimum=MathF.Min(minimum,gap);if(gap<0)penetrating++;}
    stats.Add(new{strand=i,minimumSkinClearance=minimum,penetratingSamples=penetrating});
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
var hopFrames=args.Contains("--hop")?new[]{0f,.052f,.15f,.235f,.34f,.4175f,.50f}.Select(age=>
{
    var f=ActorHopMotion.Sample(age);
    return new{age,scale=new[]{f.Scale.X,f.Scale.Y,f.Scale.Z},lift=f.Lift,pitch=f.Pitch,travel=f.Travel};
}).ToArray():null;
File.WriteAllText(args[0],JsonSerializer.Serialize(new{label="Offline source-surface audit. Partial model, diagnostic shading; NOT a Godot screenshot or visual acceptance.",eyeModel=eyeCurve.ToString(),arguments=args.Skip(1).ToArray(),naturalPose,naturalFacingDegrees,intakeContact,previewPitch=args.Contains("--back")?24:8,views=args.Contains("--back")?new[]{0,115,180,240}:args.Contains("--front")?new[]{0}:harness?new[]{0,70,160}:new[]{0,38,84},hopFrames,bowOrigin=new[]{ActorAccessoryPlacement.BowOrigin.X,ActorAccessoryPlacement.BowOrigin.Y,ActorAccessoryPlacement.BowOrigin.Z},meshes,fringe=stats}));
Console.WriteLine(JsonSerializer.Serialize(stats));
sealed record SurfaceMesh(string name,string color,List<float[]> vertices,List<int[]> faces);

