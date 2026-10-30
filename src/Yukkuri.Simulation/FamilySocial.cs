namespace Yukkuri.Simulation;

public sealed partial class PeerBond
{
    // This is a person's permission to their existing partner, never the player's toggle.
    public bool ConsentsToOtherPartners { get; set; }
    public double LastHarmAt { get; set; } = -100;
    public double NextInvitationAt { get; set; }
}

public sealed class FamilyConflict
{
    public string? TargetId { get; set; }
    public string? CauseEventId { get; set; }
    public double WarningRemaining { get; set; }
    public double Remaining { get; set; }
    public double StrikeRemaining { get; set; }
    public int Strikes { get; set; }
}

public sealed class FamilyRelationshipEvent
{
    public string Id { get; set; } = "";
    public string FirstId { get; set; } = "";
    public string SecondId { get; set; } = "";
    public double At { get; set; }
    public double? EndedAt { get; set; }
    // Permission and existing partnerships are captured at the event, not rewritten later.
    public Dictionary<string, string> BetrayedPartners { get; set; } = new();
    public Dictionary<string, KnowledgeSource> KnownBy { get; set; } = new();
    public HashSet<string> ReactedBy { get; set; } = new();
}

public sealed partial class FamilyLife
{
    public FamilyConflict Conflict { get; set; } = new();
    public double ConflictCooldownUntil { get; set; }
    public double CompanionUntil { get; set; }
    public double NextCompanionReactionAt { get; set; }
    public double LastCompanionActivityAt { get; set; } = -100;
    public double FlightRemaining { get; set; }
    public double FlightX { get; set; }
    public double FlightZ { get; set; }
}

public sealed partial class WorldState
{
    public List<FamilyRelationshipEvent> FamilyRelationshipEvents { get; set; } = new();
}

public static partial class FamilyRules
{
    public static bool IsCloseFamily(Creature a, Creature b) =>
        a.Family.MotherId == b.Id || a.Family.FatherId == b.Id ||
        b.Family.MotherId == a.Id || b.Family.FatherId == a.Id ||
        a.ParentId == b.Id || b.ParentId == a.Id ||
        a.Family.MotherId != null && a.Family.MotherId == b.Family.MotherId ||
        a.Family.FatherId != null && a.Family.FatherId == b.Family.FatherId;

    public static bool AcceptsOpenDiscussion(Creature c) =>
        c.Patience >= .8 && c.Aggression <= .25 && c.SharingSkill >= 50;

    public static void ValidateSocial(WorldState state)
    {
        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidDataException(message); }
        static bool Number(double v, double max = double.MaxValue) => double.IsFinite(v) && v >= 0 && v <= max;
        var ids = state.Creatures.Select(c => c.Id).ToHashSet();
        Require(state.FamilyRelationshipEvents != null && state.FamilyRelationshipEvents.Count <= 256, "家庭事件记录无效");
        var eventIds = new HashSet<string>();
        foreach (var e in state.FamilyRelationshipEvents!)
        {
            Require(e != null && !string.IsNullOrWhiteSpace(e.Id) && eventIds.Add(e.Id) &&
                ids.Contains(e.FirstId) && ids.Contains(e.SecondId) && e.FirstId != e.SecondId &&
                Number(e.At, state.WorldSeconds + .001) &&
                (e.EndedAt == null || Number(e.EndedAt.Value, state.WorldSeconds + .001) && e.EndedAt.Value >= e.At), "家庭事件身份或时间无效");
            Require(e!.BetrayedPartners != null && e.KnownBy != null && e.ReactedBy != null &&
                e.BetrayedPartners.All(p => ids.Contains(p.Key) && p.Key != e.FirstId && p.Key != e.SecondId &&
                    (p.Value == e.FirstId || p.Value == e.SecondId)) &&
                e.KnownBy.All(p => ids.Contains(p.Key) && Enum.IsDefined(p.Value)) &&
                e.ReactedBy.All(id => e.KnownBy.TryGetValue(id, out var source) && source == KnowledgeSource.Witness &&
                    e.BetrayedPartners.ContainsKey(id)), "家庭事件知情或当时承诺无效");
        }
        foreach (var c in state.Creatures)
        {
            var f = c.Family;
            Require(f != null && f.Peers != null && f.PartnerIds != null && f.Conflict != null, "同伴关系数据缺失");
            Require(Number(f!.ConflictCooldownUntil) && Number(f.CompanionUntil) && Number(f.NextCompanionReactionAt) &&
                double.IsFinite(f.LastCompanionActivityAt) && f.LastCompanionActivityAt >= -100 &&
                f.LastCompanionActivityAt <= state.WorldSeconds + .001 && Number(f.FlightRemaining, 5) &&
                double.IsFinite(f.FlightX) && double.IsFinite(f.FlightZ) && Math.Abs(f.FlightX) <= 8 && Math.Abs(f.FlightZ) <= 8, "同伴计时无效");
            Require(f.CompanionTargetId == null || f.CompanionTargetId != c.Id && ids.Contains(f.CompanionTargetId), "相处对象无效");
            foreach (var p in f.Peers!)
                Require(ids.Contains(p.Key) && p.Key != c.Id && p.Value != null &&
                    double.IsFinite(p.Value.LastHarmAt) && p.Value.LastHarmAt >= -100 && p.Value.LastHarmAt <= state.WorldSeconds + .001 &&
                    Number(p.Value.NextInvitationAt), "同伴记忆或邀请计时无效");
            foreach (var partner in f.PartnerIds!)
                Require(partner != c.Id && ids.Contains(partner) &&
                    state.Creatures.Single(x => x.Id == partner).Family?.PartnerIds?.Contains(c.Id) == true, "伴侣关系不对称");
            var conflict = f.Conflict!;
            Require(Number(conflict.WarningRemaining, 8) && Number(conflict.Remaining, 60) &&
                Number(conflict.StrikeRemaining, 3) && conflict.Strikes is >= 0 and <= 16, "家庭冲突计时无效");
            Require(conflict.TargetId == null
                ? conflict.CauseEventId == null && conflict.Remaining == 0 && conflict.WarningRemaining == 0 && conflict.Strikes == 0 && conflict.StrikeRemaining == 0
                : conflict.TargetId != c.Id && ids.Contains(conflict.TargetId) && conflict.CauseEventId != null &&
                    eventIds.Contains(conflict.CauseEventId) && conflict.Remaining > 0 &&
                    state.FamilyRelationshipEvents.Any(e => e.Id == conflict.CauseEventId && e.ReactedBy.Contains(c.Id)),
                "家庭冲突缺少实际知情依据或对象");
        }
    }
}

public sealed partial class WorldSimulation
{
    private PeerBond FamilyBond(Creature from, Creature to)
    {
        if (!from.Family.Peers.TryGetValue(to.Id, out var bond))
        {
            // Old relationship values are familiarity evidence only, not reciprocal love.
            bond = new() { Familiarity = Math.Clamp(from.Relationships.GetValueOrDefault(to.Id), 0, 20) };
            from.Family.Peers.Add(to.Id, bond);
        }
        return bond;
    }

    private bool FamilyActionReady(Creature c) => FamilyRules.WillSocialize(c) && !c.Quarantine &&
        !c.IsIntruder && c.RetreatRemaining <= 0 && !c.Interaction.Resting && c.Interaction.RestRemaining <= 0 &&
        c.Interaction.Height <= .25 && c.Interaction.WaitRemaining <= 0 && !c.Interaction.FollowPlayer &&
        InteractionGestureTarget != c.Id && c.Family.Conflict.TargetId == null &&
        c.Family.CareTargetId == null && c.Family.CareParcelId == null && c.Family.FlightRemaining <= 0;

    private bool FamilyVisible(Creature observer, Creature target, double range = 4.5)
    {
        if (observer.RegionId != target.RegionId || observer.Condition != PhysicalCondition.Active ||
            observer.Interaction.Resting || observer.Family.ColdStorage.Stored || observer.Quarantine != target.Quarantine ||
            observer.Interaction.Restraint == RestraintKind.Box || target.Interaction.Restraint == RestraintKind.Box ||
            target.Family.ColdStorage.Stored || target.Family.Memorialized || Distance(observer.X, observer.Z, target.X, target.Z) > range) return false;
        if (!HasGroundSightLine(observer, target.X, target.Z)) return false;
        if (!WoodlandLayout.HasRiver(State, observer.RegionId)) return true;
        double dx = target.X - observer.X, dz = target.Z - observer.Z, square = dx * dx + dz * dz;
        foreach (var tree in WoodlandLayout.Trees)
        {
            double t = square < .00001 ? 0 : Math.Clamp(((tree.X - observer.X) * dx + (tree.Z - observer.Z) * dz) / square, 0, 1);
            if (t > .02 && t < .98 && Distance(observer.X + dx * t, observer.Z + dz * t, tree.X, tree.Z) < tree.TrunkRadius + .08) return false;
        }
        return true;
    }

    private static double FamilyContactDistance(Creature a, Creature b) => InteractionFootprint(a) + InteractionFootprint(b) + .28;
    private bool FamilyInContact(Creature a, Creature b) => Math.Abs(a.Interaction.Height-b.Interaction.Height)<=.45 &&
        FamilyVisible(a, b, FamilyContactDistance(a, b));

    public string InviteCompanion(string aId, string bId)
    {
        if (State.Settings.Pause) return "当前已暂停，恢复普通时间后再安排相处。";
        var a = State.Creatures.Find(c => c.Id == aId); var b = State.Creatures.Find(c => c.Id == bId);
        if (a == null || b == null || a == b || a.RegionId != State.ActiveRegionId || !FamilyVisible(a, b, 6))
            return "先让两只油在同一区域能看见彼此的位置。";
        if (!FamilyActionReady(a) || !FamilyActionReady(b)) return "有一方正在忙、害怕或身体不适，先照顾眼前的需要。";
        var ab = FamilyBond(a, b); var ba = FamilyBond(b, a);
        if (a.Family.CompanionTargetId == b.Id && b.Family.CompanionTargetId == a.Id) return "已经在尝试相处，给它们一点时间。";
        if (State.WorldSeconds < ab.NextInvitationAt || State.WorldSeconds < ba.NextInvitationAt) return "刚刚提出过邀请，先尊重对方的距离。";
        ab.NextInvitationAt = ba.NextInvitationAt = State.WorldSeconds + 12;
        bool refusing = ab.Fear >= 40 || ba.Fear >= 40 || ab.Resentment >= 35 || ba.Resentment >= 35 ||
            a.Family.CompanionTargetId != null || b.Family.CompanionTargetId != null;
        if (refusing)
        {
            b.Family.LastEvent = $"暂时不愿与{a.Name}靠近";
            React(b, Expression.Worried, "先别过来……", "留出了距离，没有接受邀请。", 4);
            return "对方没有接受，继续点击不会增加感情。";
        }
        BeginCompanionship(a, b);
        return "它们愿意试着靠近；感情来自之后实际相处。";
    }

    private void BeginCompanionship(Creature a, Creature b)
    {
        a.Family.CompanionTargetId = b.Id; b.Family.CompanionTargetId = a.Id;
        a.Family.CompanionUntil = b.Family.CompanionUntil = State.WorldSeconds + 24;
        a.Family.NextCompanionReactionAt = b.Family.NextCompanionReactionAt = State.WorldSeconds + 2;
        a.Family.LastEvent = $"愿意与{b.Name}相处"; b.Family.LastEvent = $"接受了{a.Name}的陪伴邀请";
    }

    private void ClearCompanionship(Creature c)
    {
        var other = State.Creatures.Find(x => x.Id == c.Family.CompanionTargetId);
        if(c.Family.Lesson!=null)FinishFamilyLesson(c,"相处已中断，没听完的教导没有形成新知识。");
        if(other?.Family.Lesson?.LearnerId==c.Id)FinishFamilyLesson(other,"相处已中断，没听完的教导没有形成新知识。");
        c.Family.CompanionTargetId = null; c.Family.CompanionUntil = 0;
        c.Family.NextSocialAt = Math.Max(c.Family.NextSocialAt, State.WorldSeconds + 6);
        if (other?.Family.CompanionTargetId == c.Id)
        {
            other.Family.CompanionTargetId = null; other.Family.CompanionUntil = 0;
            other.Family.NextSocialAt = Math.Max(other.Family.NextSocialAt, State.WorldSeconds + 6);
        }
    }

    private bool WillKeepOtherPromises(Creature c, Creature proposed)
    {
        bool allAgreed = c.Family.PartnerIds.Where(id => id != proposed.Id).All(id =>
            State.Creatures.Find(x => x.Id == id) is { } existing &&
            (existing.Condition == PhysicalCondition.Dead || FamilyBond(existing, c).ConsentsToOtherPartners));
        return allAgreed || c.Aggression >= .65 || c.Patience < .25 || c.TrueDisposition == "greedy";
    }

    public bool FormPartnership(Creature a, Creature b)
    {
        if (State.Settings.Pause) return false;
        if (a == b || !State.Creatures.Contains(a) || !State.Creatures.Contains(b)) return false;
        if (!FamilyActionReady(a) || !FamilyActionReady(b) || !FamilyInContact(a, b)) return false;
        if (a.Family.CompanionTargetId != null && a.Family.CompanionTargetId != b.Id ||
            b.Family.CompanionTargetId != null && b.Family.CompanionTargetId != a.Id) return false;
        if (a.Family.PartnerIds.Contains(b.Id) && b.Family.PartnerIds.Contains(a.Id)) return true;
        if (!FamilyRules.WillPair(a, b) || !FamilyRules.WillPair(b, a) ||
            State.WorldSeconds < FamilyBond(a, b).NextInvitationAt || State.WorldSeconds < FamilyBond(b, a).NextInvitationAt ||
            !FamilyInContact(a, b) || !WillKeepOtherPromises(a, b) || !WillKeepOtherPromises(b, a)) return false;
        if (State.FamilyRelationshipEvents.Count >= 256 && State.FamilyRelationshipEvents.All(existingEvent =>
            State.Creatures.Any(c => c.Family.Conflict.CauseEventId == existingEvent.Id))) return false;
        string eventId;
        do
        {
            if (State.FamilySequence == long.MaxValue) throw new InvalidOperationException("家庭事件编号已耗尽。");
            eventId = $"family-relationship-{++State.FamilySequence:000000}";
        } while (State.FamilyRelationshipEvents.Any(existingEvent => existingEvent.Id == eventId));
        var e = new FamilyRelationshipEvent { Id = eventId, FirstId = a.Id, SecondId = b.Id, At = State.WorldSeconds };
        foreach (var pairing in new[] { (Partner: a, Other: b), (Partner: b, Other: a) })
            foreach (string previousId in pairing.Partner.Family.PartnerIds.OrderBy(id => id, StringComparer.Ordinal))
                if (previousId != pairing.Other.Id && State.Creatures.Find(x => x.Id == previousId) is { } previous &&
                    previous.Condition != PhysicalCondition.Dead && !FamilyBond(previous, pairing.Partner).ConsentsToOtherPartners)
                    e.BetrayedPartners[previous.Id] = pairing.Partner.Id;
        a.Family.PartnerIds.Add(b.Id); b.Family.PartnerIds.Add(a.Id);
        a.Family.LastEvent = $"与{b.Name}互相接受为伴侣"; b.Family.LastEvent = $"与{a.Name}互相接受为伴侣";
        State.FamilyRelationshipEvents.Add(e);
        while (State.FamilyRelationshipEvents.Count > 256)
        {
            var oldest = State.FamilyRelationshipEvents.First(x => !State.Creatures.Any(c => c.Family.Conflict.CauseEventId == x.Id));
            State.FamilyRelationshipEvents.Remove(oldest);
        }
        ObservePartnership(e, a, b);
        React(a, Expression.Shy, "以后也一起慢慢来。", $"贴近{b.Name}，接受了彼此的陪伴。", 5);
        React(b, Expression.Content, "一起慢慢来！", $"回应了{a.Name}的亲近。", 5);
        Log($"{a.Name}与{b.Name}建立了伴侣关系。");
        return true;
    }

    public string EndPartnership(string aId, string bId)
    {
        if (State.Settings.Pause) return "当前已暂停，恢复普通时间后再交流。";
        var a = State.Creatures.Find(c => c.Id == aId); var b = State.Creatures.Find(c => c.Id == bId);
        if (a == null || b == null || a == b || a.RegionId != State.ActiveRegionId || !FamilyInContact(a, b) ||
            !FamilyActionReady(a) || !FamilyActionReady(b)) return "先在双方能交流、身体安稳时说清楚。";
        if (!a.Family.PartnerIds.Contains(b.Id)) return "它们目前不是伴侣。";
        a.Family.PartnerIds.Remove(b.Id); b.Family.PartnerIds.Remove(a.Id);
        foreach (var e in State.FamilyRelationshipEvents.Where(e => e.EndedAt == null &&
            (e.FirstId == a.Id && e.SecondId == b.Id || e.FirstId == b.Id && e.SecondId == a.Id))) e.EndedAt = State.WorldSeconds;
        FamilyBond(a, b).ConsentsToOtherPartners = false; FamilyBond(b, a).ConsentsToOtherPartners = false;
        FamilyBond(a, b).NextInvitationAt = FamilyBond(b, a).NextInvitationAt = State.WorldSeconds + 60;
        FamilyBond(a, b).Affection = Math.Max(0, FamilyBond(a, b).Affection - 8);
        FamilyBond(b, a).Affection = Math.Max(0, FamilyBond(b, a).Affection - 12);
        ClearCompanionship(a);
        a.Family.LastEvent = $"与{b.Name}结束了伴侣关系"; b.Family.LastEvent = $"与{a.Name}分开，亲缘与照护责任仍保留";
        Log($"{a.Name}与{b.Name}结束了伴侣关系，过去经历和子油身份仍在。");
        return "伴侣关系已结束，过去感情不会立刻归零；重新配对需要再次相处。";
    }

    public string DiscussOpenPartnership(string aId, string bId)
    {
        if (State.Settings.Pause) return "当前已暂停，恢复普通时间后再交流。";
        var a = State.Creatures.Find(c => c.Id == aId); var b = State.Creatures.Find(c => c.Id == bId);
        if (a == null || b == null || !a.Family.PartnerIds.Contains(b.Id) || a.RegionId != State.ActiveRegionId ||
            !FamilyInContact(a, b) || !FamilyActionReady(a) || !FamilyActionReady(b)) return "需要已经结伴的双方在近处、愿意交流时讨论。";
        var ab = FamilyBond(a, b); var ba = FamilyBond(b, a);
        if (!FamilyRules.AcceptsOpenDiscussion(a) || !FamilyRules.AcceptsOpenDiscussion(b) ||
            ab.Affection < 35 || ba.Affection < 35 || ab.Fear >= 25 || ba.Fear >= 25 || ab.Resentment >= 25 || ba.Resentment >= 25)
        {
            a.Family.LastEvent = b.Family.LastEvent = "没有就接纳其他伴侣达成一致";
            return "至少一方不愿接受多伴侣。没有同意，不会替它决定。";
        }
        FamilyBond(a, b).ConsentsToOtherPartners = true; FamilyBond(b, a).ConsentsToOtherPartners = true;
        a.Family.LastEvent = b.Family.LastEvent = "双方同意以后可另外建立伴侣关系";
        return "双方同意了；这不代表其他现有伴侣也同意，更不会抹去此前的背叛。";
    }

    public string RevokeOpenPartnership(string aId, string bId)
    {
        if (State.Settings.Pause) return "当前已暂停，恢复普通时间后再交流。";
        var a = State.Creatures.Find(c => c.Id == aId); var b = State.Creatures.Find(c => c.Id == bId);
        if (a == null || b == null || !a.Family.PartnerIds.Contains(b.Id) || a.RegionId != State.ActiveRegionId ||
            !FamilyInContact(a, b) || !FamilyActionReady(a) || !FamilyActionReady(b)) return "先让双方在近处把意愿说清楚。";
        FamilyBond(a, b).ConsentsToOtherPartners = false;
        a.Family.LastEvent = $"告诉{b.Name}：以后不再接受另外建立伴侣关系";
        return "已撤回今后的许可；此前明确接受的关系不会倒算为出轨。";
    }

    private void ObservePartnership(FamilyRelationshipEvent e, Creature a, Creature b)
    {
        foreach (var observer in State.Creatures.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            if (observer.Id != a.Id && observer.Id != b.Id && (!FamilyVisible(observer, a) || !FamilyVisible(observer, b))) continue;
            if (observer.Condition != PhysicalCondition.Active || observer.Family.ColdStorage.Stored) continue;
            bool already = e.KnownBy.TryGetValue(observer.Id, out var source) && source == KnowledgeSource.Witness;
            e.KnownBy[observer.Id] = KnowledgeSource.Witness;
            // Seeing today's closeness does not prove an old, now-ended relationship.
            // Nor does later permission erase a betrayal the observer already witnessed.
            if (!already && e.EndedAt == null && e.BetrayedPartners.TryGetValue(observer.Id, out string? partnerId) &&
                observer.Family.PartnerIds.Contains(partnerId) &&
                !FamilyBond(observer, State.Creatures.Find(c => c.Id == partnerId)!).ConsentsToOtherPartners)
                LearnFamilyBetrayal(observer, e, partnerId);
        }
    }

    public string TellFamilyRelationship(string speakerId, string listenerId, string eventId)
    {
        if (State.Settings.Pause) return "当前已暂停，恢复普通时间后再交流。";
        var speaker = State.Creatures.Find(c => c.Id == speakerId); var listener = State.Creatures.Find(c => c.Id == listenerId);
        var e = State.FamilyRelationshipEvents.Find(x => x.Id == eventId);
        if (speaker == null || listener == null || e == null || speaker == listener || !FamilyInContact(speaker, listener) ||
            !FamilyActionReady(speaker) || !FamilyActionReady(listener) || !MouthFunction.CanSpeakClearly(speaker) ||
            !e.KnownBy.TryGetValue(speakerId, out var source) || source != KnowledgeSource.Witness) return "需要亲眼知情者在近处，能够清楚讲述时实际转述。";
        if (e.KnownBy.ContainsKey(listenerId)) return "它已经听说或见过这件事。";
        e.KnownBy[listenerId] = KnowledgeSource.Told;
        listener.Family.LastEvent = $"听{speaker.Name}说起一段伴侣关系，尚未亲自确认";
        // Hearsay may prompt concern, but cannot authorize attacks or omniscient guilt.
        if (e.BetrayedPartners.ContainsKey(listenerId)) React(listener, Expression.Worried, "真的吗……", "听到转述后有些不安，还需要自己确认。", 5);
        return "已经转述，记录为听说，不冒充亲眼确认。";
    }

    private void LearnFamilyBetrayal(Creature observer, FamilyRelationshipEvent e, string partnerId)
    {
        if (!e.ReactedBy.Add(observer.Id)) return;
        var partner = State.Creatures.Find(c => c.Id == partnerId)!;
        string rivalId = e.FirstId == partnerId ? e.SecondId : e.FirstId;
        var bond = FamilyBond(observer, partner);
        bond.Affection = Math.Max(0, bond.Affection - 30); bond.Resentment = Clamp100(bond.Resentment + 38);
        bond.LastHarmAt = State.WorldSeconds;
        bond.LastEvent = "亲眼确认未经同意的新伴侣关系";
        observer.Family.LastEvent = $"亲眼确认{partner.Name}违背了伴侣承诺";
        observer.Memories.Add(observer.Family.LastEvent); if (observer.Memories.Count > 30) observer.Memories.RemoveAt(0);
        React(observer, Expression.Worried, "说好一起的……", "受到背叛后退开，关系需要重新面对。", 6);
        if (State.WorldSeconds < observer.Family.ConflictCooldownUntil || observer.IsYoung || !CanSelfMove(observer) ||
            !FamilyActionReady(observer) || observer.Aggression < .5) return;
        double probability = Math.Clamp((observer.Aggression - .4) * (1.1 - observer.Patience), 0, .8);
        if (Next() >= probability) return;
        var children = State.Creatures.Where(c => c.IsYoung && c.Condition != PhysicalCondition.Dead &&
            (c.Family.MotherId == partnerId || c.Family.FatherId == partnerId || c.ParentId == partnerId) &&
            FamilyVisible(observer, c, 6)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        double branch = Next();
        string targetId = branch < .2 && children.Length > 0 ? children[0].Id : branch < .65 ? partnerId : rivalId;
        var target = State.Creatures.Find(c => c.Id == targetId)!;
        if (!FamilyVisible(observer, target, 6)) return;
        ClearCompanionship(observer);
        observer.Family.Conflict = new() { TargetId = targetId, CauseEventId = e.Id, WarningRemaining = 8, Remaining = 60 };
        observer.Family.ConflictCooldownUntil = State.WorldSeconds + 180;
        observer.Family.LastEvent = $"因背叛愤怒，正在威胁{target.Name}；可以及时隔开或安抚";
        React(observer, Expression.Angry, "不能原谅！", $"朝{target.Name}发出威胁，尚未动手。", 8);
        Log($"{observer.Name}正在威胁{target.Name}。及时制止或将双方分开可以阻止伤害。");
    }

    public string InterruptFamilyConflict(string id)
    {
        if (State.Settings.Pause) return "当前已暂停，恢复普通时间后再出面制止。";
        var c = State.Creatures.Find(x => x.Id == id);
        // The interaction courtyard has a hand/camera, not a walkable player avatar.
        // Its explicit selected-target command is a spoken interruption across the small yard.
        bool canAddress = c != null && c.RegionId == State.ActiveRegionId &&
            (State.InteractionPlaytest ? c.Interaction.Restraint != RestraintKind.Box && !c.Family.ColdStorage.Stored
                : Distance(c.X, c.Z, State.PlayerX, State.PlayerZ) <= 3);
        if (!canAddress)
            return "先靠近正在冲突的油，再出面制止。";
        if(c!.Personality.HungerTargetId!=null)
        {
            StopHungerAggression(c,"被人类及时制止饥饿袭击；仍需补充食物或隔开照看");
            React(c,Expression.Worried,"……好饿。","停止逼近同伴，仍需解决实际饥饿。",5);
            return "已制止这次饥饿袭击；请喂食或隔开，已有伤势与关系保留。";
        }
        if (c.Family.Conflict.TargetId == null) return "它现在没有准备动手的家庭冲突。";
        StopFamilyConflict(c, "被及时制止，暂时退开；受伤的关系仍需处理");
        React(c, Expression.Worried, "……先走开。", "停止靠近，留出了安全距离。", 5);
        return "已阻止这次冲突，伤势、记忆和关系不会被清空。";
    }

    private void StopFamilyConflict(Creature c, string reason)
    {
        c.Family.Conflict = new(); c.Family.ConflictCooldownUntil = Math.Max(c.Family.ConflictCooldownUntil, State.WorldSeconds + 90);
        c.Family.LastEvent = reason; c.TargetX = c.X; c.TargetZ = c.Z;
        c.Family.NextSocialAt = Math.Max(c.Family.NextSocialAt, State.WorldSeconds + 20);
    }

    // Resolve references immediately, including deaths during close-up where the
    // ordinary world clock is frozen. This is cleanup, not new witnessed knowledge.
    private void ClearFamilyTargetsTo(Creature unavailable)
    {
        foreach(var other in State.Creatures.Where(c=>c!=unavailable))
        {
            if(other.Family.CareTargetId==unavailable.Id)
                CancelFamilyCare(other,reason:"照护对象的状态改变，递送已中断；如有已取份额，已经留在身旁。");
            if(other.Family.Conflict.TargetId==unavailable.Id)
                StopFamilyConflict(other,"当前对象已经无法继续接触，停止冲突；既有后果保留");
            if(other.Family.ChildThreat.TargetId==unavailable.Id)
                StopChildThreat(other,"当前对象已经无法继续接触，停止危险行动；既有后果保留",120);
            if(other.Family.CompanionTargetId==unavailable.Id)ClearCompanionship(other);
        }
    }

    private bool TryFamilyActivity(Creature c, double dt)
    {
        if (c.Family.FlightRemaining > 0 && CanSelfMove(c) && !c.Family.ColdStorage.Stored &&
            c.Interaction.Height <= .25 && c.Interaction.EatingRemaining <= 0 && InteractionGestureTarget != c.Id)
        {
            if (c.Family.Conflict.TargetId != null) StopFamilyConflict(c, "受到伤害后先退开，停止继续冲突");
            RoomMove(c, c.Family.FlightX, c.Family.FlightZ, dt); c.Activity = "退开寻求保护"; return true;
        }
        if (c.Family.Conflict.TargetId != null) return TryFamilyConflict(c, dt);
        if (!FamilyActionReady(c)) { if (c.Family.CompanionTargetId != null) ClearCompanionship(c); return false; }
        if (c.Family.CompanionTargetId == null)
        {
            if (State.WorldSeconds < c.Family.NextSocialAt || c.IsYoung || AutomaticCompanionShouldWait(c)) return false;
            c.Family.NextSocialAt = State.WorldSeconds + 12;
            var candidate = State.Creatures.Where(x => x != c && FamilyActionReady(x) && x.Family.CompanionTargetId == null &&
                !AutomaticCompanionShouldWait(x) &&
                FamilyVisible(c, x, 4) && CanStartCompanionshipWithChild(c, x) &&
                (!c.Family.Peers.TryGetValue(x.Id, out var own) || own.Fear < 40 && own.Resentment < 35 && State.WorldSeconds >= own.NextInvitationAt) &&
                (!x.Family.Peers.TryGetValue(c.Id, out var theirs) || theirs.Fear < 40 && theirs.Resentment < 35 && State.WorldSeconds >= theirs.NextInvitationAt))
                .OrderByDescending(x => c.Family.PartnerIds.Contains(x.Id)).ThenBy(x => Distance(c.X, c.Z, x.X, x.Z))
                .ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
            if (candidate == null) return false;
            FamilyBond(c, candidate); FamilyBond(candidate, c); BeginCompanionship(c, candidate);
        }
        var companion = State.Creatures.Find(x => x.Id == c.Family.CompanionTargetId);
        if (companion == null || !FamilyActionReady(companion) || !FamilyVisible(c, companion, 6) || State.WorldSeconds >= c.Family.CompanionUntil)
        { ClearCompanionship(c); return false; }
        if (!FamilyInContact(c, companion))
        {
            if (!CanSelfMove(c)) { ClearCompanionship(c); return false; }
            RoomMove(c, companion.X, companion.Z, dt); c.Activity = $"慢慢靠近{companion.Name}"; return true;
        }
        c.Activity = c.Family.PartnerIds.Contains(companion.Id) ? $"陪在{companion.Name}身边" : $"和{companion.Name}相处";
        c.Family.LastCompanionActivityAt = State.WorldSeconds;
        if (State.WorldSeconds >= c.Family.NextCompanionReactionAt)
        {
            c.Family.NextCompanionReactionAt = State.WorldSeconds + 10;
            React(c, FamilyBond(c, companion).Affection >= 35 ? Expression.Content : Expression.Curious,
                "一起慢慢来。", $"留在{companion.Name}身旁，轻轻回应。", 3);
        }
        return true;
    }

    private bool CanStartCompanionshipWithChild(Creature adult, Creature candidate)
    {
        if(!candidate.IsYoung)return true;
        // Blood relationship and established upbringing remain separate facts.
        // This gate permits a first conversation, never an instant adoption.
        if(FamilyRules.IsCloseFamily(adult,candidate)||candidate.Family.CaregiverIds.Contains(adult.Id))return true;
        if(adult.IsYoung||adult.Patience<.65||adult.Aggression>.25||
            NaturalResources.WantsFood(adult)||NaturalResources.WantsWater(adult)||adult.Energy<40||
            NaturalResources.WantsFood(candidate)||NaturalResources.WantsWater(candidate)||candidate.Energy<40)return false;
        return (!adult.Family.Peers.TryGetValue(candidate.Id,out var own)||own.Fear<25&&own.Resentment<25)&&
            (!candidate.Family.Peers.TryGetValue(adult.Id,out var child)||child.Fear<25&&child.Resentment<25);
    }

    private void TickFamilySocial(double dt)
    {
        foreach (var c in State.Creatures.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            c.Family.FlightRemaining = Math.Max(0, c.Family.FlightRemaining - dt);
            // Recovery comes from seeing safe nearby behavior, not loading or waiting elsewhere.
            // It only lowers alarm slowly; affection still needs actual mutual companionship.
            if (FamilyActionReady(c))
                foreach (var pair in c.Family.Peers.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    var remembered = pair.Value;
                    if (remembered.Fear <= 0 && remembered.Resentment <= 0 || State.WorldSeconds - remembered.LastHarmAt < 120) continue;
                    var familiar = State.Creatures.Find(x => x.Id == pair.Key);
                    if (familiar == null || !FamilyActionReady(familiar) || !FamilyVisible(c, familiar, 2.5)) continue;
                    remembered.Fear = Math.Max(0, remembered.Fear - dt * .004);
                    remembered.Resentment = Math.Max(0, remembered.Resentment - dt * .002);
                }
            var conflict = c.Family.Conflict;
            if (conflict.TargetId != null)
            {
                conflict.Remaining = Math.Max(0, conflict.Remaining - dt);
                conflict.WarningRemaining = Math.Max(0, conflict.WarningRemaining - dt);
                conflict.StrikeRemaining = Math.Max(0, conflict.StrikeRemaining - dt);
                var target = State.Creatures.Find(x => x.Id == conflict.TargetId);
                if (conflict.Remaining <= 0 || !CanFamilyFight(c) || target == null || target.Condition == PhysicalCondition.Dead ||
                    target.Family.ColdStorage.Stored || target.Interaction.Carried || target.RegionId != c.RegionId ||
                    target.Quarantine != c.Quarantine || Distance(c.X, c.Z, target.X, target.Z) > 7)
                    StopFamilyConflict(c, "冲突已中断，留下距离；关系记忆仍在");
            }
            var other = State.Creatures.Find(x => x.Id == c.Family.CompanionTargetId);
            if (other == null) continue;
            if (!FamilyActionReady(c) || !FamilyActionReady(other) || other.Family.CompanionTargetId != c.Id ||
                State.WorldSeconds >= c.Family.CompanionUntil || !FamilyVisible(c, other, 6))
            { ClearCompanionship(c); continue; }
            if (!FamilyInContact(c, other) || c.Family.LastCompanionActivityAt != State.WorldSeconds ||
                other.Family.LastCompanionActivityAt != State.WorldSeconds) continue;
            var bond = FamilyBond(c, other);
            bool mixed = FamilyRules.CommonSpecies(c.Body.SpeciesId) != FamilyRules.CommonSpecies(other.Body.SpeciesId);
            double comfort = (c.TrueDisposition == "shy" ? .7 : 1) * (mixed ? .25 : 1);
            bond.Familiarity = Clamp100(bond.Familiarity + dt * .18 * comfort);
            if (bond.Fear < 40 && bond.Resentment < 35 && State.WorldSeconds - bond.LastHarmAt >= 30)
            {
                bond.Affection = Clamp100(bond.Affection + dt * .12 * comfort);
                bond.Fear = Math.Max(0, bond.Fear - dt * .012);
                bond.Resentment = Math.Max(0, bond.Resentment - dt * .004);
            }
            if (!c.Family.PartnerIds.Contains(other.Id) && string.CompareOrdinal(c.Id, other.Id) < 0)
                FormPartnership(c, other);
            // Only actual visible affection reveals an already existing pairing to later witnesses.
            if (c.Family.PartnerIds.Contains(other.Id))
                foreach (var e in State.FamilyRelationshipEvents.Where(e => e.EndedAt == null &&
                    (e.FirstId == c.Id && e.SecondId == other.Id || e.FirstId == other.Id && e.SecondId == c.Id)).ToArray())
                    ObservePartnership(e, c, other);
        }
    }

    private bool CanFamilyFight(Creature c) => c.Condition == PhysicalCondition.Active && !c.IsYoung &&
        CanSelfMove(c) && !c.Family.ColdStorage.Stored && c.Family.ColdStorage.RecoveryRemaining <= 0 && !c.Quarantine && !c.IsIntruder && c.Pain < 40 && c.Fear < 65 &&
        c.Fullness > 25 && c.Interaction.Hydration > 25 && c.Energy > 25 && !c.Interaction.Resting &&
        c.Interaction.Height <= .25 && c.Interaction.EatingRemaining <= 0 && !c.Interaction.Physiology.Active &&
        !c.Interaction.Digestion.Symptomatic && c.Interaction.ActiveCue == null && InteractionGestureTarget != c.Id &&
        c.RetreatRemaining <= 0 && c.Interaction.WaitRemaining <= 0 && !c.Interaction.FollowPlayer &&
        c.Interaction.RestRemaining <= 0 && c.Family.FlightRemaining <= 0;

    private bool CanProtectFamily(Creature c) => CanFamilyFight(c) && !FamilyRules.UnderIntervention(c) &&
        c.Family.Conflict.TargetId==null && c.Family.ChildThreat.TargetId==null && c.Family.CareParcelId==null;

    private bool CanPursueFamilyConflict(Creature c,Creature target) => c!=target&&CanFamilyFight(c)&&
        c.Family.Conflict.Remaining>0&&!FamilyRules.UnderIntervention(c)&&!WaterEnvironment.IsInWater(State,c)&&
        target.Condition!=PhysicalCondition.Dead&&!target.Interaction.Carried&&
        !target.Family.ColdStorage.Stored&&FamilyVisible(c,target,7);

    private bool TryFamilyConflict(Creature c, double dt)
    {
        var conflict = c.Family.Conflict;
        var target = State.Creatures.Find(x => x.Id == conflict.TargetId);
        if (target == null || !CanPursueFamilyConflict(c,target))
        { StopFamilyConflict(c, "双方已经隔开或身体无法继续，停止了冲突"); return false; }
        // A nearby capable companion can interpose before contact; no remote protective magic.
        var defender = State.Creatures.Where(x => x != c && x != target && CanProtectFamily(x) &&
            x.Courage >= .55 && FamilyVisible(x, c, 2) && FamilyInContact(x, target) &&
            (x.Family.PartnerIds.Contains(target.Id) || FamilyRules.IsCloseFamily(x, target) || target.Family.CaregiverIds.Contains(x.Id)))
            .OrderBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
        if (defender != null)
        {
            StopFamilyConflict(c, $"{defender.Name}护住了{target.Name}，冲突被隔开");
            defender.Family.LastEvent = $"及时护住了{target.Name}";
            React(defender, Expression.Angry, "不要伤害它！", $"守在{target.Name}身旁，挡住了进一步靠近。", 5);
            return true;
        }
        if (conflict.WarningRemaining > 0) { c.Activity = $"愤怒地盯着{target.Name}，尚未动手"; return true; }
        if (!FamilyInContact(c, target)) { RoomMove(c, target.X, target.Z, dt); c.Activity = $"愤怒地靠近{target.Name}"; return true; }
        c.Activity = $"与{target.Name}发生冲突";
        if (conflict.StrikeRemaining > 0) return true;
        conflict.StrikeRemaining = 3; conflict.Strikes++;
        double damage = 7 + c.Aggression * 7;
        Hurt(target, damage, "家庭冲突伤害", c.Id, BodyPart.Side);
        var bond = FamilyBond(target, c); bond.Fear = Clamp100(bond.Fear + 15); bond.Resentment = Clamp100(bond.Resentment + 12);
        bond.Affection = Math.Max(0, bond.Affection - 15); bond.LastHarmAt = State.WorldSeconds; bond.LastEvent = "受到对方的家庭冲突伤害";
        var incident = Emit(IncidentKind.Attack, target, c.Id, $"{c.Name}在家庭冲突中伤害了{target.Name}", damage);
        c.CurrentOffenseId = incident.Id; c.OffenseRemaining = 60;
        Remember(target, incident, $"被{c.Name}伤害，感到害怕。", damage);
        target.Family.LastEvent = $"受到{c.Name}伤害，需要保护与照护";
        if (target.Condition == PhysicalCondition.Active)
        {
            React(target, Expression.Calling, "不要！帮帮我！", "遭到伤害，试图退开并求助。", 5);
            if (CanSelfMove(target))
            {
                double dx = target.X - c.X, dz = target.Z - c.Z;
                double distance = Math.Max(.01, Math.Sqrt(dx * dx + dz * dz));
                target.Family.FlightX = Math.Clamp(target.X + dx / distance * 3, -7, 7);
                target.Family.FlightZ = Math.Clamp(target.Z + dz / distance * 3, -7, 7);
                target.Family.FlightRemaining = 5;
            }
        }
        if (conflict.Strikes >= 16 || target.Condition == PhysicalCondition.Dead) StopFamilyConflict(c, "这次冲突结束，伤势和家庭后果仍需处理");
        return true;
    }
}
