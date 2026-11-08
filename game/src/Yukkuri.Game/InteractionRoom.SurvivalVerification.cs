using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class InteractionRoom
{
    // Prepared source only. This entry is inert without an explicit future test flag.
    private void StartSurvivalUiVerificationIfRequested(string[] args)
    {
        int flag=Array.IndexOf(args,"--survival-ui-check-output");
        if(flag>=0&&flag+1<args.Length)_=VerifySurvivalInspector(Path.GetFullPath(args[flag+1]));
    }
    private async Task VerifySurvivalInspector(string directory)
    {
        int assertions=0;
        void Check(bool ok,string why){assertions++;if(!ok)throw new InvalidOperationException(why);}
        try
        {
            Directory.CreateDirectory(directory);_autoClock=-100000;_muted=true;
            Sim=WorldSimulation.CreateCourtyardEconomy();Sim.State.Settings.Pause=true;
            foreach(var c in Sim.State.Creatures)
            {
                c.Name="同名";c.ParentId=null;c.PersonalMemories.Clear();c.Family.MotherId=c.Family.FatherId=null;
                c.Family.PartnerIds.Clear();c.Family.CaregiverIds.Clear();
            }
            Sim.State.ObjectEvents.Clear();Sim.State.FamilyRelationshipEvents.Clear();
            Sim.State.Journal.Add("不得归属给选中对象的全局记录");BuildActors();
            var a=Sim.State.Creatures[0];var b=Sim.State.Creatures[1];
            SelectOil(a.Id);await RenderFrames(5);
            Check(!_inspectorTabs.Visible&&!_eventsTab.Visible&&!_familyPanelButton.Visible,"single property tab should hide tab strip");
            Check(RoomRecordsText().Length==0,"unlinked journal leaked");
            Sim.State.ObjectEvents.Add(new(){ObjectId=a.Id,At=0,Text="甲对象自己的事件"});RefreshRoomUi();
            Check(_inspectorTabs.Visible&&_eventsTab.Visible,"related event did not reveal tab");ShowRoomRecords();await RenderFrames(3);
            SelectOil(b.Id);await RenderFrames(3);
            Check(!ActivityPanelVisible&&RoomRecordsText().Length==0,"same-name object inherited event page");
            a.Family.PartnerIds.Add(b.Id);b.Family.PartnerIds.Add(a.Id);SelectOil(a.Id);await RenderFrames(3);
            Check(_propertiesTab.Visible&&_eventsTab.Visible&&_familyPanelButton.Visible,"three related tabs missing");
            ShowRoomRecords();Sim.State.ObjectEvents.Clear();RefreshRoomUi();await RenderFrames(3);
            Check(!ActivityPanelVisible&&!_eventsTab.Visible,"removed event did not return to attributes");
            Sim.State.Mode=PlayMode.Management;SelectObserved(null,null);await RenderFrames(3);
            Check(!_rightPanel.Visible&&Sim.Selected==null&&!_inspectorTabs.Visible,"blank ground did not collapse zero-tab inspector");
            var wide=GameUiLayout.Canvas(_ui);SelectOil(a.Id);await RenderFrames(3);
            Check(GameUiLayout.Canvas(_ui).Size.X<wide.Size.X&&_rightPanel.Visible,"reselection did not restore canvas/sidebar geometry");
            ShowCreatureMarket();var selected=Sim.State.SelectedId;SelectObserved(null,null);await RenderFrames(3);
            Check(ActivityPanelVisible&&Sim.State.SelectedId==selected,"management click collapsed selection");CloseActivityPanel();
            _ui.SetMeta("right_sidebar_width",266f);RefreshRoomUi();await RenderFrames(3);
            Check(Math.Abs(_rightPanel.Size.X-266)<1,"dynamic narrow sidebar width ignored");
            Check(IsScenePoint(GameUiLayout.Canvas(_ui).GetCenter()),"canvas input does not share layout bounds");
            Sim.State.Creatures.Remove(a);Sim.State.SelectedId=null;RefreshRoomUi();await RenderFrames(3);
            Check(!_rightPanel.Visible&&!ActivityPanelVisible,"deleted object did not return safely");
            Check(Sim.State.Settings.Pause,"inspector navigation changed pause");
            File.WriteAllText(Path.Combine(directory,"survival-ui-result.txt"),"PASS assertions="+assertions);GetTree().Quit(0);
        }
        catch(Exception e){File.WriteAllText(Path.Combine(directory,"survival-ui-error.txt"),e.ToString());GD.PushError(e.ToString());GetTree().Quit(2);}
    }
}
