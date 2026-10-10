namespace NodeWar.Simulation
{
    /// <summary>The current roster. Reserved historical numbers are never runtime aliases.</summary>
    public static class DistrictRoster
    {
        public static bool IsActive(DistrictType type)
        {
            switch (type)
            {
                case DistrictType.None: case DistrictType.Farm: case DistrictType.Mine:
                case DistrictType.Village: case DistrictType.Barracks: case DistrictType.Core:
                case DistrictType.Forge: case DistrictType.Storehouse: case DistrictType.Pier:
                case DistrictType.Workshop: case DistrictType.Town: case DistrictType.Infirmary: case DistrictType.Fortress:
                    return true;
                default: return false;
            }
        }
    }
}
