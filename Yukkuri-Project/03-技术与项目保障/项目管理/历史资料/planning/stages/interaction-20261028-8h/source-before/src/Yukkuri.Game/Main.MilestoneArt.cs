using Godot;
using System;
using Yukkuri.Simulation;

public partial class Main
{
    private void DecorateMilestoneRegion()
    {
        bool wild=Sim.State.ActiveRegionId=="wild";
        // Deliberately authored, code-native scene assets. No copied character artwork.
        if(!wild)
        {
            foreach(float x in new[]{-7.5f,-6f,-4.5f})
            {
                SceneArt.Box(_stage,new(x,.16f,-6.5f),new(1.15f,.3f,.8f),"a78561");
                SceneArt.Box(_stage,new(x,.33f,-6.5f),new(1f,.05f,.65f),"665744");
                for(int i=0;i<5;i++)Flower(_stage,new(x-.4f+i*.2f,.36f,-6.5f+(i%2)*.15f),i%2==0?"e3ad96":"e8cc85");
            }
            for(int i=0;i<9;i++)
            {
                var stone=SceneArt.Mesh(_stage,new CylinderMesh{TopRadius=.34f,BottomRadius=.38f,Height=.04f,RadialSegments=7},new(-6+i*.8f,.035f,3.5f),"c6bea3");
                stone.Scale=new(1,1,.65f);stone.RotationDegrees=new(0,i*19,0);
            }
            SceneArt.Box(_stage,new(-7.7f,.72f,3),new(.18f,1.45f,.18f),"9c7959");
            SceneArt.Box(_stage,new(-7.7f,.72f,5.5f),new(.18f,1.45f,.18f),"9c7959");
            SceneArt.Box(_stage,new(-7.7f,1.55f,4.25f),new(.22f,.24f,2.85f),"b79971");
            var sign=SceneArt.Box(_stage,new(-7.54f,1.14f,4.25f),new(.08f,.5f,1.25f),"eee0b6");
            SceneLabel(_stage,"小小乐园",new(-7.5f,1.75f,4.2f),.007f,"f8edcf");
            SceneLabel(_stage,"小岚 · 园区雇员",new(4,2.1f,-4),.0048f,"f8edcf");
            SceneArt.Box(_stage,new(5.3f,.4f,5.8f),new(2,.14f,.65f),"b69670");
            foreach(float x in new[]{4.6f,6f})SceneArt.Box(_stage,new(x,.2f,5.8f),new(.12f,.4f,.45f),"7d6951");
            SceneArt.Box(_stage,new(5.3f,.72f,6.05f),new(2,.5f,.12f),"a98966");
            var awning=SceneArt.Box(_stage,new(4.7f,1.65f,-4.2f),new(2.3f,.12f,1.2f),"d8c5a3");awning.RotationDegrees=new(8,0,0);
            for(int i=0;i<4;i++)SceneArt.Box(_stage,new(3.7f+i*.6f,1.67f,-4.2f),new(.28f,.13f,1.2f),"9ba985");
        }
        else
        {
            for(int i=0;i<16;i++)
            {
                float x=-7+(i*3.71f)%14,z=-7+(i*5.19f)%14;
                if(Math.Abs(x)<4&&Math.Abs(z)<4)continue;
                var rock=SceneArt.Ball(_stage,new(x,.16f,z),new(.6f+(i%3)*.2f,.4f,.65f),"899185");rock.RotationDegrees=new(0,i*33,0);
                Flower(_stage,new(x+.4f,.05f,z+.1f),i%2==0?"c2b2d1":"ead3a0");
            }
            SceneArt.Box(_stage,new(-6,.48f,-4),new(.16f,1,.16f),"957452");
            SceneArt.Box(_stage,new(-6,.9f,-4),new(1.5f,.45f,.12f),"c3aa7d");
            SceneLabel(_stage,"← 乐园",new(-6,1.25f,-4),.006f,"f8edcf");
            var pond=SceneArt.Mesh(_stage,new CylinderMesh{TopRadius=1.1f,BottomRadius=1.15f,Height=.03f,RadialSegments=32},new(6,.015f,-6),"799e9a");pond.Scale=new(1.5f,1,1);
            // This water is a small backdrop outside encounter routes; water interaction is a later explicit milestone.
        }
        for(int i=0;i<24;i++)
        {
            float x=-8+(i*2.37f)%16,z=-8+(i*4.11f)%16;
            if(!wild&&Math.Abs(x-1)<1.4f)continue;
            for(int j=0;j<3;j++)
            {
                var leaf=SceneArt.Box(_stage,new(x+j*.08f,.075f,z),new(.04f,.15f,.025f),wild?"647d61":"89a570");
                leaf.RotationDegrees=new(0,0,(j-1)*20);
            }
        }
        BuildPerson(_human,false);
        _employee=new Node3D{Position=new(4,0,-4)};_stage.AddChild(_employee);BuildPerson(_employee,true);_employee.Visible=!wild;
        _handVisual=new Node3D();_stage.AddChild(_handVisual);
        SceneArt.Ball(_handVisual,Vector3.Zero,new(.28f,.12f,.30f),"e6c9a8");
        for(int i=0;i<4;i++)SceneArt.Ball(_handVisual,new(-.09f+i*.06f,-.01f,.18f),new(.065f,.09f,.20f),"edcfad");
        SceneArt.Ball(_handVisual,new(.17f,-.015f,.03f),new(.16f,.1f,.09f),"e6c9a8");
        SceneArt.Box(_handVisual,new(0,.01f,-.22f),new(.21f,.15f,.20f),"6c8290");
        var paddle=new Node3D{Name="Paddle",Visible=false};_handVisual.AddChild(paddle);
        SceneArt.Box(paddle,new(0,-.1f,.18f),new(.06f,.045f,.35f),"b79972");
        SceneArt.Ball(paddle,new(0,-.09f,.43f),new(.25f,.055f,.32f),"c9ae80");
        var glove=new Node3D{Name="Glove",Visible=false};_handVisual.AddChild(glove);
        SceneArt.Ball(glove,new(0,-.025f,.05f),new(.32f,.16f,.42f),"d5dfc4");
        SceneArt.SetRenderLayer(_handVisual,SceneArt.InteractionLayer);
        _handVisual.Visible=false;
    }
    private void BuildPerson(Node3D root,bool employee)
    {
        SceneArt.Mesh(root,new CapsuleMesh{Radius=.23f,Height=.75f},new(0,.80f,0),employee?"7d967d":"68808c");
        SceneArt.Ball(root,new(0,1.36f,0),new(.43f,.46f,.41f),"e5c7a5");
        SceneArt.Ball(root,new(0,1.49f,-.03f),new(.44f,.25f,.43f),employee?"684c3e":"4b4038");
        foreach(float x in new[]{-.13f,.13f})
        {
            SceneArt.Mesh(root,new CapsuleMesh{Radius=.10f,Height=.5f},new(x,.29f,0),"514f48");
            SceneArt.Ball(root,new(x,.07f,.09f),new(.22f,.14f,.32f),"69584a");
        }
        foreach(float x in new[]{-.29f,.29f})
        {
            var arm=SceneArt.Mesh(root,new CapsuleMesh{Radius=.09f,Height=.45f},new(x,.8f,0),employee?"a2b399":"78909c");
            arm.RotationDegrees=new(0,0,x>0?10:-10);
            SceneArt.Ball(root,new(x,.55f,0),new(.16f,.16f,.15f),"e5c7a5");
        }
        if(employee)
        {
            SceneArt.Box(root,new(0,.72f,.22f),new(.35f,.45f,.035f),"ddd2b3");
            SceneArt.Box(root,new(0,.76f,.245f),new(.15f,.1f,.02f),"9caa8b");
        }
        else
        {
            SceneArt.Mesh(root,new CylinderMesh{TopRadius=.32f,BottomRadius=.34f,Height=.04f,RadialSegments=24},new(0,1.6f,0),"cbb78e");
            SceneArt.Mesh(root,new CylinderMesh{TopRadius=.20f,BottomRadius=.23f,Height=.18f,RadialSegments=24},new(0,1.7f,0),"d5c49c");
            SceneArt.Box(root,new(0,.88f,-.24f),new(.31f,.42f,.18f),"a58a65");
        }
    }
    private void Flower(Node3D parent,Vector3 at,string color)
    {
        SceneArt.Box(parent,at+new Vector3(0,.12f,0),new(.025f,.24f,.025f),"688863");
        for(int petal=0;petal<5;petal++)
        {
            float angle=petal*Mathf.Tau/5;
            SceneArt.Ball(parent,at+new Vector3(Mathf.Cos(angle)*.065f,.25f,Mathf.Sin(angle)*.065f),new(.10f,.045f,.10f),color);
        }
        SceneArt.Ball(parent,at+new Vector3(0,.27f,0),new(.06f,.04f,.06f),"d5b96e");
    }
    private void SceneLabel(Node3D parent,string text,Vector3 position,float pixelSize,string color)
    {
        if(!NameplatesEnabled)return;
        parent.AddChild(new Label3D{Text=text,Font=_font,FontSize=34,PixelSize=pixelSize,Position=position,
            Billboard=BaseMaterial3D.BillboardModeEnum.Enabled,Modulate=new Color(color),OutlineModulate=new Color("485f48"),OutlineSize=8});
    }
}
