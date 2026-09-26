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
            var bases = new List<string>();
            foreach (SuitType suit in Enum.GetValues(typeof(SuitType)))
                if (suit != SuitType.None) bases.Add(CatalogIds.SuitBase(suit.ToString()));
            foreach (DistrictType district in Enum.GetValues(typeof(DistrictType)))
                if (district != DistrictType.None && district != DistrictType.Core)
                    bases.Add(CatalogIds.DistrictBase(district.ToString()));
            return bases;
        }
    }
}
