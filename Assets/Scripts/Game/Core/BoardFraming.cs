namespace NodeWar.Core
{
    /// <summary>
    /// The camera's whole-board framing, kept free of UnityEngine so it is tested.
    /// "Fit" is the farthest zoom a match allows; the centre is the middle of the node
    /// grid, which is where the rig looks when it stands there (the same point the
    /// draft opens on). CameraController supplies the numbers and does the moving.
    /// </summary>
    public static class BoardFraming
    {
        // Within this fraction of the farthest zoom counts as already fitted, so a
        // zoom that stopped a hair short still toggles back.
        private const float AtFitFraction = 0.98f;

        /// <summary>The middle of a cols x rows node grid spaced nodeScale apart, on X and Z.</summary>
        public static void Centre(int cols, int rows, float nodeScale, out float x, out float z)
        {
            x = (cols - 1) * nodeScale * 0.5f;
            z = (rows - 1) * nodeScale * 0.5f;
        }

        /// <summary>
        /// One axis of the camera's pan bounds: whatever the BoardConfig asset says, widened if
        /// it would clip the node grid. The asset's numbers were authored for one board; the map
        /// is now a catalog value, so the grid (with one cell of margin) wins when it is larger.
        /// </summary>
        public static void ExpandBounds(float configuredMin, float configuredMax, int count, float nodeScale,
            out float min, out float max)
        {
            float gridMax = (count - 1) * nodeScale;
            min = configuredMin < -nodeScale ? configuredMin : -nodeScale;
            max = configuredMax > gridMax + nodeScale ? configuredMax : gridMax + nodeScale;
        }

        public static bool IsAtFit(float targetZoom, float maxZoom)
        {
            return targetZoom >= maxZoom * AtFitFraction;
        }

        /// <summary>Fit when not already there, otherwise back to the default zoom.</summary>
        public static float ToggleZoom(float targetZoom, float defaultZoom, float maxZoom)
        {
            return IsAtFit(targetZoom, maxZoom) ? defaultZoom : maxZoom;
        }
    }
}
