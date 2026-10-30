using Godot;
using Yukkuri.Simulation;

public partial class CreatureActor
{
    private const float WaterAttemptDuration=.55f;
    private Creature? _waterAttemptOwner;
    private string? _waterAttemptEvent,_waterAttemptGeneration;
    private string _waterAttemptRegion="";
    private long _waterAttemptSequence;
    private bool _waterAttemptAvailable,_waterAttemptActive,_waterAttemptFresh,_waterAttemptBlocked;
    private float _waterAttemptAge,_waterAttemptPulse;

    public void PrepareWaterAttempt(Creature c,WaterAttemptView? attempt,double worldSeconds,bool paused,bool close)
    {
        var water=c.Interaction.Water;
        bool available=Visible&&!close&&!c.Family.ColdStorage.Stored&&!c.Family.Memorialized;
        bool baseline=_waterAttemptOwner!=c||_waterAttemptEvent!=water.EventId||
            _waterAttemptRegion!=c.RegionId||!_waterAttemptAvailable;
        bool fresh=_waterAttemptGeneration!=water.AttemptGeneration||_waterAttemptSequence!=water.AttemptSequence;
        // Even an expired/hidden fact becomes the baseline. Returning from a view
        // change cannot reinterpret the same saved attempt as a fresh action.
        _waterAttemptOwner=c;_waterAttemptEvent=water.EventId;_waterAttemptRegion=c.RegionId;
        _waterAttemptGeneration=water.AttemptGeneration;_waterAttemptSequence=water.AttemptSequence;
        _waterAttemptAvailable=available;
        bool valid=attempt!=null&&attempt.EventId==water.EventId&&attempt.Generation==water.AttemptGeneration&&
            attempt.Sequence==water.AttemptSequence&&(attempt.Outcome is WaterAttemptOutcome.Advanced or WaterAttemptOutcome.Blocked)&&
            HasWaterBodyPresentation(c)&&WaterEnvironment.CanStruggle(c)&&water.Struggle>12;
        if(baseline||!available||!valid||paused&&fresh)
        {_waterAttemptActive=false;_waterAttemptPulse=0;return;}
        if(paused)return;
        if(fresh)
        {
            double elapsed=worldSeconds-attempt!.At;
            _waterAttemptAge=double.IsFinite(elapsed)?(float)System.Math.Max(0,elapsed):WaterAttemptDuration;
            _waterAttemptActive=_waterAttemptAge<WaterAttemptDuration;
            _waterAttemptFresh=true;_waterAttemptBlocked=attempt.Outcome==WaterAttemptOutcome.Blocked;
        }
    }

    private float WaterAttemptReach(float dt)
    {
        if(!_waterAttemptActive)return _waterAttemptPulse=0;
        // Only the short aftermath uses a presentation clock. No attempt, water
        // exposure, escape chance or displacement is generated here.
        if(_waterAttemptFresh)_waterAttemptFresh=false;
        else if(float.IsFinite(dt)&&dt>0)_waterAttemptAge+=Mathf.Min(dt,.1f);
        if(_waterAttemptAge>=WaterAttemptDuration)
        {_waterAttemptActive=false;return _waterAttemptPulse=0;}
        float phase=_waterAttemptAge/WaterAttemptDuration;
        return _waterAttemptPulse=Mathf.Sin(phase*Mathf.Pi)*(_waterAttemptBlocked?.62f:1);
    }
}
