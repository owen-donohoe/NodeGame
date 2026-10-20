using System;

namespace NodeWar.Lobby
{
    public readonly struct WorkshopClipLayout
    {
        public readonly float HeaderTop, ClipTop, CardOffset, MaskHeight, ContentHeight;
        public WorkshopClipLayout(float band, float header, float card, float scroll)
        {
            HeaderTop = Math.Max(band, scroll);
            ClipTop = HeaderTop + header / 2f;
            CardOffset = band + header - ClipTop;
            MaskHeight = Math.Max(0, CardOffset + WorkshopLayout.CardGap + card);
            ContentHeight = card + WorkshopLayout.CardGap;
        }
    }

    /// <summary>Scroll geometry in panel units, independent of Toolkit layout passes.</summary>
    public static class WorkshopLayout
    {
        public const float CardGap = 12f;
        public const float TileGap = 8f;
        public static float TileWidth(float available) => Math.Max(0, (float)Math.Floor((available - TileGap * 2) / 3f));
        public static WorkshopClipLayout Clip(float band, float header, float card, float scroll)
            => new WorkshopClipLayout(band, header, card, Math.Max(0, scroll));
    }
}
