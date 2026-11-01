namespace Yukkuri.Simulation;

public enum LifeKnowledgeSource { Inherited, Taught, Experience }
public sealed class LifeKnowledgeRecord
{
    public LifeKnowledgeSource Source { get; set; }
    public string? FromId { get; set; }
    public double At { get; set; }
    public bool Misunderstood { get; set; }
}
public sealed class FamilyLesson
{
    public string LearnerId { get; set; } = "";
    public string Key { get; set; } = "";
    public bool Belief { get; set; }
    public double Elapsed { get; set; }
    public double StartedAt { get; set; }
}
public sealed partial class FamilyLife
{
    public FamilyLesson? Lesson { get; set; }
    public double NextLessonAt { get; set; }
    public Dictionary<string,LifeKnowledgeRecord> LearningSources { get; set; } = new();
}
public static partial class FamilyRules
{
    public static string KnowledgeName(string key)=>key switch
    {
        "safe:Meal"=>"饭团","safe:Sweet"=>"甜甜","safe:Water"=>"杯里的水",
        "safe:Leaves"=>"树叶","safe:RiverWater"=>"河水",_=>""
    };
    public static string DescribeLearning(Creature c)
    {
        string beliefs=string.Join("；",c.Family.LifeKnowledge.OrderBy(p=>p.Key,StringComparer.Ordinal)
            .Where(p=>KnowledgeName(p.Key).Length>0).Select(p=>$"{KnowledgeName(p.Key)}：{(p.Value?"觉得可以吃喝":"觉得需要小心")}"));
        return beliefs.Length==0?"还在通过相处和经历了解食水。":"自己的食水判断（可能有误）："+beliefs;
    }
    public static void ValidateLearning(WorldState s)
    {
        static void Require(bool valid,string message){if(!valid)throw new InvalidDataException(message);}
        static bool Time(double value,double max=double.MaxValue)=>double.IsFinite(value)&&value>=0&&value<=max;
        var ids=s.Creatures.Select(c=>c.Id).ToHashSet(StringComparer.Ordinal);
        var learners=new HashSet<string>(StringComparer.Ordinal);
        foreach(var c in s.Creatures)
        {
            var f=c.Family;
            Require(f.LearningSources!=null&&f.LearningSources.Count<=32&&Time(f.NextLessonAt),"生活学习记录无效");
            foreach(var pair in f.LearningSources!)
            {
                var record=pair.Value;
                Require(KnowledgeName(pair.Key).Length>0&&f.LifeKnowledge.ContainsKey(pair.Key)&&record!=null&&
                    Enum.IsDefined(record.Source)&&Time(record.At,s.WorldSeconds+.001),"生活学习来源无效");
                Require(record!.Source==LifeKnowledgeSource.Experience?record.FromId==null&&!record.Misunderstood:
                    record.FromId!=c.Id&&record.FromId!=null&&ids.Contains(record.FromId),"学习的教导者或亲代来源无效");
                Require(!record.Misunderstood||record.Source==LifeKnowledgeSource.Taught,"非教导过程不能凭空产生听错记录");
            }
            if(f.Lesson is not { } lesson)continue;
            var learner=s.Creatures.Find(x=>x.Id==lesson.LearnerId);
            Require(ids.Contains(lesson.LearnerId)&&lesson.LearnerId!=c.Id&&learners.Add(lesson.LearnerId)&&
                KnowledgeName(lesson.Key).Length>0&&f.LifeKnowledge.ContainsKey(lesson.Key)&&
                Time(lesson.Elapsed,6)&&Time(lesson.StartedAt,s.WorldSeconds+.001)&&
                lesson.Elapsed<=s.WorldSeconds-lesson.StartedAt+.001&&
                !c.IsYoung&&c.Condition!=PhysicalCondition.Dead&&learner is { IsYoung:true }&&learner.Condition!=PhysicalCondition.Dead&&
                f.CompanionTargetId==lesson.LearnerId&&learner.Family.CompanionTargetId==c.Id,"正在进行的生活教导无效");
        }
    }
}

public sealed partial class WorldSimulation
{
    private bool ReadyToLearn(Creature c)=>FamilyActionReady(c)&&!NaturalResources.WantsFood(c)&&
        !NaturalResources.WantsWater(c)&&c.Energy>=40;
    private bool CanTeachChild(Creature teacher,Creature child)=>!teacher.IsYoung&&child.IsYoung&&
        ReadyToLearn(teacher)&&ReadyToLearn(child)&&MouthFunction.CanSpeakClearly(teacher)&&FamilyVisible(teacher,child,4)&&
        (child.Family.MotherId==teacher.Id||child.Family.FatherId==teacher.Id||child.ParentId==teacher.Id||
         child.Family.CaregiverIds.Contains(teacher.Id)||teacher.Patience>=.65&&teacher.Aggression<=.25&&
         teacher.Family.Peers.TryGetValue(child.Id,out var caring)&&caring.Affection>=20)&&
        child.Family.Peers.TryGetValue(teacher.Id,out var trust)&&trust.Affection>=10&&trust.Fear<35&&trust.Resentment<30;

    public string TeachFamilyKnowledge(string teacherId,string learnerId,string key)
    {
        var teacher=State.Creatures.Find(c=>c.Id==teacherId);var learner=State.Creatures.Find(c=>c.Id==learnerId);
        var error=FamilyCommandError(teacher)??FamilyCommandError(learner);if(error!=null)return error;
        if(teacher==learner||FamilyRules.KnowledgeName(key).Length==0||!teacher!.Family.LifeKnowledge.TryGetValue(key,out bool belief))
            return "只能讲述自己已经知道的食水经验，不能凭空教会未知常识。";
        if(teacher.Family.Lesson!=null||State.Creatures.Any(c=>c.Family.Lesson?.LearnerId==learnerId))return "已经有教导正在进行，先让它们说完。";
        if(State.WorldSeconds<teacher.Family.NextLessonAt||State.WorldSeconds<learner!.Family.NextLessonAt)
            return "刚刚学过或尝试过，先留一些消化和生活的时间。";
        if(!CanTeachChild(teacher,learner!)||teacher.Family.CompanionTargetId is { } t&&t!=learnerId||
            learner!.Family.CompanionTargetId is { } l&&l!=teacherId)return "双方还没有愿意安静相处，先照顾吃喝、身体和关系。";
        teacher.Family.Lesson=new(){LearnerId=learnerId,Key=key,Belief=belief,StartedAt=State.WorldSeconds};
        BeginCompanionship(teacher,learner!);
        return State.LastActionMessage="已提出教导，双方需要实际靠近、相处并听完；学到的是长辈的判断，也可能听错。";
    }

    private void FinishFamilyLesson(Creature teacher,string outcome)
    {
        var lesson=teacher.Family.Lesson;if(lesson==null)return;
        teacher.Family.Lesson=null;teacher.Family.NextLessonAt=Math.Max(teacher.Family.NextLessonAt,State.WorldSeconds+30);
        if(State.Creatures.Find(c=>c.Id==lesson.LearnerId) is { } learner)
            learner.Family.NextLessonAt=Math.Max(learner.Family.NextLessonAt,State.WorldSeconds+30);
        teacher.Family.LastEvent=outcome;
        RecordInteraction(teacher,"family-lesson",outcome,1);
    }

    private void TickFamilyLearning(double dt)
    {
        foreach(var teacher in State.Creatures.OrderBy(c=>c.Id,StringComparer.Ordinal))
        {
            if(teacher.Family.Lesson is not { } lesson)
            {
                if(State.WorldSeconds<teacher.Family.NextLessonAt||teacher.IsYoung||!ReadyToLearn(teacher)||!MouthFunction.CanSpeakClearly(teacher)||
                    teacher.Family.CompanionTargetId is not { } childId||State.Creatures.Find(c=>c.Id==childId) is not { } child||
                    !child.IsYoung||child.Family.CompanionTargetId!=teacher.Id||!FamilyInContact(teacher,child))continue;
                var unknown=teacher.Family.LifeKnowledge.OrderBy(p=>p.Key,StringComparer.Ordinal)
                    .FirstOrDefault(p=>FamilyRules.KnowledgeName(p.Key).Length>0&&!child.Family.LifeKnowledge.ContainsKey(p.Key));
                if(unknown.Key!=null)
                {
                    string message=State.LastActionMessage;
                    TeachFamilyKnowledge(teacher.Id,child.Id,unknown.Key);State.LastActionMessage=message;
                }
                continue;
            }
            if(!MouthFunction.CanSpeakClearly(teacher))
            {FinishFamilyLesson(teacher,"口部暂时无法清楚讲述，教导中断；可以继续安静陪伴，没听完的内容没有变成新知识。");continue;}
            var learner=State.Creatures.Find(c=>c.Id==lesson.LearnerId);
            if(learner==null||!CanTeachChild(teacher,learner)||teacher.Family.CompanionTargetId!=learner.Id||
                learner.Family.CompanionTargetId!=teacher.Id||State.WorldSeconds-lesson.StartedAt>45)
            {FinishFamilyLesson(teacher,"教导中断，先照顾眼前需要；没听完的内容没有变成新知识。");continue;}
            if(!FamilyInContact(teacher,learner)||teacher.Family.LastCompanionActivityAt!=State.WorldSeconds||
                learner.Family.LastCompanionActivityAt!=State.WorldSeconds)continue;
            lesson.Elapsed=Math.Min(6,lesson.Elapsed+dt);
            teacher.Activity="向幼体讲述食水经验";learner.Activity="听长辈讲述食水经验";
            if(lesson.Elapsed<6)continue;
            double errorChance=learner.Family.BirthMethod==BirthMethod.Live?.05:.15;
            bool misunderstood=Next()<errorChance;
            StoreLifeBelief(learner,lesson.Key,misunderstood?!lesson.Belief:lesson.Belief);
            learner.Family.LearningSources[lesson.Key]=new(){Source=LifeKnowledgeSource.Taught,FromId=teacher.Id,At=State.WorldSeconds,Misunderstood=misunderstood};
            learner.Family.LastEvent=$"听{teacher.Name}讲过{FamilyRules.KnowledgeName(lesson.Key)}，以后仍会按自己的经历判断。";
            RecordInteraction(learner,"learn-"+lesson.Key,learner.Family.LastEvent,2);
            React(learner,Expression.Curious,"慢慢记住啦。",learner.Family.LastEvent,4);
            FinishFamilyLesson(teacher,$"和{learner.Name}讲完一条食水经验；孩子的理解仍需日后观察。");
        }
    }

    private void LearnFoodExperience(Creature c,RoomObjectKind kind,bool safe)
    {
        string key="safe:"+kind;if(FamilyRules.KnowledgeName(key).Length==0)return;
        // An individual inference, never an oracle reading hidden contamination.
        StoreLifeBelief(c,key,safe);
        c.Family.LearningSources[key]=new(){Source=LifeKnowledgeSource.Experience,At=State.WorldSeconds};
    }

    private static void StoreLifeBelief(Creature c,string key,bool belief)
    {
        var f=c.Family;
        if(!f.LifeKnowledge.ContainsKey(key)&&f.LifeKnowledge.Count>=32)
        {
            // Old versions permitted other small belief keys. Keep the finite
            // ledger saveable when learning a supported food category later.
            string oldest=f.LifeKnowledge.Keys.OrderBy(k=>FamilyRules.KnowledgeName(k).Length>0)
                .ThenBy(k=>f.LearningSources.GetValueOrDefault(k)?.At??0).ThenBy(k=>k,StringComparer.Ordinal).First();
            f.LifeKnowledge.Remove(oldest);f.LearningSources.Remove(oldest);
        }
        f.LifeKnowledge[key]=belief;
    }
}
