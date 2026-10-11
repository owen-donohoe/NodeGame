namespace NodeWar.View
{
    /// <summary>Shared board-local territory, claim and health reservations, in cell units.</summary>
    public static class NodeFloorFootprint
    {
        // The authored ground is one cell square. Leave room outside the
        // territory stroke for antialiasing and its minimum screen width.
        public const float QuadHalfExtentCells = 0.5f;
        public const float TerritoryHalfExtentCells = 0.45f;
        public const float TerritoryWidthCells = 0.01f;
        public const float ClaimHalfExtentCells = TerritoryHalfExtentCells - 0.04f;
        public const float ClaimWidthCells = 0.03f;
        public const float HealthHalfExtentCells = TerritoryHalfExtentCells - 0.09f;
        public const float HealthWidthCells = 0.02f;
        public const float CornerRadiusCells = 0.04f;
        public const float DitherHalfExtentCells = TerritoryHalfExtentCells - TerritoryWidthCells * 0.5f;

        /// <summary>World dimensions at the board boundary; invalid scales yield an empty footprint.</summary>
        public static Scaled ForScale(float nodeScale)
            => nodeScale <= 0f || float.IsNaN(nodeScale) || float.IsInfinity(nodeScale)
                ? default : new Scaled(nodeScale);

        public readonly struct Scaled
        {
            public readonly float QuadHalfExtent;
            public readonly float TerritoryHalfExtent;
            public readonly float TerritoryWidth;
            public readonly float ClaimHalfExtent;
            public readonly float ClaimWidth;
            public readonly float HealthHalfExtent;
            public readonly float HealthWidth;
            public readonly float CornerRadius;
            public readonly float DitherHalfExtent;
            public bool IsEmpty => QuadHalfExtent <= 0f;

            internal Scaled(float scale)
            {
                QuadHalfExtent = QuadHalfExtentCells * scale;
                TerritoryHalfExtent = TerritoryHalfExtentCells * scale;
                TerritoryWidth = TerritoryWidthCells * scale;
                ClaimHalfExtent = ClaimHalfExtentCells * scale;
                ClaimWidth = ClaimWidthCells * scale;
                HealthHalfExtent = HealthHalfExtentCells * scale;
                HealthWidth = HealthWidthCells * scale;
                CornerRadius = CornerRadiusCells * scale;
                DitherHalfExtent = DitherHalfExtentCells * scale;
            }
        }
    }
}
