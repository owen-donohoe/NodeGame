using System.Collections.Generic;
using UnityEngine;

namespace NodeWar.View.Outline
{
    /// <summary>
    /// Marks a node or villager root as one outline silhouette.
    ///
    /// Everything under this component that is included in
    /// <see cref="Renderers"/> writes the same group ID into the mask, so the
    /// dilate pass finds no edge along the seams where a node's quad meets its
    /// building sprites, or where a villager's costume overlay meets its base.
    /// One continuous line around the union, which is the entire point.
    ///
    /// Added at runtime rather than authored on the prefabs, matching how
    /// NodeView already does AddComponent&lt;NodeHighlight&gt;() and how
    /// VillagerTouchTarget is built at runtime so the villager prefab needs no
    /// edit. See <see cref="Attach"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OutlineGroup : MonoBehaviour, IOutlineGroup
    {
        private static readonly Renderer[] NoRenderers = new Renderer[0];

        [Tooltip("Renderers forming the silhouette. Left empty, the group " +
                 "collects them from the children on enable.")]
        [SerializeField] private Renderer[] renderers;

        [Tooltip("Renderers under this root to keep out of the silhouette -- " +
                 "the villager's flat ground-shadow blob being the case this " +
                 "exists for.")]
        [SerializeField] private List<Renderer> excluded = new List<Renderer>();

        [Header("Debug")]
        [Tooltip("Forces one intent on, so an outline can be exercised from the " +
                 "Inspector without any gameplay wiring. Leave at None in " +
                 "shipped content -- real state is driven through SetIntent by " +
                 "selection, claim state and the pointer.")]
        [SerializeField] private OutlineStyle debugStyle = OutlineStyle.None;

        private OutlineRegistry registry;
        private uint intents;
        private OutlineStyle appliedDebugStyle = OutlineStyle.None;

        /// <summary>
        /// The resolved style, highest intent wins. There is no setter: several
        /// systems have an opinion about the same object at once, and a single
        /// assignable field would hand the frame to whichever of them ran last.
        /// Use <see cref="SetIntent"/>.
        /// </summary>
        public OutlineStyle Style => OutlineStyleMask.Highest(intents);

        public int OutlineId { get; set; }

        public Renderer[] Renderers => renderers ?? NoRenderers;

        private OutlineRegistry Registry => registry ?? (registry = OutlineRegistry.Instance);

        /// <summary>
        /// Adds a group to a root and gives it an explicit renderer list.
        /// The list is taken as-is, bypassing the child search and its filter.
        /// </summary>
        public static OutlineGroup Attach(GameObject root, Renderer[] silhouette)
        {
            if (root == null) return null;

            OutlineGroup group = root.GetComponent<OutlineGroup>();
            if (group == null) group = root.AddComponent<OutlineGroup>();

            group.SetRenderers(silhouette);
            return group;
        }

        /// <summary>
        /// Sets or clears one system's opinion of this group. Selection,
        /// contested state, hover and command acknowledgment each own their own
        /// intent and none of them can stamp on another's.
        /// </summary>
        public void SetIntent(OutlineStyle style, bool active)
        {
            uint next = OutlineStyleMask.With(intents, style, active);
            if (next == intents) return;

            intents = next;
            Registry.SyncStyle(this);
        }

        public bool HasIntent(OutlineStyle style) => OutlineStyleMask.Has(intents, style);

        /// <summary>Drops every intent, so the group stops being outlined.</summary>
        public void ClearIntents()
        {
            if (intents == 0u) return;

            intents = 0u;
            Registry.SyncStyle(this);
        }

        /// <summary>
        /// Replaces the silhouette with an explicit list. Null entries are kept
        /// rather than pruned -- the mask pass null-checks at draw time anyway,
        /// and pruning here would only move the check somewhere colder.
        /// </summary>
        public void SetRenderers(Renderer[] silhouette)
        {
            renderers = silhouette ?? NoRenderers;
        }

        /// <summary>
        /// Collects the silhouette from the children.
        ///
        /// Only MeshRenderer and SpriteRenderer are taken. That is not
        /// tidiness: a plain "every Renderer under this root" search would pick
        /// up the LineRenderer that NodeHighlight builds for its move-order
        /// pulse ring, and outline the ring. World-space UI is safe either way,
        /// because the claim bar and health ring draw through CanvasRenderer,
        /// which does not derive from Renderer and so never appears here.
        ///
        /// Allocates, via GetComponentsInChildren. Call it on setup or when the
        /// hierarchy actually changes -- never per frame.
        /// </summary>
        public void RebuildFromChildren()
        {
            Renderer[] found = GetComponentsInChildren<Renderer>(true);

            int kept = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (!IsSilhouetteRenderer(found[i])) continue;
                found[kept++] = found[i];
            }

            Renderer[] result = new Renderer[kept];
            System.Array.Copy(found, result, kept);
            renderers = result;
        }

        /// <summary>Keeps a renderer out of the silhouette on the next rebuild.</summary>
        public void Exclude(Renderer renderer)
        {
            if (renderer == null) return;
            if (excluded.Contains(renderer)) return;

            excluded.Add(renderer);
        }

        private bool IsSilhouetteRenderer(Renderer candidate)
        {
            if (candidate == null) return false;
            if (excluded.Contains(candidate)) return false;

            return candidate is MeshRenderer || candidate is SpriteRenderer;
        }

        /// <summary>
        /// Folds <see cref="debugStyle"/> into the intent mask, replacing
        /// whatever it forced last time. Routed through SetIntent rather than
        /// writing the mask directly, so the debug path exercises exactly the
        /// same code the game will.
        /// </summary>
        private void ApplyDebugStyle()
        {
            if (appliedDebugStyle == debugStyle) return;

            if (appliedDebugStyle != OutlineStyle.None) SetIntent(appliedDebugStyle, false);
            appliedDebugStyle = debugStyle;
            if (appliedDebugStyle != OutlineStyle.None) SetIntent(appliedDebugStyle, true);
        }

        private void OnEnable()
        {
            if (renderers == null || renderers.Length == 0) RebuildFromChildren();

            // Deliberately does not register on its own. A group earns its place
            // in the registry by having a style, not by existing. See
            // OutlineRegistry.
            ApplyDebugStyle();
            Registry.SyncStyle(this);
        }

        private void OnDisable()
        {
            Registry.Remove(this);
        }

#if UNITY_EDITOR
        // Editor only, so the debug field can be changed live in play mode and
        // take effect immediately. Ships as nothing.
        private void Update()
        {
            ApplyDebugStyle();
        }
#endif
    }
}
