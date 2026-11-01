using Godot;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    private Creature? _shelterPresentationOwner;
    private bool _shelterPassage,_startShelterPassage;

    public void PrepareShelterPassage(Creature creature,CreatureShelterView? visit)
    {
        var life=creature.Interaction;
        bool passage=visit?.Phase is ShelterVisitPhase.Entering or ShelterVisitPhase.Exiting&&
            creature.Condition is not (PhysicalCondition.Critical or PhysicalCondition.Dead)&&
            !life.Carried&&!life.Falling&&!life.Rolling&&!life.Supine&&life.Height<=.001&&
            life.Restraint==RestraintKind.None&&!HasWaterBodyPresentation(creature);
        if(passage&&(!_shelterPassage||!ReferenceEquals(_shelterPresentationOwner,creature)))_startShelterPassage=true;
        if(!passage)_startShelterPassage=false;
        _shelterPresentationOwner=creature;_shelterPassage=passage;
    }
    private void FaceShelterPassage(Creature creature,string pose)
    {
        if(!_shelterPassage||pose is not ("idle" or "guard" or "careful-step" or "stomach"))return;
        var rotation=_visual.Rotation;
        rotation.Y=(float)creature.Interaction.Facing;
        if(pose=="guard")rotation.Y+=Mathf.DegToRad(ActorReactionMotion.GuardYaw(
            creature.Interaction.CheekSensitivityAt(SurfaceSide.Left),creature.Interaction.CheekSensitivityAt(SurfaceSide.Right)));
        _visual.Rotation=rotation;
    }
}
