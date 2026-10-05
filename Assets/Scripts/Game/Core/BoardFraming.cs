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
