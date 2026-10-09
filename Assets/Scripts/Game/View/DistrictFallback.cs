using NodeWar.Simulation;
namespace NodeWar.View
{
    public readonly struct DistrictFallbackDescriptor
    {
        public readonly string Name, Monogram, PrefabKey, Description;
        public DistrictFallbackDescriptor(string name,string monogram,string description,string prefabKey="Crossroads")
        { Name=name; Monogram=monogram; Description=description; PrefabKey=prefabKey; }
    }
    public static class DistrictFallback
    {
        /// <summary>Exact art wins, then the D07 historical keys, then the generic node.</summary>
        public static DistrictType ResolveArt(DistrictType type,System.Func<DistrictType,bool> available)
        {
            if(available(type)) return type;
            switch(type)
            {
                case DistrictType.Town: if(available(DistrictType.Village)) return DistrictType.Village; break;
                case DistrictType.Infirmary:
                    if(available(DistrictType.Shrine)) return DistrictType.Shrine;
                    if(available(DistrictType.Sanctuary)) return DistrictType.Sanctuary; break;
                case DistrictType.Storehouse: if(available(DistrictType.Market)) return DistrictType.Market; break;
                case DistrictType.Fortress: if(available(DistrictType.Rampart)) return DistrictType.Rampart; break;
            }
            return DistrictType.None;
        }
        public static DistrictFallbackDescriptor Describe(DistrictType type)
        {
            switch(type)
            {
                case DistrictType.None: return new DistrictFallbackDescriptor("Crossroads","X","An empty connector.");
                case DistrictType.Farm: return new DistrictFallbackDescriptor("Farm","Fa","Produces food.");
                case DistrictType.Mine: return new DistrictFallbackDescriptor("Mine","Mi","Produces materials.");
                case DistrictType.Village: return new DistrictFallbackDescriptor("Recruit","V","Recruits villagers.","Village");
                case DistrictType.Barracks: return new DistrictFallbackDescriptor("Barracks","B","Equips all drafted combat suits.","Barracks");
                case DistrictType.Core: return new DistrictFallbackDescriptor("Core","C","Your home district.");
                case DistrictType.Workshop: return new DistrictFallbackDescriptor("Workshop","W","Forges mobile minion collectors with metal.");
                case DistrictType.Forge: return new DistrictFallbackDescriptor("Forge","Fo","Produces metal.");
                case DistrictType.Storehouse: return new DistrictFallbackDescriptor("Storehouse","S","Banks alternating food and materials; no workers.","Market");
                case DistrictType.Pier: return new DistrictFallbackDescriptor("Pier","P","Connects districts across a lake.");
                case DistrictType.Town: return new DistrictFallbackDescriptor("Town","T","Grants villagers once per player.","Village");
                case DistrictType.Infirmary: return new DistrictFallbackDescriptor("Infirmary","I","Local healing and Core respawn assistance.","Shrine");
                case DistrictType.Fortress: return new DistrictFallbackDescriptor("Fortress","F","Paid resistance upgrades.","Rampart");
                default: return default;
            }
        }
    }
}
