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
    }
}
