using System;
using Newtonsoft.Json;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class MatchRecordTests
    {
        [Test]
        public void CreateCopiesServerSnapshotsAndVersionsWithoutAliasing()
        {
            var first = Player();
            first.Rating.LastMatchUnixSeconds = 123;
            first.Rank.HighestArena = 2;
            first.Inventory.OwnedVariants.Add("suit.warrior.e0");
            var ids = new[] { "p0", "p1" };
            var record = MatchRecords.Create("match", ids, new[] { first, Player() }, 456, 1, 2, 3);
            ids[0] = "changed";
            first.Rating.R = 0;
            first.Rank.HighestArena = 0;
            first.Inventory.OwnedVariants.Clear();

            var stored = JsonConvert.DeserializeObject<MatchRecord>(JsonConvert.SerializeObject(record));
            Assert.That(stored.matchId, Is.EqualTo("match"));
            Assert.That(stored.playerIds, Is.EqualTo(new[] { "p0", "p1" }));
            Assert.That(stored.createdUnixSeconds, Is.EqualTo(456));
            Assert.That(stored.protocol, Is.EqualTo(1));
            Assert.That(stored.sim, Is.EqualTo(2));
            Assert.That(stored.content, Is.EqualTo(3));
            Assert.That(stored.players[0].Rating.R, Is.EqualTo(1500));
            Assert.That(stored.players[0].Rating.LastMatchUnixSeconds, Is.EqualTo(123));
            Assert.That(stored.players[0].Rank.HighestArena, Is.EqualTo(2));
            Assert.That(stored.players[0].OwnedVariants, Is.EqualTo(new[] { "suit.warrior.e0" }));
            Assert.That(stored.state, Is.EqualTo(MatchRecordState.Open));
            Assert.That(stored.reports, Is.Empty);
        }

        [Test]
        public void CreateRejectsAmbiguousRosterAndUninitializedPlayers()
        {
            Assert.Throws<ArgumentException>(() => MatchRecords.Create("match", new[] { "p0", "p0" },
                new[] { Player(), Player() }, 1, 1, 1, 1));
            Assert.Throws<ArgumentException>(() => MatchRecords.Create("match", new[] { "p0", "p1" },
                new[] { Player(), new PlayerState() }, 1, 1, 1, 1));
        }

        internal static PlayerState Player() => new PlayerState
        {
            Rating = PlayerStateDefaults.Rating(), Rank = PlayerStateDefaults.Rank(),
            Inventory = PlayerStateDefaults.Inventory(), History = PlayerStateDefaults.History()
        };
    }
}
