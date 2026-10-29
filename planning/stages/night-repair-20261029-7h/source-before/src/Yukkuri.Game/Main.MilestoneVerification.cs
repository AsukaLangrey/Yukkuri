using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Yukkuri.Simulation;

public partial class Main
{
    private async void StartMilestoneIntegration()
    {
        try
        {
            await Wait(1);
            await Screenshot("m01-menu");
            await ClickButton("menu-help");await Screenshot("m01-user-guide");
            Assert(_helpPanel.Visible&&Sim.State.Settings.Pause,"menu opens the in-game guide");
            await ClickButton("start");
            Assert(_helpPanel.Visible&&!_milestoneStarted,"guide blocks the underlying menu start action");
            await LogicalShortcut(Key.Escape);
            Assert(!_helpPanel.Visible&&_welcome.Visible&&Sim.State.Settings.Pause,"closing guide returns to the still-paused menu");
            await ClickButton("start");await Wait(1);
            Assert(Sim.State.MilestoneScenario&&!_welcome.Visible,"M01 starts from actual menu");
            await ClickButton("manual");await ClickButton("close-help");
            Assert(!Sim.State.Settings.Pause&&!_helpPanel.Visible,"gameplay guide restores the running world");
            await ClickButton("pause");await ClickButton("save");
            Assert(Sim.State.Settings.Pause,"pause before saving is effective");
            await ClickButton("pause");await ClickButton("load");
            Assert(Sim.State.Settings.Pause,"toolbar load preserves a paused save instead of old menu state");
            await LogicalShortcut(Key.Escape);await ClickButton("continue");
            Assert(Sim.State.Settings.Pause,"menu continue preserves a paused save");
            _slot=3;UpdateUi();
            await LogicalShortcut(Key.Escape);await ClickButton("continue");
            Assert(_slot==1&&_saveSlotOption.Selected==0,"menu slot-one load synchronizes the visible save-slot selector");
            // A controlled autosave fixture verifies its real menu route; natural generation is tested separately.
            Save(0,false);int autoFood=Sim.State.Food;await ClickButton("feed");
            await LogicalShortcut(Key.Escape);await ClickButton("continue-auto");
            Assert(Sim.State.Food==autoFood&&Sim.State.Settings.Pause&&!_welcome.Visible,"automatic save can be restored from the player menu");
            await ClickButton("pause");await ClickButton("save");
            await ClickButton("pause");await LogicalShortcut(Key.Escape);await ClickButton("continue");
            Assert(!Sim.State.Settings.Pause,"menu continue restores running save even if previous world was paused");
            // Replay the real Windows input failure: valid logical key, unusable scan code.
            await LogicalShortcut(Key.E);
            Assert(Sim.State.Mode==PlayMode.CloseUp,"logical E works with virtual keyboard scan code");
            var leavePoint=_leaveCloseButton.GetGlobalRect().GetCenter();
            Mouse(leavePoint,true);await Wait(.05);Mouse(leavePoint,false);await Wait(.3);
            Assert(Sim.State.Mode!=PlayMode.CloseUp,"visible return link exits close-up through mouse input");
            await LogicalShortcut(Key.E);
            await LogicalShortcut(Key.Escape);
            Assert(Sim.State.Mode!=PlayMode.CloseUp,"logical Escape exits close-up");
            int keyboardFood=Sim.State.Food;
            await LogicalShortcut(Key.F);
            Assert(Sim.State.Food==keyboardFood-1,"logical F consumes one real food item");
            await LogicalShortcut(Key.Tab);
            double keyboardX=Sim.State.PlayerX,keyboardZ=Sim.State.PlayerZ;
            Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.Pause,Keycode=Key.W,Pressed=true});
            await Wait(.5);
            Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.Pause,Keycode=Key.W,Pressed=false});
            Assert(WorldSimulation.Distance(keyboardX,keyboardZ,Sim.State.PlayerX,Sim.State.PlayerZ)>.5,"logical W moves the visible protagonist");
            await LogicalShortcut(Key.Tab);
            await Screenshot("m01-home");
            var oil=Sim.Selected!;
            await ClickButton("stroke");await Wait(1);
            Assert(Sim.State.Mode==PlayMode.CloseUp,"selecting care tool enters close-up");
            var actor=Actors[oil.Id];double trust=oil.Trust;double time=Sim.State.WorldSeconds;
            var forehead=Camera.UnprojectPosition(actor.GlobalPosition+new Vector3(0,.24f,.25f));
            Mouse(forehead,true);await Wait(1.2);await Screenshot("m01-stroke-contact");Mouse(forehead,false);await Wait(.25);
            Assert(oil.Trust>trust,"real held contact increases trust");
            Assert(Sim.State.WorldSeconds==time,"close-up contact does not age the world");
            await Screenshot("m01-stroke-response");
            await ClickButton("hug");await Wait(.15);
            var body=Camera.UnprojectPosition(actor.GlobalPosition+new Vector3(0,-.08f,.3f));
            Mouse(body,true);await Wait(1);await Screenshot("m01-hug-contact");Mouse(body,false);await Wait(.2);
            Assert(oil.ActionCounts.ContainsKey("Hug"),"hug contact reaches real simulation");
            await ClickButton("nuzzle");await Wait(.15);
            var cheek=Camera.UnprojectPosition(actor.GlobalPosition+new Vector3(.24f,0,.28f));
            Mouse(cheek,true);await Wait(1);await Screenshot("m01-nuzzle-contact");Mouse(cheek,false);await Wait(.2);
            Assert(oil.ActionCounts.ContainsKey("Nuzzle"),"nuzzle contact reaches real simulation");
            await Screenshot("m01-affection-response");
            for(int i=0;i<3;i++)
            {
                await ClickButton("train");await Wait(.1);
                var contact=Camera.UnprojectPosition(actor.GlobalPosition+new Vector3(0,.2f,.25f));
                Mouse(contact,true);await Wait(.8);Mouse(contact,false);await Wait(.15);
            }
            Assert(oil.TrainingCount==3&&oil.GuardSkill>0,"three held training actions improve actual learned ability");
            Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.E,Pressed=true});await Wait(.1);
            Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.E,Pressed=false});await Wait(.5);
            Assert(Sim.State.Mode!=PlayMode.CloseUp,"E exits interaction without catch-up");
            await ClickButton("feed");await ClickButton("observe");
            await ClickButton("commission");
            Assert(Sim.State.Progress.Completed.Contains("contract"),"actual care record pays out after successful care");
            int food=Sim.State.Food;int money=Sim.State.Money;
            await ClickButton("supplies");
            Assert(Sim.State.Food==food+20&&Sim.State.Money==money-10,"food purchase spends money and replenishes real stock");
            await Screenshot("m01-care-loop");
            await ClickButton("wild");await Wait(.7);
            Assert(Sim.State.ActiveRegionId=="wild","travel button opens wilderness");
            await Screenshot("m01-wilderness");
            var wild=Sim.Selected!;
            for(int i=0;i<6;i++)await ClickButton("feed");
            await ClickButton("rescue");await Wait(.5);
            Assert(wild.RegionId=="home"&&wild.Quarantine,"invitation UI moves trusted creature to observation area");
            await ClickButton("home");await Wait(.5);
            Sim.Select(wild.Id);UpdateUi();await Wait(.2);
            await ClickButton("admit");Assert(!wild.Quarantine,"player admission UI is effective");
            await ClickButton("save");var saved=_saves.Load(1);
            Assert(saved.State.Creatures.Any(c=>c.Id==wild.Id&&c.Belonging>=45),"save preserves new family member and belonging");
            await Screenshot("m01-admission");
            // Set encounter scheduling only. The event itself, defense and help run through normal simulation.
            Sim.State.Threat.NextAt=Sim.State.WorldSeconds;
            var defender=Sim.State.Creatures.Single(c=>c.Name=="松果");defender.GuardSkill=0;defender.Health=70;defender.Energy=40;
            await Wait(1);
            for(int i=0;i<180&&!Sim.State.Threat.HelpRequested;i++)await Wait(.25);
            Assert(Sim.State.Threat.HelpRequested,"living guardian calls for help during real encounter ticks");
            await Screenshot("m01-help");
            await ClickButton("help");await Wait(.5);
            await ClickButton("protect");
            Assert(Sim.Selected?.Speech.Contains("我们的家")==true,"rescued park guardian recognizes its existing home instead of requesting a wild invitation");
            await Wait(9);
            Assert(Sim.State.Progress.Completed.Contains("defense"),"UI protection resolves real encounter");
            await Screenshot("m01-defense-result");
            await ClickButton("save");await ClickButton("load");await Wait(.5);
            Assert(Sim.State.Progress.Completed.Contains("defense"),"load restores encounter outcome");
            Assert(Sim.State.ScenarioComplete,"all six named M01 objectives have been completed through gameplay");
            await Screenshot("m01-final");
            var storyResult=new{worldSeconds=Sim.State.WorldSeconds,progress=Sim.State.Progress.Completed.ToArray(),identity=oil.Id,scenarioComplete=Sim.State.ScenarioComplete};
            var correctionComparison=await VerifyM01CorrectionKnowledge();
            await VerifyM01PhysicalConsequences();
            var result=new{success=true,scope="Real Godot GUI/input/physics with explicitly controlled comparison fixtures; not user playtest",story=storyResult,correctionComparison,physicalComparison=new{worldSeconds=Sim.State.WorldSeconds,progress=Sim.State.Progress.Completed.ToArray()}};
            Directory.CreateDirectory(EvidenceDirectory);File.WriteAllText(Path.Combine(EvidenceDirectory,"m01-integration-result.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
            GD.Print("M01_INTEGRATION_COMPLETE");QuitGameCleanly(0);
        }
        catch(Exception e)
        {
            GD.PushError(e.ToString());
            try{await Screenshot("m01-failure");File.WriteAllText(Path.Combine(EvidenceDirectory,"m01-integration-result.json"),JsonSerializer.Serialize(new{success=false,error=e.ToString()}));}catch{}
            QuitGameCleanly(5);
        }
    }
    private async Task LogicalShortcut(Key logical)
    {
        Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.Pause,Keycode=logical,Pressed=true});
        await Wait(.1);
        Input.ParseInputEvent(new InputEventKey{PhysicalKeycode=Key.Pause,Keycode=logical,Pressed=false});
        await Wait(.3);
    }
    private async Task<object> VerifyM01CorrectionKnowledge()
    {
        var observations=new System.Collections.Generic.List<object>();
        foreach(bool justified in new[]{true,false})
        {
            // Freeze incidental movement and staff behavior in the comparison fixture.
            // The intrusion itself and the held correction still use normal world/input paths.
            StartNewMilestone();
            foreach(var region in Sim.State.Regions)region.StaffEnabled=false;
            foreach(var oil in Sim.State.Creatures){oil.BehaviorCooldown=9999;oil.DecisionIn=9999;oil.GuardDuty=false;}
            var witness=Sim.State.Creatures[0];var remote=Sim.State.Creatures[4];
            witness.X=witness.TargetX=-7;witness.Z=witness.TargetZ=3;
            remote.X=remote.TargetX=8;remote.Z=remote.TargetZ=-8;
            Creature target;
            if(justified)
            {
                Sim.State.Threat.NextAt=0;Sim.Advance(11);
                target=Sim.State.Creatures.Single(c=>c.Id==Sim.State.Threat.IntruderId);
                Assert(Sim.Knows(witness,target.CurrentOffenseId),"GUI comparison witness observed the real intrusion");
            }
            else
            {
                Sim.State.Settings.Invasions=false;target=Sim.State.Creatures[2];
                target.X=target.TargetX=-7.8;target.Z=target.TargetZ=3.8;
            }
            Sim.State.Settings.Pause=true;Sim.Select(target.Id);RebuildRegion();UpdateUi();await Wait(.3);
            await ClickButton("discipline");await Wait(1.2);
            var actor=Actors[target.Id];double trust=witness.Trust,fear=witness.Fear,remoteFear=remote.Fear;
            _pressure.Value=.25;_contactTool=ContactTool.Hand;_toolMaterial.Select(0);
            var point=Camera.UnprojectPosition(actor.GlobalPosition+new Vector3(0,-.10f,.35f)*actor.BodyScale);
            Mouse(point,true);await Wait(1);Mouse(point,false);await Wait(.2);
            Assert(target.ActionCounts.TryGetValue("Discipline",out int corrections)&&corrections>0,"comparison held mouse input applies correction");
            Assert(justified?witness.Trust>trust&&witness.Fear<=fear:witness.Fear>fear,"same moderate input distinguishes reasoned correction from arbitrary harm");
            Assert(remote.Fear==remoteFear,"remote uninformed observer remains unchanged in GUI comparison");
            await Screenshot(justified?"m01-known-intruder-correction":"m01-arbitrary-correction");
            observations.Add(new{justified,witnessTrustChange=witness.Trust-trust,witnessFearChange=witness.Fear-fear,remoteFearChange=remote.Fear-remoteFear});
            ExitCloseUp();
        }
        return observations;
    }
    private async Task VerifyM01PhysicalConsequences()
    {
        // Explicit controlled comparison fixture. All contact effects below use normal mouse input and physics.
        StartNewMilestone();Sim.State.Settings.Invasions=false;Sim.State.Settings.Pause=true;
        var target=Sim.Selected!;var near=Sim.State.Creatures[1];var far=Sim.State.Creatures[4];
        target.X=0;target.Z=0;near.X=1;near.Z=0;far.X=8;far.Z=-8;
        RebuildRegion();UpdateUi();await Wait(.4);
        await ClickButton("discipline");await Wait(1);
        var actor=Actors[target.Id];double health=target.Health,nearFear=near.Fear,farFear=far.Fear;
        _pressure.Value=.85;_contactTool=ContactTool.TrainingPaddle;_toolMaterial.Select(2);
        var cheek=Camera.UnprojectPosition(actor.GlobalPosition+new Vector3(.22f,0,.3f));
        Mouse(cheek,true);await Wait(2);await Screenshot("m01-pressure-contact");Mouse(cheek,false);await Wait(.3);
        Assert(target.Health<health&&target.Pain>0,"strong held contact creates real injury and pain");
        Assert(near.Fear>nearFear&&far.Fear==farFear,"visible near witness fears arbitrary harm while distant uninformed creature does not");
        await Screenshot("m01-pressure-response");
        await ClickButton("treat");await Wait(.2);
        var body=Camera.UnprojectPosition(actor.GlobalPosition+new Vector3(0,.17f,.26f));
        int medicine=Sim.State.Medicine;health=target.Health;
        Mouse(body,true);await Wait(1);Mouse(body,false);await Wait(.2);
        Assert(target.Health>health&&Sim.State.Medicine==medicine-1,"held nursing input spends medicine and improves real injury");
        await ClickButton("grab");await Wait(.2);
        var point=Camera.UnprojectPosition(actor.GlobalPosition);
        Mouse(point,true);await Wait(.1);
        for(int i=0;i<12;i++)Input.ParseInputEvent(new InputEventMouseButton{Position=GetViewport().GetFinalTransform()*point,ButtonIndex=MouseButton.WheelUp,Pressed=true});
        await Wait(1.8);
        Assert(actor.GlobalPosition.Y>1.8,"mouse grab and wheel physically lift target");
        await Screenshot("m01-lift");
        health=target.Health;
        Mouse(point,false);await Wait(2);
        Assert(target.Health<health&&target.InjuryCause=="跌落碰撞","actual rigid-body fall reaches impact injury rule");
        await Screenshot("m01-fall-response");
        ExitCloseUp();UpdateUi();await Wait(.3);
        await ClickButton("diary");await Wait(.3);
        Assert(_diaryPanel.Visible&&_diaryText.Text.Contains("跌落"),"player can inspect known experiences in the diary");
        await Screenshot("m01-diary");await ClickButton("close-diary");
    }
}
