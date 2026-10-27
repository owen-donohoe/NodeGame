namespace NodeWar.View
{
    /// <summary>The origin is the top centre. Dimensions are in cells before scaling.</summary>
    public static class PlinthLayout
    {
        public const float Height = 0.6f;

        public static (float x, float y, float z) WorldCentre(int x, int z, float cellScale, float topY)
        {
            return (x * cellScale, topY, z * cellScale);
        }
    }
}
