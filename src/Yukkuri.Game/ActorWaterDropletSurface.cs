using System;
using System.Numerics;

public readonly record struct WaterDropletVertex(Vector3 Position,Vector3 Normal);
public readonly record struct WaterDropletTriangle(WaterDropletVertex A,WaterDropletVertex B,WaterDropletVertex C);

public static class ActorWaterDropletSurface
{
    public const int Rows=12,Sides=16;
    private static readonly WaterDropletTriangle[] Faces=BuildTriangles();
    public static ReadOnlySpan<WaterDropletTriangle> Triangles()=>Faces;

    // Preserve the old unit sphere's poles and remain inside its radial section.
    // Narrow the upper shoulder more; the position and drop clock are external.
    public static Vector3 Point(float u,float v)
    {
        if(u<=0)return new(0,-.5f,0);
        if(u>=1)return new(0,.5f,0);
        float radius=MathF.Sqrt(u*(1-u))*(1-.42f*u*u);
        float angle=v==1?0:v*MathF.Tau;
        return new(radius*MathF.Cos(angle),u-.5f,radius*MathF.Sin(angle));
    }

    public static Vector3 Normal(float u,float v)
    {
        if(u<=0)return -Vector3.UnitY;
        if(u>=1)return Vector3.UnitY;
        float section=u*(1-u),radial=MathF.Sqrt(section);
        // Multiply (radial direction, -dr/du) by sqrt(u(1-u)) to
        // avoid a divergent slope at the two smooth closed poles.
        float vertical=-.5f*(1-2*u)*(1-.42f*u*u)+.84f*u*section;
        float angle=v==1?0:v*MathF.Tau;
        return Vector3.Normalize(new(radial*MathF.Cos(angle),vertical,radial*MathF.Sin(angle)));
    }

    private static WaterDropletTriangle[] BuildTriangles()
    {
        var vertices=new WaterDropletVertex[Rows+1,Sides];
        for(int row=0;row<=Rows;row++)for(int side=0;side<Sides;side++)
        {
            float u=(float)row/Rows,v=(float)side/Sides;
            vertices[row,side]=new(Point(u,v),Normal(u,v));
        }
        var faces=new WaterDropletTriangle[2*Sides*(Rows-1)];int index=0;
        for(int row=0;row<Rows;row++)for(int side=0;side<Sides;side++)
        {
            int next=(side+1)%Sides;
            // Godot clockwise front faces, with only one triangle at each pole.
            if(row<Rows-1)faces[index++]=new(vertices[row,side],vertices[row+1,next],vertices[row+1,side]);
            if(row>0)faces[index++]=new(vertices[row,side],vertices[row,next],vertices[row+1,next]);
        }
        return faces;
    }
}
