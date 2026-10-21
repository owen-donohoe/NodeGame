using NUnit.Framework;
using NodeWar.Backend;
namespace NodeWar.Lobby.Tests
{
    public class TeammateInviteTests
    {
        [TestCase(PlayerPresence.Online, "In lobby", "online")]
        [TestCase(PlayerPresence.InBattle, "In battle", "battle")]
        [TestCase(PlayerPresence.Offline, "Offline", "offline")]
        public void PresenceMatchesProduct(PlayerPresence p, string label, string css)
        {
            Assert.That(TeammateInviteRoster.PresenceLabel(p), Is.EqualTo(label));
            Assert.That(TeammateInviteRoster.PresenceClass(p), Is.EqualTo(css));
        }
        [Test] public void OrderingIsStableAndFiltersSelfDuplicatesAndMissingCards()
        {
            var a = new PlayerCard { PlayerId = "a", Name = "Same", Presence = PlayerPresence.Online };
            var b = new PlayerCard { PlayerId = "b", Name = "Same", Presence = PlayerPresence.Online };
            var battle = new PlayerCard { PlayerId = "battle", Name = "A", Presence = PlayerPresence.InBattle };
            var offline = new PlayerCard { PlayerId = "offline", Name = "A", Presence = PlayerPresence.Offline };
            var result = TeammateInviteRoster.Ordered(new[] { offline, b, null, battle, a, b, new PlayerCard { PlayerId = "self" } }, "self");
            Assert.That(result, Is.EqualTo(new[] { a, b, battle, offline }));
        }
        [Test] public void HandleCapsAtSixteenPlusEllipsis()
        {
            Assert.That(TeammateInviteRoster.Handle(new PlayerCard { Name = "abcdefghijklmnopq", Tag = "1234" }), Is.EqualTo("abcdefghijklmnop\u2026"));
            Assert.That(TeammateInviteRoster.Handle(new PlayerCard { Name = "Ash", Tag = "1234" }), Is.EqualTo("Ash#1234"));
        }
        [Test] public void GuildBattleWinsOtherwiseMembershipOnlineFlagMapsPresence()
        {
            var member = new GuildMember { Player = new PlayerCard { Presence = PlayerPresence.InBattle }, Online = false };
            Assert.That(TeammateInviteRoster.Presence(member), Is.EqualTo(PlayerPresence.InBattle));
            member.Player.Presence = PlayerPresence.Offline; member.Online = true;
            Assert.That(TeammateInviteRoster.Presence(member), Is.EqualTo(PlayerPresence.Online));
            member.Online = false;
            Assert.That(TeammateInviteRoster.Presence(member), Is.EqualTo(PlayerPresence.Offline));
        }
    }
}
