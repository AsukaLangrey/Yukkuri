using Godot;
using System;
using System.Collections.Generic;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private readonly Dictionary<string,int> _heardReactions=new();
    private AudioStreamPlayer _sound=null!,_roomMusic=null!;
    private SceneTree? _audioTree;
    private WorldSimulation? _audioSimulation;
    private bool _muted;
    private double _lastSound;
    private int _playingVoicePriority;
    private string? _speakingCreatureId;

    private void InitializeRoomAudio()
    {
        var args=OS.GetCmdlineUserArgs();
        _muted=Array.IndexOf(args,"--mute")>=0||
            Array.IndexOf(args,"--room-render-output")>=0||Array.IndexOf(args,"--contact-check-output")>=0;
        _sound=new AudioStreamPlayer{VolumeDb=-20};AddChild(_sound);
        _roomMusic=new AudioStreamPlayer{VolumeLinear=0};AddChild(_roomMusic);
        // Own the loop settings without mutating the cached shared resource.
        var source=GD.Load<AudioStreamWav>("res://Assets/Audio/sanctuary-loop.wav");
        if(source!=null)
        {
            var music=(AudioStreamWav)source.Duplicate();
            music.LoopMode=AudioStreamWav.LoopModeEnum.Forward;
            music.LoopBegin=0;music.LoopEnd=(int)Math.Round(music.GetLength()*music.MixRate);
            _roomMusic.Stream=music;_roomMusic.Play();_roomMusic.StreamPaused=true;
        }
        _audioSimulation=Sim;
        // Settings must also synchronize while a modal pauses the room's reaction polling.
        _audioTree=GetTree();_audioTree.ProcessFrame+=SyncRoomAudio;
        SyncRoomAudio();
    }

    private static float RoomVolume(double value,double fallback)=>
        (float)(double.IsFinite(value)?Math.Clamp(value,0,1):fallback);

    private void IgnoreCurrentRoomVoices()
    {
        _sound.Stop();_playingVoicePriority=0;_speakingCreatureId=null;
        foreach(var c in Sim.State.Creatures)_heardReactions[c.Id]=c.ReactionSerial;
    }

    private void SetRoomMuted(bool muted)
    {
        // Do this at the click, even if the modal remains open until sound is restored.
        if(muted)IgnoreCurrentRoomVoices();
        _muted=muted;SyncRoomAudio();
    }

    private void SyncRoomAudio()
    {
        if(!ReferenceEquals(_audioSimulation,Sim))
        {
            _audioSimulation=Sim;IgnoreCurrentRoomVoices();_lastSound=_elapsed-.65;
        }
        if(_speakingCreatureId is { } speakerId)
        {
            var speaker=Sim.State.Creatures.Find(c=>c.Id==speakerId);
            if(speaker==null||speaker.Condition==PhysicalCondition.Dead||speaker.Family.ColdStorage.Stored)
            {_sound.Stop();_playingVoicePriority=0;_speakingCreatureId=null;}
            else if(_sound.Playing&&_playingVoicePriority<(speaker.Condition==PhysicalCondition.Critical?120:speaker.Pain>=40?100:0))
            {
                _sound.Stop();_playingVoicePriority=0;_speakingCreatureId=null;
                _heardReactions.Remove(speakerId);_lastSound=_elapsed-.65;
            }
        }
        float music=RoomVolume(Sim.State.Settings.MusicVolume,.55);
        float effects=RoomVolume(Sim.State.Settings.EffectsVolume,.7);
        bool silentMusic=_muted||music<=0;
        if(silentMusic)
        {
            _roomMusic.StreamPaused=true;_roomMusic.VolumeLinear=0;
        }
        else
        {
            // Reuse the existing Main music gain; do not restart on selection or close-up.
            _roomMusic.VolumeDb=Mathf.LinearToDb(music)-12;
            _roomMusic.StreamPaused=false;
        }
        if(_muted||effects<=0)
        {
            IgnoreCurrentRoomVoices();_sound.VolumeLinear=0;
        }
        else _sound.VolumeDb=Mathf.LinearToDb(effects/.7f)-20;
    }

    private void PlayRoomReactionVoice()
    {
        if(_muted||RoomVolume(Sim.State.Settings.EffectsVolume,.7)<=0)
        {IgnoreCurrentRoomVoices();return;}
        Creature? speaking=null;string path="";int bestPriority=-1,bestVoicePriority=0;
        foreach(var c in Sim.State.Creatures)
        {
            if(!_actors.TryGetValue(c.Id,out var actor)||!actor.Visible||c.ReactionRemaining<=0||
                c.Speech.Length==0||c.Condition==PhysicalCondition.Dead||c.Family.ColdStorage.Stored)continue;
            if(c.Interaction.ReactionSituation is "prepare-natural-food" or "prepare-natural-water")continue;
            if(_heardReactions.TryGetValue(c.Id,out int serial)&&serial==c.ReactionSerial)continue;
            string cue=InteractionPresentation.VoiceCue(c);
            if(cue.Length==0)continue;
            int voicePriority=c.Condition==PhysicalCondition.Critical?120:cue switch{"coo-hurt"=>100,"coo-help"=>80,"coo-refuse"=>60,_=>20};
            int priority=voicePriority+(c.Id==Sim.State.SelectedId?1:0);
            if(priority<=bestPriority)continue;
            string candidatePath="res://Assets/Audio/"+cue+".wav";
            if(!ResourceLoader.Exists(candidatePath))continue;
            speaking=c;path=candidatePath;bestPriority=priority;bestVoicePriority=voicePriority;
        }
        if(speaking==null)return;
        // Selection breaks candidate ties; it never grants permission to cut off a voice.
        bool urgent=bestVoicePriority>=80&&_playingVoicePriority<bestVoicePriority;
        if(_sound.Playing&&!urgent)return;
        if(!urgent&&_elapsed-_lastSound<=.65)return;
        var stream=GD.Load<AudioStream>(path);if(stream==null)return;
        _sound.Stream=stream;_sound.Play();_lastSound=_elapsed;
        _playingVoicePriority=bestVoicePriority;
        _speakingCreatureId=speaking.Id;
        // Unplayed current reactions remain eligible until their own expiry.
        _heardReactions[speaking.Id]=speaking.ReactionSerial;
    }

    public override void _ExitTree()
    {
        if(_audioTree!=null)_audioTree.ProcessFrame-=SyncRoomAudio;
        _audioTree=null;
        if(GodotObject.IsInstanceValid(_sound)){_sound.Stop();_sound.Stream=null;}
        if(GodotObject.IsInstanceValid(_roomMusic)){_roomMusic.Stop();_roomMusic.Stream=null;}
    }
}
