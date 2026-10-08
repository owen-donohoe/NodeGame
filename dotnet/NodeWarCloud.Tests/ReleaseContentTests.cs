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




