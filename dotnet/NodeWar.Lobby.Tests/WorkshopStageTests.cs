using NodeWar.Lobby;
using NUnit.Framework;

public class WorkshopStageTests
{

    [TestCase(false)] [TestCase(true)]
    public void LockedPickCannotEndReplacement(bool suit)
    {
        var editor = new LoadoutEditor(LoadoutData.CreateEmpty(0), 0);
        editor.EquipSuit("suit_guardian"); editor.EquipSuit("suit_scout");
        editor.EquipDistrict("node_barracks"); editor.EquipDistrict("node_village");
        var model = new LoadoutScreenModel(editor.ToLoadout(), 0);
        model.Inspect(suit ? "suit_medic" : "node_forge", suit);
        Assert.IsFalse(model.Use(true, false));
        Assert.IsTrue(model.Replacing);
        Assert.IsFalse(suit ? model.CompleteSuitReplace(0, true, false) : model.CompleteReplace(0, true, false));
        Assert.IsTrue(model.Replacing);
        Assert.IsTrue(suit ? model.CompleteSuitReplace(1, true, false) : model.CompleteReplace(3, true, false));
    }

    [Test]
    public void ShortStageUsesFrontSlotsAndKeepsChosenStorageMapping()
    {
        string[] chosen = { "", "node_town" };
        var grants = LoadoutRules.LockedDistricts(1);
        CollectionAssert.AreEqual(new[] { "node_town", "node_farm", "node_mine" }, StageSlotMap.WithGranted(chosen, grants));
        Assert.AreEqual(1, StageSlotMap.ToChosen(chosen, 0));
        Assert.AreEqual(-1, StageSlotMap.ToChosen(chosen, 1));
        Assert.AreEqual("node_farm", StageSlotMap.IdAt(chosen, grants, 1));
        Assert.AreEqual("", StageSlotMap.IdAt(chosen, grants, 4));
        CollectionAssert.AreEqual(grants, StageSlotMap.WithGranted(new string[2], grants));
    }

    private static LoadoutScreenModel Full(int arena)
    {
        var model = new LoadoutScreenModel(new LoadoutData { suitIDs = new string[3], districtIDs = new string[5] }, 2);
        int capacity = model.ToData().districtIDs.Length;
        for (int i = 0; i < capacity; i++) { model.Preview("district" + i); model.UsePreview(true, false); }
        model.Preview("replacement");
        return model;
    }

    [TestCase(1)] [TestCase(2)]
    public void ReplacesThePickedSlotWithoutChangingCapacityOrOtherChoices(int arena)
    {
        var model = Full(arena);
        var before = model.ToData();
        Assert.IsTrue(model.BeginReplace(true, false));
        Assert.IsTrue(model.Replacing);
        Assert.IsTrue(model.CompleteReplace(1, true, false));
        var after = model.ToData();
        Assert.AreEqual(before.districtIDs.Length, after.districtIDs.Length);
        Assert.AreEqual("district0", after.districtIDs[0]);
        Assert.AreEqual("replacement", after.districtIDs[1]);
        Assert.AreEqual("district1", before.districtIDs[1]);
        CollectionAssert.AreEqual(before.suitIDs, after.suitIDs);
        CollectionAssert.AreEqual(before.districtEras, after.districtEras);
        CollectionAssert.AreEqual(before.skinIDs, after.skinIDs);
        Assert.IsFalse(model.Replacing);
    }

    [TestCase(false, false)] [TestCase(true, true)]
    public void EligibilityAndBusyGatesAreRecheckedAtThePickedSlot(bool canUse, bool busy)
    {
        var model = Full(2);
        Assert.IsTrue(model.BeginReplace(true, false));
        Assert.IsFalse(model.CompleteReplace(0, canUse, busy));
        Assert.AreEqual("district0", model.ToData().districtIDs[0]);
        Assert.IsFalse(model.Replacing);
        Assert.IsFalse(model.BeginReplace(canUse, busy));
    }

    [Test]
    public void CancelOrAChangedPreviewCannotEquipThePendingDistrict()
    {
        var model = Full(2);
        model.BeginReplace(true, false);
        model.CancelReplace();
        Assert.IsFalse(model.CompleteReplace(0, true, false));
        model.BeginReplace(true, false);
        model.Preview("another");
        Assert.IsFalse(model.Replacing);
        Assert.IsFalse(model.CompleteReplace(0, true, false));
        Assert.AreEqual("district0", model.ToData().districtIDs[0]);
    }

    [Test]
    public void EmptySlotsStillUseFirstEmptyAndInvalidOrDuplicateReplacementIsRefused()
    {
        var model = new LoadoutScreenModel(new LoadoutData { suitIDs = new string[3], districtIDs = new string[5] }, 2);
        model.Preview("new");
        Assert.IsFalse(model.BeginReplace(true, false));
        Assert.IsTrue(model.UsePreview(true, false));
        Assert.AreEqual("new", model.ToData().districtIDs[0]);
        var editor = new LoadoutEditor(model.ToData(), 2);
        Assert.IsFalse(editor.ReplaceDistrictSlot(4, "other"));
        Assert.IsFalse(editor.ReplaceDistrictSlot(-1, "other"));
        Assert.IsFalse(editor.ReplaceDistrictSlot(0, "new"));
        Assert.IsFalse(editor.ReplaceDistrictSlot(0, ""));
    }

    [TestCase(2, 2, false)] [TestCase(2, 5, true)] [TestCase(5, 5, false)]
    [TestCase(0, 0, false)] [TestCase(-1, 2, true)]
    public void StageRespectsChosenCapacity(int equipped, int capacity, bool hasEmpty)
    {
        Assert.AreEqual(hasEmpty, StageSlots.HasEmpty(equipped, capacity));
    }

    [Test]
    public void CompactStageSlotsMapBackToTheOriginalChosenIndex()
    {
        string[] ids = { "", "farm", null, "bank", "forge" };
        Assert.AreEqual(0, StageSlotMap.ToStage(ids, 1));
        Assert.AreEqual(1, StageSlotMap.ToStage(ids, 3));
        Assert.AreEqual(3, StageSlotMap.ToChosen(ids, 1));
        Assert.AreEqual(-1, StageSlotMap.ToStage(ids, 0));
        Assert.AreEqual(-1, StageSlotMap.ToStage(ids, 6));
        Assert.AreEqual(-1, StageSlotMap.ToChosen(ids, 4));
        Assert.AreEqual(-1, StageSlotMap.ToChosen(null, 0));
    }

    [Test]
    public void ReplacementBackHandlerCancelsWithoutClosingTheUnderlyingPanel()
    {
        var router = new LobbyOverlayRouter();
        var model = Full(2);
        bool closed = false;
        router.Open(new LobbyOverlay("panel", OverlayKind.Panel), () => closed = true);
        model.BeginReplace(true, false);
        router.Open(new LobbyOverlay("replacing", OverlayKind.Menu, backHandler: () =>
        {
            model.CancelReplace(); router.Closed("replacing"); return true;
        }), model.CancelReplace);
        router.Close(router.Stack.Top.ID);
        Assert.IsFalse(model.Replacing);
        Assert.IsFalse(closed);
        Assert.AreEqual("panel", router.Stack.Top.ID);
    }
}
