using NodeWar.Backend;
using NodeWar.Lobby;
using NUnit.Framework;

public class LocalCrateInventoryTests
{
    [Test]
    public void OpeningSmallKeepsStockAndPublishesFixedRewards()
    {
        int held = 2;
        int saves = 0;
        var inventory = new LocalCrateInventory(() => held, () => 0.25f, count => { held = count; saves++; });
        int changes = 0;
        inventory.Changed += () => changes++;
        Assert.AreEqual(CrateSize.Small, inventory.NextSize);
        Assert.AreEqual(0, inventory.Count(CrateSize.Medium));
        Assert.IsTrue(inventory.TryOpen(CrateSize.Small, out var first));
        Assert.AreEqual(2, held);
        Assert.AreEqual(0, saves);
        Assert.AreEqual(0, changes);
        Assert.AreEqual(2, first.Count);
        Assert.AreEqual(CrateRewardKind.Coins, first[0].Kind);
        Assert.Greater(first[0].Amount, 0);
        Assert.AreEqual(CrateRewardKind.Premium, first[1].Kind);
        Assert.IsTrue(inventory.TryOpen(CrateSize.Small, out var second));
        CollectionAssert.AreEqual(first, second);
        held = 0;
        Assert.IsFalse(inventory.TryOpen(CrateSize.Small, out var empty));
        Assert.IsEmpty(empty);
        Assert.AreEqual(0, saves);
    }

    [TestCase(CrateSize.Medium)] [TestCase((CrateSize)999)]
    public void UnsupportedCratesDoNotConsumeSmall(CrateSize size)
    {
        int held = 3;
        var inventory = new LocalCrateInventory(() => held, () => 0f, count => held = count);
        Assert.IsFalse(inventory.TryOpen(size, out var rewards));
        Assert.IsEmpty(rewards);
        Assert.AreEqual(3, held);
    }

    [TestCase(-1f, 0f)] [TestCase(0f, 0f)] [TestCase(0.5f, 0.5f)] [TestCase(2f, 1f)]
    [TestCase(float.NaN, 0f)] [TestCase(float.PositiveInfinity, 1f)]
    public void SavedProgressIsBounded(float saved, float expected)
    {
        var inventory = new LocalCrateInventory(() => -3, () => saved, _ => Assert.Fail("Must not save"));
        Assert.AreEqual(0, inventory.Count(CrateSize.Small));
        Assert.AreEqual(expected, inventory.NextProgress);
        Assert.IsFalse(inventory.TryOpen(CrateSize.Small, out _));
    }

    [Test]
    public void ExternalProfileChangesNotifyOnceAndAdvanceParentBadges()
    {
        int held = 1;
        float progress = 0f;
        var inventory = new LocalCrateInventory(() => held, () => progress, count => held = count);
        var badges = new BadgeBoard();
        int changes = 0;
        inventory.Changed += () => { changes++; badges.Set("crates", inventory.Count(CrateSize.Small) + inventory.Count(CrateSize.Medium)); };
        held = 4;
        inventory.Refresh();
        inventory.Refresh();
        Assert.AreEqual(4, badges.Count("crates"));
        progress = 0.5f;
        inventory.Refresh();
        Assert.AreEqual(2, changes);
    }
}
