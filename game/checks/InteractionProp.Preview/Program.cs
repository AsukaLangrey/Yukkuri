using System.Numerics;
using System.Text.Json;
using Yukkuri.Simulation;

if(args.Length==0)throw new ArgumentException("Supply a JSON output path. This program only exports source geometry.");
var meshes=new List<object>();
void Surface(string name,string color,int rows,int columns,Func<float,float,Vector3> point)
{
    var vertices=new List<float[]>();var faces=new List<int[]>();
    for(int row=0;row<=rows;row++)for(int n=0;n<=columns;n++)
    {var p=point(row/(float)rows,n/(float)columns);vertices.Add([p.X,p.Y,p.Z]);}
    for(int row=0;row<rows;row++)for(int n=0;n<columns;n++)
    {int a=row*(columns+1)+n,b=a+columns+1;faces.Add([a,b,b+1]);faces.Add([a,b+1,a+1]);}
    meshes.Add(new{name,color,vertices,faces});
}
void Tube(string name,string color,Func<float,Vector3> path,float radius)
{
    Surface(name,color,64,8,(u,v)=>
    {
        var tangent=Vector3.Normalize(path(MathF.Min(1,u+.001f))-path(MathF.Max(0,u-.001f)));
        var reference=MathF.Abs(tangent.Y)>.95f?Vector3.UnitX:Vector3.UnitY;
        var right=Vector3.Normalize(Vector3.Cross(tangent,reference));var up=Vector3.Normalize(Vector3.Cross(tangent,right));
        return path(u)+radius*(right*MathF.Cos(v*MathF.Tau)+up*MathF.Sin(v*MathF.Tau));
    });
}
void Crumbs(bool chips,bool hand=false)
{
    for(int index=0;index<(chips?InteractionFoodSurface.ChipCount:InteractionFoodSurface.GrainCount);index++)
        Surface("crumb "+index,chips?"75513e":"f0dfb9",6,12,(u,v)=>hand?InteractionFoodSurface.HandCrumbPoint(index,u*MathF.PI,v*MathF.Tau):InteractionFoodSurface.CrumbPoint(index,u*MathF.PI,v*MathF.Tau,chips));
}
bool food=args.Contains("--meal")||args.Contains("--sweet")||args.Contains("--spoon");
void Rounded(string name,string color,Vector3 center,Vector3 size,float bevel=.018f)
{
    for(int face=0;face<6;face++)
        Surface(name+" face "+face,color,InteractionFixtureSurface.Segments,InteractionFixtureSurface.Segments,(u,v)=>center+InteractionFixtureSurface.Sample(face,u,v,size,bevel).Point);
}
if(args.Contains("--box"))
{
    Rounded("floor","9c7552",new(0,InteractionFixtureGeometry.BoxFloorY,0),new(InteractionFixtureGeometry.BoxWidth,InteractionFixtureGeometry.BoxFloorThickness,InteractionFixtureGeometry.BoxDepth));
    Rounded("roof","b28c62",new(0,InteractionFixtureGeometry.BoxRoofY,0),new(InteractionFixtureGeometry.BoxWidth,InteractionFixtureGeometry.BoxRoofThickness,InteractionFixtureGeometry.BoxDepth));
    foreach(float side in new[]{-InteractionFixtureGeometry.BoxSideX,InteractionFixtureGeometry.BoxSideX})
    {
        Rounded("side","b08b61",new(side,InteractionFixtureGeometry.BoxWallY,InteractionFixtureGeometry.BoxSidePanelZ),new(.10f,InteractionFixtureGeometry.BoxWallHeight,InteractionFixtureGeometry.BoxSidePanelDepth));
        Rounded("post","8e684c",new(side,InteractionFixtureGeometry.BoxWallY,InteractionFixtureGeometry.BoxFrontZ),new(.10f,InteractionFixtureGeometry.BoxWallHeight,.10f));
    }
    Rounded("back","ab835b",new(0,InteractionFixtureGeometry.BoxWallY,InteractionFixtureGeometry.BoxBackZ),new(InteractionFixtureGeometry.BoxBackWidth,InteractionFixtureGeometry.BoxWallHeight,InteractionFixtureGeometry.BoxBackThickness));
    float top=InteractionFixtureGeometry.BoxWallTop-.01f,bottom=InteractionFixtureGeometry.BoxBarBottom;
    foreach(float x in new[]{-.48f,-.24f,0,.24f,.48f})Tube("round bar","6e706b",u=>new(x,bottom+(top-bottom)*u,InteractionFixtureGeometry.BoxFrontZ),.0175f);
    Rounded("lower rail","6e706b",new(0,bottom,InteractionFixtureGeometry.BoxFrontZ),new(InteractionFixtureGeometry.BoxBackWidth,.035f,.035f),.008f);
    Rounded("upper rail","6e706b",new(0,InteractionFixtureGeometry.BoxWallTop-.0175f,InteractionFixtureGeometry.BoxFrontZ),new(InteractionFixtureGeometry.BoxBackWidth,.035f,.035f),.008f);
    foreach(float x in new[]{-.85f,.85f})foreach(float y in new[]{top-.12f,bottom+.12f})
        Surface("fastening","6e685b",8,16,(u,v)=>new(x+.015f*MathF.Sin(u*MathF.PI)*MathF.Cos(v*MathF.Tau),y+.015f*MathF.Cos(u*MathF.PI),InteractionFixtureGeometry.BoxFrontZ+.05f+.006f*MathF.Sin(u*MathF.PI)*MathF.Sin(v*MathF.Tau)));
}
else if(args.Contains("--heat-tool"))
{
    Rounded("base","777569",new(0,-.035f,0),new(.24f,.06f,.18f),.012f);
    Rounded("plate","dc9c62",new(0,.003f,0),new(.20f,.018f,.14f),.004f);
    Rounded("handle","927c64",new(0,-.04f,.15f),new(.065f,.06f,.20f),.014f);
}
else if(args.Contains("--treasure"))
{
    Surface("polished pebble","7b9b92",24,64,(u,v)=>InteractionTreasureSurface.Point(u*MathF.PI,v*MathF.Tau));
    Surface("pale mineral vein","c5c8ac",4,64,InteractionTreasureSurface.Vein);
}
else if(args.Contains("--care"))
{
    Surface("folded care cloth","e5dfcd",16,64,(u,v)=>InteractionCareSurface.Pad(u,v));
    Surface("gauze layer","f0ebdb",12,48,(u,v)=>InteractionCareSurface.Pad(u,v,true));
    Tube("stitched seam","c9c2ad",InteractionCareSurface.PadSeam,.0015f);
}
else if(args.Contains("--needle"))
{
    Surface("needle","b8c2bb",16,16,InteractionCareSurface.Needle);
    Tube("eye","b8c2bb",InteractionCareSurface.NeedleEye,.0017f);
    Tube("thread","dfcda3",InteractionCareSurface.Thread,.0015f);
}
else if(args.Contains("--meal"))
{
    foreach(bool inside in new[]{false,true})Surface("plate",inside?"eae4cf":"d8d5c4",12,64,(u,v)=>InteractionFoodSurface.Plate(u,v*MathF.Tau,inside));
    Tube("plate rim","f2ecda",u=>InteractionFoodSurface.PlateRim(u*MathF.Tau),.008f);
    Surface("meal","e6d3a8",14,64,(u,v)=>InteractionFoodSurface.Meal(u,v*MathF.Tau));
    Surface("meal bottom","d7c297",8,48,(u,v)=>InteractionFoodSurface.Meal(u,v*MathF.Tau,true));
    Surface("meal edge","dfcaa0",2,64,(u,v)=>InteractionFoodSurface.MealEdge(u,v*MathF.Tau));Crumbs(false);
}
else if(args.Contains("--spoon"))
{
    foreach(bool inside in new[]{false,true})Surface("spoon",inside?"c5a883":"af906c",10,48,(u,v)=>InteractionFoodSurface.SpoonBowl(u,v*MathF.Tau,inside));
    Tube("spoon rim","d2b894",u=>InteractionFoodSurface.SpoonRim(u*MathF.Tau),.0035f);
    Tube("spoon handle","ba9a74",InteractionFoodSurface.SpoonHandle,.014f);
    Surface("spoon handle cap","ba9a74",8,16,(u,v)=>InteractionFoodSurface.SpoonHandle(1)+new Vector3(MathF.Sin(u*MathF.PI)*MathF.Cos(v*MathF.Tau),MathF.Cos(u*MathF.PI),MathF.Sin(u*MathF.PI)*MathF.Sin(v*MathF.Tau))*.014f);
    if(!args.Contains("--empty"))
    {
        Surface("meal","e6d3a8",14,64,(u,v)=>InteractionFoodSurface.HandMeal(u,v*MathF.Tau));
        Surface("meal bottom","d7c297",8,48,(u,v)=>InteractionFoodSurface.HandMeal(u,v*MathF.Tau,true));
        Surface("meal edge","dfcaa0",2,64,(u,v)=>InteractionFoodSurface.MealEdge(u,v*MathF.Tau,true));Crumbs(false,true);
    }
}
else if(args.Contains("--sweet"))
{
    Surface("cookie top","d9aa70",12,72,(u,v)=>InteractionFoodSurface.CookieTop(u,v*MathF.Tau));
    Surface("cookie side","bc8652",8,72,(u,v)=>InteractionFoodSurface.CookieSide(u,v*MathF.Tau));
    Surface("cookie bottom","b77f4b",6,72,(u,v)=>InteractionFoodSurface.CookieTop(u,v*MathF.Tau,true));Crumbs(true);
}
else for(int panel=0;panel<InteractionBallSurface.Panels;panel++)
    Surface("soft ball panel "+panel,InteractionBallSurface.Colors[panel],InteractionBallSurface.Rows,InteractionBallSurface.ColumnsPerPanel,(u,v)=>InteractionBallSurface.Point(u,(panel+v)*MathF.Tau/InteractionBallSurface.Panels));
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
File.WriteAllText(args[0],JsonSerializer.Serialize(new{label="Offline prop surface audit; no game/native rendering or performance acceptance.",previewScale=args.Contains("--needle")?1000:720,previewPitch=food||args.Contains("--needle")||args.Contains("--care")||args.Contains("--heat-tool")?30:8,views=new[]{0,38,84},meshes}));
Console.WriteLine(args[0]);
