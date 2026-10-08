using NodeWar.Simulation;
namespace NodeWar.View
{
    public readonly struct DistrictFallbackDescriptor
    {
        public readonly string Name;
        public readonly string Monogram;
        public readonly string PrefabKey;
        public readonly string Description;
        public DistrictFallbackDescriptor(string name, string monogram, string description)
        { Name = name; Monogram = monogram; Description = description; PrefabKey = "Crossroads"; }
    }

    /// <summary>Readable identity independent of art assets; Crossroads is the existing generic node prefab.</summary>
    public static class DistrictFallback
    {
        public static DistrictFallbackDescriptor Describe(DistrictType type)
        {
            switch (type)
            {
                case DistrictType.None: return new DistrictFallbackDescriptor("Crossroads", "X", "An empty connector.");
                case DistrictType.Farm: return new DistrictFallbackDescriptor("Farm", "Fa", "Produces food.");
                case DistrictType.Mine: return new DistrictFallbackDescriptor("Mine", "Mi", "Produces materials.");
                case DistrictType.Village: return new DistrictFallbackDescriptor("Village", "V", "Recruits villagers.");
                case DistrictType.Barracks: return new DistrictFallbackDescriptor("Barracks", "B", "An army district.");
                case DistrictType.Core: return new DistrictFallbackDescriptor("Core", "C", "Your home district.");
                case DistrictType.Forge: return new DistrictFallbackDescriptor("Forge", "Fo", "Produces metal.");
                case DistrictType.Market: return new DistrictFallbackDescriptor("Market", "Ma", "Produces food and materials.");
                case DistrictType.Pier: return new DistrictFallbackDescriptor("Pier", "P", "Connects districts across a lake.");
                case DistrictType.Town: return new DistrictFallbackDescriptor("Town", "T", "Grants villagers on its first capture.");
                case DistrictType.Infirmary: return new DistrictFallbackDescriptor("Infirmary", "I", "A healing district.");
                case DistrictType.Fortress: return new DistrictFallbackDescriptor("Fortress", "F", "A defensive district.");
                default: return default;
            }
        }
    }
}
