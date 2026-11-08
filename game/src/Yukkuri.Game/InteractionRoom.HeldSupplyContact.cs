using System;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private RoomObject? _bodySupplySource;
    private CreatureActor? _bodySupplyActor;

    private void ResetBodySupplyContact()
    {
        _bodySupplySource=null;_bodySupplyActor=null;
    }

    // Only identity and actual mouth contact cross frames. Motion/force sampling
    // and the independent bottle opening keep their own, unrelated baselines.
    private bool ObserveBodySupplyContact(RoomObject source,Contact? contact,float dt,double sampleSeconds)
    {
        if(_supplyBiteTaken||!source.HeldByHand||source.Portions<=0||!_hand.Visible||
            contact is not {Part:BodyPart.Mouth,Blocked:false,TargetKind:InteractionTargetKind.Body}||
            !_actors.TryGetValue(contact.Id,out var actor)||!actor.Visible)
        {ResetBodySupplyContact();return false;}

        bool timely=float.IsFinite(dt)&&dt>0&&dt<=ContactMotion.MaximumSampleSeconds&&
            double.IsFinite(sampleSeconds)&&sampleSeconds>0&&sampleSeconds<=ContactMotion.MaximumSampleSeconds;
        bool continuous=timely&&ReferenceEquals(_bodySupplySource,source)&&ReferenceEquals(_bodySupplyActor,actor);
        // A first contact, replacement actor/source or stalled frame only records
        // what is now visible. No pre-arrival portion of that frame is consumed.
        _bodySupplySource=source;_bodySupplyActor=actor;
        return continuous;
    }
}
