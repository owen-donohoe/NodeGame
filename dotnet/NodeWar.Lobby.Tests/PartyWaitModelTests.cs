using NodeWar.Lobby;
using NUnit.Framework;

public class PartyWaitModelTests
{
    [TestCase(0, false)] [TestCase(14999, false)] [TestCase(15000, true)] [TestCase(15001, true)]
    public void TeamRandomQueueAppearsAfterFifteenSeconds(long elapsed, bool visible)
    {
        var model = new PartyWaitModel(LobbyMode.Team2v2);
        model.Advance(elapsed);
        Assert.AreEqual(visible, model.CanQueueRandom);
        Assert.IsFalse(model.BattleReady);
    }

    [Test]
    public void FourSlotsMustFillAndPrivateRandomQueueDoesNotAppear()
    {
        var model = new PartyWaitModel(LobbyMode.FreeForAll4);
        Assert.AreEqual(1, model.Filled);
        Assert.AreEqual(4, model.Capacity);
        model.SetFilled(3);
        Assert.IsFalse(model.BattleReady);
        model.SetFilled(20);
        Assert.AreEqual(4, model.Filled);
        Assert.IsTrue(model.BattleReady);
        model.Advance(long.MaxValue);
        Assert.IsFalse(model.CanQueueRandom);
        model.SetFilled(-1);
        Assert.AreEqual(1, model.Filled);
    }

    [Test]
    public void CancelIsTerminalAndTimeCannotRunBackwards()
    {
        var model = new PartyWaitModel(LobbyMode.Team2v2);
        model.Advance(-10);
        Assert.AreEqual(0, model.ElapsedMilliseconds);
        model.Advance(16000);
        model.Advance(100);
        Assert.AreEqual(16000, model.ElapsedMilliseconds);
        model.Cancel();
        model.Advance(20000);
        model.SetFilled(2);
        Assert.IsTrue(model.Cancelled);
        Assert.IsFalse(model.CanQueueRandom);
        Assert.AreEqual(1, model.Filled);
        Assert.AreEqual(16000, model.ElapsedMilliseconds);
    }
}
