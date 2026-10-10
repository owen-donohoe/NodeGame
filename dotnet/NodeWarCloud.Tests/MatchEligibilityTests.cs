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

        [TestCase(0)]
        [TestCase(1)]
        public void MixedArena1And2_HighNoWarriorAccepted(int highSeat)
        {
            var (record, log) = Current(highSeat);
            Assert.That(MatchEligibility.Check(record, log), Is.Null);
            log.draft = Array.Empty<DraftPlacement>();
            Assert.That(MatchEligibility.Check(record, log), Is.Null, "Fewer placements remain allowed.");
        }

        [TestCase(0)]
        [TestCase(1)]
        public void HighPlacementsOutsideChosenBudget_Refused(int highSeat)
        {
            var (record, log) = Current(highSeat);
            log.draft = new[] { new DraftPlacement { playerID = highSeat, districtType = DistrictType.Farm } };
            Assert.That(MatchEligibility.Check(record, log), Does.Contain("budget"));
        }

        [TestCase("districts")]
        [TestCase("suits")]
        [TestCase("missingWarrior")]
        public void LowCapacityExpansion_Refused(string invalid)
        {
            var (record, log) = Current(1);
            if (invalid == "districts") log.loadouts[0].districts = new[] {
                (int)DistrictType.Barracks, (int)DistrictType.Forge, (int)DistrictType.Town };
            if (invalid == "suits") log.loadouts[0].suits = new[] {
                (int)SuitType.Warrior, (int)SuitType.Guardian, (int)SuitType.Scout, (int)SuitType.Medic };
            if (invalid == "missingWarrior") log.loadouts[0].suits = new[] { (int)SuitType.Guardian };
            Assert.That(MatchEligibility.Check(record, log), Is.Not.Null);
        }

        [TestCase("duplicateSuit")]
        [TestCase("duplicateDistrict")]
        [TestCase("invalidSuit")]
        [TestCase("invalidDistrict")]
        [TestCase("tooManySuits")]
        [TestCase("tooManyDistricts")]
        [TestCase("repeatPlacement")]
        [TestCase("repeatBasePlacement")]
        public void CurrentBudgets_RejectInvalidChoices(string invalid)
        {
            var (record, log) = Current(1);
            if (invalid == "duplicateSuit") log.loadouts[1].suits = new[] { (int)SuitType.Guardian, (int)SuitType.Guardian };
            if (invalid == "duplicateDistrict") log.loadouts[1].districts = new[] { (int)DistrictType.Barracks, (int)DistrictType.Barracks };
            if (invalid == "invalidSuit") log.loadouts[1].suits = new[] { 999 };
            if (invalid == "invalidDistrict") log.loadouts[1].districts = new[] { (int)DistrictType.Core };
            if (invalid == "tooManySuits") log.loadouts[1].suits = new[] {
                (int)SuitType.Guardian, (int)SuitType.Scout, (int)SuitType.Medic, (int)SuitType.Warrior };
            if (invalid == "tooManyDistricts") log.loadouts[1].districts = new[] {
                (int)DistrictType.Barracks, (int)DistrictType.Forge, (int)DistrictType.Town,
                (int)DistrictType.Fortress, (int)DistrictType.Workshop, (int)DistrictType.Farm };
            if (invalid == "repeatPlacement") log.draft = new[] {
                new DraftPlacement { playerID = 1, districtType = DistrictType.Barracks },
                new DraftPlacement { playerID = 1, districtType = DistrictType.Barracks } };
            if (invalid == "repeatBasePlacement") log.draft = new[] {
                new DraftPlacement { playerID = 0, districtType = DistrictType.Farm },
                new DraftPlacement { playerID = 0, districtType = DistrictType.Farm } };
            Assert.That(MatchEligibility.Check(record, log), Is.Not.Null);
        }

        [Test]
        public void LegacyProtocol_Unchanged()
        {
            var (record, log) = Current(1);
            record.protocol = log.header.protocol = 5;
            log.loadouts[0].districts = new[] { (int)DistrictType.Barracks,
                (int)DistrictType.Forge, (int)DistrictType.Town };
            log.loadouts[1].suits = new[] { (int)SuitType.Guardian, (int)SuitType.Guardian };
            log.draft = new[] { new DraftPlacement { playerID = 1, districtType = DistrictType.Farm } };
            Assert.That(MatchEligibility.Check(record, log), Is.Null);
        }

        internal static (MatchRecord record, Log log) Current(int highSeat)
        {
            var board = PremadeMaps.Hourglass01();
            var balance = GameBalanceData.Default();
            var players = new[] { MatchRecordTests.Player(), MatchRecordTests.Player() };
            foreach (var player in players)
                player.Inventory.OwnedVariants.AddRange(new[] { "suit.warrior.e0", "suit.guardian.e0",
                    "suit.scout.e0", "suit.medic.e0", "district.farm.e0", "district.mine.e0",
                    "district.village.e0", "district.barracks.e0", "district.forge.e0",
                    "district.town.e0", "district.fortress.e0", "district.workshop.e0" });
            players[highSeat].Rank.Arena = 2;
            players[1 - highSeat].Rank.Arena = 1;
            var record = MatchRecords.Create("m", new[] { "p0", "p1" }, players, 1, 6,
                (ushort)SimulationVersion.Current, BalanceHasher.Hash(balance), PremadeMaps.Hourglass01Id, BoardHasher.Hash(board));
            var log = new Log {
                header = new MatchLogHeader { matchId = "m", playerIds = record.playerIds,
                    protocol = record.protocol, sim = record.sim, content = record.content },
                setup = MatchSetup.ForShippedMap(record.mapId, record.content), board = board,
                loadouts = new PlayerLoadout[2],
                draft = new[] { new DraftPlacement { playerID = 1 - highSeat, districtType = DistrictType.Farm,
                    gridX = 1, gridZ = highSeat == 1 ? 5 : 1 },
                    new DraftPlacement { playerID = highSeat, districtType = DistrictType.Barracks,
                    gridX = 2, gridZ = highSeat == 1 ? 1 : 5 } }
            };
            log.loadouts[1 - highSeat] = new PlayerLoadout {
                suits = new[] { (int)SuitType.Warrior, (int)SuitType.Guardian, (int)SuitType.Scout },
                districts = new[] { (int)DistrictType.Barracks, (int)DistrictType.Forge } };
            log.loadouts[highSeat] = new PlayerLoadout {
                suits = new[] { (int)SuitType.Guardian, (int)SuitType.Scout, (int)SuitType.Medic },
                districts = new[] { (int)DistrictType.Barracks, (int)DistrictType.Forge,
                    (int)DistrictType.Town, (int)DistrictType.Fortress, (int)DistrictType.Workshop } };
            return (record, log);
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
                new PlayerLoadout { suits = new[] { (int)SuitType.Warrior }, districts = Array.Empty<int>() },
                new PlayerLoadout { suits = new[] { (int)SuitType.Warrior }, districts = Array.Empty<int>() }
            },
            draft = new[] { new DraftPlacement { playerID = 0, districtType = DistrictType.Farm } }
        };
    }
}
