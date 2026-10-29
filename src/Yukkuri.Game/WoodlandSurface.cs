using System;
using System.Collections.Generic;
using System.Numerics;
using Yukkuri.Simulation;

public sealed record WoodlandPatch(string Name,string Color,int Rows,int Columns,Func<float,float,Vector3> Point,bool Flip=false);

// Production geometry also exported by the CPU preview. No Godot or desktop dependency.
public static class WoodlandSurface
{
    public const float WaterHeight=NaturalIntakeGeometry.WaterHeight;
    public static float LeafBiteHeight=>NaturalIntakeGeometry.LeafBiteHeight;
    public static Vector3 Leaf(float length,float width,float u,float v)
    {
        return NaturalIntakeGeometry.Leaf(length,width,u,v);
    }
    public static IEnumerable<WoodlandPatch> Leaves(int count=8)
    {
        // A depleted patch leaves small dry remnants, so the missing food is visible.
        if(count<=0)
        {
            for(int n=0;n<3;n++)
            {
                int index=n;
                yield return new("eaten leaf remnant "+index,"a49b73",6,4,(u,v)=>
                    Leaf(.16f,.065f,u,v)+new Vector3((index-1)*.13f,0,(index%2)*.1f));
            }
            yield break;
        }
        for(int i=0;i<count;i++)
        {
            int index=i;float angle=i*2.399963f;
            var center=new Vector3(MathF.Cos(angle)*(.05f+i*.04f),i*.005f,MathF.Sin(angle)*(.05f+i*.04f));
            Vector3 Position(float u,float v)
            {
                var p=Leaf(.38f+index%3*.05f,.19f,u,v);
                return center+new Vector3(p.X*MathF.Cos(angle)-p.Z*MathF.Sin(angle),p.Y,p.X*MathF.Sin(angle)+p.Z*MathF.Cos(angle));
            }
            yield return new("leaf "+index,index%3==0?"9dab62":index%3==1?"739658":"b6ad6c",10,6,Position);
            yield return new("leaf vein "+index,"c6ca85",10,1,(u,v)=>Position(u,.49f+v*.02f)+Vector3.UnitY*.002f);
        }
    }
    public static IEnumerable<WoodlandPatch> River()
    {
        // Ground is at y=0. Water and dry banks sit just above it in this prototype terrain.
        yield return new("west river bank","aaa584",64,6,(u,v)=>
        {float z=-8+16*u,edge=(float)WoodlandLayout.RiverEdge(z);return new(edge-.32f+v*.32f,.0025f,z);},Flip:true);
        yield return new("river surface","7aaba3",96,12,(u,v)=>
        {float z=-8+16*u,edge=(float)WoodlandLayout.RiverEdge(z);return new(edge+(8.1f-edge)*v,WaterHeight,z);},Flip:true);
        for(int i=0;i<22;i++)
        {
            float z=-7.6f+i*.69f,side=i%3*.34f;
            yield return new("flow trace "+i,"aacac0",12,1,(u,v)=>
            {float zz=z+u*.29f;return new((float)WoodlandLayout.RiverEdge(zz)+.24f+side+.04f*MathF.Sin(u*MathF.PI)+(v-.5f)*.016f,.027f,zz);},Flip:true);
        }
        for(int i=0;i<11;i++)
        {
            float z=-7+i*1.3f;
            // Keep the authored drinking approach free of decorative stones.
            if(MathF.Abs(z-(float)WoodlandLayout.BankZ)<.8f)continue;
            var center=new Vector3((float)WoodlandLayout.RiverEdge(z)-.17f,.05f,z);
            foreach(var patch in Pebble(center,new(.11f,.075f,.16f),i%2==0?"b7b5a1":"959d8e"))yield return patch;
        }
    }
    public static IEnumerable<WoodlandPatch> Tree(Vector3 origin,float scale=1)
    {
        yield return new("tree trunk","947855",12,12,(u,v)=>
        {
            float y=u*1.6f*scale;var trunk=new WoodlandTree(origin.X,origin.Z,scale).TrunkAt(y);
            return new((float)(trunk.X+trunk.Radius*MathF.Cos(v*MathF.Tau)),origin.Y+y,(float)(trunk.Z+trunk.Radius*MathF.Sin(v*MathF.Tau)));
        },Flip:true);
        for(int i=0;i<5;i++)
        {
            int k=i;float a=i*2.4f;
            var crown=new Vector3(MathF.Cos(a)*.36f,1.55f+(i%2)*.35f,MathF.Sin(a)*.32f);
            yield return new("tree crown "+i,i%2==0?"8fa568":"aab975",16,24,(u,v)=>
            {
                float phi=u*MathF.PI,theta=v*MathF.Tau;
                float ripple=1+.04f*MathF.Sin(theta*5+phi*4+k);
                return origin+scale*(crown+new Vector3(MathF.Sin(phi)*MathF.Cos(theta)*.65f*ripple,MathF.Cos(phi)*.62f,MathF.Sin(phi)*MathF.Sin(theta)*.57f*ripple));
            });
        }
    }
    public static IEnumerable<WoodlandPatch> Pebble(Vector3 center,Vector3 size,string color)
    {
        yield return new("bank pebble",color,10,16,(u,v)=>center+size*new Vector3(
            MathF.Sin(u*MathF.PI)*MathF.Cos(v*MathF.Tau),MathF.Cos(u*MathF.PI),MathF.Sin(u*MathF.PI)*MathF.Sin(v*MathF.Tau)));
    }
}
