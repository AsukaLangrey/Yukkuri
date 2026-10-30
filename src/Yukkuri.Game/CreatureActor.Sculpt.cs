using Godot;
using System;

public partial class CreatureActor
{
    private MeshInstance3D SculptSurface(Node3D parent,int rows,int columns,Func<float,float,Vector3> point,
        string color,Func<float,float,Color>? tint=null,bool flipNormal=false)
    {
        using var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        void Vertex(float u,float v)
        {
            const float step=.0005f;
            Vector3 du=point(Mathf.Min(1,u+step),v)-point(Mathf.Max(0,u-step),v);
            Vector3 dv=point(u,Mathf.Min(1,v+step))-point(u,Mathf.Max(0,v-step));
            var cross=dv.Cross(du);
            if(cross.LengthSquared()<1e-15f)
            {
                // Polar centers have coincident ring vertices; use the adjacent surface's limit.
                float sampleU=Mathf.Clamp(u,.001f,.999f);
                du=point(sampleU+step,v)-point(sampleU-step,v);
                dv=point(sampleU,Mathf.Min(1,v+step))-point(sampleU,Mathf.Max(0,v-step));
                cross=dv.Cross(du);
            }
            var normal=cross.Normalized();
            surface.SetNormal(flipNormal?-normal:normal);
            if(tint!=null)surface.SetColor(tint(u,v));surface.AddVertex(point(u,v));
        }
        for(int r=0;r<rows;r++)for(int c=0;c<columns;c++)
        {
            float a=(float)r/rows,b=(float)(r+1)/rows,x=(float)c/columns,y=(float)(c+1)/columns;
            // Godot's clockwise front faces must agree with the authored outward normals.
            if(flipNormal){Vertex(a,x);Vertex(b,y);Vertex(b,x);Vertex(a,x);Vertex(a,y);Vertex(b,y);}
            else{Vertex(a,x);Vertex(b,x);Vertex(b,y);Vertex(a,x);Vertex(b,y);Vertex(a,y);}
        }
        surface.Index();
        var mesh=SceneArt.Mesh(parent,surface.Commit(),Vector3.Zero,color);
        if(tint!=null)
        {
            var mat=(StandardMaterial3D)SceneArt.Mat(color).Duplicate();mat.VertexColorUseAsAlbedo=true;
            mesh.MaterialOverride=mat;
        }
        return mesh;
    }
    private void SculptTube(Node3D parent,Func<float,Vector3> path,float radius,string color,int steps=32)
    {
        SculptSurface(parent,steps,8,(u,v)=>
        {
            Vector3 tangent=(path(Mathf.Min(1,u+.001f))-path(Mathf.Max(0,u-.001f))).Normalized();
            Vector3 axis=Mathf.Abs(tangent.Y)>.95f?Vector3.Right:Vector3.Up;
            Vector3 right=tangent.Cross(axis).Normalized(),up=tangent.Cross(right).Normalized();
            return path(u)+radius*(right*Mathf.Cos(v*Mathf.Tau)+up*Mathf.Sin(v*Mathf.Tau));
        },color);
    }
    private void BuildBackHair(Node3D parent,string color)
    {
        SculptSurface(parent,ActorHairSurface.BackRows,ActorHairSurface.BackSides,
            (u,v)=>AccessoryPoint(ActorHairSurface.Shell(u,v)),color,(u,v)=>
            {
                float ridge=Mathf.Cos(v*Mathf.Tau*14+u*.7f)*.027f;
                float crown=Mathf.Sin(u*Mathf.Pi)*.07f;
                float tips=Mathf.SmoothStep(.70f,1,u)*.10f;
                return new Color(1+ridge+crown-tips,1+ridge+crown*.8f-tips,1+ridge+crown*.6f-tips);
            });
        SculptSurface(parent,2,ActorHairSurface.BackSides,(u,v)=>
            AccessoryPoint(System.Numerics.Vector3.Lerp(ActorHairSurface.Shell(1,v),ActorHairSurface.Shell(1,v,true),u)),color);
    }
    private void BuildHairLock(Node3D parent,HairLockShape shape,string color)
    {
        // Cross-section clearance does not depend on the angular vertex. Cache it only
        // during construction so finite-difference normals don't redo the same search.
        var sections=new System.Collections.Generic.Dictionary<float,HairLockSection>();
        HairLockSection Section(float u)
        {
            if(!sections.TryGetValue(u,out var section))sections[u]=section=ActorHairSurface.Section(shape,u);
            return section;
        }
        SculptSurface(parent,20,32,(u,v)=>AccessoryPoint(ActorHairSurface.Point(Section(u),v)),color,(u,v)=>
        {
            float glow=Mathf.Pow(Mathf.Max(0,Mathf.Sin(v*Mathf.Tau)),6)*Mathf.Sin(u*Mathf.Pi)*.17f;
            float root=Mathf.Pow(1-u,3)*.07f,tip=Mathf.SmoothStep(.78f,1,u)*.08f;
            return new Color(1+glow-root-tip,1+glow*.85f-root-tip,1+glow*.65f-root-tip);
        });
        // A restrained highlight follows the lock's direction, rather than painting another round blob.
        var highlight=SceneArt.Mat(color).AlbedoColor.Lightened(.08f).ToHtml(false);
        SculptTube(parent,u=>AccessoryPoint(ActorHairSurface.Highlight(shape,.17f+u*.455f)),.0035f,highlight,16);
    }
    private void BuildClothBow(Node3D parent,string fabric,string trim,float width=1,Func<Vector3,Vector3>? conform=null)
    {
        Vector3 Fit(Vector3 point)=>conform?.Invoke(point)??point;
        foreach(float sign in new[]{-1f,1f})
        {
            var wing=new Node3D();parent.AddChild(wing);
            SculptSurface(wing,22,24,(u,v)=>
            {
                return Fit(ClothBowPoint(sign,u,v)*width);
            },fabric,(u,v)=>new Color(1,1,1).Darkened(Mathf.Pow(Mathf.Abs(v*2-1),2)*.12f),flipNormal:sign>0);
            // Backing and folded hems give the bow an edge when viewed from the side.
            SculptSurface(wing,12,24,(u,v)=>Fit((ClothBowPoint(sign,u,v)-Vector3.Back*ActorAccessorySurface.ClothThickness)*width),
                fabric,flipNormal:sign<0);
            foreach(float edge in new[]{0f,1f})
            {
                SculptTube(wing,u=>Fit((ClothBowPoint(sign,u,edge)-Vector3.Back*ActorAccessorySurface.ClothThickness*.5f)*width),
                    .006f*width,fabric,24);
                float inset=edge==0?.07f:.93f;
                SculptTube(wing,u=>Fit(AccessoryPoint(ActorAccessorySurface.BowTrim(sign,u,inset))*width),.005f*width,trim,24);
            }
            SculptTube(wing,v=>Fit((ClothBowPoint(sign,1,v)-Vector3.Back*ActorAccessorySurface.ClothThickness*.5f)*width),
                .006f*width,fabric,32);
            SculptTube(wing,v=>Fit(AccessoryPoint(ActorAccessorySurface.BowTrim(sign,.965f,v))*width),.005f*width,trim,32);
            Vector3 Tail(float u,float v)=>AccessoryPoint(ActorAccessorySurface.BowTail(sign,u,v))*width;
            SculptSurface(parent,18,16,(u,v)=>Fit(Tail(u,v)),fabric,flipNormal:sign<0);
            SculptSurface(parent,18,16,(u,v)=>Fit(Tail(u,v)-Vector3.Back*(ActorAccessorySurface.ClothThickness*width)),fabric,flipNormal:sign>0);
            foreach(float edge in new[]{0f,1f})SculptTube(parent,u=>Fit(Tail(u,edge)-Vector3.Back*(ActorAccessorySurface.ClothThickness*.5f*width)),.006f*width,fabric,24);
            SculptTube(parent,v=>Fit(Tail(1,v)-Vector3.Back*(ActorAccessorySurface.ClothThickness*.5f*width)),.006f*width,fabric,20);
            SculptTube(parent,v=>Fit(Tail(.91f,v)+Vector3.Back*.004f*width),.0035f*width,trim,24);
        }
        if(conform==null)
        {
            var knot=SceneArt.Ball(parent,new(0,0,.045f*width),new Vector3(.16f,.14f,.13f)*width,fabric);
            knot.RotationDegrees=new(0,0,-5);
        }
        else SculptSurface(parent,16,32,(u,v)=>Fit((new Vector3(0,0,.045f)+new Vector3(.08f*Mathf.Sin(u*Mathf.Pi)*Mathf.Cos(v*Mathf.Tau),.07f*Mathf.Cos(u*Mathf.Pi),.065f*Mathf.Sin(u*Mathf.Pi)*Mathf.Sin(v*Mathf.Tau)))*width),fabric);
    }
    private static Vector3 AccessoryPoint(System.Numerics.Vector3 p)=>new(p.X,p.Y,p.Z);
    private static Vector3 ClothBowPoint(float sign,float u,float v)=>AccessoryPoint(ActorAccessorySurface.Bow(sign,u,v));
    private static Vector3 HatConePoint(float t,float angle)=>AccessoryPoint(ActorAccessorySurface.HatCone(t,angle));
    private void BuildSculptedHat(Node3D hat)
    {
        Vector3 Brim(float u,float v)=>AccessoryPoint(ActorAccessorySurface.HatBrim(u,v*Mathf.Tau));
        SculptSurface(hat,14,96,Brim,"3e3c46");
        SculptSurface(hat,14,96,(u,v)=>AccessoryPoint(ActorAccessorySurface.HatBrimUnderside(u,v*Mathf.Tau)),"34323c",flipNormal:true);
        SculptTube(hat,v=>AccessoryPoint(ActorAccessorySurface.HatBrimRim(v*Mathf.Tau)),
            ActorAccessorySurface.BrimThickness*.5f,"55505c",96);
        SculptSurface(hat,36,96,(u,v)=>HatConePoint(u,v*Mathf.Tau),"45434e",
            (u,v)=>new Color(1,1,1).Darkened(.10f*Mathf.Max(0,Mathf.Sin(v*Mathf.Tau*3))*(1-u)),flipNormal:true);
        SculptSurface(hat,6,96,(u,v)=>
        {
            float t=.045f+u*.16f;Vector3 p=HatConePoint(t,v*Mathf.Tau);
            return p+new Vector3(Mathf.Cos(v*Mathf.Tau)*.006f,0,Mathf.Sin(v*Mathf.Tau)*.006f);
        },"e9dfc5",flipNormal:true);
        foreach(float t in new[]{.045f,.205f})
            SculptTube(hat,v=>HatConePoint(t,v*Mathf.Tau)+new Vector3(Mathf.Cos(v*Mathf.Tau),0,Mathf.Sin(v*Mathf.Tau))*.009f,
                .0045f,"c7baa0",64);
        BuildClothBow(hat,"f0e8d6","fff4df",ActorAccessorySurface.HatBowScale,
            p=>AccessoryPoint(ActorAccessorySurface.HatBow(new(p.X,p.Y,p.Z))));
    }
}
