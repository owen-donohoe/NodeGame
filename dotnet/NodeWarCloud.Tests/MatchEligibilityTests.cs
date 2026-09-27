using System;
using NodeWar.Backend;
using NodeWar.MatchLog;
using NodeWar.Simulation;
using NUnit.Framework;
using Log = NodeWar.MatchLog.MatchLog;

namespace NodeWar.Cloud.Tests
{
    public class MatchEligibilityTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void DraftedEraMustBeOwnedAndWithinSnapshotArena(bool district)
        {
            var record = Record();
            var log = Log();
            string baseId = district ? "district.farm" : "suit.warrior";
            int type = district ? (int)DistrictType.Farm : (int)SuitType.Warrior;
            var eras = new int[district ? NodeWar.Lobby.LoadoutTypes.DistrictTypeCount : NodeWar.Lobby.LoadoutTypes.SuitTypeCount];
            eras[type] = 1;
            if (district) log.loadouts[0].districtEras = eras;
            else log.loadouts[0].suitEras = eras;

            record.players[0].Rank.Arena = 1;
            Assert.That(MatchEligibility.Check(record, log), Does.Contain("not owned"));
            record.players[0].OwnedVariants.Add(CatalogIds.Variant(baseId, 1));
            Assert.That(MatchEligibility.Check(record, log), Is.Null);
            record.players[0].Rank.Arena = 0;
            Assert.That(MatchEligibility.Check(record, log), Does.Contain("snapshot arena"));
        }

        [Test]
        public void NullEraTablesMeanOwnedStartersAndUnusedTypesDoNotNeedEligibility()
        {
            var log = Log();
            var record = Record();
            Assert.That(MatchEligibility.Check(record, log), Is.Null);
            log.loadouts[0].suitEras = new int[NodeWar.Lobby.LoadoutTypes.SuitTypeCount];
            log.loadouts[0].suitEras[(int)SuitType.Scout] = 5;
            Assert.That(MatchEligibility.Check(record, log), Is.Null);
            record.players[1].OwnedVariants.Clear();
            Assert.That(MatchEligibility.Check(record, log), Does.Contain("not owned"));
        }

        private static MatchRecord Record()
        {
            var first = MatchRecordTests.Player();
            var second = MatchRecordTests.Player();
            foreach (var player in new[] { first, second })
                player.Inventory.OwnedVariants.AddRange(new[] { "suit.warrior.e0", "district.farm.e0" });
            return MatchRecords.Create("m", new[] { "p0", "p1" }, new[] { first, second }, 1, 1, 1, 1);
        }

        private static Log Log() => new Log
        {
            header = new MatchLogHeader { matchId = "m", playerIds = new[] { "p0", "p1" }, protocol = 1, sim = 1, content = 1 },
            loadouts = new[]
            {
                new PlayerLoadout { suits = new[] { (int)SuitType.Warrior }, nodes = Array.Empty<int>() },
                new PlayerLoadout { suits = new[] { (int)SuitType.Warrior }, nodes = Array.Empty<int>() }
            },
            draft = new[] { new DraftPlacement { playerID = 0, districtType = DistrictType.Farm } }
        };
    }
}
