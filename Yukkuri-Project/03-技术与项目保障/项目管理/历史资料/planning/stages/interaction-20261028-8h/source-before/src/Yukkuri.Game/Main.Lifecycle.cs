using Godot;
using System;

public partial class Main
{
    private bool _quitting;
    private void StopAudioForExit()
    {
        if(GodotObject.IsInstanceValid(_music)){_music.Stop();_music.Stream=null;}
        if(GodotObject.IsInstanceValid(_sfx)){_sfx.Stop();_sfx.Stream=null;}
    }
    private async void QuitGameCleanly(int exitCode=0)
    {
        if(_quitting)return;
        _quitting=true;_ready=false;
        try
        {
            _held=null;
            foreach(var actor in Actors.Values)if(GodotObject.IsInstanceValid(actor))actor.Freeze=true;
            StopAudioForExit();
            // Let the audio thread consume stop/detach requests before engine teardown.
            await ToSignal(GetTree().CreateTimer(.2),SceneTreeTimer.SignalName.Timeout);
        }
        catch(Exception error){GD.PushError("Shutdown cleanup failed: "+error);exitCode=11;}
        GetTree().Quit(exitCode);
    }
    public override void _ExitTree()=>StopAudioForExit();
}
