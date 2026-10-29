using System.Numerics;
using Yukkuri.Simulation;

public readonly record struct ContactStroke(float Distance,Vector2 Direction);
public sealed class ContactStrokeTracker
{
    private string? _target;
    private BodyPart _part;
    private SurfaceSide _side;
    private Vector2 _pointer;
    private Vector3 _point;
    public void Reset()=>_target=null;
    public ContactStroke Sample(string? target,BodyPart part,Vector2 pointer,Vector3 localPoint,float bodyScale,SurfaceSide side=SurfaceSide.Whole)
    {
        var stroke=new ContactStroke(0,Vector2.Zero);
        if(target!=null&&target==_target&&part==_part&&side==_side&&Vector2.DistanceSquared(pointer,_pointer)>.000001f)
        {
            Vector3 movement=(localPoint-_point)*bodyScale;
            Vector2 direction=new(movement.X,-movement.Y);
            if(direction.LengthSquared()>0)direction/=System.MathF.Max(.025f,direction.Length());
            stroke=new(movement.Length(),direction);
        }
        _target=target;_part=part;_side=side;_pointer=pointer;_point=localPoint;return stroke;
    }
}
