using System;

namespace NodeWar.Lobby
{
    public readonly struct WorkshopClipLayout
    {
        public readonly float HeaderTop, ClipTop, CardOffset, MaskHeight, ContentHeight;
        public WorkshopClipLayout(float band, float header, float card, float scroll, float clipInset)
        {
            HeaderTop = Math.Max(band, scroll);
            ClipTop = HeaderTop + clipInset;
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
        // Fixed header geometry, so the page lays out in its first frame without measuring.
        public const float PresetsHeight = 59f, PresetsGap = 8f, SelectorHeight = 52f;
        public const float HeaderHeight = PresetsHeight + PresetsGap + SelectorHeight;
        // The scroll mask edge sits at the vertical middle of the Districts|Suits selector.
        public const float SelectorMiddle = PresetsHeight + PresetsGap + SelectorHeight / 2f;
        public static float TileWidth(float available) => Math.Max(0, (available - TileGap) / 2f);
        public static WorkshopClipLayout Clip(float band, float header, float card, float scroll, float clipInset = SelectorMiddle)
            => new WorkshopClipLayout(band, header, card, Math.Max(0, scroll), clipInset);
    }
}
