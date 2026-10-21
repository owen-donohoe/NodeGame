using System;
using NodeWar.Backend;
using NodeWar.Lobby;
using NUnit.Framework;

public class WorkshopRegressionTests
{
    [TestCase(0)] [TestCase(1)] [TestCase(2)]
    public void FreshLoadoutAddsChoicesAndSavesComplete(int arena)
    {
        var model = new LoadoutScreenModel(LoadoutData.CreateEmpty(arena), arena);
        foreach (string id in new[] { "node_barracks", "node_village" })
        {
            model.Inspect(id, false); Assert.IsTrue(model.Use(true, false));
        }
        foreach (string id in new[] { "suit_guardian", "suit_scout" })
        {
            model.Inspect(id, true); Assert.IsTrue(model.Use(true, false));
        }
        var profile = new PlayerProfileData();
        LoadoutPresets.Store(ref profile, model.ToData());
        Assert.IsTrue(LoadoutRules.IsComplete(profile.loadout.ToLoadout(), arena));
        CollectionAssert.AreEqual(model.ToData().suitIDs, profile.loadout.ToLoadout().suitIDs);
        CollectionAssert.AreEqual(model.ToData().districtIDs, profile.loadout.ToLoadout().districtIDs);
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
        var granted = StageLoadoutTypes.Suits(LoadoutRules.LockedSuits(0));
        CollectionAssert.AreEqual(new[] { 5, 6, 3 }, StageLoadoutTypes.WithGranted(chosen, granted));
        Assert.AreEqual(-1, StageSlotMap.ToChosen(ids, chosen.Length));
        Assert.AreEqual("suit_warrior", StageSlotMap.IdAt(ids, LoadoutRules.LockedSuits(0), chosen.Length));
        CollectionAssert.AreEqual(new[] { 5, 6, 3 }, StageLoadoutTypes.WithGranted(chosen, granted, granted));
    }

    [TestCase(false)] [TestCase(true)]
    public void StageInspectionDoesNotChangeTheBrowserTabOrTheItemKind(bool browsingSuits)
    {
        var model = new LoadoutScreenModel(new LoadoutData { suitIDs = new string[3], districtIDs = new string[5] }, 2);
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
        var model = new LoadoutScreenModel(new LoadoutData { suitIDs = new string[3], districtIDs = new string[5] }, 2);
        model.Inspect("node_fortress", false);
        Assert.AreEqual("node_fortress", model.PreviewId);
        Assert.IsFalse(model.Use(false, false));
        Assert.IsFalse(model.Replacing);
        Assert.IsTrue(LoadoutScreenModel.ValidChoice(Array.Empty<LoadoutChoice>(), null));
        var locked = new LoadoutChoice("locked", "node.fortress", "Locked", 0,
            new UnlockState(UnlockStatus.CannotUnlock, 4, false), false);
        Assert.IsFalse(LoadoutScreenModel.ValidChoice(new[] { locked }, locked.ID));
        Assert.IsFalse(LoadoutScreenModel.NeedsEquipment(locked));
    }
}
