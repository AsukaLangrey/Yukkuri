using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

// Execute only on the authorized independent Alice/dot host, never on the source workstation.
int failures = 0;
void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
void Check(string name, Action action)
{
    try { action(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex); }
}
void Tick(WorldSimulation sim, double seconds)
{
    for (int n = 0; n < (int)Math.Round(seconds * 20); n++)
    { sim.AdvanceInteractionPresentation(.05); sim.Advance(.05); }
}
WorldSimulation Fixture(ulong seed = 912345)
{
    var sim = WorldSimulation.CreateInteractionPlaytest(seed);
    sim.State.RoomObjects.Clear(); sim.State.PlayerX = 0; sim.State.PlayerZ = 7;
    for (int n = 0; n < sim.State.Creatures.Count; n++)
    {
        var c = sim.State.Creatures[n];
        c.IsYoung = false; c.Body.AgeSeconds = 2000; c.InnerAgeSeconds = 2000;
        c.Fullness = c.Energy = c.Interaction.Hydration = 100;
        c.TrueDisposition = "gentle"; c.Aggression = .08; c.Patience = .6; c.Courage = .2;
        c.Trust = 20; c.Fear = c.Pain = 0; c.Family.NextSocialAt = 10000;
        c.Interaction.NextAutonomyAt = 10000; c.Interaction.ToyInterest = 0;
        c.X = n == 0 ? 0 : n == 1 ? 1.65 : -1.65; c.Z = c.TargetZ = 0; c.TargetX = c.X;
    }
    return sim;
}
void PairReady(Creature a, Creature b)
{
    a.Family.Peers[b.Id] = new() { Familiarity = 90, Affection = 90 };
    b.Family.Peers[a.Id] = new() { Familiarity = 90, Affection = 90 };
}
WorldSimulation Reload(WorldSimulation sim)
{
    var s = JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State, SaveStore.JsonOptions), SaveStore.JsonOptions)!;
    SaveStore.Validate(s); return new(s);
}
WorldSimulation ConflictFixture(bool lethal = true)
{
    var sim = Fixture(); var a = sim.State.Creatures[0]; var b = sim.State.Creatures[1]; var target = sim.State.Creatures[2];
    sim.State.Settings.LethalInjuries = lethal;
    a.Aggression = 1; a.Patience = 0;
    b.X = b.TargetX = -5.5; b.Z = b.TargetZ = 5.5;
    target.X = target.TargetX = 1.65;
    PairReady(a, b); PairReady(b, target);
    a.Family.PartnerIds.Add(b.Id); b.Family.PartnerIds.Add(a.Id);
    b.Family.PartnerIds.Add(target.Id); target.Family.PartnerIds.Add(b.Id);
    var e = new FamilyRelationshipEvent
    {
        Id = "family-relationship-000001", FirstId = b.Id, SecondId = target.Id,
        BetrayedPartners = new() { [a.Id] = b.Id }, KnownBy = new() { [a.Id] = KnowledgeSource.Witness }, ReactedBy = new() { a.Id }
    };
    sim.State.FamilySequence = 1; sim.State.FamilyRelationshipEvents.Add(e);
    a.Family.Conflict = new() { TargetId = target.Id, CauseEventId = e.Id, WarningRemaining = 8, Remaining = 60 };
    a.Family.ConflictCooldownUntil = 180;
    return sim;
}

Check("邀请不凭空增加感情，重复点击不刷人油信任", () =>
{
    var sim = Fixture(); var a = sim.State.Creatures[0]; var b = sim.State.Creatures[1];
    a.Trust = b.Trust = 100;
    for (int n = 0; n < 100; n++) sim.InviteCompanion(a.Id, b.Id);
    Require(a.Family.Peers[b.Id].Affection == 0 && b.Family.Peers[a.Id].Affection == 0, "点击直接增加了感情");
    Require(a.Trust == 100 && b.Trust == 100, "同伴邀请影响了对人信任");
    Require(!sim.FormPartnership(a, b), "高人类信任直接等于两油爱慕");
    Tick(sim, 8);
    Require(a.Family.Peers[b.Id].Affection > 0 && b.Family.Peers[a.Id].Affection > 0, "真实近距离相处没有积累");
    Require(a.Family.Peers[b.Id].Affection < 2, "相处收益异常");
});
Check("单方喜欢不能形成伴侣，身体与需求可以拒绝", () =>
{
    var sim = Fixture(); var a = sim.State.Creatures[0]; var b = sim.State.Creatures[1];
    PairReady(a, b); b.Family.Peers[a.Id].Affection = 0;
    Require(!sim.FormPartnership(a, b), "复制了单方关系");
    b.Family.Peers[a.Id].Affection = 90; b.Pain = 60;
    sim.InviteCompanion(a.Id, b.Id); Require(a.Family.CompanionTargetId == null, "疼痛被好感盖过");
    b.Pain = 0; b.Interaction.Hydration = 10;
    Require(!sim.FormPartnership(a, b), "缺水时仍接受配对");
});
Check("可见但远距离不会隔空形成伴侣，邀请会实际靠近", () =>
{
    var sim = Fixture(); var a = sim.State.Creatures[0]; var b = sim.State.Creatures[1];
    sim.State.Creatures[2].X = sim.State.Creatures[2].TargetX = -6;
    b.X = b.TargetX = 4; PairReady(a, b);
    Require(!sim.FormPartnership(a, b), "隔空配对");
    sim.InviteCompanion(a.Id, b.Id); Tick(sim, 2);
    Require(WorldSimulation.Distance(a.X, a.Z, b.X, b.Z) < 4, "相处邀请没有实际移动");
    Require(WorldSimulation.Distance(a.X, a.Z, b.X, b.Z) >= 1.55, "靠近穿过了对方身体");
});
Check("双方关系成立且不凭空生成宝宝", () =>
{
    var sim = Fixture(); var a = sim.State.Creatures[0]; var b = sim.State.Creatures[1]; PairReady(a, b);
    Require(sim.FormPartnership(a, b), "已符合条件却不能结伴");
    Require(a.Family.PartnerIds.Contains(b.Id) && b.Family.PartnerIds.Contains(a.Id), "伴侣不对称");
    Require(sim.State.Creatures.Count == 3 && a.Family.Pregnancy.Phase == PregnancyPhase.None, "结伴直接生成宝宝或绕过孕育请求");
    Require(sim.State.FamilyRelationshipEvents.Count == 1, "未记录唯一关系事件");
    sim.FormPartnership(a, b); Require(sim.State.FamilyRelationshipEvents.Count == 1, "重复形成同一关系");
});
Check("关闭繁育仍可结伴，但不能开始孕育", () =>
{
    var sim = Fixture(); var a = sim.State.Creatures[0]; var b = sim.State.Creatures[1]; PairReady(a, b);
    sim.SetBreedingPermission(a.Id, false);
    Require(sim.FormPartnership(a, b), "禁止繁育同时禁止了伴侣关系");
    sim.RequestPregnancy(a.Id, b.Id, BirthMethod.Stem);
    Require(a.Family.Pregnancy.Phase == PregnancyPhase.None, "禁止繁育后仍开始孕育");
});
Check("通常守承诺，少数违背承诺但远处原伴侣不会全图知情", () =>
{
    var sim = Fixture(); var original = sim.State.Creatures[0]; var partner = sim.State.Creatures[1]; var rival = sim.State.Creatures[2];
    PairReady(original, partner); Require(sim.FormPartnership(original, partner), "前置结伴失败");
    original.X = original.TargetX = -6; original.Z = original.TargetZ = 5;
    rival.X = rival.TargetX = 0; PairReady(partner, rival);
    Require(!sim.FormPartnership(partner, rival), "通常性格无条件违背承诺");
    partner.TrueDisposition = "greedy";
    Require(sim.FormPartnership(partner, rival), "已允许的背叛分支不可达");
    var e = sim.State.FamilyRelationshipEvents.Last();
    Require(e.BetrayedPartners[original.Id] == partner.Id, "未保存当时承诺");
    Require(!e.KnownBy.ContainsKey(original.Id) && original.Family.Peers[partner.Id].Resentment == 0, "远处自动知道背叛");
});
Check("亲眼知情可难过但不必动手，转述不能冒充目击", () =>
{
    var sim = Fixture(); var original = sim.State.Creatures[0]; var partner = sim.State.Creatures[1]; var rival = sim.State.Creatures[2];
    PairReady(original, partner); Require(sim.FormPartnership(original, partner), "前置失败");
    original.X = original.TargetX = -6; original.Z = original.TargetZ = 5;
    rival.X = rival.TargetX = 0; PairReady(partner, rival); partner.TrueDisposition = "greedy";
    Require(sim.FormPartnership(partner, rival), "背叛前置失败"); var e = sim.State.FamilyRelationshipEvents.Last();
    original.X = original.TargetX = -1.65; original.Z = original.TargetZ = 0;
    sim.TellFamilyRelationship(rival.Id, original.Id, e.Id);
    Require(e.KnownBy[original.Id] == KnowledgeSource.Told && original.Family.Conflict.TargetId == null, "转述成为暴力依据");
    sim.InviteCompanion(partner.Id, rival.Id); Tick(sim, 2);
    Require(e.KnownBy[original.Id] == KnowledgeSource.Witness && e.ReactedBy.Contains(original.Id), "实际见到亲近后未升级为目击");
    Require(original.Family.Peers[partner.Id].Resentment >= 38 && original.Family.Conflict.TargetId == null, "温和个体被固定写成暴力剧情");
});
Check("多伴侣必须明确双方接受，不替其他伴侣同意", () =>
{
    var sim = Fixture(); var a = sim.State.Creatures[0]; var b = sim.State.Creatures[1]; var c = sim.State.Creatures[2];
    PairReady(a, b); Require(sim.FormPartnership(a, b), "前置失败"); sim.DiscussOpenPartnership(a.Id, b.Id);
    Require(!a.Family.Peers[b.Id].ConsentsToOtherPartners, "普通个体被玩家强行同意");
    a.Patience = b.Patience = .9; a.SharingSkill = b.SharingSkill = 80;
    sim.DiscussOpenPartnership(a.Id, b.Id);
    Require(a.Family.Peers[b.Id].ConsentsToOtherPartners && b.Family.Peers[a.Id].ConsentsToOtherPartners, "实际同意未双向记录");
    c.X = c.TargetX = 3.3; PairReady(b, c); Require(sim.FormPartnership(b, c), "明确同意后不能另配");
    Require(sim.State.FamilyRelationshipEvents.Last().BetrayedPartners.Count == 0, "接受的关系被认作出轨");
    Require(!c.Family.Peers[b.Id].ConsentsToOtherPartners, "新伴侣自动继承了别人的许可");
});
Check("背叛冲突有伴侣、子油和第三者分支，也保留不动手分支", () =>
{
    var targets = new HashSet<string>(); bool peaceful = false;
    for (ulong seed = 1; seed <= 1024 && (targets.Count < 3 || !peaceful); seed++)
    {
        var sim = Fixture(unchecked(seed * 11400714819323198485UL)); var original = sim.State.Creatures[0]; var partner = sim.State.Creatures[1]; var rival = sim.State.Creatures[2];
        PairReady(original, partner); Require(sim.FormPartnership(original, partner), "前置失败");
        original.Aggression = 1; original.Patience = 0; partner.TrueDisposition = "greedy";
        partner.X = partner.TargetX = .825; partner.Z = partner.TargetZ = 1.43;
        rival.X = rival.TargetX = 1.65; rival.Z = rival.TargetZ = 0; PairReady(partner, rival);
        // A fourth, dependent child provides a real target; its identity is deterministic.
        var child = new Creature
        {
            Id = "social-child", Name = "子油", IsYoung = true, RegionId = "home", X = -1.5, Z = 1.3, TargetX = -1.5, TargetZ = 1.3,
            Body = new() { Id = "social-child-body", SpeciesId = "reimu" },
            Registration = new() { IdentityId = "social-child-identity" },
            Family = new() { MotherId = original.Id, FatherId = partner.Id },
            Interaction = new() { Enabled = true, Accessory = new() { Id = "social-child-accessory" } }
        };
        sim.State.Creatures.Add(child);
        Require(sim.FormPartnership(partner, rival), "背叛前置失败");
        string? target = original.Family.Conflict.TargetId;
        if (target == null) peaceful = true;
        else targets.Add(target == partner.Id ? "partner" : target == rival.Id ? "rival" : target == child.Id ? "child" : "invalid");
        Require(original.Family.Conflict.WarningRemaining is 0 or 8, "冲突绕过预警");
    }
    Require(targets.SetEquals(new[] { "partner", "rival", "child" }) && peaceful, "分支缺失或所有背叛都必然动手");
});
Check("撤回只约束未来，分手保留亲缘并有再次相处的间隔", () =>
{
    var sim = Fixture(); var a = sim.State.Creatures[0]; var b = sim.State.Creatures[1]; var child = sim.State.Creatures[2];
    PairReady(a, b); Require(sim.FormPartnership(a, b), "前置失败");
    child.Family.MotherId = a.Id; child.Family.FatherId = b.Id; child.Family.CaregiverIds.Add(a.Id);
    a.Patience = b.Patience = .9; a.SharingSkill = b.SharingSkill = 80; sim.DiscussOpenPartnership(a.Id, b.Id);
    sim.RevokeOpenPartnership(a.Id, b.Id);
    Require(!a.Family.Peers[b.Id].ConsentsToOtherPartners && b.Family.Peers[a.Id].ConsentsToOtherPartners, "单方撤回改掉了对方的意愿");
    sim.EndPartnership(a.Id, b.Id);
    Require(!a.Family.PartnerIds.Contains(b.Id) && !b.Family.PartnerIds.Contains(a.Id), "分手未双向结束");
    Require(child.Family.MotherId == a.Id && child.Family.FatherId == b.Id && child.Family.CaregiverIds.Contains(a.Id), "分手清掉了血缘或照护责任");
    Require(!sim.FormPartnership(a, b), "分手后立即刷回伴侣");
    sim.State.WorldSeconds = 61;
    Require(sim.FormPartnership(a, b), "后续重新相处后永远不能再配对");
});
Check("跨捕食类别关系更难建立，但双方成熟仍有可能", () =>
{
    var sim = Fixture(); var a = sim.State.Creatures[0]; var b = sim.State.Creatures[1]; b.Body.SpeciesId = "remilia";
    PairReady(a, b); a.Family.Peers[b.Id].Affection = 40;
    Require(!sim.FormPartnership(a, b), "跨类别门槛与普通种相同");
    a.Family.Peers[b.Id].Affection = 90;
    Require(sim.FormPartnership(a, b), "跨类别被永久禁止");
});
Check("暂停与近看不推进关系，暂停不接受关系变更", () =>
{
    var sim = Fixture(); var a = sim.State.Creatures[0]; var b = sim.State.Creatures[1];
    sim.State.Settings.Pause = true; sim.InviteCompanion(a.Id, b.Id);
    Require(a.Family.CompanionTargetId == null, "暂停仍开始相处");
    PairReady(a, b); Require(!sim.FormPartnership(a, b), "暂停仍建立伴侣");
    sim.State.Settings.Pause = false; sim.InviteCompanion(a.Id, b.Id); sim.State.Mode = PlayMode.CloseUp;
    double before = a.Family.Peers[b.Id].Affection; Tick(sim, 30);
    Require(a.Family.Peers[b.Id].Affection == before && sim.State.WorldSeconds == 0, "近看推进了关系或时间");
});
Check("吃喝学习休息占用期间，不靠相邻位置偷偷涨感情", () =>
{
    var sim = Fixture(); var a = sim.State.Creatures[0]; var b = sim.State.Creatures[1];
    sim.InviteCompanion(a.Id, b.Id); b.Interaction.RestRemaining = 10;
    double before = a.Family.Peers[b.Id].Affection; Tick(sim, 2);
    Require(a.Family.Peers[b.Id].Affection == before && a.Family.CompanionTargetId == null, "休息被当作主动陪伴");
});
Check("受伤后需要亲见持续安全才能慢慢修复，远处等待不抹记忆", () =>
{
    var sim = Fixture(); var a = sim.State.Creatures[0]; var b = sim.State.Creatures[1]; PairReady(a, b);
    var bond = a.Family.Peers[b.Id]; bond.Fear = 45; bond.Resentment = 38; bond.LastHarmAt = 0;
    sim.State.WorldSeconds = 121; b.X = b.TargetX = 6; Tick(sim, 2);
    Require(bond.Fear == 45 && bond.Resentment == 38, "离得很远也自动忘掉伤害");
    b.X = b.TargetX = 1.65; b.Z = b.TargetZ = 0; Tick(sim, 2);
    Require(bond.Fear < 45 && bond.Fear > 44 && bond.Resentment < 38 && bond.Resentment > 37, "安全经历没有渐进修复或恢复过快");
    Require(bond.Affection == 90, "旁观安全被当作主动亲密");
});
Check("冷藏、抱起与失能不能继续靠近攻击", () =>
{
    foreach (int state in new[] { 0, 1, 2 })
    {
        var sim = ConflictFixture(); var actor = sim.State.Creatures[0]; var target = sim.State.Creatures[2];
        if (state == 0) actor.Interaction.FootIntegrity = 20;
        if (state == 1) target.Interaction.Carried = true;
        if (state == 2) actor.Family.ColdStorage.Stored = true;
        Tick(sim, .5);
        Require(actor.Family.Conflict.TargetId == null && target.Health == 100, "失能或隔离后仍攻击");
    }
});
Check("家庭暴力有可干预预警，口头制止不要求隐藏玩家移动", () =>
{
    var sim = ConflictFixture(); var actor = sim.State.Creatures[0]; var target = sim.State.Creatures[2];
    Tick(sim, 7); Require(target.Health == 100, "预警期提前伤害");
    sim.InterruptFamilyConflict(actor.Id); Tick(sim, 10);
    Require(target.Health == 100 && actor.Family.Conflict.TargetId == null, "及时制止未阻止伤害");
    Require(sim.State.FamilyRelationshipEvents[0].ReactedBy.Contains(actor.Id), "干预抹去了知情历史");
});
Check("护伴或照护者必须实际在近处才能隔开冲突", () =>
{
    var sim = ConflictFixture(); var actor = sim.State.Creatures[0]; var defender = sim.State.Creatures[1]; var target = sim.State.Creatures[2];
    defender.X = defender.TargetX = .825; defender.Z = defender.TargetZ = 1.43; defender.Courage = .8;
    target.Family.CaregiverIds.Add(defender.Id);
    Tick(sim, 1);
    Require(actor.Family.Conflict.TargetId == null && target.Health == 100 && defender.Family.LastEvent.Contains("护住"), "近处照护者未保护目标");
});
Check("冲突伤害进入共同身体流程，致死设置分别生效", () =>
{
    foreach (bool lethal in new[] { false, true })
    {
        var sim = ConflictFixture(lethal); var target = sim.State.Creatures[2];
        target.Health = 0; target.Condition = PhysicalCondition.Critical; target.CriticalRemaining = 90; target.FillingIntegrity = 5;
        Tick(sim, 9);
        Require(target.FillingIntegrity == 0, "近身冲突没有进入共同Hurt");
        Require((target.Condition == PhysicalCondition.Dead) == lethal, "冲突绕过或忽略致死设置");
        Require(target.Family.Peers.ContainsKey(sim.State.Creatures[0].Id), "受害者没有独立伤害记忆");
    }
});
Check("同档继续推进，警告与伤害不会重抽或重复", () =>
{
    var sim = ConflictFixture(); Tick(sim, 3); var restored = Reload(sim);
    Tick(sim, 10); Tick(restored, 10);
    Require(JsonSerializer.Serialize(sim.State, SaveStore.JsonOptions) == JsonSerializer.Serialize(restored.State, SaveStore.JsonOptions), "相同存档继续推进产生不同结果");
});
Check("旧档缺新增字段有默认，显式空和未知冲突依据拒绝", () =>
{
    var sim = Fixture(); var node = JsonNode.Parse(JsonSerializer.Serialize(sim.State, SaveStore.JsonOptions))!;
    node.AsObject().Remove("FamilyRelationshipEvents");
    node["Creatures"]![0]!["Family"]!.AsObject().Remove("Conflict");
    var old = JsonSerializer.Deserialize<WorldState>(node.ToJsonString(), SaveStore.JsonOptions)!;
    FamilyRules.ValidateSocial(old); Require(old.Creatures[0].Family.Conflict.TargetId == null, "旧档没有安全默认");
    node["Creatures"]![0]!["Family"]!["Conflict"] = null;
    bool rejected = false;
    try { FamilyRules.ValidateSocial(JsonSerializer.Deserialize<WorldState>(node.ToJsonString(), SaveStore.JsonOptions)!); }
    catch (InvalidDataException) { rejected = true; }
    Require(rejected, "显式null没有拒绝");
    var invalid = ConflictFixture(); invalid.State.Creatures[0].Family.Conflict.CauseEventId = "nonexistent"; rejected = false;
    try { FamilyRules.ValidateSocial(invalid.State); } catch (InvalidDataException) { rejected = true; }
    Require(rejected, "伪造冲突依据被接受");
});
Check("历史关系淘汰保留正在冲突的依据，编号不覆盖旧事件", () =>
{
    var sim = Fixture(); var a = sim.State.Creatures[0]; var b = sim.State.Creatures[1]; var c = sim.State.Creatures[2];
    PairReady(a, b); PairReady(a, c); a.TrueDisposition = "greedy";
    a.Family.PartnerIds.Add(c.Id); c.Family.PartnerIds.Add(a.Id);
    for (int n = 1; n <= 256; n++)
        sim.State.FamilyRelationshipEvents.Add(new()
        { Id = $"family-relationship-{n:000000}", FirstId = a.Id, SecondId = b.Id, EndedAt = 0 });
    var active = sim.State.FamilyRelationshipEvents[0];
    active.BetrayedPartners[c.Id] = a.Id; active.KnownBy[c.Id] = KnowledgeSource.Witness; active.ReactedBy.Add(c.Id);
    c.Family.Conflict = new() { TargetId = a.Id, CauseEventId = active.Id, WarningRemaining = 8, Remaining = 60 };
    // A stale counter must advance past existing IDs, not duplicate or overwrite them.
    sim.State.FamilySequence = 0;
    Require(sim.FormPartnership(a, b), "新的有效关系被历史记录堵住");
    Require(sim.State.FamilyRelationshipEvents.Count == 256 && sim.State.FamilyRelationshipEvents.Contains(active), "淘汰删除了冲突依据");
    Require(sim.State.FamilyRelationshipEvents.All(e => e.Id != "family-relationship-000002"), "未淘汰最早可移除历史");
    Require(sim.State.FamilyRelationshipEvents.Last().Id == "family-relationship-000257", "编号冲突或覆盖");
    FamilyRules.ValidateSocial(sim.State);
});
Check("已有伴侣仍不能在休息或失能时绕过准备条件", () =>
{
    var sim = Fixture(); var a = sim.State.Creatures[0]; var b = sim.State.Creatures[1]; PairReady(a, b);
    Require(sim.FormPartnership(a, b), "前置失败");
    b.Interaction.Resting = true; Require(!sim.FormPartnership(a, b), "已有关系让休息被忽略");
    b.Interaction.Resting = false; b.Condition = PhysicalCondition.Critical;
    Require(!sim.FormPartnership(a, b), "已有关系让濒危被忽略");
});

Environment.ExitCode = failures == 0 ? 0 : 1;
