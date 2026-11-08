namespace Yukkuri.Simulation;

public enum LifeSkill { Cleaning, Follow, Stop, Wait, Care }
public enum SkillEvidenceKind { Demonstration, Attempt, Completed, Failed, Interrupted }
public enum LifeDuty { None, Cleaning, Care }
public sealed class SkillEvidence
{
    public LifeSkill Skill { get; set; }
    public SkillEvidenceKind Kind { get; set; }
    public double At { get; set; }
    public string Description { get; set; } = "";
}
public sealed class SkillEvidenceTotals
{
    public int Demonstrations { get; set; }
    public int Attempts { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public int Interrupted { get; set; }
}
public sealed class LifeDevelopment
{
    // Defaults deliberately do not invent evidence for older saves.
    public Dictionary<LifeSkill,SkillEvidenceTotals> Skills { get; set; } = new();
    public List<SkillEvidence> Recent { get; set; } = new();
    public LifeDuty Duty { get; set; }
    public string? RecipientId { get; set; }
    public double NextDutyCheck { get; set; }
    public double CleaningStalledSeconds { get; set; }
    public LearnedCue? RecordedCue { get; set; }
    public string? CleaningOrderedById { get; set; }
    public string? OrderedWasteId { get; set; }
    public double CleaningOrderUntil { get; set; }
    public double NextDelegationAt { get; set; }
    public string LastChoreMessage { get; set; } = "";
}
public sealed partial class InteractionLife
{
    public LifeDevelopment Development { get; set; } = new();
}
public static class LifeDevelopmentRules
{
    public static string SkillName(LifeSkill skill)=>skill switch
    {LifeSkill.Cleaning=>"清洁",LifeSkill.Follow=>"跟随",LifeSkill.Stop=>"停下",LifeSkill.Wait=>"等待",_=>"照护"};
    public static LifeSkill CueSkill(LearnedCue cue)=>cue switch
    {LearnedCue.Follow=>LifeSkill.Follow,LearnedCue.Stop=>LifeSkill.Stop,_=>LifeSkill.Wait};
    public static void Validate(WorldState state)
    {
        var ids=state.Creatures.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
        foreach(var c in state.Creatures)
        {
            var d=c.Interaction.Development;
            if(d==null||d.Skills==null||d.Recent==null||d.Skills.Count>5||d.Recent.Count>40||!Enum.IsDefined(d.Duty)||
                !double.IsFinite(d.NextDutyCheck)||d.NextDutyCheck<0||!double.IsFinite(d.CleaningStalledSeconds)||d.CleaningStalledSeconds is <0 or >10||
                d.RecordedCue is { } cue&&!Enum.IsDefined(cue)||
                !double.IsFinite(d.CleaningOrderUntil)||d.CleaningOrderUntil<0||!double.IsFinite(d.NextDelegationAt)||d.NextDelegationAt<0||
                d.LastChoreMessage==null||d.LastChoreMessage.Length>240||
                (d.CleaningOrderedById==null?d.OrderedWasteId!=null:d.CleaningOrderedById==c.Id||!ids.Contains(d.CleaningOrderedById)||string.IsNullOrWhiteSpace(d.OrderedWasteId))||
                (d.Duty==LifeDuty.Care?(d.RecipientId==null||d.RecipientId==c.Id||!ids.Contains(d.RecipientId)):d.RecipientId!=null))
                throw new InvalidDataException("能力或分工记录无效");
            foreach(var (skill,total) in d.Skills)
                if(!Enum.IsDefined(skill)||total==null||new[]{total.Demonstrations,total.Attempts,total.Completed,total.Failed,total.Interrupted}.Any(v=>v is <0 or >1000000))
                    throw new InvalidDataException("能力计数无效");
            double previous=-1;
            foreach(var e in d.Recent)
            {
                if(e==null||!Enum.IsDefined(e.Skill)||!Enum.IsDefined(e.Kind)||!double.IsFinite(e.At)||e.At<previous||e.At<0||e.At>state.WorldSeconds+.001||
                    string.IsNullOrWhiteSpace(e.Description)||e.Description.Length>240)
                    throw new InvalidDataException("能力经历无效");
                previous=e.At;
            }
        }
    }
}
public sealed partial class WorldSimulation
{
    private void RecordAbility(Creature c,LifeSkill skill,SkillEvidenceKind kind,string description)
    {
        var d=c.Interaction.Development;
        if(!d.Skills.TryGetValue(skill,out var total))d.Skills[skill]=total=new();
        static int Add(int n)=>Math.Min(1000000,n+1);
        switch(kind)
        {
            case SkillEvidenceKind.Demonstration:total.Demonstrations=Add(total.Demonstrations);break;
            case SkillEvidenceKind.Attempt:total.Attempts=Add(total.Attempts);break;
            case SkillEvidenceKind.Completed:total.Completed=Add(total.Completed);break;
            case SkillEvidenceKind.Failed:total.Failed=Add(total.Failed);break;
            case SkillEvidenceKind.Interrupted:total.Interrupted=Add(total.Interrupted);break;
        }
        d.Recent.Add(new(){Skill=skill,Kind=kind,At=State.WorldSeconds,Description=description.Length>240?description[..240]:description});
        if(d.Recent.Count>40)d.Recent.RemoveRange(0,d.Recent.Count-40);
    }
    private void SynchronizeAbilityAttempts(Creature c)
    {
        var d=c.Interaction.Development;
        if(d.RecordedCue is { } previous&&c.Interaction.ActiveCue!=previous)
        {RecordAbility(c,LifeDevelopmentRules.CueSkill(previous),SkillEvidenceKind.Interrupted,"练习被其他动作或身体状态中断，未计为完成。");d.RecordedCue=null;}
    }
    public string AbilitySummary(string id)
    {
        var c=State.Creatures.Find(c=>c.Id==id);if(c==null)return "对象已不在这里。";
        var lines=new List<string>();
        foreach(var skill in Enum.GetValues<LifeSkill>())
        {
            c.Interaction.Development.Skills.TryGetValue(skill,out var t);t??=new();
            string level=skill==LifeSkill.Cleaning
                ?t.Completed==0?"尚无独立完成记录":$"熟悉度 {c.Interaction.CleaningLearning:0}/100"
                :skill==LifeSkill.Care?"以送达食水或完成陪护为依据":"以实际动作完成为依据";
            lines.Add($"{LifeDevelopmentRules.SkillName(skill)} · {level}\n示范 {t.Demonstrations} · 尝试 {t.Attempts} · 完成 {t.Completed} · 未完成 {t.Failed} · 中断 {t.Interrupted}");
        }
        lines.Add("完成次数是经历，不是永久熟练保证；跟随等指令完成也不等同于无人提示的自主运用。旧存档不会补造这些新记录。");
        lines.Add(CleaningDisposition(c));
        foreach(var (cue,learning) in c.Interaction.Learning)
            lines.Add($"{LifeDevelopmentRules.SkillName(LifeDevelopmentRules.CueSkill(cue))}原有累计：尝试 {learning.Attempts} / 完成 {learning.Successes} / 非示范运用 {learning.Applications}");
        lines.Add(FamilyRules.DescribeLearning(c));
        foreach(var (key,source) in c.Family.LearningSources.OrderBy(p=>p.Key,StringComparer.Ordinal))
            lines.Add($"{FamilyRules.KnowledgeName(key)}：{(source.Source==LifeKnowledgeSource.Experience?"实际经历":source.Source==LifeKnowledgeSource.Taught?"听过教导":"亲代传承")}，{source.At:0}秒记录；仍可能判断有误。");
        return string.Join("\n\n",lines);
    }
}
