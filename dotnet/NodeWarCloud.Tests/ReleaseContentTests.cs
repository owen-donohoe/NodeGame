using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodeWar.Backend;
using NodeWar.Backend.Editor;
using NodeWar.Simulation;
using NodeWar.Progression;
using NUnit.Framework;
namespace NodeWar.Cloud.Tests
{
    public class ReleaseContentTests
    {
        [Test] public void WorkshopCatalogAllErasAndSkinValidate()
        {
            var items = NodeWar.Cloud.ServerCatalog.Items;
            for (int era = 0; era < 6; era++) Assert.IsTrue(items.Any(i => i.Id == "district.workshop.e" + era && !i.Retired));
            Assert.AreEqual(7, items.Count(i => i.BaseId == "district.workshop" && !i.Retired));
            Assert.That(CatalogValidation.Validate(items, CatalogIds.EraCount), Is.Empty);
            var state = new PlayerState { Rank = new RankRecord { Arena = 5, HighestArena = 5 }, Inventory = PlayerStateDefaults.Inventory() };
            state.Inventory.OwnedVariants.Add("district.workshop.e5");
            state.Inventory.Equipped.Variants["district.workshop"] = "district.workshop.e5";
            state.Inventory.EquippedNodeIDs = new[] { "node_workshop", "" };
            DistrictMigration.Apply(state);
            Assert.AreEqual("district.workshop.e5", state.Inventory.Equipped.Variants["district.workshop"]);
            Assert.AreEqual("node_workshop", state.Inventory.EquippedNodeIDs[0]);
            Assert.IsFalse(DistrictMigration.Apply(state));
        }
        [Test] public void E2BalanceExtensionsAreZeroNeutralForE1()
        {
            Assert.IsTrue(BalanceCatalog.Embedded.TryGet(-893741384, out var b));
            b.minionHP = b.minionMetalCost = b.minionMoveSpeedTicks = 0;
            b.districtStats = b.districtStats.Where(d => d.districtType != DistrictType.Workshop).ToArray();
            Assert.AreEqual(-1600299589, BalanceHasher.Hash(b));
        }
        // Serialization oracle only. This explicitly built balance is not evidence about the Editor asset.
        [Test] public void NewExportHasExactClientDataShape()
        {
            var client=GameBalanceData.Default(); client.recruitBaseCost=7;
            string json=BalanceExportData.Serialize(client); var shape=JObject.Parse(json);
            foreach(var field in typeof(GameBalanceData).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)) Assert.That(shape.Property(field.Name),Is.Not.Null,field.Name);
            Assert.That(shape["recruitBaseCost"].Value<int>(),Is.EqualTo(7));
            Assert.That(shape["districtStats"].Any(d=>d["districtType"].Value<int>()==17 && d["fortressMaterialsCosts"].Count()==4),Is.True);
            var decoded=JsonConvert.DeserializeObject<GameBalanceData>(json);
            Assert.That(BalanceHasher.Hash(decoded),Is.EqualTo(BalanceHasher.Hash(client)));
            Assert.That(JToken.DeepEquals(shape,JObject.FromObject(decoded)),Is.True);
        }
        [Test] public void MissingSerializedCoreRulesCannotExportAsFreeActions()
        {
            var data=GameBalanceData.Default(); data.recruitBaseCost=0;
            Assert.That(BalanceExportData.ReleaseValid(data,out _),Is.False);
            data=GameBalanceData.Default(); data.districtStats=Array.Empty<DistrictStats>();
            Assert.That(BalanceExportData.ReleaseValid(data,out _),Is.False);
        }
        [Test] public void InvalidCoreRulesFailBeforeWritingExport()
        {
            var data=GameBalanceData.Default(); data.captureBonusMaxSteps=-1;
            Assert.That(BalanceExportData.ReleaseValid(data,out var reason),Is.False); Assert.That(reason,Is.Not.Empty);
        }
        [Test] public void ExplicitCompleteBalancePassesReleaseValidation()
        {Assert.That(BalanceExportData.ReleaseValid(CompleteClient(),out var reason),Is.True,reason);}

        // D6: the parts of the v4 release that need no new export.
        [Test] public void V4ContentAndMigrationAreComplete()
        {
            // Active types exclude Market and every old merged district; Storehouse replaced Market.
            var active = CatalogKeys.CatalogDistrictTypes;
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 6, 14, 15, 16, 17, 18, 19 }, active);
            foreach (int retired in new[] { 5, 7, 8, 9, 10, 11, 12, 13 }) CollectionAssert.DoesNotContain(active, retired);
            Assert.That(DistrictRoster.IsActive(DistrictType.Market), Is.False);
            Assert.That(DistrictRoster.IsActive(DistrictType.Storehouse), Is.True);

            // Storehouse has all six eras in the catalog; nothing active belongs to a retired base.
            var items = NodeWar.Cloud.ServerCatalog.Items;
            for (int era = 0; era < GameBalanceData.EraCount; era++)
                Assert.That(items.Any(i => !i.Retired && i.Id == CatalogIds.Variant(CatalogKeys.DistrictBase(18), era)), Is.True, "storehouse era " + era);
            Assert.That(items.Count(i => i.BaseId == "district.storehouse" && !i.Retired), Is.EqualTo(7));
            foreach (string retiredBase in new[] { "district.market", "district.camp", "district.shrine", "district.arsenal", "district.sanctuary", "district.watchtower", "district.rampart" })
                Assert.That(items.Where(i => i.BaseId == retiredBase), Is.All.Matches<CatalogItem>(i => i.Retired), retiredBase);
            Assert.That(CatalogValidation.Validate(items, CatalogIds.EraCount), Is.Empty);

            // Records and history written under simulation 3 still read.
            var old = MatchRecords.Create("old-v3", new[] { "p0", "p1" }, new[] { MatchRecordTests.Player(), MatchRecordTests.Player() }, 100, 5, 3, 1);
            old.state = MatchRecordState.Settled;
            old.outcomes = new[]
            {
                new MatchOutcome { won = true, rrDelta = 20, rrAfter = 1520, arenaAfter = 1 },
                new MatchOutcome { won = false, rrDelta = -20, rrAfter = 1480, arenaAfter = 0 }
            };
            Assert.That(old.sim, Is.EqualTo(3));
            var records = new InMemoryMatchRecordStore(old);
            var history = new InMemoryPlayerRecordStore();
            history.WriteAsync(new PlayerState { History = new HistoryRecord { MatchIds = new System.Collections.Generic.List<string> { "old-v3" } } });
            var read = MatchHistory.ForPlayer("p0", records, history).GetAwaiter().GetResult();
            Assert.That(read, Has.Count.EqualTo(1));
            Assert.That((read[0].matchId, read[0].won, read[0].rrDelta), Is.EqualTo(("old-v3", true, 20)));

            // A simulation-3 allocation is refused before any player is read.
            var balance = GameBalanceData.Default();
            int reads = 0;
            var allocation = new MatchAllocation(id => { reads++; return System.Threading.Tasks.Task.FromResult(MatchRecordTests.Player()); },
                new InMemoryMatchRecordStore(), RefereeTests.Catalog(balance), id => null);
            var roster = new[]
            {
                new AllocationPlayer { PlayerId = "p0", Protocol = ProtocolVersion.Current, Sim = 3, Content = BalanceHasher.Hash(balance) },
                new AllocationPlayer { PlayerId = "p1", Protocol = ProtocolVersion.Current, Sim = 3, Content = BalanceHasher.Hash(balance) }
            };
            var result = allocation.Allocate("m-v3", roster, 1234567).GetAwaiter().GetResult();
            Assert.That(result.ok, Is.False); StringAssert.Contains("Unsupported", result.error);
            Assert.That(reads, Is.Zero);
        }

        // The lead exports the Editor balance asset at D-E; this is the whole acceptance assertion for it.
        [Test]
        public void V4ExportHashMatchesFilenameAndCarriesBankTuning()
        {
            var assembly = typeof(BalanceCatalog).Assembly;
            var v4 = new System.Collections.Generic.List<(string id, GameBalanceData data)>();
            foreach (string name in assembly.GetManifestResourceNames().Where(n => n.StartsWith("NodeWar.Cloud.Balances.") && n.EndsWith(".json")))
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(name));
                var data = JsonConvert.DeserializeObject<GameBalanceData>(reader.ReadToEnd());
                if (data.collectProgressPerUnit > 0) v4.Add((name.Substring("NodeWar.Cloud.Balances.".Length).Replace(".json", ""), data));
            }
            Assert.That(v4, Is.Not.Empty, "no v4 balance export is embedded");
            foreach (var (id, data) in v4)
            {
                Assert.That(BalanceHasher.Hash(data).ToString(CultureInfo.InvariantCulture), Is.EqualTo(id), "new file hash == filename");
                Assert.That(BalanceExportData.ReleaseValid(data, out var reason), Is.True, reason);
                Assert.That(data.BankTuningValid(), Is.True);
                Assert.That(BalanceCatalog.Embedded.TryGet(int.Parse(id, CultureInfo.InvariantCulture), out _), Is.True);
            }
        }

        private static GameBalanceData CompleteClient()
        {
            var data=GameBalanceData.Default(); var entries=data.districtStats.ToList();
            for(int era=0;era<GameBalanceData.EraCount;era++) entries.Add(new DistrictStats {districtType=DistrictType.Pier,era=era});
            data.districtStats=entries.ToArray(); return data;
        }
        // C-E supplies the exact serialized Editor Data and content-addressed export path.
        // An explicit gate cannot turn a GameBalanceData.Default() export into release evidence.
        [Test,Explicit("C-E: set NODEWAR_CLIENT_BALANCE_JSON and NODEWAR_RELEASE_BALANCE_JSON to the exact Editor Data and new export.")]
        public void NewExportMatchesClientBalanceAndCatalog()
        {
            string clientPath=Environment.GetEnvironmentVariable("NODEWAR_CLIENT_BALANCE_JSON");
            string exportPath=Environment.GetEnvironmentVariable("NODEWAR_RELEASE_BALANCE_JSON");
            Assert.That(File.Exists(clientPath),Is.True,"exact serialized client Data is required");
            Assert.That(File.Exists(exportPath),Is.True,"new Editor export is required");
            var client=JObject.Parse(File.ReadAllText(clientPath)); var exported=JObject.Parse(File.ReadAllText(exportPath));
            Assert.That(JToken.DeepEquals(client,exported),Is.True);
            var data=exported.ToObject<GameBalanceData>(); Assert.That(BalanceExportData.ReleaseValid(data,out var reason),Is.True,reason);
            Assert.That(Path.GetFileNameWithoutExtension(exportPath),Is.EqualTo(BalanceHasher.Hash(data).ToString(CultureInfo.InvariantCulture)));
            var assembly=typeof(BalanceCatalog).Assembly;
            Assert.That(assembly.GetManifestResourceNames(),Does.Contain("NodeWar.Cloud.Balances."+Path.GetFileName(exportPath)));
            Assert.That(BalanceCatalog.Embedded.TryGet(BalanceHasher.Hash(data),out var loaded),Is.True);
            Assert.That(JToken.DeepEquals(JObject.FromObject(loaded),exported),Is.True);
            foreach(string name in assembly.GetManifestResourceNames().Where(n=>n.StartsWith("NodeWar.Cloud.Balances.")&&n.EndsWith(".json")))
            {
                using var reader=new StreamReader(assembly.GetManifestResourceStream(name)); var historical=JsonConvert.DeserializeObject<GameBalanceData>(reader.ReadToEnd());
                string id=name.Substring("NodeWar.Cloud.Balances.".Length).Replace(".json","");
                int expected=id=="1932662518"?-1295462839:id=="1966419918"?-564302031:int.Parse(id,CultureInfo.InvariantCulture);
                Assert.That(BalanceHasher.Hash(historical),Is.EqualTo(expected),name);
            }
            foreach(int type in CatalogKeys.CatalogDistrictTypes)
                for(int era=0;era<GameBalanceData.EraCount;era++)
                    Assert.That(data.districtStats.Any(d=>(int)d.districtType==type&&d.era==era),Is.True,$"district {type} era {era}");
            Assert.That(PremadeMaps.Catalog.TryGet(PremadeMaps.Hourglass01Id,out var serverBoard),Is.True);
                        string clientBoardHash=Environment.GetEnvironmentVariable("NODEWAR_CLIENT_BOARD_HASH");
            Assert.That(clientBoardHash,Is.Not.Null.And.Not.Empty,"C-E client board hash is required");
            Assert.That(BoardHasher.Hash(serverBoard),Is.EqualTo(int.Parse(clientBoardHash,CultureInfo.InvariantCulture)));
            Assert.That(CatalogValidation.Validate(ServerCatalog.Items,CatalogIds.EraCount),Is.Empty);
            foreach(int type in CatalogKeys.CatalogDistrictTypes)
                for(int era=0;era<CatalogIds.EraCount;era++)
                    Assert.That(ServerCatalog.Items.Any(i=>!i.Retired&&i.Id==CatalogIds.Variant(CatalogKeys.DistrictBase(type),era)),Is.True);
        }
    }
}

