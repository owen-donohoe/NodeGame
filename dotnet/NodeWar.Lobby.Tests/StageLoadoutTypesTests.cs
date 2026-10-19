using NodeWar.Lobby;
using NUnit.Framework;

public class StageLoadoutTypesTests
{
    [Test]
    public void MapsChosenIdsInOrderAndLeavesEmptyPlotsForMissingEntries()
    {
        CollectionAssert.AreEqual(new[] { 18, 1, 19 }, StageLoadoutTypes.Districts(
            new[] { "node_storehouse", "", "node_farm", null, "node_workshop" }));
        CollectionAssert.AreEqual(new[] { 3, 5 }, StageLoadoutTypes.Suits(new[] { "suit_warrior", "SUIT_GUARDIAN" }));
    }

    [Test]
    public void MalformedAndAbsentIdsDoNotCreateScenery()
    {
        Assert.IsEmpty(StageLoadoutTypes.Districts(null));
        Assert.IsEmpty(StageLoadoutTypes.Suits(new[] { "suit_999", "node_farm", "garbage", null }));
        Assert.IsEmpty(StageLoadoutTypes.Districts(new[] { "node_999", "suit_warrior" }));
    }

    [Test]
    public void ComparesSnapshotsByContent()
    {
        Assert.IsTrue(StageLoadoutTypes.Same<string>(null, null));
        Assert.IsFalse(StageLoadoutTypes.Same(null, new[] { "a" }));
        Assert.IsFalse(StageLoadoutTypes.Same(new[] { "a" }, new[] { "a", "b" }));
        Assert.IsFalse(StageLoadoutTypes.Same(new[] { "a" }, new[] { "b" }));
        Assert.IsTrue(StageLoadoutTypes.Same(new[] { 1, 2 }, new[] { 1, 2 }));
    }
}
