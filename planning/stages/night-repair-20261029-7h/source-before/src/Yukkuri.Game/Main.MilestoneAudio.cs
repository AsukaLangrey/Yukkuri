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
        try
        {
            _muted=false;StartNewMilestone();using var capture=new AudioEffectCapture{BufferLength=3};
            int captureIndex=AudioServer.GetBusEffectCount(0);
            AudioServer.AddBusEffect(0,capture);
            var all=new List<Vector2>();var reports=new List<object>();
            async Task<double> Sample(string name,double seconds)
            {
                capture.ClearBuffer();await Wait(seconds);
                var samples=capture.GetBuffer(capture.GetFramesAvailable());
                Assert(samples.Length>1000,"mixer produces samples for "+name);
                double peak=samples.Max(v=>Math.Max(Math.Abs(v.X),Math.Abs(v.Y)));
                double rms=Math.Sqrt(samples.Average(v=>((double)v.X*v.X+(double)v.Y*v.Y)*.5));
                Assert(peak<.99,"mixed audio is not clipped for "+name);
                all.AddRange(samples);reports.Add(new{name,samples=samples.Length,peak,rms});
                return rms;
            }
            SetMusic();Assert(await Sample("sanctuary music",1.2)>1e-5,"music decodes and reaches the audio mix");
            Sim.State.Settings.MusicVolume=0;SetMusic();await Wait(.2);
            foreach(var cue in new[]{"coo-stroke","coo-hug","coo-nuzzle","coo-hurt","coo-refuse","coo-help"})
            {
                Sound(cue);Assert(await Sample(cue,1.2)>1e-5,"reaction cue reaches the audio mix "+cue);
            }
            Sim.State.Settings.EffectsVolume=0;Sound("coo-hug");
            Assert(await Sample("zero volume",1)<.00002,"zero music and effects settings silence the mix");
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
            File.WriteAllText(Path.Combine(EvidenceDirectory,"m01-integration-result.json"),JsonSerializer.Serialize(new{success=true,scope="Godot decoded music and reaction cues captured from Master mix; not a subjective listening review",driver=AudioServer.GetDriverName(),reports},new JsonSerializerOptions{WriteIndented=true}));
            AudioServer.RemoveBusEffect(0,captureIndex);
            GD.Print("M01_INTEGRATION_COMPLETE");
            // Exercise the same signal route as the window close button.
            GetWindow().EmitSignal(Window.SignalName.CloseRequested);
        }
        catch(Exception e){GD.PushError(e.ToString());QuitGameCleanly(8);}
    }
}
