using System.Numerics;

namespace Yukkuri.Simulation;

public static class ShelterBodyEnvelope
{
    private readonly record struct Envelope(double Radius,double Height);
    // Authored skin, hair and accessory support samples, including the largest
    // live-pregnancy deformation and its three-degree lean. No per-oil cache.
    private static readonly Envelope[] Shapes=Build();
    private static Envelope[] Build()
    {
        var result=new Envelope[6];
        for(int pregnant=0;pregnant<2;pregnant++)for(int style=0;style<3;style++)
        {
            var scale=pregnant==1?new Vector3(1.11f,1.015f,1.066f):Vector3.One;
            double radius=0,top=0,bottom=0;
            void Include(Vector3 p){radius=Math.Max(radius,Math.Sqrt(p.X*p.X+p.Z*p.Z));top=Math.Max(top,p.Y);bottom=Math.Min(bottom,p.Y);}
            foreach(float pitch in new[]{-3f,0f,3f})
            {
                var rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,pitch*MathF.PI/180);
                Vector3 Body(Vector3 p)=>Vector3.Transform(p*scale,rotation)+new Vector3(0,.46f,0);
                for(int latitude=0;latitude<=24;latitude++)for(int longitude=0;longitude<48;longitude++)
                {
                    float a=latitude*MathF.PI/24,b=longitude*MathF.Tau/48;
                    Include(Body(new(ActorFaceSurface.RadiusX*MathF.Sin(a)*MathF.Cos(b),
                        ActorFaceSurface.CenterY+ActorFaceSurface.RadiusY*MathF.Cos(a),
                        ActorFaceSurface.CenterZ+ActorFaceSurface.RadiusZ*MathF.Sin(a)*MathF.Sin(b))));
                }
                foreach(var point in ActorHairSurface.BackSupport)Include(Body(point));
                if(style>0)foreach(float tilt in new[]{0f,12f})
                {
                    var transform=ActorAccessorySupport.Transform(style==2,new(0,.46f,0),scale,1,rotation,tilt);
                    foreach(var point in ActorAccessorySupport.Points(style==2))Include(Vector3.Transform(point,transform));
                }
            }
            result[pregnant*3+style]=new(radius+.025,top-bottom+.025);
        }
        return result;
    }
    private static Envelope For(Creature c)
    {
        bool pregnant=c.Family.Pregnancy.Method==BirthMethod.Live&&c.Family.Pregnancy.Phase is PregnancyPhase.Gestating or PregnancyPhase.Recovering;
        int style=!c.Interaction.HasAccessory?0:CreatureAppearance.UsesHat(c.Body.SpeciesId)?2:1;
        return Shapes[(pregnant?3:0)+style];
    }
    public static double Radius(Creature c)
    {
        double age=c.IsYoung?.73:c.IsElder?1.03:1;
        return Math.Max(WorldSimulation.InteractionFootprint(c),For(c).Radius*age);
    }
    public static double Height(Creature c)=>For(c).Height*(c.IsYoung?.73:c.IsElder?1.03:1);
}
