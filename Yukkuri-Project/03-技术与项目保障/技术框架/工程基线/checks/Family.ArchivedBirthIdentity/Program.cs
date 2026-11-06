using System.Reflection;
using System.Text.Json;
using Yukkuri.Simulation;

// Synthetic accepted-save robustness checks, not evidence of an ordinary-gameplay
// reproduction or a corrupt historical user save. Run only on an authorised host.
Console.OutputEncoding = new System.Text.UTF8Encoding(false);
string output = args.Length > 0 ? args[0] : Path.Combine(".local", "archived-birth-identity", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(output);
int failures = 0, passes = 0;
void Need(bool value, string reason) { if (!value) throw new Exception(reason); }
string Json(WorldSimulation sim) => JsonSerializer.Serialize(sim.State, SaveStore.JsonOptions);
void Tick(WorldSimulation sim, double seconds)
{
    for (int n = 0; n < (int)Math.Round(seconds * 20); n++)
    { sim.AdvanceInteractionPresentation(.05); sim.Advance(.05); }
}
void Check(string name, Action run)
{
    try { run(); passes++; Console.WriteLine("PASS " + name); }
    catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + ": " + e); }
}
WorldSimulation Pair()
{
    var sim = WorldSimulation.CreateCourtyardEconomy(42);
    sim.State.RoomObjects.Clear(); sim.State.Creatures.RemoveAt(2);
    sim.State.AutonomousBreeding = false; Ready(sim); return sim;
}
void Ready(WorldSimulation sim)
{
    for (int n = 0; n < 2; n++)
    {
        var c = sim.State.Creatures[n]; c.X = c.TargetX = n * 1.65; c.Z = c.TargetZ = 0;
        c.Fullness = c.Interaction.Hydration = c.Health = c.Energy = 100;
        c.Pain = c.Fear = 0; c.Trust = 90; c.Interaction.NextAutonomyAt = 10000;
        c.Interaction.ToyInterest = 0; c.Family.NextSocialAt = c.Family.NextCareAt = 10000;
    }
}
void Sell(WorldSimulation sim, Creature creature)
{
    int money = sim.State.Money, price = CourtyardEconomy.Quote(creature).Price;
    string result = sim.SellCreatureToShop(creature.Id, price);
    Need(!sim.State.Creatures.Contains(creature) && sim.State.Money == money + price, result);
}
void AddAndSellSyntheticAdult(WorldSimulation sim, string id)
{
    // Introduce an unrelated complete adult once; never rewrite a living ID.
    var adult = JsonSerializer.Deserialize<Creature>(
        JsonSerializer.Serialize(sim.State.Creatures[1], SaveStore.JsonOptions), SaveStore.JsonOptions)!;
    adult.Id = id; adult.Name = "Synthetic archived adult";
    adult.Body.Id = id + "-body"; adult.Registration.IdentityId = id + "-identity";
    adult.Interaction.Accessory.Id = id + "-accessory"; adult.Family = new();
    adult.X = adult.TargetX = -5; adult.Z = adult.TargetZ = -5;
    sim.State.Creatures.Add(adult); Sell(sim, adult);
    Need(adult.Family.BirthEventId == null, "synthetic adult acquired a birth event");
}
WorldSimulation DiskTwice(WorldSimulation sim, string label)
{
    var store = new SaveStore(Path.Combine(output, label));
    for (int n = 0; n < 2; n++)
    {
        SaveStore.Validate(sim.State); string before = Json(sim);
        store.Save(sim.State, 1); var result = store.Load(1);
        Need(!result.RecoveredBackup, "unexpected backup"); sim = new(result.State);
        Need(Json(sim) == before, "disk roundtrip mutated state");
        SaveStore.Validate(sim.State);
    }
    return sim;
}
void Begin(WorldSimulation sim)
{
    var mother = sim.State.Creatures[0];
    string response = sim.RequestPregnancy(mother.Id, sim.State.Creatures[1].Id, BirthMethod.Stem);
    Need(mother.Family.Pregnancy.Phase == PregnancyPhase.Preparing, response);
}
Creature[] Birth(WorldSimulation sim)
{
    Begin(sim); Tick(sim, 280);
    var mother = sim.State.Creatures[0];
    var babies = sim.State.Creatures.Where(c => c.Family.BirthEventId == mother.Family.Pregnancy.EventId).ToArray();
    Need(babies.Length >= 2, "real bounded birth failed: " + mother.Family.LastEvent); return babies;
}
void Unique(WorldSimulation sim)
{
    Need(sim.State.KnownCreatures.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() ==
        sim.State.KnownCreatures.Count(), "global creature identity collision");
    SaveStore.Validate(sim.State);
}

foreach (bool stale in new[] { false, true })
    Check("normal birth sale reload protection; stale counter=" + stale, () =>
    {
        var sim = Pair(); var babies = Birth(sim);
        string birth = babies[0].Family.BirthEventId!;
        string[] oldIds = babies.Select(c => c.Id).ToArray();
        foreach (var baby in babies) Sell(sim, baby);
        sim = DiskTwice(sim, "control-" + stale + "-first");
        Tick(sim, 180); Ready(sim);
        Need(sim.State.Creatures[0].Family.Pregnancy.Phase == PregnancyPhase.None, "recovery unfinished");
        if (stale) sim.State.FamilySequence = 0;
        Need(sim.State.CompletedBirths.Contains(birth), "completed birth guard absent");
        var next = Birth(sim);
        Need(next.All(c => !oldIds.Contains(c.Id)), "normal births reused sold ID");
        foreach (string id in oldIds) Need(!sim.Select(id), "sold selection revived");
        Unique(sim); DiskTwice(sim, "control-" + stale + "-second");
    });

foreach (string suffix in new[] { "-0", "-99", "" })
    Check("synthetic accepted-save archived allocation invariant; suffix=" + suffix, () =>
    {
        var sim = Pair(); string id = "family-birth-1" + suffix;
        AddAndSellSyntheticAdult(sim, id); sim.State.Creatures[0].Family.MotherId = id;
        sim.State.FamilySequence = 0;
        Need(sim.State.CompletedBirths.Count == 0, "unexpected completed birth record");
        sim = DiskTwice(sim, "synthetic-before-" + suffix);
        Need(!sim.Select(id), "archive selectable before allocation");
        string archive = JsonSerializer.Serialize(sim.State.CreatureSales, SaveStore.JsonOptions);
        var babies = Birth(sim);
        Need(babies.All(c => c.Family.BirthEventId == "family-birth-2"), "reserved birth prefix not skipped");
        Need(!sim.Select(id), "sold selection revived");
        Need(sim.State.KnownCreatures.Single(c => c.Id == id).Name == "Synthetic archived adult", "ancestor identity changed");
        Need(archive == JsonSerializer.Serialize(sim.State.CreatureSales, SaveStore.JsonOptions), "archive changed");
        Unique(sim); DiskTwice(sim, "synthetic-after-" + suffix);
    });

foreach (bool lastSlot in new[] { false, true })
    Check("synthetic late archived collision is atomic; last slot=" + lastSlot, () =>
    {
        var sim = Pair(); Begin(sim);
        for (int n = 0; n < 1200 && sim.State.Creatures[0].Family.Pregnancy.EventId == null; n++) Tick(sim, .05);
        var mother = sim.State.Creatures[0]; var father = sim.State.Creatures[1];
        var pregnancy = mother.Family.Pregnancy;
        Need(pregnancy.Phase == PregnancyPhase.Gestating && pregnancy.PlannedChildren >= 2, "pregnancy did not begin");
        string id = pregnancy.EventId + "-" + (lastSlot ? pregnancy.PlannedChildren - 1 : 0);
        // Explicit robustness fixture: introduce an unrelated adult after event allocation,
        // then use the real sale command. This is not claimed as an ordinary spawn path.
        AddAndSellSyntheticAdult(sim, id); sim = DiskTwice(sim, "late-" + lastSlot);
        mother = sim.State.Creatures[0]; father = sim.State.Creatures[1]; pregnancy = mother.Family.Pregnancy;
        var deliver = typeof(WorldSimulation).GetMethod("TryCreateFamilyChildren", BindingFlags.NonPublic | BindingFlags.Instance)!;
        string before = Json(sim);
        for (int attempt = 0; attempt < 2; attempt++)
        {
            bool delivered = (bool)deliver.Invoke(sim, new object?[]
                { mother, father, pregnancy, mother.RegionId, mother.X, mother.Z, null, null })!;
            Need(!delivered, "late archived collision was delivered");
            Need(Json(sim) == before, "failed delivery partially changed world, RNG, activities or birth ledger");
        }
        Tick(sim, 280);
        Need(!sim.State.CompletedBirths.Contains(pregnancy.EventId!), "blocked birth was settled");
        Need(!sim.State.Creatures.Any(c => c.Family.BirthEventId == pregnancy.EventId), "partial litter created");
        Need(!sim.Select(id), "late archive became selectable");
        Unique(sim); DiskTwice(sim, "late-after-" + lastSlot);
    });
Console.WriteLine($"PASSES {passes}; FAILURES {failures}");
return failures == 0 ? 0 : 1;
