using System;

public sealed class ActorPoseClock
{
    private string _pose="";
    private int _reaction=-1;
    public float Age {get;private set;}
    public void Advance(string pose,int reaction,float delta,bool paused)
    {
        bool discrete=pose is "poke" or "flick" or "slap" or "push" or "land" or "settle" or "ball-bump" or "greet" or "proud";
        if(pose!=_pose||discrete&&reaction!=_reaction)Age=0;
        else if(!paused&&float.IsFinite(delta))Age+=Math.Max(0,delta);
        _pose=pose;_reaction=reaction;
    }
}
