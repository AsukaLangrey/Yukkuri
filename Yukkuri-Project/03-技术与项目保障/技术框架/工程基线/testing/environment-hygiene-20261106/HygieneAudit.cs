using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Yukkuri.Simulation;
public partial class HygieneAudit : Node
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    private async Task Frames(int count){for(int i=0;i<count;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
    public override async void _Ready()
    {
        int failures=0;
        void Check(string name,bool passed,string detail=""){GD.Print((passed?"PASS ":"FAIL ")+name+" "+detail);if(!passed)failures++;}
        try
        {
            if(!OS.GetCmdlineUserArgs().Contains("--interaction-test-profile"))throw new Exception("isolated profile required");
            var room=GD.Load<PackedScene>("res://InteractionRoom.tscn").Instantiate<InteractionRoom>();AddChild(room);room.SetProcess(false);
            T Field<T>(string name)=>(T)typeof(InteractionRoom).GetField(name,Flags)!.GetValue(room)!;
            void Refresh()=>typeof(InteractionRoom).GetMethod("RefreshRoomUi",Flags)!.Invoke(room,null);
            var sim=Field<WorldSimulation>("Sim");sim.State.RoomObjects.Clear();sim.State.Settings.Pause=true;
            string? selected=sim.State.SelectedId;var label=Field<Label>("_environmentLabel");
            foreach(var size in new[]{new Vector2I(1440,900),new Vector2I(1920,1080)})
            {
                GetWindow().Size=size;Refresh();await Frames(12);
                foreach(bool hasSelection in new[]{true,false})
                {
                    sim.State.SelectedId=hasSelection?selected:null;Refresh();await Frames(3);
                    Check(size+" selection="+hasSelection+" score visible",label.IsVisibleInTree()&&label.Text.Contains("100/100"),label.Text);
                    var panel=Field<PanelContainer>("_rightPanel");var rect=label.GetGlobalRect();var pr=panel.GetGlobalRect();
                    Check(size+" label fits sidebar",rect.Position.X>=pr.Position.X&&rect.End.X<=pr.End.X&&rect.Size.Y>0,rect.ToString());
                }
            }
            var waste=new RoomObject{Kind=RoomObjectKind.Feces,Portions=3,RegionId=sim.State.ActiveRegionId};sim.State.RoomObjects.Add(waste);Refresh();await Frames(3);
            Check("paused live update",label.Text.Contains("76/100"),label.Text);
            typeof(InteractionRoom).GetField("_selectedRoomObjectId",Flags)!.SetValue(room,waste.Id);Refresh();await Frames(3);
            var details=Field<Label>("_objectDetails");Check("observe waste detail",details.Text.Contains("每份扣 8")&&Field<Label>("_who").Text=="粪便",details.Text);
            string wasteLabel=(string)typeof(InteractionRoom).GetMethod("WasteLabel",Flags)!.Invoke(room,new object[]{waste})!;
            Check("hover and click waste feedback",wasteLabel.Contains("3 份")&&wasteLabel.Contains("每份扣 8")&&wasteLabel.Contains("Del"),wasteLabel);
            waste.Portions=1000;
            string longLabel=(string)typeof(InteractionRoom).GetMethod("WasteLabel",Flags)!.Invoke(room,new object[]{waste})!;
            typeof(InteractionRoom).GetField("_sceneHoverHint",Flags)!.SetValue(room,longLabel);Refresh();await Frames(4);
            var feedback=Field<Label>("_feedback");var left=Field<PanelContainer>("_leftPanel");
            Check("maximum stack feedback fits left sidebar",left.GetGlobalRect().Encloses(feedback.GetGlobalRect()),feedback.GetGlobalRect().ToString());
            Check("feedback ignores pointer",feedback.MouseFilter==Control.MouseFilterEnum.Ignore);
            sim.State.Settings.Pause=false;sim.CleanSceneObject(waste.Id);Refresh();await Frames(3);
            Check("actual clean UI recovery",label.Text.Contains("100/100"),label.Text);
            sim.State.ActiveRegionId="woodland";sim.State.RoomObjects.Add(new(){Kind=RoomObjectKind.Urine,RegionId="woodland",Portions=2});Refresh();await Frames(3);
            Check("active region UI",label.Text.Contains("90/100"),label.Text);
        }
        catch(Exception e){failures++;GD.Print("FAIL harness "+e);}
        GD.Print("HYGIENE_UI_FAILURES: "+failures);GetTree().Quit(failures==0?0:1);
    }
}
