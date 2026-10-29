using Godot;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    private void BuildAccessoryWear(Node3D root,bool hat)
    {
        var dirt=new Node3D{Name="DirtMarks"};root.AddChild(dirt);
        var damage=new Node3D{Name="WearMarks"};root.AddChild(damage);
        var stitches=new Node3D{Name="RepairStitches"};root.AddChild(stitches);
        Vector3 Surface(float u,float v)=>hat?HatConePoint(u,v)+new Vector3(Mathf.Cos(v),0,Mathf.Sin(v))*.006f:
            ClothBowPoint(1,u,v)+Vector3.Back*.006f;
        foreach(float center in new[]{.36f,.72f})
        {
            var patch=SculptSurface(dirt,5,32,(r,a)=>
            {
                float angle=a*Mathf.Tau,wobble=1+.1f*Mathf.Sin(angle*5);
                return Surface(center+Mathf.Cos(angle)*.10f*r*wobble,
                    (hat?1.5f:.60f)+Mathf.Sin(angle)*(hat?.19f:.16f)*r*wobble);
            },hat?"8b7965":"8e6855",flipNormal:true);
            SceneArt.EnableSurfaceFade(patch);
        }
        for(int index=0;index<2;index++)
        {
            float center=index==0?.40f:.69f;
            var tear=new Node3D{Name="Tear"+index};damage.AddChild(tear);
            SculptTube(tear,t=>Surface(center+(t-.5f)*.24f,(hat?1.5f:.58f)+Mathf.Sin(t*18)*.025f),.009f,hat?"24212a":"5f3337",24);
        }
        for(int n=0;n<8;n++)
        {
            float u=.29f+n*.027f;
            var stitch=new Node3D{Name="Stitch"+n};stitches.AddChild(stitch);
            SculptTube(stitch,t=>Surface(u+(t-.5f)*.018f,(hat?1.5f:.58f)+(t-.5f)*.18f)+Vector3.Back*.004f,.004f,"e7d5b9",5);
        }
    }
    private static void RefreshAccessoryWear(Node3D? root,AccessoryState state)
    {
        if(root==null)return;
        var dirt=root.GetNodeOrNull<Node3D>("DirtMarks");
        if(dirt!=null)
        {
            dirt.Visible=state.Dirt>1;
            foreach(var child in dirt.GetChildren())if(child is MeshInstance3D patch)
                SceneArt.SetSurfaceOpacity(patch,(float)state.Dirt/30);
        }
        var wear=root.GetNodeOrNull<Node3D>("WearMarks");
        if(wear!=null)
        {
            wear.Visible=state.Integrity<90;
            int index=0;foreach(var child in wear.GetChildren())if(child is Node3D tear)
                tear.Visible=state.Integrity<(index++==0?90:50);
        }
        var stitches=root.GetNodeOrNull<Node3D>("RepairStitches");
        if(stitches!=null)
        {
            stitches.Visible=state.RepairProgress>1;
            int index=0;foreach(var child in stitches.GetChildren())if(child is Node3D stitch)
                stitch.Visible=index++<state.RepairProgress/4;
        }
    }
}
