using Godot;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Yukkuri.Simulation;

public partial class Main
{
    private async void StartMilestoneStates()
    {
        try
        {
            StartNewMilestone();Sim.State.Settings.Invasions=false;
            foreach(var c in Sim.State.Creatures){c.Fullness=100;c.Health=100;c.BehaviorCooldown=9999;c.DecisionIn=9999;}
            var hungryResident=Sim.State.Creatures[0];hungryResident.Fullness=10;hungryResident.X=hungryResident.TargetX=6;hungryResident.Z=hungryResident.TargetZ=-5;
            var hungryIntruder=Sim.State.Creatures[7];hungryIntruder.RegionId="home";hungryIntruder.IsIntruder=true;hungryIntruder.Fullness=0;hungryIntruder.X=hungryIntruder.TargetX=-6;hungryIntruder.Z=hungryIntruder.TargetZ=-5;
            Sim.ActiveRegion.StaffCooldown=9999;_employee!.Position=new Vector3(0,0,-4);
            Sim.State.Settings.Pause=true;var employeePaused=_employee.Position;
            await Wait(.8);bool employeeStopped=_employee.Position.DistanceTo(employeePaused)<.01f;
            hungryResident.Fullness=100;_employee.Position=new Vector3(0,0,-4);
            Sim.State.Settings.Pause=false;await Wait(1.5);bool employeeIgnoresIntruder=_employee.Position.X>0;
            hungryResident.Fullness=10;hungryResident.X=hungryResident.TargetX=-6;
            hungryIntruder.X=hungryIntruder.TargetX=6;_employee.Position=new Vector3(0,0,-4);
            await Wait(1.5);bool employeeApproachesResident=_employee.Position.X<0;
            GD.Print($"STAFF_PRESENTATION paused={employeeStopped}, ignores_intruder={employeeIgnoresIntruder}, approaches_resident={employeeApproachesResident}");
            Assert(employeeStopped&&employeeIgnoresIntruder&&employeeApproachesResident,"employee stays still when paused, ignores hungry intruders and approaches eligible residents");
            await Wait(.5);StartNewMilestone();Sim.State.Settings.Pause=true;
            var oil=Sim.Selected!;
            while(oil.Health>0)Sim.Hurt(oil,10,"状态显示验证",null);
            await Wait(.4);UpdateUi();await Screenshot("m01-state-critical");
            Assert(oil.Condition==PhysicalCondition.Critical&&_detail.Text.Contains("重伤待救"),"critical status is visible and distinct");
            await ClickButton("treat");await Wait(.6);
            var actor=Actors[oil.Id];var point=Camera.UnprojectPosition(actor.GlobalPosition+new Vector3(0,.17f,.26f));
            Mouse(point,true);await Wait(.8);Mouse(point,false);await Wait(.3);
            Assert(oil.Condition==PhysicalCondition.Recovering,"real nursing input stabilizes critical creature");
            await Screenshot("m01-state-recovering");
            ExitCloseUp();oil.Fullness=10;UpdateUi();
            Assert(_relationship.Text.Contains("184 秒"),"low-nutrition recovery estimate reflects slower progress");
            int foodBefore=Sim.State.Food;await ClickButton("feed");
            Assert(Sim.State.Food==foodBefore-1&&oil.Fullness>10&&oil.Condition==PhysicalCondition.Recovering,"real feeding button nourishes recovering creature without premature mobility");
            await ClickButton("feed");UpdateUi();
            Assert(oil.Fullness>35&&_relationship.Text.Contains("55 秒"),"feeding updates the recovery estimate to the nourished rate");
            await Screenshot("m01-state-recovery-nutrition");
            Sim.State.Settings.Pause=false;Sim.Advance(60);Sim.State.Settings.Pause=true;
            await Wait(.3);UpdateUi();await Screenshot("m01-state-recovered");
            Assert(oil.Condition==PhysicalCondition.Active,"stabilized creature recovers after simulated recovery interval");
            oil.Trust=85;oil.Familiarity=12;
            // Reproduce death while an emergency request is active. This is a controlled state fixture.
            oil.Health=1;oil.FillingIntegrity=1;
            Sim.Contact(oil.Id,new(ContactAction.Discipline,ContactArea.Cheek,ContactTool.TrainingPaddle,1,6));
            await Wait(.4);UpdateUi();var first=actor.VisualMotionOffset;
            await Wait(.7);var second=actor.VisualMotionOffset;
            await Screenshot("m01-state-dead");
            var observation=new{condition=oil.Condition.ToString(),pose=actor.PoseTag,relationship=_relationship.Text,helpTarget=Sim.State.HelpTargetId,
                firstOffset=new[]{first.X,first.Y,first.Z},secondOffset=new[]{second.X,second.Y,second.Z},
                noLivingMotion=first.IsZeroApprox()&&second.IsZeroApprox(),noSocialReadiness=_relationship.Text.Contains("无法回应"),noDeadHelp=Sim.State.HelpTargetId!=oil.Id,
                noPostFatalSpeech=oil.Speech==""&&oil.LastReaction=="再也没有回应。",noDeadFeeding=Buttons["feed"].Disabled};
            File.WriteAllText(Path.Combine(EvidenceDirectory,"body-state-observation.json"),JsonSerializer.Serialize(observation,new JsonSerializerOptions{WriteIndented=true}));
            Assert(observation.noLivingMotion&&observation.noSocialReadiness&&observation.noDeadHelp&&observation.noPostFatalSpeech&&observation.noDeadFeeding,"fatal contact leaves no living motion, readiness, speech, feeding or stale help request");
            StartNewMilestone();foreach(var region in Sim.State.Regions)region.StaffEnabled=false;
            Sim.State.Threat.NextAt=0;Sim.Advance(11);Sim.State.Settings.Pause=true;
            var guardian=Sim.State.Creatures[1];guardian.X=-7.5;guardian.Z=3.8;Sim.Select(guardian.Id);
            while(guardian.Health>0)Sim.Hurt(guardian,10,"保护重伤者验证",null);
            RebuildRegion();UpdateUi();await Wait(.3);
            Assert(!Buttons["protect"].Disabled&&guardian.Activity.StartsWith("重伤待救"),"critical victim has a usable protection action and no stale guarding activity while paused");
            await ClickButton("protect");
            Assert(Sim.State.Threat.PlayerResponded&&guardian.Condition==PhysicalCondition.Critical&&Sim.State.HelpTargetId==guardian.Id,"real protection button repels attacker while keeping critical rescue request");
            await Screenshot("m01-state-critical-protected");
            StartNewMilestone();await ClickButton("close");await Wait(.5);
            var suspended=Actors[Sim.Selected!.Id];suspended.Position+=Vector3.Up*2;suspended.LinearVelocity=Vector3.Zero;
            await ClickButton("diary");var pausedPosition=suspended.Position;double pausedHealth=Sim.Selected.Health;
            await Wait(1);await Screenshot("m01-diary-paused-physics");
            Assert(suspended.Freeze&&suspended.Position.DistanceTo(pausedPosition)<.01f&&Sim.Selected.Health==pausedHealth,"diary pauses close-up physics instead of dropping the creature behind the panel");
            int pausedFood=Sim.State.Food;await ClickButton("supplies");
            Assert(Sim.State.Food==pausedFood&&_diaryPanel.Visible,"diary blocks background purchase controls");
            await ClickButton("close-diary");
            await Wait(.2);Assert(!suspended.Freeze,"closing diary restores close-up physics");
            suspended.Position+=Vector3.Up*2;suspended.LinearVelocity=Vector3.Zero;
            await ClickButton("settings");pausedPosition=suspended.Position;pausedHealth=Sim.Selected.Health;
            await Wait(1);
            Assert(suspended.Freeze&&suspended.Position.DistanceTo(pausedPosition)<.01f&&Sim.Selected.Health==pausedHealth,"settings pauses close-up physics");
            await ClickButton("close-settings");await Wait(.2);
            Assert(!suspended.Freeze&&!Sim.State.Settings.Pause,"closing settings restores the previous running state");
            ExitCloseUp();StartNewMilestone();UpdateUi();
            var rosterClick=ChooseRoster("小铃");
            Sim.State.Threat.NextAt=0;Sim.Advance(11);UpdateUi();
            await rosterClick;
            Assert(Sim.Selected?.Name=="小铃","an arriving intruder does not dispose an existing resident button during input");
            StartNewMilestone();Sim.State.Settings.Invasions=false;
            foreach(var region in Sim.State.Regions)region.StaffEnabled=false;
            foreach(var creature in Sim.State.Creatures){creature.BehaviorCooldown=9999;creature.DecisionIn=9999;}
            Travel("wild");var bullied=Sim.State.Creatures[5];var bully=Sim.State.Creatures[7];
            foreach(var creature in Sim.State.Creatures){creature.X=creature.TargetX=8;creature.Z=creature.TargetZ=-8;}
            bullied.X=bullied.TargetX=0;bullied.Z=bullied.TargetZ=0;
            bully.X=bully.TargetX=.2;bully.Z=bully.TargetZ=0;bully.BehaviorCooldown=0;
            Sim.Advance(.25);Sim.State.Settings.Pause=true;RebuildRegion();UpdateUi();
            await ClickButton("help");Assert(Sim.Selected?.Id==bullied.Id,"help button selects the actual victim of a current wild attack");
            await ClickButton("protect");double protectedTrust=bullied.Trust;
            Assert(bully.RetreatRemaining>0&&bully.CurrentOffenseId==null,"protection button ends the recorded wild offense");
            await ClickButton("protect");
            Assert(bullied.Trust==protectedTrust&&Buttons["help"].Disabled,"repeated protection input gives no extra reward and resolved help disappears");
            await Screenshot("m01-resolved-wild-help");
            StartNewMilestone();Travel("wild");
            var migrating=Sim.State.Creatures[7];Sim.Select(migrating.Id);Sim.State.Threat.NextAt=0;
            Sim.Advance(11);Sim.State.Settings.Pause=true;RebuildRegion();UpdateUi();
            Assert(migrating.RegionId=="home"&&Sim.Selected?.RegionId=="wild","autonomous invasion updates the visible selection before saving in the wild");
            await ClickButton("save");Assert(_saves.Load(_slot).State.SelectedId==Sim.State.SelectedId,"save button preserves a valid selection after the selected oil moves away");
            Travel("home");Sim.Select(migrating.Id);Sim.State.Threat.Phase=ThreatPhase.Retreating;Sim.State.Threat.PhaseTime=8;
            Sim.State.Settings.Pause=false;Sim.Advance(.25);Sim.State.Settings.Pause=true;RebuildRegion();UpdateUi();
            await ClickButton("save");await ClickButton("load");
            Assert(Sim.Selected?.RegionId=="home"&&Sim.State.Creatures.Single(c=>c.Id==migrating.Id).RegionId=="wild","retreat while selected supports real save and reload without a remote selection");
            await Screenshot("m01-selected-retreat-save");
            StartNewMilestone();
            foreach(var region in Sim.State.Regions)region.StaffEnabled=false;
            foreach(var creature in Sim.State.Creatures){creature.BehaviorCooldown=9999;creature.DecisionIn=9999;creature.GuardDuty=false;}
            Sim.State.Threat.NextAt=0;Sim.Advance(18);
            var ongoingIntruder=Sim.State.Creatures.Single(c=>c.IsIntruder);
            Sim.State.Settings.Pause=true;Sim.Select(ongoingIntruder.Id);RebuildRegion();UpdateUi();
            await ClickButton("stroke");await Wait(.3);
            Assert(Actors[ongoingIntruder.Id].DisplayedExpression==Yukkuri.Simulation.Expression.Defiant&&Actors[ongoingIntruder.Id].PoseTag=="defiant",
                "ongoing intrusion retains its defiant face and posture in close-up after speech expires");
            await Screenshot("m01-ongoing-intruder-expression");
            File.WriteAllText(Path.Combine(EvidenceDirectory,"m01-integration-result.json"),JsonSerializer.Serialize(new{success=true,scope="Controlled injury/recovery/death/protection scenarios with real nursing, feeding and protection input; fixture clocks advanced explicitly, not a natural-play session",observation}));
            GD.Print("M01_INTEGRATION_COMPLETE");QuitGameCleanly(0);
        }
        catch(Exception e){GD.PushError(e.ToString());QuitGameCleanly(9);}
    }
}
