using Godot;
using System;
using System.Collections.Generic;
using Yukkuri.Simulation;
public partial class InteractionRoom
{
    private Node3D _skyObjects=null!,_sunDisc=null!,_moonDisc=null!;
    private readonly List<MeshInstance3D> _stars=new();
    private void BuildCelestialObjects()
    {
        _skyObjects=new Node3D{Name="CelestialSky"};_camera.AddChild(_skyObjects);
        MeshInstance3D Orb(Node3D parent,float radius,string color)
        {
            var mesh=new MeshInstance3D{Mesh=new SphereMesh{Radius=radius,Height=radius*2,RadialSegments=16,Rings=8},
                MaterialOverride=new StandardMaterial3D{AlbedoColor=new Color(color),ShadingMode=BaseMaterial3D.ShadingModeEnum.Unshaded},CastShadow=GeometryInstance3D.ShadowCastingSetting.Off};
            parent.AddChild(mesh);return mesh;
        }
        _sunDisc=Orb(_skyObjects,.40f,"ffe0a0");_moonDisc=Orb(_skyObjects,.31f,"e5edff");
        // Fixed decorative star map; it never consumes the world's random sequence.
        for(int n=0;n<56;n++)
        {
            var star=Orb(_skyObjects,n%7==0?.034f:.018f,"d8e5fa");
            star.SetMeta("sx",((n*37%101)/100f-.5f));star.SetMeta("sy",.30f+(n*23%61)/250f);_stars.Add(star);
        }
    }
    private void RefreshCelestialObjects(CourtyardDate date)
    {
        if(_skyObjects==null)return;
        _skyObjects.Visible=Sim.State.Mode!=PlayMode.CloseUp;
        float width=_camera.Size*_ui.Size.X/Mathf.Max(1,_ui.Size.Y),height=_camera.Size;
        float day=(float)Math.Clamp((date.Hour-date.Sunrise)/(date.Sunset-date.Sunrise),0,1);
        float nightHour=(float)(date.Hour<date.Sunrise?date.Hour+24:date.Hour);
        float night=(nightHour-(float)date.Sunset)/(24-(float)date.Sunset+(float)date.Sunrise);
        _sunDisc.Visible=date.Daylight>.05;_moonDisc.Visible=date.Daylight<.3;
        _sunDisc.Position=new((day-.5f)*width*.75f,height*(.36f+.08f*Mathf.Sin(day*Mathf.Pi)),-75);
        _moonDisc.Position=new((night-.5f)*width*.7f,height*(.35f+.08f*Mathf.Sin(night*Mathf.Pi)),-75);
        foreach(var star in _stars){star.Visible=date.Daylight<.25;star.Position=new(star.GetMeta("sx").AsSingle()*width,star.GetMeta("sy").AsSingle()*height,-80);}
    }
}
