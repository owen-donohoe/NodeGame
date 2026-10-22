using NodeWar.Backend;
using NodeWar.Lobby;
using NUnit.Framework;

public class RecentOpponentListTests
{
    [Test]
    public void HandlesUseNewestSnapshotAndFallBackToId()
    {
        CollectionAssert.AreEqual(new[] { "First#TAG", "fallback", "Named" }, RecentOpponentList.HandlesFrom(new[]
        {
            new MatchHistoryEntry { opponentPlayerId = "p1", OpponentName = "First", OpponentTag = "TAG" },
            new MatchHistoryEntry { opponentPlayerId = "p1", OpponentName = "Older", OpponentTag = "OLD" },
            new MatchHistoryEntry { opponentPlayerId = "fallback", OpponentName = "", OpponentTag = "" },
            new MatchHistoryEntry { opponentPlayerId = "p3", OpponentName = "Named" }
        }));
        Assert.IsEmpty(RecentOpponentList.HandlesFrom(null));
    }

    [Test]
    public void PreservesRecentOrderAndOmitsMissingOrDuplicateOpponents()
    {
        CollectionAssert.AreEqual(new[] { "recent", "older" }, RecentOpponentList.From(new[]
        {
            new MatchHistoryEntry { opponentPlayerId = "recent" }, null,
            new MatchHistoryEntry { opponentPlayerId = "older" },
            new MatchHistoryEntry { opponentPlayerId = "recent" }, new MatchHistoryEntry()
        }));
        Assert.IsEmpty(RecentOpponentList.From(null));
    }
}
