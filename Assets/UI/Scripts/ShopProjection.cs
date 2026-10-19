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
            float referenceDistance, float stall, float position, float panelHeight, float screenAnchor)
        {
            float behind = stall - position;
            bool visible = depth > 0f && behind >= -0.001f && behind <= 1f;
            float recession = 1f - 0.4f * Math.Max(0f, Math.Min(1f, behind));
            float scale = referenceDistance / Math.Max(0.001f, distance) * recession;
            return new ShopPanelPose(x, y - panelHeight * scale * screenAnchor, scale, visible);
        }

        public static bool Changed(float previous, float current, float epsilon)
            => float.IsNaN(previous) || Math.Abs(previous - current) > epsilon;
    }
}
