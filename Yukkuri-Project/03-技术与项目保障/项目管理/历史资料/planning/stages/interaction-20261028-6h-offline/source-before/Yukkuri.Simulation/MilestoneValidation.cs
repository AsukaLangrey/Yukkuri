namespace Yukkuri.Simulation;

public static class MilestoneValidation
{
    public static void Validate(WorldState s)
    {
        if(!s.MilestoneScenario)return;
        static void Require(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        static bool Finite(params double[] values)=>values.All(double.IsFinite);
        static bool Range(params double[] values)=>Finite(values)&&values.All(v=>v>=0&&v<=100);
        Require(s.Settings!=null&&s.Progress!=null&&s.Threat!=null&&s.Incidents!=null&&s.PlayerKnownIncidents!=null,"M01世界对象为空");
        var settings=s.Settings!;var progress=s.Progress!;var threat=s.Threat!;
        Require(s.ScenarioVersion==1&&s.Medicine>=0&&s.IncidentSequence>=0&&Range(s.EnvironmentRating)&&Finite(s.SocialTimer)&&s.SocialTimer>=0,"M01世界数值无效");
        Require(Finite(settings.Speed,settings.MusicVolume,settings.EffectsVolume)&&settings.Speed is >=.25 and <=3&&settings.MusicVolume is >=0 and <=1&&settings.EffectsVolume is >=0 and <=1,"设置数值无效");
        Require(progress.Completed!=null&&progress.CareActions>=0&&progress.TrainingSessions>=0&&progress.Rescued>=0&&progress.Defenses>=0&&progress.CareContracts>=0&&Finite(progress.NextContractAt),"阶段进展无效");
        Require(Enum.IsDefined(threat.Phase)&&Finite(threat.NextAt,threat.PhaseTime,threat.EncounterCooldown)&&threat.PhaseTime>=0&&threat.Sequence>=0,"入侵状态无效");
        var ids=s.Creatures.Select(c=>c.Id).ToHashSet();
        Require((threat.IntruderId==null||ids.Contains(threat.IntruderId))&&(threat.DefenderId==null||ids.Contains(threat.DefenderId))&&(s.HelpTargetId==null||ids.Contains(s.HelpTargetId)),"事件对象引用无效");
        var incidentIds=new HashSet<string>();
        foreach(var e in s.Incidents!)
        {
            Require(e!=null,"空事件");
            Require(!string.IsNullOrWhiteSpace(e!.Id)&&incidentIds.Add(e.Id)&&Enum.IsDefined(e.Kind)&&Finite(e.At,e.X,e.Z,e.Severity)&&e.At>=0&&e.Severity>=0&&s.Regions.Any(r=>r.Id==e.RegionId),"事件数据无效");
            Require((e.TargetId==null||ids.Contains(e.TargetId))&&(e.ActorId==null||e.ActorId=="player"||ids.Contains(e.ActorId)),"事件身份引用无效");
        }
        foreach(var c in s.Creatures)
        {
            Require(Range(c.Trust,c.Fear,c.Attachment,c.Resentment,c.Obedience,c.Belonging,c.GuardSkill,c.SharingSkill,c.Pain,c.Strain,c.FillingIntegrity,c.TrainingFatigue),"心理/能力数值越界");
            Require(Finite(c.Courage,c.Aggression,c.Patience)&&c.Courage is >=0 and <=1&&c.Aggression is >=0 and <=1&&c.Patience is >=0 and <=1,"个性数值越界");
            Require(Finite(c.CriticalRemaining,c.RecoveryRemaining,c.OffenseRemaining,c.BehaviorCooldown,c.SocialCooldown,c.RetreatRemaining,c.ReactionRemaining,c.LastPlayerTouchAt),"行为时间无效");
            Require(Enum.IsDefined(c.Condition)&&Enum.IsDefined(c.Expression)&&c.CriticalRemaining>=0&&c.RecoveryRemaining>=0&&c.RetreatRemaining>=0&&c.ReactionRemaining>=0,"身体/反应状态无效");
            Require(c.ParentId==null||c.ParentId!=c.Id&&ids.Contains(c.ParentId),"亲油引用无效");
            Require(c.Knowledge!=null&&c.PersonalMemories!=null&&c.ActionCounts!=null,"记忆数据为空");
            foreach(var k in c.Knowledge!)Require(k!=null&&incidentIds.Contains(k.IncidentId)&&Enum.IsDefined(k.Source)&&Finite(k.LearnedAt),"知情记录无效");
            foreach(var m in c.PersonalMemories!)Require(m!=null&&!string.IsNullOrWhiteSpace(m.Text)&&Finite(m.At,m.EmotionalWeight),"个人记忆无效");
            Require(c.ActionCounts!.All(k=>k.Value>=0)&&c.TrainingCount>=0&&c.Familiarity>=0&&c.CareSessionCount>=0,"互动记录无效");
        }
    }
}
