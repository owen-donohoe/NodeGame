using System;
using System.Collections.Generic;
using NodeWar.Simulation;
using UnityEngine;

namespace NodeWar.Backend
{
    public enum CatalogEntryKind { Variant, Skin }

    [Serializable]
    public sealed class CatalogEntry
    {
        public string id;
        public CatalogEntryKind kind;
        public string baseId;
        public int era = -1;
        public bool retired;
    }

    [CreateAssetMenu(fileName = "Catalog", menuName = "Node War/Backend/Catalog")]
    public sealed class CatalogDefinition : ScriptableObject
    {
        public List<CatalogEntry> entries = new List<CatalogEntry>();
    }

    /// <summary>Used by Editor generation and the local fake; never linked into Shared or Cloud Code.</summary>
    public static class CatalogBases
    {
        public static List<string> All()
        {
            // From the explicit key tables, never from enum member names, so a
            // domain rename cannot change what the catalog is generated with.
            var bases = new List<string>();
            for (int suit = 1; suit < CatalogKeys.SuitTableLength; suit++)
                bases.Add(CatalogKeys.SuitBase(suit));
            foreach (int district in CatalogKeys.CatalogDistrictTypes)
                bases.Add(CatalogKeys.DistrictBase(district));
            return bases;
        }
    }
}
