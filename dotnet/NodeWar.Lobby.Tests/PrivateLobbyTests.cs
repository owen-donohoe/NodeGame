using NodeWar.Lobby;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class PrivateLobbyTests
    {
        [TestCase(LobbyMode.Ranked1v1, 2)]
        [TestCase(LobbyMode.Team2v2, 4)]
        [TestCase(LobbyMode.FreeForAll4, 4)]
        public void SeatsHaveUniquePositionsAndModeColours(LobbyMode mode, int capacity)
        {
            var m = new PrivateLobbyModel(mode, "host", "Host#ONE");
            Assert.That(m.Capacity, Is.EqualTo(capacity));
            for (int i = 0; i < capacity; i++)
            {
                Assert.That(m.Colour(i), Is.EqualTo(mode == LobbyMode.Team2v2 ? i / 2 : i));
                for (int j = 0; j < i; j++) Assert.That(m.Position(i), Is.Not.EqualTo(m.Position(j)));
            }
            Assert.That(m.Position(0), Is.EqualTo(mode == LobbyMode.Team2v2 ? PrivateSeatPosition.TopLeft : PrivateSeatPosition.Top));
            Assert.That(m.Position(capacity - 1), Is.EqualTo(mode == LobbyMode.Team2v2 ? PrivateSeatPosition.BottomRight :
                mode == LobbyMode.FreeForAll4 ? PrivateSeatPosition.Left : PrivateSeatPosition.Bottom));
        }

        [Test]
        public void OnlyCurrentHostCanSwapTransferKickOrStart()
        {
            var m = new PrivateLobbyModel(LobbyMode.Ranked1v1, "h", "Host");
            Assert.That(m.CanStart("h"), Is.False);
            Assert.That(m.AddGuest("g", "Guest"), Is.True);
            Assert.That(m.AddGuest("g", "Duplicate"), Is.False);
            Assert.That(m.Swap("g", 0, 1), Is.False);
            Assert.That(m.MakeHost("g", 0), Is.False);
            Assert.That(m.Remove("g", 0), Is.False);
            Assert.That(m.CanStart("g"), Is.False);
            Assert.That(m.CanStart("h"), Is.True);
            Assert.That(m.Swap("h", -1, 1), Is.False);
            Assert.That(m.Swap("h", 0, 2), Is.False);
            Assert.That(m.Swap("h", 0, 1), Is.True);
            Assert.That(m.Player(1), Is.EqualTo("h"));
            Assert.That(m.HostId, Is.EqualTo("h"));
            Assert.That(m.MakeHost("h", 0), Is.True);
            Assert.That(m.Swap("h", 0, 1), Is.False);
            Assert.That(m.CanStart("g"), Is.True);
            Assert.That(m.Remove("g", 0), Is.False);
            Assert.That(m.Remove("g", 1), Is.True);
            Assert.That(m.CanStart("g"), Is.False);
        }

        [TestCase(LobbyMode.Team2v2)]
        [TestCase(LobbyMode.FreeForAll4)]
        public void FourFilledSeatsNeverEnableSimulationLaunch(LobbyMode mode)
        {
            var m = new PrivateLobbyModel(mode, "h", "Host");
            for (int i = 1; i < 4; i++) Assert.That(m.AddGuest("g" + i, "Guest"), Is.True);
            Assert.That(m.CanStart("h"), Is.False);
            Assert.That(m.AddGuest("fifth", "Overflow"), Is.False);
            Assert.That(m.Swap("h", 0, 3), Is.True);
        }

        [Test]
        public void SnapshotsPreserveSeatsAuthorityAndRevision()
        {
            var m = new PrivateLobbyModel(LobbyMode.Team2v2, "h", "Host#ABC");
            m.AddGuest("g", "Guest#XYZ"); m.Swap("h", 1, 3); m.MakeHost("h", 3);
            byte[] data = new PrivateLobbyPacket { Kind = 1, Model = m }.Encode();
            Assert.That(PrivateLobbyPacket.TryRead(data, out var p), Is.True);
            Assert.That(p.Model.HostId, Is.EqualTo("g"));
            Assert.That(p.Model.Player(3), Is.EqualTo("g"));
            Assert.That(p.Model.Name(3), Is.EqualTo("Guest#XYZ"));
            Assert.That(p.Model.Revision, Is.EqualTo(m.Revision));
            for (int i = 0; i < data.Length; i++)
            {
                byte[] truncated = new byte[i]; System.Array.Copy(data, truncated, i);
                Assert.That(PrivateLobbyPacket.TryRead(truncated, out _), Is.False);
            }
            var trailing = new byte[data.Length + 1]; System.Array.Copy(data, trailing, data.Length);
            Assert.That(PrivateLobbyPacket.TryRead(trailing, out _), Is.False);
        }

        private static PrivateLobbyModel Ready()
        {
            var m = new PrivateLobbyModel(LobbyMode.Ranked1v1, "h", "Host");
            m.AddGuest("g", "Guest"); return m;
        }

        [Test]
        public void StartFreezesEveryRosterMutation()
        {
            var m = Ready();
            Assert.That(m.BeginStart("g"), Is.False);
            Assert.That(m.Phase, Is.EqualTo(PrivatePhase.Waiting));
            Assert.That(m.BeginStart("h"), Is.True);
            Assert.That(m.Phase, Is.EqualTo(PrivatePhase.Starting));
            int revision = m.Revision;
            Assert.That(m.Swap("h", 0, 1), Is.False);
            Assert.That(m.MakeHost("h", 1), Is.False);
            Assert.That(m.Remove("h", 1), Is.False);
            Assert.That(m.AddGuest("x", "X"), Is.False);
            Assert.That(m.BeginStart("h"), Is.False);
            Assert.That(m.Revision, Is.EqualTo(revision));
            Assert.That(m.Player(1), Is.EqualTo("g"));
        }

        [Test]
        public void CancellingTheStartUnfreezesTheRoster()
        {
            var m = Ready(); m.BeginStart("h"); m.CancelStart();
            Assert.That(m.Starting, Is.False);
            Assert.That(m.Swap("h", 0, 1), Is.True);
            Assert.That(m.Starting, Is.False);
        }

        [Test]
        public void RevisionChangeCancelsPendingStartAndNeedsANewOne()
        {
            var m = Ready(); m.BeginStart("h"); int at = m.Revision;
            Assert.That(m.IsStartingAt(at), Is.True);
            // A guest leaving cancels first, then mutates (the launcher's departure path).
            m.CancelStart(); m.Remove("h", 1);
            Assert.That(m.Revision, Is.GreaterThan(at));
            Assert.That(m.Starting, Is.False);
            Assert.That(m.IsStartingAt(at), Is.False);
            m.AddGuest("g2", "G2");
            Assert.That(m.Starting, Is.False);
            Assert.That(m.BeginStart("h"), Is.True);
        }

        [Test]
        public void StaleStartFlagIsIgnoredAfterAnyRevisionChange()
        {
            var m = Ready(); m.BeginStart("h"); int stale = m.Revision;
            m.CancelStart(); m.Swap("h", 0, 1);
            Assert.That(m.IsStartingAt(stale), Is.False);
            Assert.That(m.IsStartingAt(m.Revision), Is.False);
            Assert.That(m.Phase, Is.EqualTo(PrivatePhase.Waiting));
        }
    }
}
