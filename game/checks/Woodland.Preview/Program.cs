using System.Numerics;
using System.Text.Json;
using Yukkuri.Simulation;

if(args.Length==0)throw new ArgumentException("Supply an output file; this exports production geometry without running Godot.");
var patches=new List<WoodlandPatch>();
bool leaves=args.Contains("--leaves"),tree=args.Contains("--tree"),riverBall=args.Contains("--river-ball");
object? ballEvidence=null,layoutEvidence=null;
if(riverBall)
{
    var sim=WorldSimulation.CreateInteractionPlaytest();sim.State.Creatures.Clear();sim.State.SelectedId=null;
    sim.State.RoomObjects.RemoveAll(o=>o.Kind!=RoomObjectKind.RiverWater);
    var ball=new RoomObject{Kind=RoomObjectKind.Ball,X=WoodlandLayout.DrinkX-.4,Z=WoodlandLayout.BankZ,VelocityX=3};sim.State.RoomObjects.Add(ball);
    for(int n=0;n<12;n++){sim.AdvanceInteractionPresentation(.1);sim.Advance(.1);}
    float center=RoomBallEnvironment.CenterHeight(sim.State,ball);var origin=new Vector3((float)ball.X,0,(float)ball.Z);
    ballEvidence=new{seconds=1.2,ball.X,ball.Z,ball.VelocityX,ball.VelocityZ,centerHeight=center,inWater=RoomBallEnvironment.InWater(sim.State,ball)};
    foreach(var patch in WoodlandSurface.River().Where(p=>p.Name is "west river bank" or "river surface"))
        patches.Add(patch with{Rows=32,Point=(u,v)=>patch.Point(((float)ball.Z-1.2f+2.4f*u+8)/16,v)-origin});
    patches.Add(new("dry ground","c6c99b",32,1,(u,v)=>
    {
        float z=(float)ball.Z-1.2f+2.4f*u,edge=(float)WoodlandLayout.RiverEdge(z);
        return new Vector3(edge-1.4f+1.4f*v,-.012f,z)-origin;
    },Flip:true));
    for(int panel=0;panel<InteractionBallSurface.Panels;panel++)
    {
        int index=panel;
        patches.Add(new("soft ball panel "+panel,InteractionBallSurface.Colors[panel],InteractionBallSurface.Rows,InteractionBallSurface.ColumnsPerPanel,
            (u,v)=>InteractionBallSurface.Point(u,(index+v)*MathF.Tau/InteractionBallSurface.Panels)+Vector3.UnitY*center));
    }
}
else if(leaves)patches.AddRange(WoodlandSurface.Leaves(args.Contains("--depleted")?0:8));
else if(tree)patches.AddRange(WoodlandSurface.Tree(Vector3.Zero));
else
{
    patches.AddRange(WoodlandSurface.River());
    foreach(var treeSite in WoodlandLayout.Trees)
        patches.AddRange(WoodlandSurface.Tree(new((float)treeSite.X,0,(float)treeSite.Z),(float)treeSite.Scale));
    var state=WorldSimulation.CreateInteractionPlaytest().State;
    layoutEvidence=state.RoomObjects.Where(o=>NaturalResources.IsWild(o.Kind)).Select(o=>new{o.Id,kind=o.Kind.ToString(),o.X,o.Z,o.Height,o.Portions}).ToArray();
    foreach(var source in state.RoomObjects.Where(o=>o.Kind==RoomObjectKind.Leaves))
    {
        var pos=new Vector3((float)source.X,(float)source.Height,(float)source.Z);
        foreach(var patch in WoodlandSurface.Leaves(Math.Min(10,NaturalResources.VisibleFoodPortions(state,source))))
            patches.Add(patch with{Point=(u,v)=>patch.Point(u,v)+pos});
    }
    patches.Add(new("ground","c6c99b",1,1,(u,v)=>new(-8+u*16,-.012f,-8+v*16)));
}
var meshes=new List<object>();int verticesCount=0,facesCount=0;
foreach(var patch in patches)
{
    var vertices=new List<float[]>();var faces=new List<int[]>();
    for(int r=0;r<=patch.Rows;r++)for(int c=0;c<=patch.Columns;c++)
    {
        var p=patch.Point(r/(float)patch.Rows,c/(float)patch.Columns);
        if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z))throw new Exception("Nonfinite "+patch.Name);
        vertices.Add([p.X,p.Y,p.Z]);
    }
    for(int r=0;r<patch.Rows;r++)for(int c=0;c<patch.Columns;c++)
    {
        int a=r*(patch.Columns+1)+c,b=a+patch.Columns+1;
        if(patch.Flip){faces.Add([a,b+1,b]);faces.Add([a,a+1,b+1]);}
        else{faces.Add([a,b,b+1]);faces.Add([a,b+1,a+1]);}
    }
    verticesCount+=vertices.Count;facesCount+=faces.Count;meshes.Add(new{name=patch.Name,color=patch.Color,clockwise=true,vertices,faces});
}
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
File.WriteAllText(args[0],JsonSerializer.Serialize(new{label="Production woodland geometry exported offline; no game rendering",ballEvidence,layoutEvidence,previewScale=riverBall?170:leaves?700:tree?210:36,previewPitch=riverBall?35:leaves?45:tree?12:50,views=new[]{0,38,84},meshes}));
Console.WriteLine($"Exported {patches.Count} production patches, {verticesCount} vertices, {facesCount} triangles. Not a game screenshot.");
