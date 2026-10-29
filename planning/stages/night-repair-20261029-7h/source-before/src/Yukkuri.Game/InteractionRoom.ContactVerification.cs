using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    private async Task VerifyContactGeometry(string output)
    {
        var checks=new List<object>();int failures=0;
        void Check(string name,bool passed,string actual)
        {checks.Add(new{name,passed,actual});if(!passed)failures++;GD.Print((passed?"PASS ":"FAIL ")+name+": "+actual);}
        try
        {
            _autoClock=-10000;_muted=true;
            var c=Sim.Selected!;Sim.State.Mode=PlayMode.CloseUp;
            await RenderFrames(60);var actor=_actors[c.Id];
            Vector2 cheek=_camera.UnprojectPosition(actor.ContactPoint(BodyPart.Cheek,-1))+new Vector2(9,3);
            var hit=ContactAt(cheek);
            float offset=hit==null?float.PositiveInfinity:_camera.UnprojectPosition(hit.World).DistanceTo(cheek);
            Check("tool contact stays under the actual cursor",hit!=null&&offset<1,$"surface projection offset {offset:F3}px");
            Vector2 underside=_camera.UnprojectPosition(actor.ContactPoint(BodyPart.Bottom));
            hit=ContactAt(underside);
            Check("upright hidden underside cannot be touched through the face",hit?.Part!=BodyPart.Bottom,hit?.Part.ToString()??"no hit");
            c.Interaction.Supine=true;await RenderFrames(60);
            underside=_camera.UnprojectPosition(actor.ContactPoint(BodyPart.Bottom));hit=ContactAt(underside);
            Check("turning over exposes the real underside",hit?.Part==BodyPart.Bottom,hit?.Part.ToString()??"no hit");
            c.Interaction.Supine=false;c.Interaction.Accessory.Place=AccessoryPlace.Stored;
            await RenderFrames(60);
            Vector2 hiddenAccessory=_camera.UnprojectPosition(actor.ContactPoint(BodyPart.Accessory));hit=ContactAt(hiddenAccessory);
            Check("stored accessories have no phantom contact region",hit?.Part!=BodyPart.Accessory,hit?.Part.ToString()??"no hit");
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
            File.WriteAllText(output,JsonSerializer.Serialize(new{at=DateTimeOffset.UtcNow,scope="Engine ray/contact geometry, authored states; not native mouse acceptance",failures,checks},new JsonSerializerOptions{WriteIndented=true}));
            GetTree().Quit(failures==0?0:1);
        }
        catch(Exception ex){GD.PushError(ex.ToString());GetTree().Quit(2);}
    }
}
