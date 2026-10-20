using System;
using System.IO;
using NodeWar.Backend;
using NodeWar.Lobby;
using NUnit.Framework;

public class WorkshopRegressionTests
{
    private static string Source(string path)
    {
        var root = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (root != null && !Directory.Exists(Path.Combine(root.FullName, "Assets"))) root = root.Parent;
        return File.ReadAllText(Path.Combine(root.FullName, path));
    }

    [TestCase(1)] [TestCase(2)]
    public void FreshLoadoutFillsAllDistrictsAndReplacesEachSuitThenSavesComplete(int arena)
    {
        var model = new LoadoutScreenModel(LoadoutData.CreateEmpty(arena));
        var profile = new PlayerProfileData();
        string[] districts = arena == 1 ? new[] { "node_barracks", "node_forge" } :
            new[] { "node_farm", "node_mine", "node_village", "node_barracks", "node_forge" };
        string[] suits = arena == 1 ? new[] { "suit_guardian", "suit_scout" } :
            new[] { "suit_warrior", "suit_guardian", "suit_scout" };
        Assert.IsFalse(LoadoutRules.IsComplete(model.ToData(), arena));
        foreach (string id in districts)
        {
            Assert.IsTrue(LoadoutRules.IsDistrictOffered(id, arena));
            model.Inspect(id, false);
            Assert.IsTrue(model.Use(true, false));
            LoadoutPresets.Store(ref profile, model.ToData());
        }
        // Front-middle is stage index 1, even when villagers stand in front of it.
        model.Inspect("node_workshop", false);
        Assert.IsFalse(model.Use(true, false));
        Assert.IsTrue(model.Replacing);
        var cluster = new DistrictCluster(districts.Length);
        cluster.Use(false);
        var pick = new StagePick(false, 1);
        Assert.IsTrue(pick.CanPick(true, false));
        Assert.IsFalse(new StagePick(true, 0).CanPick(true, false));
        var result = cluster.Pick(pick.Index);
        Assert.IsTrue(model.CompleteReplace(StageSlotMap.ToChosen(model.ToData().districtIDs, result.Slot), true, false));
        Assert.AreEqual("node_workshop", model.ToData().districtIDs[1]);
        foreach (string id in suits) { model.Inspect(id, true); Assert.IsTrue(model.Use(true, false)); }
        for (int slot = 0; slot < suits.Length; slot++)
        {
            string replacement = slot % 2 == 0 ? "suit_berserker" : "suit_medic";
            // Reuse the suit just removed when replacing the third slot.
            if (slot == 2) replacement = "suit_warrior";
            var before = model.ToData();
            model.Inspect(replacement, true);
            Assert.IsFalse(model.Use(true, false));
            Assert.IsTrue(model.ReplacingSuit);
            var hit = new StagePick(true, StageSlotMap.ToStage(before.suitIDs, slot));
            Assert.IsTrue(hit.CanPick(true, true));
            Assert.IsTrue(model.CompleteSuitReplace(StageSlotMap.ToChosen(before.suitIDs, hit.Index), true, false));
            Assert.IsFalse(model.Replacing);
            var after = model.ToData();
            for (int i = 0; i < suits.Length; i++) Assert.AreEqual(i == slot ? replacement : before.suitIDs[i], after.suitIDs[i]);
            LoadoutPresets.Store(ref profile, after);
            model = new LoadoutScreenModel(profile.loadoutPresets[profile.activeLoadoutPreset].ToLoadout());
            CollectionAssert.AreEqual(after.suitIDs, model.ToData().suitIDs);
        }
        Assert.IsTrue(LoadoutRules.IsComplete(profile.loadout.ToLoadout(), arena), "HomePage BATTLE predicate");
        CollectionAssert.AreEqual(StageLoadoutTypes.Suits(model.ToData().suitIDs), StageLoadoutTypes.Suits(profile.loadout.ToLoadout().suitIDs));
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)]
    public void VillagerWalkingLanesStayDisjointEvenAtTheWidestPop(int count)
    {
        float widest = 0.7f / 0.78f;
        for (int i = 0; i < count; i++)
        {
            Assert.That(SuitReplacementMotion.LaneX(i, count), Is.InRange(-1f, 1f));
            if (i + 1 < count)
                Assert.That(SuitReplacementMotion.WanderX(i + 1, count, -1) - SuitReplacementMotion.WanderX(i, count, 1), Is.GreaterThan(widest));
        }
    }

    [Test]
    public void EmptyPlotsOutsideTheChosenDeckCannotConsumeAReplacement()
    {
        var cluster = new DistrictCluster(2);
        cluster.Use(false);
        Assert.AreEqual(DistrictClusterAction.None, cluster.Pick(4).Action);
        Assert.IsTrue(cluster.Shaking);
        Assert.AreEqual(DistrictClusterAction.Replaced, cluster.Pick(1).Action);
    }

    [Test]
    public void FixedGrantsAreNotDuplicatedAndHaveNoChosenReplacementIndex()
    {
        var ids = new[] { "suit_guardian", "suit_scout" };
        var chosen = StageLoadoutTypes.Suits(ids);
        var granted = StageLoadoutTypes.Suits(LoadoutRules.LockedSuits(1));
        CollectionAssert.AreEqual(new[] { 5, 6, 3 }, StageLoadoutTypes.WithGranted(chosen, granted));
        Assert.AreEqual(-1, StageSlotMap.ToChosen(ids, chosen.Length));
        Assert.AreEqual("suit_warrior", StageSlotMap.IdAt(ids, LoadoutRules.LockedSuits(1), chosen.Length));
        CollectionAssert.AreEqual(new[] { 5, 6, 3 }, StageLoadoutTypes.WithGranted(chosen, granted, granted));
    }

    [TestCase(false)] [TestCase(true)]
    public void StageInspectionDoesNotChangeTheBrowserTabOrTheItemKind(bool browsingSuits)
    {
        var model = new LoadoutScreenModel(LoadoutData.CreateEmpty(2));
        model.Browse(browsingSuits);
        model.Inspect(browsingSuits ? "node_barracks" : "suit_warrior", !browsingSuits);
        Assert.AreEqual(browsingSuits, model.BrowsingSuits);
        Assert.AreEqual(!browsingSuits, model.PreviewIsSuit);
        Assert.IsTrue(model.Use(true, false));
        Assert.AreEqual(model.PreviewId, browsingSuits ? model.ToData().districtIDs[0] : model.ToData().suitIDs[0]);
    }

    [Test]
    public void LockedPreviewOpensWithoutEquippingAndEmptyCosmeticsDoNotBlockBaseUse()
    {
        var model = new LoadoutScreenModel(LoadoutData.CreateEmpty(2));
        model.Inspect("node_fortress", false);
        Assert.AreEqual("node_fortress", model.PreviewId);
        Assert.IsFalse(model.Use(false, false));
        Assert.IsFalse(model.Replacing);
        Assert.IsTrue(LoadoutScreenModel.ValidChoice(Array.Empty<LoadoutChoice>(), null));
        var locked = new LoadoutChoice("locked", "node.fortress", "Locked", 0,
            new UnlockState(UnlockStatus.CannotUnlock, 4, false), false);
        Assert.IsFalse(LoadoutScreenModel.ValidChoice(new[] { locked }, locked.ID));
        Assert.IsFalse(LoadoutScreenModel.NeedsEquipment(locked));
        string page = Source("Assets/UI/Scripts/WorkshopPage.cs");
        StringAssert.Contains("tile.Card = new Button(() => PreviewItem(id));", page);
        StringAssert.Contains("tile.Card.pickingMode = PickingMode.Position;", page);
        StringAssert.Contains("tile.Card.SetEnabled(true);", page);
    }
}
