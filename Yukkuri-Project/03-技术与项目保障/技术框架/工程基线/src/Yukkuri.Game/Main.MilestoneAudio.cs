using Godot;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public partial class Main
{
    private async void StartMilestoneAudio()
    {
        using var capture=new AudioEffectCapture{BufferLength=3};
        int captureIndex=-1;
        bool wasProcessing=IsProcessing();
        try
        {
            _muted=false;StartNewMilestone();
            // Keep autonomous help cues and region rebuilds out of isolated mixer samples.
            // AudioStreamPlayer children and SceneTree timers continue independently.
            SetProcess(false);
            _sfx.Stop();
            int effectIndex=AudioServer.GetBusEffectCount(0);
            AudioServer.AddBusEffect(0,capture);
            captureIndex=effectIndex;
            var all=new List<Vector2>();var reports=new List<object>();
            async Task<double> Sample(string name,double seconds)
            {
                capture.ClearBuffer();var discarded=capture.GetDiscardedFrames();await Wait(seconds);
                var samples=capture.GetBuffer(capture.GetFramesAvailable());
                Assert(capture.GetDiscardedFrames()==discarded,"capture does not overflow for "+name);
                Assert(samples.Length>1000,"mixer produces samples for "+name);
                double peak=samples.Max(v=>Math.Max(Math.Abs(v.X),Math.Abs(v.Y)));
                double rms=Math.Sqrt(samples.Average(v=>((double)v.X*v.X+(double)v.Y*v.Y)*.5));
                Assert(peak<.99,"mixed audio is not clipped for "+name);
                all.AddRange(samples);reports.Add(new{name,samples=samples.Length,peak,rms});
                return rms;
            }
            foreach(var region in new[]{"home","wild"})
            {
                Sim.State.ActiveRegionId=region;SetMusic();
                string name=region=="home"?"sanctuary music":"wild edge music";
                Assert(await Sample(name,1.2)>1e-5,"music decodes and reaches the audio mix for "+region);
                double length=_music.Stream.GetLength();
                Assert(length>1,"music has enough duration for a loop boundary sample for "+region);
                _music.Seek((float)(length-.25));
                // Discard the tail: the following sample must come from after the wrap.
                await Wait(.5);
                Assert(await Sample(name+" after loop boundary",.6)>1e-5&&_music.Playing,
                    "music continues beyond its end for "+region);
            }
            Sim.State.ActiveRegionId="home";
            Sim.State.Settings.MusicVolume=0;SetMusic();await Wait(.2);
            Assert(await Sample("zero music volume",.4)<.00002,"zero music setting silences music");
            _music.Stop();await Wait(.2);
            Assert(await Sample("cue baseline",.4)<.00002,"reaction samples start without other audio");
            foreach(var cue in new[]{"coo-stroke","coo-hug","coo-nuzzle","coo-hurt","coo-refuse","coo-help"})
            {
                Sound(cue);Assert(await Sample(cue,1.2)>1e-5,"reaction cue reaches the audio mix "+cue);
                Assert(!_sfx.Playing,"reaction cue ends instead of looping "+cue);
            }
            Sim.State.Settings.EffectsVolume=0;SetMusic();Sound("coo-hug");
            Assert(await Sample("zero volume",1)<.00002,"zero music and effects settings silence the mix");
            _sfx.Stop();_muted=true;
            Sim.State.Settings.MusicVolume=.55;Sim.State.Settings.EffectsVolume=.7;
            SetMusic();Sound("coo-hug");await Wait(.2);
            Assert(await Sample("muted playback request",1)<.00002,"mute suppresses new music and effects playback");
            Directory.CreateDirectory(EvidenceDirectory);
            using(var stream=File.Create(Path.Combine(EvidenceDirectory,"m01-mixed-audio.wav")))
            using(var writer=new BinaryWriter(stream))
            {
                int rate=(int)AudioServer.GetMixRate(),bytes=all.Count*4;
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+bytes);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);writer.Write((short)1);writer.Write((short)2);writer.Write(rate);writer.Write(rate*4);writer.Write((short)4);writer.Write((short)16);
                writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(bytes);
                foreach(var sample in all){writer.Write((short)(Math.Clamp(sample.X,-1,1)*32767));writer.Write((short)(Math.Clamp(sample.Y,-1,1)*32767));}
            }
            File.WriteAllText(Path.Combine(EvidenceDirectory,"m01-integration-result.json"),JsonSerializer.Serialize(new{success=true,scope="Isolated M01 mixer: both music tracks after loop boundaries, reaction cues, zero volume and muted playback requests; not default InteractionRoom playback or a subjective listening review",driver=AudioServer.GetDriverName(),reports},new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("M01_INTEGRATION_COMPLETE");
            // Exercise the same signal route as the window close button.
            GetWindow().EmitSignal(Window.SignalName.CloseRequested);
        }
        catch(Exception e){GD.PushError(e.ToString());QuitGameCleanly(8);}
        finally
        {
            // Detach before the using declaration disposes the capture, including failures.
            if(captureIndex>=0)AudioServer.RemoveBusEffect(0,captureIndex);
            SetProcess(wasProcessing);
        }
    }
}
