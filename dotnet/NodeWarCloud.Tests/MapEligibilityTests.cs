using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodeWar.Backend;
using NodeWar.MatchLog;
using NodeWar.Simulation;
using NodeWar.Tests;
using NUnit.Framework;
using Log = NodeWar.MatchLog.MatchLog;

namespace NodeWar.Cloud.Tests
{
    /// <summary>
    /// The server decides which map a ranked match is on, writes it into the
    /// record, and refuses any log whose board is not the catalog's board for
    /// that map. A client-supplied hash is never the authority.
    /// </summary>
    public class MapEligibilityTests
    {
        private static readonly DraftPlacement[] HourglassDraft =
        {
            new DraftPlacement { playerID = 0, districtType = DistrictType.Farm, gridX = 1, gridZ = 1 },
            new DraftPlacement { playerID = 1, districtType = DistrictType.Village, gridX = 4, gridZ = 2 }
        };

        private static Log HourglassLog(GameBalanceData balance, BoardConfigData? board = null, MatchSetup setup = null)
        {
            BoardConfigData b = board ?? PremadeMaps.Hourglass01();
            return RefereeTests.Record(balance, 100, _ => null, b,
                setup ?? MatchSetup.ForShippedMap(PremadeMaps.Hourglass01Id, BalanceHasher.Hash(balance)),
                HourglassDraft);
        }

        private static Referee ShippedReferee(GameBalanceData balance) =>
            new Referee(RefereeTests.Catalog(balance));

        [Test]
        public void AlteredBoardUnderValidMapId_IsRejected()
        {
            GameBalanceData balance = GameBalanceData.Default();

            RefereeVerdict honest = ShippedReferee(balance).Verify(MatchLogFormat.Write(HourglassLog(balance)));
            Assert.That(honest.ok, Is.True, honest.error);

            // The attacker opens a shortcut across the lake, then recomputes the
            // board hash so the log is perfectly self-consistent.
            BoardConfigData altered = PremadeMaps.Hourglass01();
            altered.terrain[3 * altered.gridCols + 2] = TerrainType.Land;
            altered.terrain[3 * altered.gridCols + 3] = TerrainType.Land;
            var forged = new MatchSetup(PremadeMaps.Hourglass01Id, BoardHasher.Hash(altered),
                (ushort)SimulationVersion.Current, BalanceHasher.Hash(balance));
            Log log = HourglassLog(balance, altered, forged);

            RefereeVerdict own = RefereeTests.NewReferee(RefereeTests.Catalog(balance)).Verify(MatchLogFormat.Write(log));
            Assert.That(own.ok, Is.False, "A referee that trusts its own fixture catalog would not catch this; the next one must.");

            RefereeVerdict verdict = ShippedReferee(balance).Verify(MatchLogFormat.Write(log));
            Assert.That(verdict.ok, Is.False);
            Assert.That(verdict.error, Does.Contain("catalog"));
            Assert.That(verdict.ticksReplayed, Is.Zero, "Refused before replaying a single tick.");

            // Same board, but a map ID the server has never shipped.
            Log unknown = HourglassLog(balance, null,
                new MatchSetup("hourglass-99", BoardHasher.Hash(PremadeMaps.Hourglass01()),
                    (ushort)SimulationVersion.Current, BalanceHasher.Hash(balance)));
            verdict = ShippedReferee(balance).Verify(MatchLogFormat.Write(unknown));
            Assert.That(verdict.ok, Is.False);
            Assert.That(verdict.error, Does.Contain("map"));
        }

        [Test]
        public void AlteredBoardUnderValidMapId_IsRejected_TheCorrectFixedMapIsAccepted()
        {
            GameBalanceData balance = GameBalanceData.Default();
            Log log = HourglassLog(balance);
            Assert.That(ShippedReferee(balance).Verify(MatchLogFormat.Write(log)).ok, Is.True);
            Assert.That(ShippedReferee(balance).Verify(MatchLogFormat.Write(log)).ok, Is.True, "and again, deterministically");
        }

        [Test]
        public void WrongMapOrRulesAgainstTheRecord_AreRefused()
        {
            GameBalanceData balance = GameBalanceData.Default();
            int content = BalanceHasher.Hash(balance);
            Log log = HourglassLog(balance);
            MatchRecord record = RecordFor(log, content);

            Assert.That(MatchEligibility.Check(record, log), Is.Null);

            record.mapId = "hourglass-02";
            Assert.That(MatchEligibility.Check(record, log), Does.Contain("map"));

            record = RecordFor(log, content);
            record.boardHash += 1;
            Assert.That(MatchEligibility.Check(record, log), Does.Contain("map"));

            record = RecordFor(log, content);
            record.sim = (ushort)(record.sim + 1);
            Assert.That(MatchEligibility.Check(record, log), Is.Not.Null);

            // A log whose setup disagrees with the record, rules included.
            record = RecordFor(log, content);
            log.setup = new MatchSetup(log.setup.MapId, log.setup.BoardHash, log.setup.SimulationVersion, content + 1);
            Assert.That(MatchEligibility.Check(record, log), Does.Contain("map"));

            // A log with no setup at all cannot be a current match.
            log.setup = null;
            Assert.That(MatchEligibility.Check(record, log), Does.Contain("map"));

            // A current-version record with no map is never admitted.
            Log fresh = HourglassLog(balance);
            record = RecordFor(fresh, content);
            record.mapId = null;
            Assert.That(MatchEligibility.Check(record, fresh), Does.Contain("map"));
        }

        [Test]
        public void OldMatchRecordStillReads()
        {
            var record = MatchRecords.Create("old", new[] { "p0", "p1" },
                new[] { MatchRecordTests.Player(), MatchRecordTests.Player() }, 99, 4, 2, 5);
            record.state = MatchRecordState.Settled;
            record.outcomes = new[] { new MatchOutcome { won = true, rrDelta = 12, rrAfter = 112 },
                                      new MatchOutcome { won = false, rrDelta = -12 } };

            // A record stored by version 2 never had a map.
            JObject stored = JObject.Parse(JsonConvert.SerializeObject(record));
            stored.Remove("mapId");
            stored.Remove("boardHash");
            Assert.That(stored.ContainsKey("mapId"), Is.False);

            var read = JsonConvert.DeserializeObject<MatchRecord>(stored.ToString());
            Assert.That(read.matchId, Is.EqualTo("old"));
            Assert.That(read.playerIds, Is.EqualTo(new[] { "p0", "p1" }));
            Assert.That(read.sim, Is.EqualTo(2));
            Assert.That(read.state, Is.EqualTo(MatchRecordState.Settled));
            Assert.That(read.outcomes[0].won, Is.True);
            Assert.That(read.outcomes[0].rrDelta, Is.EqualTo(12));
            Assert.That(read.mapId, Is.Null);
            Assert.That(read.boardHash, Is.Zero);
            Assert.That(read.players[0].Rating.R, Is.EqualTo(1500));

            // A version 2 record's history is untouched by the check: its log header is all it asks.
            Log oldLog = new Log
            {
                header = new MatchLogHeader { matchId = "old", playerIds = new[] { "p0", "p1" }, protocol = 4, sim = 2, content = 5 },
                loadouts = new[] { new PlayerLoadout { suits = new int[0], districts = new int[0] },
                                   new PlayerLoadout { suits = new int[0], districts = new int[0] } }
            };
            Assert.That(MatchEligibility.Check(read, oldLog), Is.Null);
        }

        private static MatchRecord RecordFor(Log log, int content)
        {
            MatchRecord record = MatchRecords.Create(log.header.matchId, log.header.playerIds,
                new[] { MatchRecordTests.Player(), MatchRecordTests.Player() }, 1, log.header.protocol,
                log.header.sim, content, PremadeMaps.Hourglass01Id, BoardHasher.Hash(PremadeMaps.Hourglass01()));
            foreach (var player in record.players)
            {
                player.OwnedVariants.Add(CatalogIds.Variant("suit.warrior", 0));
                player.OwnedVariants.Add(CatalogIds.Variant("district.farm", 0));
                player.OwnedVariants.Add(CatalogIds.Variant("district.village", 0));
            }
            return record;
        }
    }
}
