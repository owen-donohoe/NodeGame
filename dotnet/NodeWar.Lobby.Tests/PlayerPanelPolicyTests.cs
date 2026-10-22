using System.Collections.Generic;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class PlayerPanelPolicyTests
    {
        [TestCase(GuildRole.Member, false)]
        [TestCase(GuildRole.CoLeader, true)]
        [TestCase(GuildRole.Leader, true)]
        public void ModerationRequiresOfficerAndMembership(GuildRole role, bool allowed)
        {
            var member = new GuildMember { Player = new PlayerCard { PlayerId = "target" } };
            var guild = new GuildView { Summary = new GuildSummary { GuildId = "mine" }, OwnRole = role, Members = new List<GuildMember> { member } };
            Assert.AreEqual(allowed, PlayerPanelPolicy.ModerationTarget(guild, "target", "self") != null);
            Assert.IsNull(PlayerPanelPolicy.ModerationTarget(guild, "outsider", "self"));
            Assert.IsNull(PlayerPanelPolicy.ModerationTarget(guild, "target", "target"));
            Assert.IsNull(PlayerPanelPolicy.ModerationTarget(null, "target", "self"));
        }
        [Test]
        public void HistoryUsesNewestTenWithoutMutatingServiceResult()
        {
            var input = new List<MatchSummary> { null };
            for (int i = 0; i < 12; i++) input.Add(new MatchSummary { MatchId = i.ToString("00"), CreatedUnix = i });
            var result = PlayerPanelPolicy.LastTen(input);
            Assert.AreEqual(10, result.Count);
            Assert.AreEqual("11", result[0].MatchId);
            Assert.AreEqual("02", result[9].MatchId);
            Assert.IsNull(input[0]); Assert.AreEqual(13, input.Count);
        }
        [Test]
        public void EqualHistoryTimesHaveStableIdOrder()
        {
            var result = PlayerPanelPolicy.LastTen(new List<MatchSummary> { new MatchSummary { MatchId = "b" }, new MatchSummary { MatchId = "a" } });
            Assert.AreEqual("a", result[0].MatchId);
            Assert.IsEmpty(PlayerPanelPolicy.LastTen(null));
        }
    }
}
