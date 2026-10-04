using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NodeWar.Simulation;

namespace NodeWar.Cloud
{
    /// <summary>Trusted build-time balances, checked against their content-addressed filenames.</summary>
    public sealed class BalanceCatalog
    {
        private static readonly Lazy<BalanceCatalog> embedded = new Lazy<BalanceCatalog>(() =>
            new BalanceCatalog(ReadEmbeddedFiles(), message => Console.Error.WriteLine(message)));
        private readonly Dictionary<int, GameBalanceData> balances = new Dictionary<int, GameBalanceData>();

        public static BalanceCatalog Embedded => embedded.Value;

        /// <param name="files">Filename and JSON pairs; also permits in-memory test catalogs.</param>
        public BalanceCatalog(IEnumerable<KeyValuePair<string, string>> files, Action<string> logWarning)
        {
            var serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver(),
                Culture = CultureInfo.InvariantCulture,
                TypeNameHandling = TypeNameHandling.None
            });
            foreach (var file in files)
            {
                try
                {
                    string name = Path.GetFileNameWithoutExtension(file.Key);
                    if (!int.TryParse(name, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int hash))
                        throw new FormatException("Filename is not a signed content hash.");
                    using var text = new StringReader(file.Value);
                    using var json = new JsonTextReader(text);
                    GameBalanceData balance = serializer.Deserialize<GameBalanceData>(json);
                    if (BalanceHasher.Hash(balance) != hash)
                        throw new FormatException("Balance hash does not match filename.");
                    balances.Add(hash, balance);
                }
                catch (Exception ex) when (ex is JsonException || ex is FormatException || ex is ArgumentException)
                {
                    logWarning?.Invoke("Skipping balance '" + file.Key + "': " + ex.Message);
                }
            }
        }

        internal bool TryGet(int contentHash, out GameBalanceData balance) => balances.TryGetValue(contentHash, out balance);

        private static IEnumerable<KeyValuePair<string, string>> ReadEmbeddedFiles()
        {
            var assembly = typeof(BalanceCatalog).Assembly;
            const string prefix = "NodeWar.Cloud.Balances.";
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (!name.StartsWith(prefix, StringComparison.Ordinal) || !name.EndsWith(".json", StringComparison.Ordinal))
                    continue;
                using var stream = assembly.GetManifestResourceStream(name);
                using var reader = new StreamReader(stream);
                yield return new KeyValuePair<string, string>(name.Substring(prefix.Length), reader.ReadToEnd());
            }
        }
    }
}
