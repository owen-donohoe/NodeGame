using NodeWar.Backend;
using NodeWar.Lobby;
using NUnit.Framework;

public class RecentOpponentListTests
{
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
