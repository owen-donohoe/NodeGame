using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodeWar.Backend;
using NodeWar.Progression;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class CatalogTests
    {
        internal const string CatalogPath = "dotnet/NodeWarCloud/NodeWarCloud/Catalog/catalog.json";

        internal static IEnumerable<string> ExpectedBases() =>
            Enum.GetValues(typeof(SuitType)).Cast<SuitType>().Where(s => s != SuitType.None)
                .Select(s => CatalogIds.SuitBase(s.ToString()))
                .Concat(Enum.GetValues(typeof(DistrictType)).Cast<DistrictType>()
                    .Where(d => d != DistrictType.None && d != DistrictType.Core)
                    .Select(d => CatalogIds.DistrictBase(d.ToString())));

        [Test]
        public void EmbeddedCatalog_ContainsExactlyEveryEnumBaseAndEra_AndDefaultSkin()
        {
            var expected = ExpectedBases().SelectMany(b => Enumerable.Range(0, CatalogIds.EraCount)
                .Select(e => CatalogIds.Variant(b, e)).Append(CatalogIds.DefaultSkin(b))).ToList();
            var catalog = ServerCatalog.Items;
            Assert.That(CatalogValidation.Validate(catalog, 6), Is.Empty);
            Assert.That(catalog.Select(i => i.Id), Is.EquivalentTo(expected));
            Assert.That(catalog.Count, Is.EqualTo(161));
            foreach (var item in catalog)
            {
                Assert.That(item.Retired, Is.False);
                Assert.That(ExpectedBases(), Does.Contain(item.BaseId));
                Assert.That(item.Id, Is.EqualTo(item.Kind == CatalogItemKind.Variant
                    ? CatalogIds.Variant(item.BaseId, item.Era) : CatalogIds.DefaultSkin(item.BaseId)));
                if (item.Kind == CatalogItemKind.Skin) Assert.That(item.Era, Is.EqualTo(-1));
            }
        }

        [Test]
        public void DiskCatalog_UsesStringKinds_AndMatchesEmbeddedResource()
        {
            string json = File.ReadAllText(Path.Combine(RepositoryRoot(), CatalogPath));
            foreach (var item in JArray.Parse(json)) Assert.That(item["Kind"].Type, Is.EqualTo(JTokenType.String));
            Assert.That(JsonConvert.SerializeObject(ServerCatalog.Parse(json)),
                Is.EqualTo(JsonConvert.SerializeObject(ServerCatalog.Items)));
        }

        [Test]
        public void CandidateCatalog_PassesAuthoritativeValidationAgainstCommittedFile()
        {
            string candidatePath = Environment.GetEnvironmentVariable("NODEWAR_CATALOG_CANDIDATE")
                ?? Path.Combine(RepositoryRoot(), CatalogPath);
            var candidate = ServerCatalog.Parse(File.ReadAllText(candidatePath));
            Assert.That(CatalogValidation.Validate(candidate, CatalogIds.EraCount), Is.Empty);
            Assert.That(CatalogValidation.ValidateAgainstPrevious(CommittedCatalog(), candidate), Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void PreviousCatalogHarness_RefusesRemovalOrRename(bool rename)
        {
            var previous = CommittedCatalog();
            var candidate = ServerCatalog.Parse(JsonConvert.SerializeObject(previous));
            string oldId = candidate[0].Id;
            if (rename) candidate[0].Id += ".renamed";
            else candidate.RemoveAt(0);
            Assert.That(CatalogValidation.Validate(candidate, CatalogIds.EraCount), Is.Empty);
            Assert.That(CatalogValidation.ValidateAgainstPrevious(previous, candidate),
                Has.Some.Contains(oldId).And.Contains("removed or renamed"));
        }

        [TestCase("Kind")]
        [TestCase("BaseId")]
        [TestCase("Era")]
        public void PreviousCatalogHarness_RefusesIdentityReuse(string field)
        {
            var previous = CommittedCatalog();
            var candidate = ServerCatalog.Parse(JsonConvert.SerializeObject(previous));
            if (field == "Kind") candidate[0].Kind = CatalogItemKind.Skin;
            if (field == "BaseId") candidate[0].BaseId = "suit.unknown";
            if (field == "Era") candidate[0].Era = 5;
            Assert.That(CatalogValidation.ValidateAgainstPrevious(previous, candidate), Has.Some.Contains("must not be reused"));
        }

        [Test]
        public void PreviousCatalogHarness_AllowsRetirementWithoutRemovingIdentity()
        {
            var previous = CommittedCatalog();
            var candidate = ServerCatalog.Parse(JsonConvert.SerializeObject(previous));
            candidate[0].Retired = true;
            Assert.That(CatalogValidation.Validate(candidate, CatalogIds.EraCount), Is.Empty);
            Assert.That(CatalogValidation.ValidateAgainstPrevious(previous, candidate), Is.Empty);
        }

        private static List<CatalogItem> CommittedCatalog()
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = RepositoryRoot(), RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
            };
            start.ArgumentList.Add("show");
            start.ArgumentList.Add((Environment.GetEnvironmentVariable("NODEWAR_CATALOG_BASELINE_REF") ?? "HEAD") + ":" + CatalogPath);
            using var process = Process.Start(start);
            string json = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.That(process.ExitCode, Is.Zero, "Cannot read committed catalog: " + error);
            return ServerCatalog.Parse(json);
        }

        private static string RepositoryRoot()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "CLAUDE.md"))) return directory.FullName;
            throw new DirectoryNotFoundException("Cannot locate the Node War checkout.");
        }
    }
}
