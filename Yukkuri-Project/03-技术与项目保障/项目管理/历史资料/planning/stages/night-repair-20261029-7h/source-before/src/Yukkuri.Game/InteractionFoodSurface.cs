using System;
using System.Numerics;

public static class InteractionFoodSurface
{
    public const int GrainCount=18,ChipCount=7;
    public const float MealHandScale=.4f,SweetHandScale=.55f;
    public const float MealGroundOffset=-.016f,SweetGroundOffset=-.01f;
    public static float PlateHeight(float x,float z)=>.033f+.062f*MathF.Pow(Math.Clamp(MathF.Sqrt(x*x+z*z)/.285f,0,1),4);
    public static Vector3 Plate(float radius,float angle,bool inside)
    {
        float r=(inside?.285f:.30f)*radius;
        return new(r*MathF.Cos(angle),inside?.033f+.062f*MathF.Pow(radius,4):.016f+.075f*MathF.Pow(radius,4),r*MathF.Sin(angle));
    }
    public static Vector3 PlateRim(float angle)=>new(.2925f*MathF.Cos(angle),.094f,.2925f*MathF.Sin(angle));
    public static Vector3 Meal(float radius,float angle,bool bottom=false)
    {
        float x=.215f*radius*MathF.Cos(angle),z=.19f*radius*MathF.Sin(angle);
        float y=PlateHeight(x,z)+(bottom?.001f:.005f+.085f*MathF.Pow(MathF.Max(0,1-radius*radius),.7f));
        return new(x,y,z);
    }
    public static Vector3 SpoonBowl(float radius,float angle,bool inside)
        =>new(.10f*radius*MathF.Cos(angle),(inside?.008f:.001f)+.022f*radius*radius,.07f*radius*MathF.Sin(angle));
    public static Vector3 SpoonRim(float angle)=>new(.10f*MathF.Cos(angle),.028f,.07f*MathF.Sin(angle));
    public static Vector3 SpoonHandle(float u)=>new(0,.016f-.006f*MathF.Sin(u*MathF.PI),.065f+u*.23f);
    public static Vector3 HandMeal(float radius,float angle,bool bottom=false)
    {
        float x=.215f*MealHandScale*radius*MathF.Cos(angle),z=.19f*MealHandScale*radius*MathF.Sin(angle);
        float baseHeight=.008f+.022f*Math.Clamp(x*x/.01f+z*z/.0049f,0,1);
        return new(x,baseHeight+(bottom?.001f:.006f+.034f*MathF.Pow(MathF.Max(0,1-radius*radius),.7f)),z);
    }
    public static Vector3 MealEdge(float height,float angle,bool hand=false)=>Vector3.Lerp(hand?HandMeal(1,angle,true):Meal(1,angle,true),hand?HandMeal(1,angle):Meal(1,angle),height);
    public static float CookieRadius(float angle)=>.18f*(1+.015f*MathF.Cos(angle*12));
    public static float CookieTopHeight(float radius)=>.085f+.012f*(1-radius*radius);
    public static Vector3 CookieTop(float radius,float angle,bool bottom=false)
    {
        float r=(CookieRadius(angle)-.003f)*radius;
        return new(r*MathF.Cos(angle),bottom?.01f:CookieTopHeight(radius),r*MathF.Sin(angle));
    }
    public static Vector3 CookieSide(float height,float angle)
    {
        float bevel=.003f*MathF.Pow(MathF.Abs(height*2-1),6),r=CookieRadius(angle)-bevel;
        return new(r*MathF.Cos(angle),.01f+height*.075f,r*MathF.Sin(angle));
    }
    private readonly record struct Crumb(Vector3 Center,Vector3 Radii,float Yaw);
    private static readonly Crumb[] Grains=BuildCrumbs(false),Chips=BuildCrumbs(true);
    private static Crumb[] BuildCrumbs(bool chips)
    {
        int count=chips?ChipCount:GrainCount;var crumbs=new Crumb[count];
        for(int index=0;index<count;index++)
        {
            float radius=.76f*MathF.Sqrt((index+.5f)/count),angle=index*2.399963f;
            var center=chips?CookieTop(radius,angle):Meal(radius,angle);
            center.Y-=chips?.0015f:.003f;
            crumbs[index]=new(center,chips?new(.014f,.005f,.010f):new(.022f,.009f,.013f),angle*.7f);
        }
        return crumbs;
    }
    private static Vector3 Rotate(Vector3 p,float angle)=>new(p.X*MathF.Cos(angle)+p.Z*MathF.Sin(angle),p.Y,-p.X*MathF.Sin(angle)+p.Z*MathF.Cos(angle));
    public static Vector3 CrumbPoint(int index,float latitude,float longitude,bool chips)
    {
        var crumb=(chips?Chips:Grains)[index];
        var unit=new Vector3(MathF.Sin(latitude)*MathF.Cos(longitude),MathF.Cos(latitude),MathF.Sin(latitude)*MathF.Sin(longitude));
        return crumb.Center+Rotate(unit*crumb.Radii,crumb.Yaw);
    }
    public static Vector3 CrumbNormal(int index,float latitude,float longitude,bool chips)
    {
        var crumb=(chips?Chips:Grains)[index];
        var unit=new Vector3(MathF.Sin(latitude)*MathF.Cos(longitude),MathF.Cos(latitude),MathF.Sin(latitude)*MathF.Sin(longitude));
        return Vector3.Normalize(Rotate(unit/crumb.Radii,crumb.Yaw));
    }
    public static Vector3 HandCrumbPoint(int index,float latitude,float longitude)
    {
        var source=Grains[index];float radius=MathF.Sqrt(source.Center.X*source.Center.X/(.215f*.215f)+source.Center.Z*source.Center.Z/(.19f*.19f));
        float angle=MathF.Atan2(source.Center.Z/.19f,source.Center.X/.215f);
        var center=HandMeal(radius,angle)-Vector3.UnitY*.0012f;
        return center+(CrumbPoint(index,latitude,longitude,false)-source.Center)*MealHandScale;
    }
    public static Vector3 MealOffer=>HandMeal(1,-MathF.PI*.5f);
    public static Vector3 SweetOffer=>CookieTop(1,-MathF.PI*.5f)*SweetHandScale;
}
