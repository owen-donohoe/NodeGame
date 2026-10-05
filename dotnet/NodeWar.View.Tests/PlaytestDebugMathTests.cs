using NUnit.Framework;
using NodeWar.Debugging;
using NodeWar.Simulation;

public class PlaytestDebugMathTests
{
    [TestCase(true, false, true, true, false, true)]
    [TestCase(false, false, true, true, false, false)]
    [TestCase(true, true, true, true, false, false)]
    [TestCase(true, false, false, true, false, false)]
    [TestCase(true, false, true, false, false, false)]
    [TestCase(true, false, true, true, true, false)]
    public void ControlsOnlyRunInDevelopmentLocalActiveMatches(bool development, bool networked, bool localRunner,
        bool playing, bool gameOver, bool expected)
    {
        Assert.AreEqual(expected, PlaytestDebugMath.Allowed(development, networked, localRunner, playing, gameOver));
    }

    [TestCase(0)]
    [TestCase(2340)]
    [TestCase(2500)]
    public void CountdownIsFiftyTicksFromNowWithoutMutatingSource(int tick)
    {
        var source = GameBalanceData.Default();
        int originalHash = BalanceHasher.Hash(source);
        Assert.IsTrue(PlaytestDebugMath.TrySuddenDeathInFiveSeconds(source, tick, out var changed));
        Assert.AreEqual(tick + 50, changed.suddenDeathTicks[0]);
        Assert.AreEqual(1, changed.suddenDeathThresholds[0]);
        Assert.IsTrue(changed.SuddenDeathValid());
        Assert.AreEqual(2400, source.suddenDeathTicks[0]);
        Assert.AreEqual(originalHash, BalanceHasher.Hash(source));
        Assert.AreNotSame(source.suddenDeathTicks, changed.suddenDeathTicks);
        Assert.AreNotSame(source.suddenDeathThresholds, changed.suddenDeathThresholds);
        Assert.AreNotEqual(originalHash, BalanceHasher.Hash(changed));
        int next;
        Assert.AreEqual(5, NodeWar.View.BreachTempoMath.SuddenDeathCountdown(changed.suddenDeathTicks,
            changed.suddenDeathThresholds, tick, 10, out next));
        Assert.AreEqual(1, next);
    }

    [Test]
    public void MultipleDropsKeepTheirSpacing()
    {
        var source = GameBalanceData.Default();
        source.suddenDeathTicks = new[] { 2400, 3000 };
        source.suddenDeathThresholds = new[] { 2, 1 };
        Assert.IsTrue(PlaytestDebugMath.TrySuddenDeathInFiveSeconds(source, 100, out var changed));
        CollectionAssert.AreEqual(new[] { 150, 750 }, changed.suddenDeathTicks);
        CollectionAssert.AreEqual(new[] { 2400, 3000 }, source.suddenDeathTicks);
    }

    [TestCase(-1)]
    [TestCase(int.MaxValue)]
    public void InvalidOrOverflowingTickIsRejected(int tick)
    {
        Assert.IsFalse(PlaytestDebugMath.TrySuddenDeathInFiveSeconds(GameBalanceData.Default(), tick, out _));
    }

    [Test]
    public void DisabledOrInvalidBreachScheduleIsRejected()
    {
        var source = GameBalanceData.Default();
        source.breachBarMax = 0;
        Assert.IsFalse(PlaytestDebugMath.TrySuddenDeathInFiveSeconds(source, 0, out _));
        source = GameBalanceData.Default();
        source.suddenDeathTicks = null;
        Assert.IsFalse(PlaytestDebugMath.TrySuddenDeathInFiveSeconds(source, 0, out _));
    }
}
