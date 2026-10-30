using Godot;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    private void ApplyHeatBodyPose(Creature c,string pose)
    {
        if(c.Condition is PhysicalCondition.Dead or PhysicalCondition.Critical||HasWaterBodyPresentation(c))return;
        float tension=Mathf.Clamp((float)c.Interaction.HeatTension/100,0,1);
        if(tension<=.001f)return;
        bool fixedBody=c.Interaction.Restraint!=RestraintKind.None||c.Interaction.Carried;
        if(fixedBody)
        {
            // Heat tightens the existing pose instead of resetting its support orientation.
            _visual.Scale*=new Vector3(1+tension*.025f,1-tension*.065f,1+tension*.025f);
            _visual.Position+=new Vector3(Mathf.Sin(_phase*39)*.008f*tension,0,0)*BodyScale;
            if(pose=="burn-tense")PoseTag="burn-tense";
        }
        else if(pose=="burn-flee")
        {
            _visual.Position+=new Vector3(Mathf.Sin(_phase*21)*.012f*tension,Mathf.Abs(Mathf.Sin(_phase*8))*.018f*tension,0)*BodyScale;
            _visual.RotationDegrees+=new Vector3(-tension*5,0,0);
            PoseTag="burn-flee";
        }
    }
}
