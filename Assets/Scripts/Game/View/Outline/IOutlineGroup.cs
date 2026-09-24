using UnityEngine;

namespace NodeWar.View.Outline
{
    /// <summary>
    /// What <see cref="OutlineRegistry"/> needs from a thing that can be
    /// outlined. <see cref="OutlineGroup"/> is the only production implementer.
    ///
    /// This exists so the registry's ID lifecycle -- the part with real edge
    /// cases in it -- can be unit tested against a plain object, with no
    /// GameObject, no scene, and no render pipeline. Without it the only way to
    /// test allocation would be to build a Unity hierarchy per case.
    /// </summary>
    public interface IOutlineGroup
    {
        /// <summary>The resolved style. <see cref="OutlineStyle.None"/> means not outlined.</summary>
        OutlineStyle Style { get; }

        /// <summary>
        /// The mask ID this group currently holds, or
        /// <see cref="OutlineIdAllocator.None"/> when it holds none. Written by
        /// the registry and by nothing else.
        /// </summary>
        int OutlineId { get; set; }

        /// <summary>
        /// The renderers whose union forms the silhouette.
        ///
        /// Returned as the live cached array rather than a copy or an
        /// enumerable, because the mask pass walks this every frame for every
        /// active group and the budget allows no per-frame allocation. Entries
        /// may be null or disabled; the pass filters at draw time rather than
        /// the group pruning eagerly, since nothing in this project destroys
        /// nodes or villagers -- a dead villager is a live GameObject with
        /// disabled renderers.
        /// </summary>
        Renderer[] Renderers { get; }

        /// <summary>
        /// A colour this group's line is drawn in over its style's palette
        /// colour, blended by alpha: 0 leaves the palette colour, 1 replaces it.
        ///
        /// Node ownership is what this exists for. The style still decides
        /// whether there is a line, how thick it is and which line wins a pixel;
        /// the tint only changes what colour that line is. Read by the composite
        /// pass at draw time, so it needs no registry sync.
        /// </summary>
        Color Tint { get; }
    }
}
