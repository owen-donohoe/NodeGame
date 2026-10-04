using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NodeWar.Backend;
using NodeWar.Progression;

namespace NodeWar.Cloud
{
    public static class ServerCatalog
    {
        private static readonly Lazy<IReadOnlyList<CatalogItem>> catalog = new Lazy<IReadOnlyList<CatalogItem>>(Load);
        public static IReadOnlyList<CatalogItem> Items => catalog.Value;

        public static List<CatalogItem> Parse(string json)
        {
            var items = JsonConvert.DeserializeObject<List<CatalogItem>>(json, new StringEnumConverter());
            if (items == null) throw new InvalidDataException("Catalog must be a JSON array.");
            var errors = CatalogValidation.Validate(items, CatalogIds.EraCount);
            if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
            return items;
        }

        private static IReadOnlyList<CatalogItem> Load()
        {
            using var stream = typeof(ServerCatalog).Assembly.GetManifestResourceStream("NodeWarCloud.Catalog.catalog.json")
                ?? throw new InvalidDataException("Embedded catalog is missing.");
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd()).AsReadOnly();
        }
    }
}
