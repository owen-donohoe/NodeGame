using NodeWar.Simulation;

namespace NodeWar.View
{
    /// <summary>Presentation-only sample for the shipped hourglass, in replay order.</summary>
    public static class BoardVisualsBenchDraft
    {
        public static DraftPlacement[] Create()
        {
            return new[]
            {
                Place(1, DistrictType.Village, 1, 1), Place(0, DistrictType.Village, 1, 5),
                Place(1, DistrictType.Farm, 2, 1), Place(0, DistrictType.Farm, 2, 5),
                Place(1, DistrictType.Mine, 4, 1), Place(0, DistrictType.Mine, 4, 5),
                Place(0, DistrictType.Pier, 1, 3)
            };
        }

        private static DraftPlacement Place(int player, DistrictType district, int x, int z)
        {
            return new DraftPlacement { playerID = player, districtType = district, gridX = x, gridZ = z };
        }
    }
}
