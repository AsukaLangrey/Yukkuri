using System.Text.Json;
using System.Text.Json.Serialization;

namespace Yukkuri.Simulation;

public sealed record LoadResult(WorldState State, bool RecoveredBackup, string Message);
public sealed class SaveStore
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented=true, Converters={new JsonStringEnumConverter()} };
    private readonly string _directory;
    public SaveStore(string directory) { _directory=Path.GetFullPath(directory); Directory.CreateDirectory(_directory); }
    public string SlotPath(int slot) { if(slot is < 0 or > 3)throw new ArgumentOutOfRangeException(nameof(slot));return Path.Combine(_directory,$"slot-{slot}.json"); }
    public void Save(WorldState state,int slot)
    {
        Validate(state);
        string target=SlotPath(slot), temporary=target+".writing", backup=target+".bak";
        if(File.Exists(target))
        {
            try { Read(target); }
            catch(Exception e) when(e is JsonException or InvalidDataException)
            { throw new InvalidDataException("现有存档损坏；已保留原件。请先从备份恢复或另存槽位。",e); }
        }
        using(var file=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
        { JsonSerializer.Serialize(file,state,JsonOptions);file.Flush(true); }
        Read(temporary);
        if(File.Exists(target)) File.Replace(temporary,target,backup);
        else File.Move(temporary,target);
    }
    public LoadResult Load(int slot)
    {
        string target=SlotPath(slot);
        try { return new(Read(target),false,"已读取存档，不结算离线时间。"); }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            try { return new(Read(target+".bak"),true,"主档不可用，已读取备份。原主档保留；请另存槽位。"); }
            catch(Exception b) when(b is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
            {throw new InvalidDataException("主档和备份均不可用；文件未改动。",new AggregateException(e,b));}
        }
    }
    private static WorldState Read(string path)
    {
        using var stream=File.OpenRead(path);
        var state=JsonSerializer.Deserialize<WorldState>(stream,JsonOptions)??throw new InvalidDataException("存档为空");
        Validate(state);return state;
    }
    public static void Validate(WorldState s)
    {
        if(s.SchemaVersion!=WorldState.CurrentVersion)throw new InvalidDataException("不支持的存档版本");
        if(s.RandomState==0 || !double.IsFinite(s.WorldSeconds) || s.WorldSeconds<0 || !double.IsFinite(s.TickRemainder) || s.TickRemainder<0 || s.TickRemainder>1 || s.Money<0 || s.Food<0 || !Enum.IsDefined(s.Mode))throw new InvalidDataException("世界数据越界");
        if(s.Regions==null || s.Creatures==null || s.Journal==null || s.CompletedCareRecords==null || s.Regions.Count<1 || s.Creatures.Count>10000)throw new InvalidDataException("数据列表无效");
        if(s.Regions.Any(r=>r==null||string.IsNullOrWhiteSpace(r.Id)||string.IsNullOrWhiteSpace(r.Name)||!Enum.IsDefined(r.Kind)))throw new InvalidDataException("地区数据无效");
        foreach(var v in new[]{s.PlayerX,s.PlayerZ,s.ObservationSkill,s.WageTimer})if(!double.IsFinite(v))throw new InvalidDataException("世界数值无效");
        if(Math.Abs(s.PlayerX)>9||Math.Abs(s.PlayerZ)>9||s.ObservationSkill is < 0 or > 10||s.WageTimer<0)throw new InvalidDataException("世界数值越界");
        var regions=s.Regions.Select(r=>r.Id).ToHashSet();
        if(regions.Count!=s.Regions.Count || !regions.Contains(s.ActiveRegionId))throw new InvalidDataException("地区引用无效");
        var ids=new HashSet<string>();var bodies=new HashSet<string>();var identities=new HashSet<string>();
        foreach(var c in s.Creatures)
        {
            if(c==null || string.IsNullOrWhiteSpace(c.Id) || !ids.Add(c.Id) || c.Body==null || c.Registration==null || !bodies.Add(c.Body.Id) || !identities.Add(c.Registration.IdentityId) || !regions.Contains(c.RegionId) || c.Observation==null || c.Memories==null || c.Relationships==null)throw new InvalidDataException("个体身份或地区无效");
            if(string.IsNullOrWhiteSpace(c.Body.Id)||string.IsNullOrWhiteSpace(c.Registration.IdentityId)||string.IsNullOrWhiteSpace(c.Name)||c.Observation.Notes==null||c.Observation.Samples<0||c.Observation.FeedingEvidence<0||!double.IsFinite(c.Observation.Confidence)||c.Observation.Confidence is < 0 or > 1||!Enum.IsDefined(c.Body.ReproductionRole))throw new InvalidDataException("档案数据无效");
            // Missing legacy Speech uses its initializer; explicit null must not reach UI Length reads.
            if(c.Speech==null)throw new InvalidDataException("个体台词数据无效");
            foreach(var value in new[]{c.X,c.Z,c.TargetX,c.TargetZ,c.Fullness,c.Energy,c.Health,c.Trust,c.InnerAgeSeconds,c.Body.AgeSeconds,c.DecisionIn}) if(!double.IsFinite(value))throw new InvalidDataException("个体数值无效");
            if(Math.Abs(c.X)>9 || Math.Abs(c.Z)>9 || c.Fullness is < 0 or > 100 || c.Energy is < 0 or > 100 || c.Health is < 0 or > 100 || c.InnerAgeSeconds<0 || c.Body.AgeSeconds<0)throw new InvalidDataException("个体数值越界");
        }
        if(s.SelectedId!=null && !s.Creatures.Any(c=>c.Id==s.SelectedId && c.RegionId==s.ActiveRegionId))throw new InvalidDataException("选中个体无效");
        if(s.Mode==PlayMode.CloseUp && s.SelectedId==null)throw new InvalidDataException("特写缺少对象");
        var facilityIds=new HashSet<string>();
        foreach(var r in s.Regions) if(!double.IsFinite(r.StaffCooldown)||r.Facilities==null || r.Facilities.Any(f=>f==null||string.IsNullOrWhiteSpace(f.Id)||!facilityIds.Add(f.Id)||!Enum.IsDefined(f.Kind)||f.Stock<0||!double.IsFinite(f.X)||!double.IsFinite(f.Z)||Math.Abs(f.X)>9||Math.Abs(f.Z)>9))throw new InvalidDataException("设施数据无效");
        MilestoneValidation.Validate(s);
        InteractionValidation.Validate(s);
    }
}
