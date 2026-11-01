using Godot;
using System.Collections.Generic;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    private const int IntakeMeshCacheLimit=128;
    private Node3D? _intakeMouth;
    private MeshInstance3D? _intakeSurface;
    private readonly Dictionary<(int,int,int),ArrayMesh> _intakeMeshCache=new();
    private int _intakeKeyWidth=-1,_intakeKeyHeight=-1,_intakeKeyTongue=-1;
    private Creature? _intakeOwner;
    private IntakeMouthShape _intakeDisplayShape,_intakeExitShape,_intakeEntryShape;
    private bool _intakeWasActive,_intakeEnding;
    private float _intakeVisibility,_intakeExitAge,_intakeExitVisibility,_intakeEntryVisibility;

    private void BuildIntakeMouth()
    {
        _intakeMouth=new Node3D{Name="IntakeMouth",Visible=false};_visual.AddChild(_intakeMouth);
        var material=(StandardMaterial3D)SceneArt.Mat("ffffff").Duplicate();
        material.VertexColorUseAsAlbedo=true;material.Roughness=.70f;
        _intakeSurface=new MeshInstance3D{MaterialOverride=material,Layers=SceneArt.CreatureLayer,CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
        _intakeMouth.AddChild(_intakeSurface);SceneArt.EnableSurfaceFade(_intakeSurface);
    }
    private static ArrayMesh BuildIntakeSurface(IntakeMouthShape shape)
    {
        using var surface=new SurfaceTool();surface.Begin(Mesh.PrimitiveType.Triangles);
        foreach(bool tongue in new[]{false,true})
        {
            void Vertex(float radius,float angle)
            {
                var p=ActorIntakeMouthSurface.Point(shape,tongue,radius,angle);
                var normal=ActorFaceSurface.Normal(p.X,p.Y);
                Color color;
                if(tongue)
                    color=new Color("dc8d85").Darkened(Mathf.SmoothStep(.45f,1,radius)*.16f);
                else
                {
                    float edge=Mathf.SmoothStep(.58f,1,radius);
                    float warmth=Mathf.Max(0,-Mathf.Sin(angle))*radius*.16f;
                    color=new Color("503039").Lerp(new Color("a56b5d"),edge*.72f).Lerp(new Color("a65e57"),warmth);
                }
                surface.SetNormal(new(normal.X,normal.Y,normal.Z));surface.SetColor(color);
                surface.AddVertex(new(p.X,p.Y,p.Z));
            }
            const int rings=5,segments=48;
            for(int ring=0;ring<rings;ring++)for(int n=0;n<segments;n++)
            {
                float a=ring/(float)rings,b=(ring+1)/(float)rings,x=n*Mathf.Tau/segments,y=(n+1)*Mathf.Tau/segments;
                Vertex(a,x);Vertex(b,y);Vertex(b,x);
                if(ring>0){Vertex(a,x);Vertex(a,y);Vertex(b,y);}
            }
        }
        surface.Index();return surface.Commit();
    }
    private void SetIntakeShape(IntakeMouthShape shape)
    {
        // Fine shape steps bound the cache without changing the real mouth anchor,
        // body deformation, portion timing or source contact.
        int width=(int)Mathf.Round(shape.Width*1000),height=(int)Mathf.Round(shape.Height*1000),tongue=(int)Mathf.Round(shape.Tongue*32);
        if(width==_intakeKeyWidth&&height==_intakeKeyHeight&&tongue==_intakeKeyTongue)return;
        var key=(width,height,tongue);
        if(!_intakeMeshCache.TryGetValue(key,out var mesh))
        {
            if(_intakeMeshCache.Count>=IntakeMeshCacheLimit)_intakeMeshCache.Clear();
            mesh=BuildIntakeSurface(new(true,width/1000f,height/1000f,tongue/32f));_intakeMeshCache[key]=mesh;
        }
        _intakeSurface!.Mesh=mesh;
        _intakeKeyWidth=width;_intakeKeyHeight=height;_intakeKeyTongue=tongue;
    }
    private void PresentIntakeMouth(Creature creature,IntakeMouthShape shape,float delta,bool blocked)
    {
        if(_intakeMouth==null)BuildIntakeMouth();
        if(_intakeOwner!=creature||blocked)
        {
            // A new actor/save starts at current actual progress, not the beginning
            // of a remembered bite. Critical/dead bodies take priority.
            _intakeOwner=creature;_intakeDisplayShape=blocked?ActorIntakeMouthSurface.Inactive:shape;
            _intakeVisibility=!blocked&&shape.Active?1:0;
            _intakeWasActive=!blocked&&shape.Active;_intakeEnding=false;_intakeExitAge=0;
        }
        else if(delta>0)
        {
            if(shape.Active)
            {
                if(!_intakeWasActive){_intakeEntryShape=_intakeDisplayShape;_intakeEntryVisibility=_intakeVisibility;}
                _intakeEnding=false;_intakeExitAge=0;
                _intakeVisibility=Mathf.Min(1,_intakeVisibility+delta/.08f);
                float entry=Mathf.Clamp((_intakeVisibility-_intakeEntryVisibility)/Mathf.Max(.001f,1-_intakeEntryVisibility),0,1);
                _intakeDisplayShape=_intakeVisibility>=1?shape:ActorIntakeMouthSurface.Blend(_intakeEntryShape,shape,entry);
            }
            else
            {
                if(_intakeWasActive){_intakeExitShape=_intakeDisplayShape;_intakeExitVisibility=_intakeVisibility;_intakeExitAge=0;_intakeEnding=true;}
                if(_intakeEnding)
                {
                    _intakeExitAge+=delta;
                    float finish=Mathf.Clamp(_intakeExitAge/.10f,0,1);finish=finish*finish*(3-2*finish);
                    // Only close/return after an offer or completed bite; never
                    // advance another chew, sip or swallow from this local clock.
                    _intakeDisplayShape=ActorIntakeMouthSurface.Blend(_intakeExitShape,ActorIntakeMouthSurface.ShapeForFrame(4),finish);
                    _intakeVisibility=_intakeExitVisibility*(1-finish);
                    if(finish>=1)_intakeEnding=false;
                }
                else _intakeVisibility=0;
            }
            _intakeWasActive=shape.Active;
        }
        _intakeMouth!.Visible=_intakeVisibility>.001f;
        if(_intakeMouth.Visible)SetIntakeShape(_intakeDisplayShape);
        SceneArt.SetSurfaceOpacity(_intakeSurface!,_intakeVisibility);
        float ordinary=1-_intakeVisibility;_mouth.Visible=ordinary>.001f;
        // Preserve the existing emotion blend while handing back to its mouth.
        // Source weights are not changed: fear and pain retain their priority.
        foreach(var pair in _mouthFadeMeshes)
            foreach(var mesh in pair.Value)SceneArt.SetSurfaceOpacity(mesh,_mouthWeights[pair.Key]*ordinary);
    }
}
