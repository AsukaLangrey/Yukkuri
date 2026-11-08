using System.Numerics;
using System.Text.Json.Serialization;

namespace Yukkuri.Simulation;

public sealed partial class InteractionLife
{
    // Only the current contact owns this pose. A save must never resume a held roll.
    [JsonIgnore] public bool Rolling { get; private set; }
    [JsonIgnore] public Quaternion RollOrientation { get; private set; }=Quaternion.Identity;

    internal void BeginRoll()
    {
        Rolling=true;
        RollOrientation=Supine?Quaternion.CreateFromAxisAngle(Vector3.UnitX,InteractionFixtureGeometry.SupineAngle*MathF.PI/180):Quaternion.Identity;
    }
    internal void MoveRoll(double dx,double dz,double scale)
    {
        float length=(float)Math.Sqrt(dx*dx+dz*dz);
        if(length<=.00001f)return;
        var axis=new Vector3((float)dz/length,0,-(float)dx/length);
        var turn=Quaternion.CreateFromAxisAngle(axis,length/(.48f*(float)scale));
        RollOrientation=Quaternion.Normalize(turn*RollOrientation);
        WetRestRemaining=0;
    }
    internal void EndRoll(){Rolling=false;RollOrientation=Quaternion.Identity;}
}
