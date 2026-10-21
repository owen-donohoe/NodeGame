using System;

namespace NodeWar.Lobby
{
    public readonly struct ShopPanelPose
    {
        public readonly float X, Y, Scale;
        public readonly bool Visible;
        public ShopPanelPose(float x, float y, float scale, bool visible)
        {
            X = x; Y = y; Scale = scale; Visible = visible;
        }
    }

    /// <summary>Inputs are panel-local pixels, camera depth and world distance.</summary>
    public static class ShopProjection
    {
        public static ShopPanelPose Project(float x, float y, float depth, float distance,
            float referenceDistance, float stall, float position, float panelHeight, float screenAnchor,
            float restY = 0f, float targetY = 0f)
        {
            float behind = position - stall;
            bool visible = IsVisible(stall, position, depth);
            float recession = 1f - 0.4f * Math.Max(0f, Math.Min(1f, behind));
            float scale = Math.Max(0.85f, Math.Min(1f, referenceDistance / Math.Max(0.001f, distance) * recession));
            return new ShopPanelPose((float)Math.Round(x), (float)Math.Round(y + targetY - restY - panelHeight * scale * screenAnchor), scale, visible);
        }

        /// <summary>Offer panels follow resisted edge travel while the stage camera stays bounded.</summary>
        public static float EdgeOffset(float position, float pixelsPerStall)
            => -(position - Math.Max(0f, Math.Min(ShopSnap.StallCount - 1, position))) * pixelsPerStall;

        public static ShopPanelPose Constrain(ShopPanelPose pose, float width, float height,
            float left, float top, float right, float bottom)
        {
            float maxX = Math.Max(left, right - width * pose.Scale);
            float maxY = Math.Max(top, bottom - height * pose.Scale);
            return new ShopPanelPose(PixelInside(pose.X, left, maxX),
                PixelInside(pose.Y, top, maxY), pose.Scale, pose.Visible);
        }

        private static float PixelInside(float value, float min, float max) =>
            (float)Math.Max(Math.Ceiling(min), Math.Min(Math.Floor(max), Math.Round(value)));

        public static bool IsVisible(float stall, float position, float depth)
            => depth > 0f && Math.Abs(position - stall) <= 0.001f;

        /// <summary>Far to near, ID breaking ties. Reuses both arrays and reports order changes.</summary>
        public static bool OrderByDistance(float[] distances, int[] order)
        {
            bool changed = false;
            for (int i = 1; i < order.Length; i++)
            {
                int key = order[i], j = i - 1;
                while (j >= 0 && (distances[order[j]] < distances[key] ||
                    distances[order[j]] == distances[key] && order[j] > key))
                {
                    order[j + 1] = order[j];
                    j--;
                    changed = true;
                }
                order[j + 1] = key;
            }
            return changed;
        }

        public static bool Changed(float previous, float current, float epsilon)
            => float.IsNaN(previous) || Math.Abs(previous - current) > epsilon;
    }
}
