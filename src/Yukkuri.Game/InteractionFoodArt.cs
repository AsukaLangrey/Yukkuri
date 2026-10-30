using Godot;

public static class InteractionFoodArt
{
    private static Vector3 V(System.Numerics.Vector3 p)=>new(p.X,p.Y,p.Z);
    public static void Meal(Node3D parent,bool includeFood=true)
    {
        var plate=new Node3D{Position=Vector3.Up*InteractionFoodSurface.MealGroundOffset};parent.AddChild(plate);parent=plate;
        InteractionToolArt.Surface(parent,12,64,(u,v)=>V(InteractionFoodSurface.Plate(u,v*Mathf.Tau,true)),"eae4cf");
        InteractionToolArt.Surface(parent,12,64,(u,v)=>V(InteractionFoodSurface.Plate(u,v*Mathf.Tau,false)),"d8d5c4",true);
        InteractionToolArt.Tube(parent,u=>V(InteractionFoodSurface.PlateRim(u*Mathf.Tau)),.008f,"f2ecda",64);
        if(includeFood)Mound(parent);
    }
    public static Node3D Spoon(Node3D parent)
    {
        InteractionToolArt.Surface(parent,10,48,(u,v)=>V(InteractionFoodSurface.SpoonBowl(u,v*Mathf.Tau,true)),"c5a883");
        InteractionToolArt.Surface(parent,10,48,(u,v)=>V(InteractionFoodSurface.SpoonBowl(u,v*Mathf.Tau,false)),"af906c",true);
        InteractionToolArt.Tube(parent,u=>V(InteractionFoodSurface.SpoonRim(u*Mathf.Tau)),.0035f,"d2b894",48);
        InteractionToolArt.Tube(parent,u=>V(InteractionFoodSurface.SpoonHandle(u)),.014f,"ba9a74",24);
        InteractionToolArt.Surface(parent,8,16,(u,v)=>V(InteractionFoodSurface.SpoonHandle(1))+new Vector3(Mathf.Sin(u*Mathf.Pi)*Mathf.Cos(v*Mathf.Tau),Mathf.Cos(u*Mathf.Pi),Mathf.Sin(u*Mathf.Pi)*Mathf.Sin(v*Mathf.Tau))*.014f,"ba9a74");
        var portion=new Node3D();parent.AddChild(portion);
        InteractionToolArt.Surface(portion,14,64,(u,v)=>V(InteractionFoodSurface.HandMeal(u,v*Mathf.Tau)),"e6d3a8");
        // Match the side wall's angular vertices so the underside has no gaps.
        InteractionToolArt.Surface(portion,8,64,(u,v)=>V(InteractionFoodSurface.HandMeal(u,v*Mathf.Tau,true)),"d7c297",true);
        InteractionToolArt.Surface(portion,2,64,(u,v)=>V(InteractionFoodSurface.MealEdge(u,v*Mathf.Tau,true)),"dfcaa0",true);
        Crumbs(portion,false,true);return portion;
    }
    private static void Mound(Node3D parent)
    {
        InteractionToolArt.Surface(parent,14,64,(u,v)=>V(InteractionFoodSurface.Meal(u,v*Mathf.Tau)),"e6d3a8");
        InteractionToolArt.Surface(parent,8,64,(u,v)=>V(InteractionFoodSurface.Meal(u,v*Mathf.Tau,true)),"d7c297",true);
        InteractionToolArt.Surface(parent,2,64,(u,v)=>V(InteractionFoodSurface.MealEdge(u,v*Mathf.Tau)),"dfcaa0",true);
        Crumbs(parent,false);
    }
    public static void Sweet(Node3D parent,float scale=1,bool grounded=false)
    {
        var cookie=new Node3D{Scale=Vector3.One*scale,Position=grounded?Vector3.Up*InteractionFoodSurface.SweetGroundOffset:Vector3.Zero};parent.AddChild(cookie);
        InteractionToolArt.Surface(cookie,12,72,(u,v)=>V(InteractionFoodSurface.CookieTop(u,v*Mathf.Tau)),"d9aa70");
        InteractionToolArt.Surface(cookie,8,72,(u,v)=>V(InteractionFoodSurface.CookieSide(u,v*Mathf.Tau)),"bc8652",true);
        InteractionToolArt.Surface(cookie,6,72,(u,v)=>V(InteractionFoodSurface.CookieTop(u,v*Mathf.Tau,true)),"b77f4b",true);
        Crumbs(cookie,true);
    }
    private static void Crumbs(Node3D parent,bool chips,bool hand=false)
    {
        using var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        if(chips)
        {
            for(int index=0;index<InteractionFoodSurface.ChipCount;index++)
                foreach(var face in InteractionFoodSurface.ChipTriangles(index))
                {
                    surface.SetNormal(V(face.Normal));
                    surface.AddVertex(V(face.A));surface.AddVertex(V(face.B));surface.AddVertex(V(face.C));
                }
        }
        else for(int index=0;index<InteractionFoodSurface.GrainCount;index++)
        {
            void Vertex(float u,float v)
            {surface.SetNormal(V(InteractionFoodSurface.CrumbNormal(index,u*Mathf.Pi,v*Mathf.Tau,chips)));surface.AddVertex(V(hand?InteractionFoodSurface.HandCrumbPoint(index,u*Mathf.Pi,v*Mathf.Tau):InteractionFoodSurface.CrumbPoint(index,u*Mathf.Pi,v*Mathf.Tau,chips)));}
            for(int row=0;row<6;row++)for(int n=0;n<12;n++)
            {float a=row/6f,b=(row+1)/6f,x=n/12f,y=(n+1)/12f;Vertex(a,x);Vertex(b,x);Vertex(b,y);Vertex(a,x);Vertex(b,y);Vertex(a,y);}
        }
        surface.Index();SceneArt.Mesh(parent,surface.Commit(),Vector3.Zero,chips?"75513e":"f0dfb9");
    }
}
