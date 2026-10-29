namespace Yukkuri.Simulation;

public sealed class FamilyLossKnowledge
{
    public KnowledgeSource Source { get; set; }
    public KnowledgeSource FirstSource { get; set; }
    public double LearnedAt { get; set; }
    public double? ConfirmedAt { get; set; }
    public string? InformantId { get; set; }
    public bool FarewellSeen { get; set; }
}

public sealed partial class FamilyLife
{
    public bool Memorialized { get; set; }
    public double? MemorializedAt { get; set; }
    public bool DeathWitnessesRecorded { get; set; }
    public Dictionary<string, FamilyLossKnowledge> KnownLosses { get; set; } = new();
    public string? GrievingForId { get; set; }
    public double GriefRemaining { get; set; }
    public double NextGriefAt { get; set; }
    public double NextLossConversationAt { get; set; }
}

public static partial class FamilyRules
{
    public static string DescribeAftermath(Creature c)
    {
        if (c.Condition == PhysicalCondition.Dead)
            return c.Family.Memorialized ? "已经妥善安置；身份、亲缘和经历仍保留" :
                c.Family.ColdStorage.Stored ? "已经离世，遗体仍在冷藏中；需先取出再安置" : "已经离世，可以妥善安置遗体";
        return c.Family.GriefRemaining > 0 ? "正在经历失去熟悉之人的悲伤；先照顾身体，也可以有人安静陪着" : "";
    }

    public static void ValidateAftermath(WorldState state)
    {
        static void Require(bool valid, string message)
        { if (!valid) throw new InvalidDataException(message); }
        static bool Number(double value, double max = double.MaxValue) => double.IsFinite(value) && value >= 0 && value <= max;
        static bool LossSource(KnowledgeSource source) => source is KnowledgeSource.Witness or KnowledgeSource.Discovered or KnowledgeSource.Told;
        var creatures = state.Creatures.ToDictionary(c => c.Id, StringComparer.Ordinal);
        foreach (var c in state.Creatures)
        {
            var f = c.Family;
            Require(f != null && f.KnownLosses != null && f.KnownLosses.Count <= creatures.Count, "离世知情记录缺失或越界");
            Require(!f!.DeathWitnessesRecorded || c.Condition == PhysicalCondition.Dead, "生者不能有自己的死亡见证结算");
            Require(f.Memorialized
                ? c.Condition == PhysicalCondition.Dead && f.MemorializedAt.HasValue && Number(f.MemorializedAt.Value, state.WorldSeconds + .001) &&
                    f.MemorializedAt.Value + .001 >= c.Interaction.Injuries.DeathAt &&
                    !f.ColdStorage.Stored && c.Interaction.Restraint == RestraintKind.None && !c.Interaction.Carried &&
                    !c.Interaction.Falling && !c.Interaction.Lowering && c.Interaction.Height == 0 &&
                    c.Interaction.EatingRemaining == 0 && c.Interaction.GroundIntakeSourceId == null &&
                    f.CareParcelId == null && f.CareTargetId == null && f.CompanionTargetId == null && f.Conflict.TargetId == null
                : f.MemorializedAt == null, "遗体安置状态与身体或占用冲突");
            Require(Number(f.GriefRemaining, 300) && Number(f.NextGriefAt) && Number(f.NextLossConversationAt), "悲伤时间无效");
            Require(f.GriefRemaining == 0 ? f.GrievingForId == null :
                c.Condition != PhysicalCondition.Dead && f.GrievingForId != null && f.KnownLosses!.ContainsKey(f.GrievingForId), "悲伤缺少已获知的离世对象");
            foreach (var pair in f.KnownLosses!)
            {
                var knowledge = pair.Value;
                Require(pair.Key != c.Id && creatures.TryGetValue(pair.Key, out var deceased) && deceased.Condition == PhysicalCondition.Dead &&
                    knowledge != null && LossSource(knowledge.Source) && LossSource(knowledge.FirstSource) &&
                    Number(knowledge.LearnedAt, state.WorldSeconds + .001), "离世知情对象或来源无效");
                Require(knowledge!.ConfirmedAt == null ? knowledge.Source == KnowledgeSource.Told :
                    knowledge.Source != KnowledgeSource.Told && Number(knowledge.ConfirmedAt.Value, state.WorldSeconds + .001) &&
                    knowledge.ConfirmedAt.Value >= knowledge.LearnedAt, "离世亲自确认时间无效");
                Require(knowledge.LearnedAt + .001 >= creatures[pair.Key].Interaction.Injuries.DeathAt, "不能在死亡事实之前获知离世");
                Require(knowledge.FirstSource == KnowledgeSource.Told
                    ? knowledge.Source != KnowledgeSource.Witness && knowledge.InformantId != null && knowledge.InformantId != c.Id &&
                        knowledge.InformantId != pair.Key && creatures.ContainsKey(knowledge.InformantId)
                    : knowledge.InformantId == null && knowledge.Source == knowledge.FirstSource, "离世转述来源或升级过程无效");
                Require(knowledge.Source != KnowledgeSource.Witness || creatures[pair.Key].Family.DeathWitnessesRecorded &&
                    Math.Abs(knowledge.LearnedAt - creatures[pair.Key].Interaction.Injuries.DeathAt) <= .001, "死亡目击不能由后来发现追认");
                Require(!knowledge.FarewellSeen || creatures[pair.Key].Family.Memorialized &&
                    knowledge.Source != KnowledgeSource.Told, "告别记忆缺少实际安置和亲自确认");
            }
        }
    }
}

public sealed partial class WorldSimulation
{
    // Called inside Die after its fact/time is written, before support cleanup can move the body.
    // A later tick or load can discover a body, but must never invent a witnessed death.
    private void RegisterFamilyDeath(Creature deceased)
    {
        if (deceased.Condition != PhysicalCondition.Dead || deceased.Family.DeathWitnessesRecorded) return;
        deceased.Family.DeathWitnessesRecorded = true;
        deceased.Family.GriefRemaining = 0; deceased.Family.GrievingForId = null;
        foreach (var observer in State.Creatures.OrderBy(c => c.Id, StringComparer.Ordinal))
            if (observer != deceased && FamilyVisible(observer, deceased))
                LearnFamilyLoss(observer, deceased, KnowledgeSource.Witness);
    }

    private bool IsCloseLoss(Creature observer, Creature deceased) =>
        FamilyRules.IsCloseFamily(observer, deceased) || observer.Family.PartnerIds.Contains(deceased.Id) ||
        observer.Family.CaregiverIds.Contains(deceased.Id) || deceased.Family.CaregiverIds.Contains(observer.Id);

    private void LearnFamilyLoss(Creature observer, Creature deceased, KnowledgeSource source, Creature? informant = null)
    {
        if (observer == deceased || observer.Condition == PhysicalCondition.Dead || deceased.Condition != PhysicalCondition.Dead) return;
        if (observer.Family.KnownLosses.TryGetValue(deceased.Id, out var prior))
        {
            // Upgrading hearsay after actually seeing the body confirms the fact once,
            // without a second emotional penalty or a fictional death-time eyewitness.
            if (prior.Source == KnowledgeSource.Told && source == KnowledgeSource.Discovered)
            {
                prior.Source = KnowledgeSource.Discovered; prior.ConfirmedAt = State.WorldSeconds;
                observer.Family.LastEvent = $"后来亲自看见{deceased.Name}已经离世；此前是听说";
                RecordInteraction(observer, "family-loss-confirmed-" + deceased.Id, observer.Family.LastEvent, 1);
            }
            return;
        }
        var knowledge = new FamilyLossKnowledge
        {
            Source = source, FirstSource = source, LearnedAt = State.WorldSeconds,
            ConfirmedAt = source == KnowledgeSource.Told ? null : State.WorldSeconds,
            InformantId = source == KnowledgeSource.Told ? informant?.Id : null
        };
        observer.Family.KnownLosses.Add(deceased.Id, knowledge);
        bool family = IsCloseLoss(observer, deceased);
        bool friend = observer.Family.Peers.TryGetValue(deceased.Id, out var bond) && bond.Affection >= 35;
        // Older saves used personal incident memories before KnownLosses existed.
        // Preserve their emotional settlement, without upgrading old distance-only
        // knowledge into a new claim that the observer really saw the death.
        bool previouslyAcknowledged = observer.PersonalMemories.Any(memory => State.Incidents.Any(incident =>
            incident.Kind == IncidentKind.Death && incident.TargetId == deceased.Id && incident.Id == memory.IncidentId));
        double duration = previouslyAcknowledged ? 0 : family ? 180 : friend ? 90 : 0;
        if (duration > 0)
        {
            if (duration >= observer.Family.GriefRemaining) observer.Family.GrievingForId = deceased.Id;
            observer.Family.GriefRemaining = Math.Min(300, Math.Max(observer.Family.GriefRemaining, duration));
            observer.Family.NextGriefAt = Math.Max(observer.Family.NextGriefAt, State.WorldSeconds + 6);
        }
        double alarm = source == KnowledgeSource.Told ? 1 : source == KnowledgeSource.Witness ? 6 : 3;
        if (!previouslyAcknowledged) observer.Fear = Clamp100(observer.Fear + alarm);
        string fact = source switch
        {
            KnowledgeSource.Witness => $"亲眼见到{deceased.Name}失去回应、离世",
            KnowledgeSource.Discovered => $"后来发现{deceased.Name}已经离世，没有目睹经过",
            _ => $"听{informant!.Name}说{deceased.Name}已经离世，尚未亲自确认"
        };
        // The authoritative DeathCause stays on the deceased. Seeing a body or hearing
        // a name does not reveal who caused it or the underlying medical mechanism.
        observer.Family.LastEvent = fact;
        RecordInteraction(observer, "family-loss-" + deceased.Id, fact, family ? 8 : friend ? 5 : 2);
    }

    public string TellFamilyLoss(string speakerId, string listenerId, string deceasedId)
    {
        if (State.Settings.Pause) return "当前已暂停，恢复普通时间后再交流。";
        var speaker = State.Creatures.Find(c => c.Id == speakerId);
        var listener = State.Creatures.Find(c => c.Id == listenerId);
        var deceased = State.Creatures.Find(c => c.Id == deceasedId);
        if (speaker == null || listener == null || deceased == null || speaker == listener ||
            speaker.RegionId != State.ActiveRegionId || deceased.Condition != PhysicalCondition.Dead ||
            !speaker.Family.KnownLosses.ContainsKey(deceasedId) || !ReadyForLossConversation(speaker) || !ReadyForLossConversation(listener) ||
            speaker.Family.CompanionTargetId != null && speaker.Family.CompanionTargetId != listener.Id ||
            listener.Family.CompanionTargetId != null && listener.Family.CompanionTargetId != speaker.Id ||
            !FamilyInContact(speaker, listener)) return "需要已经知情的油和另一只在近处、愿意交流时说起这件事。";
        if (listener.Family.KnownLosses.ContainsKey(deceasedId)) return "它已经知道这次离世，不会重复承受同一消息。";
        if (State.WorldSeconds < speaker.Family.NextLossConversationAt) return "刚刚谈过离世的消息，先留一点时间消化和陪伴。";
        LearnFamilyLoss(listener, deceased, KnowledgeSource.Told, speaker);
        speaker.Family.NextLossConversationAt = State.WorldSeconds + 30;
        return "已实际转述，记录为听说；没有替它编造目击或具体死因。";
    }

    public string LayToRest(string id)
    {
        if (State.Settings.Pause) return "当前已暂停，恢复普通时间后再安置。";
        var deceased = State.Creatures.Find(c => c.Id == id);
        if (deceased == null || deceased.RegionId != State.ActiveRegionId) return "先选择当前区域的遗体。";
        if (deceased.Condition != PhysicalCondition.Dead) return "它还活着，不能作为遗体安置；有伤势应继续照护。";
        if (deceased.Family.Memorialized) return "已经妥善安置，身份和家庭记录仍然保留。";
        if (deceased.Family.ColdStorage.Stored) return "先把遗体从冷藏取出，再妥善安置。";

        // Capture attendees before removing an enclosing fixture or hiding the body.
        // Releasing a box must not retroactively make everyone an eyewitness.
        var attendees = State.Creatures.Where(c => c != deceased && FamilyVisible(c, deceased)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        foreach (var observer in attendees)
        {
            LearnFamilyLoss(observer, deceased, KnowledgeSource.Discovered);
            if (observer.Family.KnownLosses[deceased.Id].FarewellSeen) continue;
            observer.Family.KnownLosses[deceased.Id].FarewellSeen = true;
            observer.Family.LastEvent = $"在场与{deceased.Name}告别，仍记得过去的相处";
            RecordInteraction(observer, "family-farewell-" + deceased.Id, observer.Family.LastEvent, 2);
        }

        StopIncapacitatedActions(deceased);
        ResetNaturalResourceSearch(deceased);
        if (InteractionGestureTarget == id) _interactionGesture = null;
        var i = deceased.Interaction;
        i.Restraint = RestraintKind.None; i.Carried = false; i.Falling = false; i.Lowering = false;
        i.Height = 0; i.VerticalVelocity = i.VelocityX = i.VelocityZ = 0;
        i.Supine = false; i.SupineRemaining = 0; i.EndRoll(); i.RestRemaining = 0;
        i.ReactionPose = "dead"; i.ReactionPoseRemaining = 0; i.HelpWaitingUntil = 0;
        deceased.Family.Conflict = new(); deceased.Family.FlightRemaining = 0;
        deceased.Family.GriefRemaining = 0; deceased.Family.GrievingForId = null;
        foreach (var other in State.Creatures.Where(c => c != deceased))
        {
            if (other.Family.CareTargetId == id) { DropCareParcel(other); other.Family.CareTargetId = null; }
            if (other.Family.CompanionTargetId == id) ClearCompanionship(other);
            if (other.Family.Conflict.TargetId == id) StopFamilyConflict(other, "对象已经离世并安置，冲突停止，既有后果保留");
        }
        // Keep identity, position, kinship and cause intact. Rendering/collision omit
        // Memorialized individuals; no teleport, fall or new damage is simulated here.
        deceased.Family.Memorialized = true; deceased.Family.MemorializedAt = State.WorldSeconds;
        deceased.Family.LastEvent = "遗体已经妥善安置，身份、亲缘和死因记录保留";
        deceased.Activity = "已安置"; deceased.Speech = ""; deceased.ReactionRemaining = 0;
        deceased.LastReaction = "已经妥善安置。";
        Log($"已经妥善安置{deceased.Name}，保留其身份和家庭记录。");
        return State.LastActionMessage = "已经妥善安置；留下来的油仍需要正常吃喝、照护和陪伴。";
    }

    private bool ReadyForLossConversation(Creature c) => FamilyActionReady(c) &&
        c.Fullness >= 55 && c.Interaction.Hydration >= 55 && c.Energy >= 40 &&
        !c.Interaction.NaturalIntakePending;

    private void TickFamilyAftermath(double dt)
    {
        var visibleBodies = State.Creatures.Where(c => c.Condition == PhysicalCondition.Dead && !c.Family.Memorialized &&
            !c.Family.ColdStorage.Stored).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        foreach (var observer in State.Creatures.OrderBy(c => c.Id, StringComparer.Ordinal))
        {
            if (observer.Condition == PhysicalCondition.Dead) continue;
            foreach (var deceased in visibleBodies)
                if ((!observer.Family.KnownLosses.TryGetValue(deceased.Id, out var known) || known.Source == KnowledgeSource.Told) &&
                    FamilyVisible(observer, deceased, 3))
                    LearnFamilyLoss(observer, deceased, KnowledgeSource.Discovered);

            var f = observer.Family;
            bool receivingCompany = f.LastCompanionActivityAt == State.WorldSeconds && f.CompanionTargetId is { } companionId &&
                State.Creatures.Find(c => c.Id == companionId) is { } companion && companion.Family.LastCompanionActivityAt == State.WorldSeconds;
            f.GriefRemaining = Math.Max(0, f.GriefRemaining - BiologicalDelta(observer, dt) * (receivingCompany ? 1.5 : 1));
            if (f.GriefRemaining == 0) f.GrievingForId = null;

            // The older management loop does not consume CompanionTargetId.
            // Keep its factual knowledge and grief clock, without queuing an action
            // which that mode cannot perform. Manual nearby conversation remains usable.
            if (!State.InteractionPlaytest) continue;
            if (!ReadyForLossConversation(observer)) continue;
            if (receivingCompany && State.WorldSeconds >= f.NextLossConversationAt)
            {
                var listener = State.Creatures.Find(c => c.Id == f.CompanionTargetId);
                if (listener != null && ReadyForLossConversation(listener) && FamilyInContact(observer, listener))
                {
                    var loss = f.KnownLosses.OrderBy(p => p.Key, StringComparer.Ordinal)
                        .FirstOrDefault(p => !listener.Family.KnownLosses.ContainsKey(p.Key));
                    if (loss.Key != null && State.Creatures.Find(c => c.Id == loss.Key) is { } reportedDead)
                    {
                        LearnFamilyLoss(listener, reportedDead, KnowledgeSource.Told, observer);
                        f.NextLossConversationAt = State.WorldSeconds + 30;
                    }
                }
            }
            if (f.GriefRemaining <= 0 || f.CompanionTargetId != null || State.WorldSeconds < Math.Max(f.NextGriefAt, f.NextSocialAt)) continue;
            f.NextGriefAt = State.WorldSeconds + 20;
            var comforter = State.Creatures.Where(c => c != observer && ReadyForLossConversation(c) && c.Family.CompanionTargetId == null &&
                FamilyVisible(observer, c, 4) && observer.Family.Peers.TryGetValue(c.Id, out var own) &&
                own.Affection >= 15 && own.Fear < 40 && own.Resentment < 35 && State.WorldSeconds >= own.NextInvitationAt &&
                c.Family.Peers.TryGetValue(observer.Id, out var other) && other.Affection >= 15 && other.Fear < 40 && other.Resentment < 35 &&
                State.WorldSeconds >= other.NextInvitationAt && State.WorldSeconds >= c.Family.NextSocialAt)
                .OrderByDescending(c => observer.Family.PartnerIds.Contains(c.Id) || FamilyRules.IsCloseFamily(observer, c))
                .ThenBy(c => Distance(observer.X, observer.Z, c.X, c.Z)).ThenBy(c => c.Id, StringComparer.Ordinal).FirstOrDefault();
            if (comforter == null)
            {
                f.LastEvent = "想找熟悉的油陪一会儿；眼前的吃喝和照护仍照常进行";
                continue;
            }
            BeginCompanionship(observer, comforter);
            f.LastEvent = $"想念已经离世的熟悉伙伴，愿意让{comforter.Name}安静陪在身边";
        }
    }
}
