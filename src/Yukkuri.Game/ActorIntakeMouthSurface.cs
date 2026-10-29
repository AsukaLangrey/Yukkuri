using System;
using System.Collections.Generic;
using System.Numerics;

public readonly record struct IntakeMouthLayer(bool Stroke,Vector2 A,Vector2 B,float Width,string Color,float Offset=.012f)
{
    public int Rows=>Stroke?12:5;
    public int Columns=>Stroke?1:48;
    public Vector3 Point(float u,float v)
    {
        if(!Stroke)return ActorFaceSurface.Point(A.X,A.Y,B.X,B.Y,u,v*MathF.Tau,Offset);
        var direction=Vector2.Normalize(B-A);
        var p=Vector2.Lerp(A,B,u)+new Vector2(-direction.Y,direction.X)*(v-.5f)*Width;
        return new(p.X,p.Y,ActorFaceSurface.Height(p.X,p.Y,Offset));
    }
}

// These are the same prebuilt layers used by the live mouth and source previews.
public static class ActorIntakeMouthSurface
{
    public const int FrameCount=5;
    private static readonly IReadOnlyList<IntakeMouthLayer>[] Frames=
    [
        Array.AsReadOnly(new[]{new IntakeMouthLayer(false,new(0,-.175f),new(.085f,.09f),0,"7e4543")}),
        Array.AsReadOnly(new[]{new IntakeMouthLayer(true,new(-.055f,-.175f),new(.055f,-.175f),.018f,"9b5c4d")}),
        Array.AsReadOnly(new[]{new IntakeMouthLayer(false,new(0,-.175f),new(.11f,.065f),0,"7e4543"),
            new IntakeMouthLayer(false,new(.015f,-.19f),new(.048f,.023f),0,"dc8d85",.018f)}),
        Array.AsReadOnly(new[]{new IntakeMouthLayer(false,new(0,-.175f),new(.032f,.045f),0,"7e4543")}),
        Array.AsReadOnly(new[]{new IntakeMouthLayer(true,new(-.03f,-.175f),new(.03f,-.175f),.012f,"9b5c4d")})
    ];
    public static IReadOnlyList<IntakeMouthLayer> Layers(int frame)=>Frames[frame];
}
