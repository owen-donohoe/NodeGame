using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class BalanceCatalogTests
    {
        // The three exports in Balances/ and what BalanceHasher makes of each on
        // this tree. Only the newest still hashes to its own filename: the other
        // two predate hasher extensions and the catalog skips them as mismatched.
        // A rename of fields must not move any of these numbers.
        private static readonly Dictionary<string, int> Shipped = new Dictionary<string, int>
        {
            ["1832066265"] = 1832066265,
            ["1932662518"] = -1295462839,
            ["1966419918"] = -564302031
        };

        [Test]
        public void HistoricalExports_RetainNamedHashes()
        {
            var assembly = typeof(BalanceCatalog).Assembly;
            const string prefix = "NodeWar.Cloud.Balances.";
            var files = new List<KeyValuePair<string, string>>();
            foreach (string name in assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith(prefix, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.Ordinal)))
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(name));
                files.Add(new KeyValuePair<string, string>(name.Substring(prefix.Length), reader.ReadToEnd()));
            }

            Assert.That(files.Select(f => Path.GetFileNameWithoutExtension(f.Key)), Is.SupersetOf(Shipped.Keys),
                "All named historical exports must remain when a new release is added.");
            foreach (var file in files)
            {
                string name = Path.GetFileNameWithoutExtension(file.Key);
                GameBalanceData balance = JsonConvert.DeserializeObject<GameBalanceData>(file.Value);
                int expected = Shipped.TryGetValue(name, out int historical) ? historical : int.Parse(name, System.Globalization.CultureInfo.InvariantCulture);
                Assert.AreEqual(expected, BalanceHasher.Hash(balance), file.Key);
            }

            var warnings = new List<string>();
            var catalog = new BalanceCatalog(files, warnings.Add);
            Assert.That(catalog.TryGet(1832066265, out _), Is.True);
            Assert.AreEqual(2, warnings.Count, "The two stale exports are skipped, not loaded.");
        }
    }
}
