using System.Text.Json;
using System.Text.Json.Nodes;
using Yukkuri.Simulation;

// For the independently authorized Alice/dot test host only. Never run on the development workstation.
int failures = 0;
void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
void Check(string name, Action action)
{
    try { action(); Console.WriteLine("PASS " + name); }
    catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + ": " + error); }
}
void Tick(WorldSimulation sim, double seconds)
{
    for (int n = 0; n < (int)Math.Round(seconds * 20); n++)
    { sim.AdvanceInteractionPresentation(.05); sim.Advance(.05); }
}
WorldSimulation Fixture()
{
    var sim = WorldSimulation.CreateInteractionPlaytest(20261030);
    sim.State.RoomObjects.Clear(); sim.State.PlayerX = 0; sim.State.PlayerZ = 7;
    for (int n = 0; n < sim.State.Creatures.Count; n++)
    {
        var c = sim.State.Creatures[n];
        c.IsYoung = false; c.InnerAgeSeconds = c.Body.AgeSeconds = 2000;
        c.Fullness = c.Energy = c.Interaction.Hydration = 100; c.Fear = c.Pain = 0;
        c.TrueDisposition = "gentle"; c.Aggression = .08; c.Patience = .6; c.Courage = .2;
        c.Family.NextSocialAt = 10000; c.Interaction.NextAutonomyAt = 10000; c.DecisionIn = 10000;
        c.Interaction.ToyInterest = 0;
        c.X = c.TargetX = n == 0 ? 0 : n == 1 ? 1.65 : -5.5;
        c.Z = c.TargetZ = n == 2 ? 5.5 : 0;
    }
    sim.State.Creatures[1].Family.MotherId = sim.State.Creatures[0].Id;
    return sim;
}
void Kill(WorldSimulation sim, Creature c) => sim.Hurt(c, 200, "测试内部死因记录", null);
WorldSimulation Reload(WorldSimulation sim)
{
    var state = JsonSerializer.Deserialize<WorldState>(JsonSerializer.Serialize(sim.State, SaveStore.JsonOptions), SaveStore.JsonOptions)!;
    SaveStore.Validate(state); return new(state);
}

Check("死亡时刻实际可见才是目击，同一消息只结算一次恐惧", () =>
{
    var sim = Fixture(); var deceased = sim.State.Creatures[0]; var relative = sim.State.Creatures[1]; var distant = sim.State.Creatures[2];
    Kill(sim, deceased);
    Require(deceased.Condition == PhysicalCondition.Dead && deceased.Family.DeathWitnessesRecorded, "死亡事实没有接入家庭钩子");
    var knowledge = relative.Family.KnownLosses[deceased.Id];
    Require(knowledge.Source == KnowledgeSource.Witness && knowledge.FirstSource == KnowledgeSource.Witness, "在场死亡目击来源错误");
    Require(relative.Fear == 6, "同一次离世被旧事件和家庭系统重复增加恐惧");
    Require(!distant.Family.KnownLosses.ContainsKey(deceased.Id), "远处亲友自动知情");
    double fear = relative.Fear; double memory = relative.Interaction.Memories["family-loss-" + deceased.Id];
    Tick(sim, 4);
    Require(relative.Fear <= fear && relative.Interaction.Memories["family-loss-" + deceased.Id] == memory, "反复看到遗体重复结算");
    Require(relative.Family.MotherId == deceased.Id, "离世清除了亲缘");
});
Check("后来发现只确认已经离世，不编造亲眼经过或死因", () =>
{
    var sim = Fixture(); var deceased = sim.State.Creatures[0]; var later = sim.State.Creatures[2]; Kill(sim, deceased);
    later.X = later.TargetX = -1.65; later.Z = later.TargetZ = 0; Tick(sim, .5);
    Require(later.Family.KnownLosses[deceased.Id].Source == KnowledgeSource.Discovered, "后来到场被追认为目击死亡");
    Require(!later.Family.LastEvent.Contains("测试内部死因记录") &&
        later.PersonalMemories.All(m => !m.Text.Contains("测试内部死因记录")), "见到遗体凭空知道了具体死因");
});
Check("冷藏内部死亡不广播，取出后才可能被发现", () =>
{
    var sim = Fixture(); var deceased = sim.State.Creatures[0]; var observer = sim.State.Creatures[1];
    sim.SetColdStorage(deceased.Id, true); Require(deceased.Family.ColdStorage.Stored, "冷藏前置失败");
    Kill(sim, deceased); Tick(sim, .5);
    Require(!observer.Family.KnownLosses.ContainsKey(deceased.Id), "隔着冷藏获知死亡");
    sim.LayToRest(deceased.Id); Require(!deceased.Family.Memorialized, "未取出就安置了冷藏遗体");
    sim.SetColdStorage(deceased.Id, false); Tick(sim, .5);
    Require(observer.Family.KnownLosses[deceased.Id].Source == KnowledgeSource.Discovered, "取出被追认为冷藏内部的死亡目击");
});
Check("箱体遮挡与不同区域阻止知情，安置不补造告别在场", () =>
{
    var sim = Fixture(); var deceased = sim.State.Creatures[0]; var observer = sim.State.Creatures[1]; var remote = sim.State.Creatures[2];
    sim.State.Regions.Add(new() { Id = "elsewhere", Name = "另一处", Kind = RegionKind.Wilderness }); remote.RegionId = "elsewhere";
    deceased.Interaction.Restraint = RestraintKind.Box; Kill(sim, deceased);
    Require(!observer.Family.KnownLosses.ContainsKey(deceased.Id) && !remote.Family.KnownLosses.ContainsKey(deceased.Id), "隔箱或跨区域自动知情");
    sim.LayToRest(deceased.Id);
    Require(deceased.Family.Memorialized && deceased.Interaction.Restraint == RestraintKind.None, "安置未释放装置");
    Require(!observer.Family.KnownLosses.ContainsKey(deceased.Id), "解除箱体时倒算了此前在场告别");
    Tick(sim, 1); Require(!remote.Family.KnownLosses.ContainsKey(deceased.Id), "安置向其他区域广播");
});
Check("树干遮挡不能看到死亡，绕到可见处才发现", () =>
{
    var sim = Fixture(); var deceased = sim.State.Creatures[0]; var observer = sim.State.Creatures[1];
    sim.State.RoomObjects.Add(new() { Id = "after-river", Kind = RoomObjectKind.RiverWater, RegionId = "home", X = 6.5, Z = 2.3 });
    deceased.X = deceased.TargetX = -6.7; deceased.Z = deceased.TargetZ = -6.2;
    observer.X = observer.TargetX = -6.7; observer.Z = observer.TargetZ = -3.4;
    Kill(sim, deceased); Require(!observer.Family.KnownLosses.ContainsKey(deceased.Id), "隔着树干目击了死亡");
    observer.X = observer.TargetX = -4.8; observer.Z = observer.TargetZ = -6.2; Tick(sim, .5);
    Require(observer.Family.KnownLosses[deceased.Id].Source == KnowledgeSource.Discovered, "实际绕到近旁仍未发现");
});
Check("实际转述标听说，后来确认不重复加恐惧或改成目击", () =>
{
    var sim = Fixture(); var deceased = sim.State.Creatures[0]; var speaker = sim.State.Creatures[1]; var listener = sim.State.Creatures[2];
    Kill(sim, deceased); listener.X = listener.TargetX = 3.3; listener.Z = listener.TargetZ = 0;
    sim.TellFamilyLoss(speaker.Id, listener.Id, deceased.Id);
    var knowledge = listener.Family.KnownLosses[deceased.Id];
    Require(knowledge.Source == KnowledgeSource.Told && knowledge.InformantId == speaker.Id && knowledge.ConfirmedAt == null, "转述伪装为目击");
    double fear = listener.Fear;
    for (int n = 0; n < 20; n++) sim.TellFamilyLoss(speaker.Id, listener.Id, deceased.Id);
    Require(listener.Fear == fear, "重复转述叠加恐惧");
    listener.X = listener.TargetX = -1.65; Tick(sim, .5);
    Require(knowledge.Source == KnowledgeSource.Discovered && knowledge.FirstSource == KnowledgeSource.Told &&
        knowledge.InformantId == speaker.Id && knowledge.ConfirmedAt.HasValue, "亲自确认丢失原始转述来源");
    Require(listener.Fear <= fear, "确认同一离世重复施加恐惧");
});
Check("安置后仍能实际转述，但附近路过不凭空发现隐藏遗体", () =>
{
    var sim = Fixture(); var deceased = sim.State.Creatures[0]; var speaker = sim.State.Creatures[1]; var listener = sim.State.Creatures[2];
    Kill(sim, deceased); sim.LayToRest(deceased.Id);
    listener.X = listener.TargetX = 3.3; listener.Z = listener.TargetZ = 0; Tick(sim, .5);
    Require(!listener.Family.KnownLosses.ContainsKey(deceased.Id), "已经安置的遗体仍自动被路人发现");
    sim.TellFamilyLoss(speaker.Id, listener.Id, deceased.Id);
    Require(listener.Family.KnownLosses[deceased.Id].Source == KnowledgeSource.Told, "实际转述被安置状态挡住或来源错误");
});
Check("告别只记实际在场者，重复安置不刷恐惧和记忆", () =>
{
    var sim = Fixture(); var deceased = sim.State.Creatures[0]; var attendee = sim.State.Creatures[1]; var absent = sim.State.Creatures[2];
    Kill(sim, deceased); double fear = attendee.Fear;
    sim.LayToRest(deceased.Id); Require(attendee.Family.KnownLosses[deceased.Id].FarewellSeen, "实际在场未留下告别记忆");
    Require(!absent.Family.KnownLosses.ContainsKey(deceased.Id), "缺席者有告别记忆");
    double memory = attendee.Interaction.Memories["family-farewell-" + deceased.Id];
    for (int n = 0; n < 20; n++) sim.LayToRest(deceased.Id);
    Require(attendee.Fear == fear && attendee.Interaction.Memories["family-farewell-" + deceased.Id] == memory, "安置反复结算");
});
Check("悬挂遗体安置不产生跌落或新死亡，并保留身份亲缘死因", () =>
{
    var sim = Fixture(); var deceased = sim.State.Creatures[0]; var relative = sim.State.Creatures[1];
    deceased.Family.PartnerIds.Add(relative.Id); relative.Family.PartnerIds.Add(deceased.Id);
    deceased.Interaction.Restraint = RestraintKind.Hanging; deceased.Interaction.Height = 2;
    Kill(sim, deceased); double x = deceased.X, z = deceased.Z; string cause = deceased.Interaction.Injuries.DeathCause;
    int deaths = sim.State.Incidents.Count(e => e.Kind == IncidentKind.Death); int count = sim.State.Creatures.Count;
    sim.LayToRest(deceased.Id);
    Require(deceased.X == x && deceased.Z == z && deceased.Interaction.Height == 0 && !deceased.Interaction.Falling, "安置触发了移动或跌落");
    Require(sim.State.Creatures.Count == count && sim.State.Incidents.Count(e => e.Kind == IncidentKind.Death) == deaths &&
        deceased.Condition == PhysicalCondition.Dead && deceased.Interaction.Injuries.DeathCause == cause, "安置删除身份、复活或重复死亡");
    Require(relative.Family.MotherId == deceased.Id && relative.Family.PartnerIds.Contains(deceased.Id), "安置清掉亲缘或既有关系");
});
Check("死亡与安置只退还一次尚未入口的食物", () =>
{
    var sim = Fixture(); var deceased = sim.State.Creatures[0];
    var meal = new RoomObject { Id = "reserved-meal", Kind = RoomObjectKind.Meal, RegionId = "home", X = .7, Z = 0, Portions = 0 };
    sim.State.RoomObjects.Add(meal);
    deceased.Interaction.GroundIntake = true; deceased.Interaction.NaturalIntakePending = true;
    deceased.Interaction.GroundIntakeSourceId = meal.Id; deceased.Interaction.GroundIntakeKind = meal.Kind;
    deceased.Interaction.EatingKind = IntakeKind.Meal; deceased.Interaction.EatingRemaining = 1.6;
    Kill(sim, deceased); Require(meal.Portions == 1, "死亡未释放尚未入口的预约份量");
    sim.LayToRest(deceased.Id); sim.LayToRest(deceased.Id);
    Require(meal.Portions == 1 && deceased.Interaction.GroundIntakeSourceId == null && deceased.Interaction.EatingRemaining == 0, "安置重复退还或未清占用");
});
Check("悲伤有期限但记忆保留，饥渴优先于陪伴", () =>
{
    var sim = Fixture(); var deceased = sim.State.Creatures[0]; var relative = sim.State.Creatures[1];
    Kill(sim, deceased); Require(relative.Family.GriefRemaining is > 0 and <= 300, "悲伤没有有限期限");
    relative.Fullness = relative.Interaction.Hydration = 10; relative.Family.NextSocialAt = 0; Tick(sim, 2);
    Require(relative.Family.CompanionTargetId == null, "饥渴时被悲伤强制拖去相处");
    relative.Fullness = relative.Interaction.Hydration = 100; Tick(sim, 181);
    Require(relative.Family.GriefRemaining == 0 && relative.Family.GrievingForId == null &&
        relative.Family.KnownLosses.ContainsKey(deceased.Id), "悲伤永久占用生活或缓解后删除记忆");
});
Check("实际陪伴可以缓解悲伤，不能瞬间清除经历", () =>
{
    var sim = Fixture(); var deceased = sim.State.Creatures[0]; var relative = sim.State.Creatures[1]; var companion = sim.State.Creatures[2];
    Kill(sim, deceased); companion.X = companion.TargetX = 3.3; companion.Z = companion.TargetZ = 0;
    relative.Family.Peers[companion.Id] = new() { Familiarity = 30, Affection = 20 };
    companion.Family.Peers[relative.Id] = new() { Familiarity = 30, Affection = 20 };
    relative.Family.NextSocialAt = companion.Family.NextSocialAt = 0;
    Tick(sim, 4);
    Require(relative.Family.LastCompanionActivityAt > 0 && relative.Family.GriefRemaining < 176 && relative.Family.GriefRemaining > 160, "陪伴没有实际发生或一次清空悲伤");
    Require(relative.Family.KnownLosses.ContainsKey(deceased.Id), "陪伴抹掉离世事实");
});
Check("暂停拒绝安置与转述，活体不能被安置", () =>
{
    var sim = Fixture(); var deceased = sim.State.Creatures[0]; var relative = sim.State.Creatures[1];
    sim.LayToRest(relative.Id); Require(!relative.Family.Memorialized, "活体被当成遗体");
    Kill(sim, deceased); sim.State.Settings.Pause = true;
    sim.LayToRest(deceased.Id); Require(!deceased.Family.Memorialized, "暂停时仍安置");
    var listener = sim.State.Creatures[2]; listener.X = 3.3; listener.Z = 0;
    sim.TellFamilyLoss(relative.Id, listener.Id, deceased.Id);
    Require(!listener.Family.KnownLosses.ContainsKey(deceased.Id), "暂停仍转述离世消息");
    double grief = relative.Family.GriefRemaining; Tick(sim, 10);
    Require(relative.Family.GriefRemaining == grief, "暂停推进悲伤时间");
});
Check("相同存档继续推进一致，加载不重复见证或告别", () =>
{
    var sim = Fixture(); Kill(sim, sim.State.Creatures[0]); sim.LayToRest(sim.State.Creatures[0].Id);
    var restored = Reload(sim); Tick(sim, 3); Tick(restored, 3);
    Require(JsonSerializer.Serialize(sim.State, SaveStore.JsonOptions) == JsonSerializer.Serialize(restored.State, SaveStore.JsonOptions), "读取后重复记忆或继续推进不一致");
});
Check("旧档缺字段有默认，显式空、假安置和追认目击被拒绝", () =>
{
    var sim = Fixture(); var node = JsonNode.Parse(JsonSerializer.Serialize(sim.State, SaveStore.JsonOptions))!;
    var family = node["Creatures"]![0]!["Family"]!.AsObject(); family.Remove("KnownLosses"); family.Remove("Memorialized");
    var old = JsonSerializer.Deserialize<WorldState>(node.ToJsonString(), SaveStore.JsonOptions)!; FamilyRules.ValidateAftermath(old);
    Require(!old.Creatures[0].Family.Memorialized && old.Creatures[0].Family.KnownLosses.Count == 0, "旧档默认创造了安置或知情");
    family["KnownLosses"] = null; bool rejected = false;
    try { FamilyRules.ValidateAftermath(JsonSerializer.Deserialize<WorldState>(node.ToJsonString(), SaveStore.JsonOptions)!); }
    catch (InvalidDataException) { rejected = true; }
    Require(rejected, "显式空知情记录未拒绝");
    sim.State.Creatures[0].Family.Memorialized = true; sim.State.Creatures[0].Family.MemorializedAt = 0; rejected = false;
    try { FamilyRules.ValidateAftermath(sim.State); } catch (InvalidDataException) { rejected = true; }
    Require(rejected, "活体伪造安置通过校验");
    sim = Fixture(); Kill(sim, sim.State.Creatures[0]);
    var knowledge = sim.State.Creatures[1].Family.KnownLosses[sim.State.Creatures[0].Id];
    knowledge.FirstSource = KnowledgeSource.Told; knowledge.InformantId = sim.State.Creatures[2].Id; rejected = false;
    try { FamilyRules.ValidateAftermath(sim.State); } catch (InvalidDataException) { rejected = true; }
    Require(rejected, "听说被伪造为死亡目击");
});
Check("已有旧版死亡记忆不在迁移时再次叠加恐惧", () =>
{
    var sim = Fixture(); var deceased = sim.State.Creatures[0]; var relative = sim.State.Creatures[1]; Kill(sim, deceased);
    var death = sim.State.Incidents.Single(e => e.Kind == IncidentKind.Death && e.TargetId == deceased.Id);
    relative.Family.KnownLosses.Clear(); relative.Family.GriefRemaining = 0; relative.Family.GrievingForId = null;
    relative.Fear = 8;
    relative.PersonalMemories.Add(new() { IncidentId = death.Id, Text = "旧版已知离世记忆", At = 0, EmotionalWeight = 10 });
    Tick(sim, .5);
    Require(relative.Fear <= 8 && relative.Family.GriefRemaining == 0, "迁移旧记忆又重新施加悲伤或恐惧");
    Require(relative.Family.KnownLosses[deceased.Id].Source == KnowledgeSource.Discovered, "迁移把未复核的旧来源认定为死亡目击");
});
Check("普通模式获知与悲伤计时可用，不排入无法执行的陪伴动作", () =>
{
    var sim = Fixture(); sim.State.InteractionPlaytest = false; sim.State.MilestoneScenario = false;
    var deceased = sim.State.Creatures[0]; var relative = sim.State.Creatures[1]; var later = sim.State.Creatures[2];
    Kill(sim, deceased); double grief = relative.Family.GriefRemaining; double age = deceased.InnerAgeSeconds;
    later.X = later.TargetX = -1.65; later.Z = later.TargetZ = 0;
    relative.Family.NextSocialAt = later.Family.NextSocialAt = 0;
    relative.Family.NextGriefAt = 0;
    relative.Family.Peers[later.Id] = new() { Familiarity = 30, Affection = 20 };
    later.Family.Peers[relative.Id] = new() { Familiarity = 30, Affection = 20 };
    Tick(sim, 2);
    Require(later.Family.KnownLosses[deceased.Id].Source == KnowledgeSource.Discovered, "普通模式没有实际发现遗体");
    Require(relative.Family.GriefRemaining < grief && relative.Family.CompanionTargetId == null, "普通模式悲伤不减或挂起无法消费的陪伴任务");
    Require(deceased.InnerAgeSeconds == age, "普通模式死者继续长大");
    sim.LayToRest(deceased.Id); Require(deceased.Family.Memorialized, "普通模式安置接口不可用");
});

Environment.ExitCode = failures == 0 ? 0 : 1;
