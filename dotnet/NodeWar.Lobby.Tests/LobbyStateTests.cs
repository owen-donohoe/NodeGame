using NodeWar.Lobby;
using NUnit.Framework;

public class LobbyStateTests
{
    [Test]
    public void BadgesAggregateDottedDescendantsAndNotifyOnlyChanges()
    {
        var board = new BadgeBoard();
        int changes = 0;
        board.Changed += () => changes++;
        board.Set("shop.daily", 2);
        board.Set("shop.weekly.skin", 3);
        board.Set("shopping", 9);
        board.Set("shop.daily", 2);
        Assert.AreEqual(5, board.Count("shop"));
        Assert.AreEqual(3, board.Count("shop.weekly"));
        Assert.AreEqual(2, board.Count("shop.daily"));
        Assert.AreEqual(0, board.Count("absent"));
        board.Clear("shop.daily");
        board.Clear("missing");
        Assert.AreEqual(3, board.Count("shop"));
        board.Set("shop.weekly.skin", -1);
        Assert.AreEqual(0, board.Count("shop"));
        Assert.AreEqual(5, changes);
        board.Set(null, 1);
        board.Clear(null);
        Assert.AreEqual(0, board.Count(null));
    }

    [Test]
    public void BadgeTotalsSaturateWithoutOverflow()
    {
        var board = new BadgeBoard();
        board.Set("shop.a", int.MaxValue);
        board.Set("shop.b", 1);
        Assert.AreEqual(int.MaxValue, board.Count("shop"));
    }

    [TestCase(99, false)] [TestCase(100, false)] [TestCase(399, false)] [TestCase(400, true)]
    public void LoadingWaitsThreeHundredMilliseconds(long now, bool visible)
    {
        Assert.AreEqual(visible, new AsyncState(100).ShowLoading(now));
    }

    [TestCase(AsyncResult.Error, AsyncStatus.Error)]
    [TestCase(AsyncResult.Offline, AsyncStatus.Offline)]
    [TestCase(AsyncResult.Empty, AsyncStatus.Empty)]
    public void InitialResultsAndRetry(AsyncResult result, AsyncStatus expected)
    {
        var state = new AsyncState().Reduce(result, 500);
        Assert.AreEqual(expected, state.Status);
        Assert.IsFalse(state.HasContent);
        Assert.IsFalse(state.ShowBanner);
        state = state.Reduce(AsyncResult.Retry, 600);
        Assert.AreEqual(AsyncStatus.Loading, state.Status);
        Assert.IsFalse(state.ShowLoading(899));
        Assert.IsTrue(state.ShowLoading(900));
    }

    [TestCase(AsyncResult.Error, false)] [TestCase(AsyncResult.Offline, true)]
    public void FailedRefreshKeepsContentAndSuccessfulRefreshClearsBanner(AsyncResult failure, bool offline)
    {
        var state = new AsyncState().Reduce(AsyncResult.Ready, 50).Reduce(AsyncResult.Loading, 100);
        Assert.IsTrue(state.HasContent);
        state = state.Reduce(failure, 500);
        Assert.AreEqual(AsyncStatus.Ready, state.Status);
        Assert.IsTrue(state.HasContent);
        Assert.IsTrue(state.ShowBanner);
        Assert.AreEqual(offline, state.IsOffline);
        state = state.Reduce(AsyncResult.Retry, 600);
        Assert.AreEqual(AsyncStatus.Loading, state.Status);
        Assert.IsTrue(state.HasContent);
        state = state.Reduce(AsyncResult.Ready, 650);
        Assert.IsFalse(state.ShowBanner);
        Assert.IsFalse(state.IsOffline);
        Assert.IsFalse(state.ShowLoading(1000));
    }
}
