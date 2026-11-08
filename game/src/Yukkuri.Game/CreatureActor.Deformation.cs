using Godot;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    private Vector3? _undeformedSkinScale;
    private Vector3 _undeformedSkinPosition;
    private void ApplyContactDeformation(Creature c)
    {
        var i=c.Interaction;
        if(_skinContact!=null)
        {
            if(_undeformedSkinScale==null){_undeformedSkinScale=_skinContact.Scale;_undeformedSkinPosition=_skinContact.Position;}
            _skinContact.Scale=_undeformedSkinScale.Value;_skinContact.Position=_undeformedSkinPosition;
        }
        if(_hairContact!=null){_hairContact.Scale=Vector3.One;_hairContact.Position=Vector3.Zero;}
        if(i.PullRemaining>0&&i.PullAmount>0)
        {
            float amount=(float)(i.PullAmount*System.Math.Min(1,i.PullRemaining/.15));
            if(i.PulledPart==BodyPart.Head&&_hairContact!=null)
            {_hairContact.Scale=new(1,1+amount*.3f,1);_hairContact.Position=Vector3.Up*amount*.06f;}
            else if(_skinContact!=null)
            {
                float sign=i.PulledSide==SurfaceSide.Left?-1:1;
                _skinContact.Scale=_undeformedSkinScale!.Value*new Vector3(1+amount*.28f,1,1);
                _skinContact.Position=_undeformedSkinPosition+new Vector3(sign*amount*.10f,0,0);
            }
        }
        if(i.ImpactCompressionRemaining>0)
        {
            float compression=(float)(i.ImpactCompression*System.Math.Min(1,i.ImpactCompressionRemaining/.2));
            _visual.Scale*=new Vector3(1+compression*.4f,1-compression,1+compression*.2f);
        }
    }
}
