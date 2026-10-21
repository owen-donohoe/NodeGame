using NUnit.Framework;
namespace NodeWar.Lobby.Tests
{
    public class LanRoomTests
    {
        [TestCase(0, LobbyMode.Unranked1v1, 2)]
        [TestCase(1, LobbyMode.Team2v2, 4)]
        [TestCase(2, LobbyMode.FreeForAll4, 4)]
        public void LanUsesPrivateSeatPositionsAndColours(int option, LobbyMode mode, int count)
        {
            var state = new LanRoomState(); state.Host(); state.SelectMode(option);
            var room = new PrivateLobbyModel(state.Mode, "host", "Host");
            Assert.That(state.Mode, Is.EqualTo(mode)); Assert.That(room.Capacity, Is.EqualTo(count));
            for (int i = 0; i < count; i++)
            {
                Assert.That(room.Position(i), Is.EqualTo(ModeDefinition.For(mode).Position(i)));
                Assert.That(room.Colour(i), Is.EqualTo(ModeDefinition.For(mode).Team(i)));
                if (i > 0) Assert.That(room.Player(i), Is.Null);
            }
            Assert.That(state.PlaceholderPlay, Is.EqualTo(count == 4));
        }
        [Test] public void ModeSwitchPreservesRealPeerAndAdvancesSnapshotRevision()
        {
            var room = new PrivateLobbyModel(LobbyMode.Unranked1v1, "host", "Host"); room.AddGuest("peer", "Peer"); room.Swap("host", 0, 1);
            int revision = room.Revision;
            room = room.WithMode("host", LobbyMode.Team2v2);
            Assert.That(room.Revision, Is.GreaterThan(revision)); Assert.That(room.SeatOf("peer"), Is.GreaterThanOrEqualTo(0));
            Assert.That(room.Player(2), Is.Null); Assert.That(room.Player(3), Is.Null);
            Assert.That(room.CanStart("host"), Is.False);
            room = room.WithMode("host", LobbyMode.Unranked1v1);
            Assert.That(room.CanStart("host"), Is.True);
            Assert.That(room.WithMode("peer", LobbyMode.Team2v2), Is.SameAs(room));
        }
        [TestCase(false)][TestCase(true)] public void GuestLeaveAndKickReturnToChoice(bool kicked)
        {
            var state = new LanRoomState(); state.Join(); state.Connect(); state.Joined();
            if (kicked) state.Kicked(); else state.Leave();
            Assert.That(state.Page, Is.EqualTo(LanRoomPage.Choice));
        }
        [Test] public void JoinFailureCanRetryAndCancelReturnsToChoice()
        {
            var state = new LanRoomState(); state.Join(); state.Connect(); state.Join();
            Assert.That(state.Page, Is.EqualTo(LanRoomPage.Join)); state.Leave();
            Assert.That(state.Page, Is.EqualTo(LanRoomPage.Choice));
        }
        [Test] public void OfflinePeersHaveDistinctRoomIdsWhileRealProfilesKeepTheirIds()
        {
            Assert.That(LanRoomState.PlayerId("local-guest", true, "a"), Is.Not.EqualTo(LanRoomState.PlayerId("local-guest", true, "b")));
            Assert.That(LanRoomState.PlayerId(null, false, "a"), Is.EqualTo("lan-a"));
            Assert.That(LanRoomState.PlayerId("ugs-player", false, "a"), Is.EqualTo("ugs-player"));
        }
        [Test] public void GuestCannotChangeRoomMode()
        {
            var state = new LanRoomState(); state.Join(); state.Connect(); state.Joined(); state.SelectMode(1);
            Assert.That(state.Mode, Is.EqualTo(LobbyMode.Unranked1v1));
        }
    }
}
