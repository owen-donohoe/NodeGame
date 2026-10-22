using System;

namespace NodeWar.Lobby
{
    public readonly struct ShopMarketFrame
    {
        public readonly float CameraX, CameraBack, AnchorX, ReferenceDistance;
        public ShopMarketFrame(float cameraX, float cameraBack, float anchorX, float referenceDistance)
        {
            CameraX = cameraX; CameraBack = cameraBack; AnchorX = anchorX; ReferenceDistance = referenceDistance;
        }
    }

    /// <summary>Reserves the left 45% for paper scenery and starts offers at 54%.</summary>
    public static class ShopMarketFraming
    {
        public const float FieldOfView = 42f;
        public const float CameraRise = 2.5f;
        public const float StallX = -0.82f;
        public const float StallNearDistance = 7f;

        // Until dither is available, suppress the whole foreground stall.
        // Measure to its visual centre (one world unit above the ground).
        public static bool ShouldRenderStall(float distanceSquared)
            => distanceSquared >= StallNearDistance * StallNearDistance;

        public static ShopMarketFrame ForAspect(float aspect)
        {
            aspect = Math.Max(0.1f, aspect);
            float back = 8.5f * Math.Max(1f, (390f / 844f) / aspect);
            float distance = (float)Math.Sqrt(back * back + CameraRise * CameraRise);
            float halfWidth = distance * (float)Math.Tan(FieldOfView * Math.PI / 360) * aspect;
            float cameraX = StallX + halfWidth * 0.52f;
            float anchorX = cameraX + halfWidth * 0.08f;
            float dx = anchorX - cameraX;
            return new ShopMarketFrame(cameraX, back, anchorX, (float)Math.Sqrt(distance * distance + dx * dx));
        }
    }
}
