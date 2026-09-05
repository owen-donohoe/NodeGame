using System.Collections.Generic;

namespace NodeWar.View.Outline
{
    /// <summary>
    /// Owns ID allocation and the list of groups the mask pass should draw.
    ///
    /// The key decision here is that <b>a group only holds an ID while it is
    /// actually outlined</b>. Registration is not what grants an ID; a style
    /// other than <see cref="OutlineStyle.None"/> is. That single rule buys
    /// three things at once:
    ///
    /// - Exhaustion stops being reachable in practice. The board has far more
    ///   than 255 nodes and villagers over a match, but the number outlined in
    ///   any one frame is a handful.
    /// - "Nothing is outlined" -- the common case in normal play -- is a list
    ///   with zero entries, so the renderer feature can skip both passes and
    ///   allocate no render target at all.
    /// - Nothing has to be cleaned up when a node or villager goes away, which
    ///   matters because in this project they never do. Nodes are instantiated
    ///   once by GameManager.SpawnNodeViews and never destroyed; a dead villager
    ///   is hidden by VillagerView disabling its SpriteRenderers, not by being
    ///   destroyed or deactivated. OnEnable/OnDisable therefore fire exactly
    ///   once each, at Instantiate, and cannot be relied on as a lifecycle.
    ///
    /// Instance-based with a shared <see cref="Instance"/> rather than a static
    /// class, so tests get an isolated registry instead of inheriting whatever
    /// the previous test left behind.
    /// </summary>
    public sealed class OutlineRegistry
    {
        /// <summary>The registry the runtime uses. Tests construct their own.</summary>
        public static OutlineRegistry Instance { get; } = new OutlineRegistry();

        /// <summary>
        /// Empties the shared registry when play mode starts.
        ///
        /// Today this is belt and braces: ProjectSettings/EditorSettings.asset
        /// has m_EnterPlayModeOptions 0, so the domain still reloads and statics
        /// reset on their own. The guard is here because that is one checkbox
        /// away from changing, and the failure it would cause is nasty and
        /// non-obvious -- groups from the previous session still holding IDs, so
        /// outlines appear on nothing and the pool leaks a little every run.
        /// Billboard.facingInitialized already carries this same latent hazard.
        /// </summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode() => Instance.Clear();

        private readonly OutlineIdAllocator allocator = new OutlineIdAllocator();

        // Capacity chosen so the common case never grows the list. Selecting a
        // large group of villagers is the realistic upper bound in normal play.
        private readonly List<IOutlineGroup> active = new List<IOutlineGroup>(32);

        /// <summary>
        /// The groups currently holding an ID, in no meaningful order.
        ///
        /// For inspection and tests. <b>The mask pass must not foreach over
        /// this</b> -- iterating an IReadOnlyList&lt;T&gt; through the interface
        /// boxes the enumerator, which is a heap allocation every frame in the
        /// one loop that runs every frame. Use <see cref="ActiveCount"/> with
        /// <see cref="GetActive"/> instead.
        /// </summary>
        public IReadOnlyList<IOutlineGroup> Active => active;

        public int ActiveCount => active.Count;

        /// <summary>
        /// Indexed access for the mask pass's hot loop. Allocation-free, unlike
        /// enumerating <see cref="Active"/>.
        /// </summary>
        public IOutlineGroup GetActive(int index) => active[index];

        /// <summary>
        /// True when there is anything at all to draw. The renderer feature
        /// checks this before adding either pass, so an idle frame costs one
        /// integer compare and nothing else.
        /// </summary>
        public bool HasWork => active.Count > 0;

        /// <summary>How many groups have been refused an ID because the pool was full.</summary>
        public int DroppedCount => allocator.DroppedCount;

        /// <summary>
        /// Brings the registry in line with the group's current
        /// <see cref="IOutlineGroup.Style"/>. Called by the group whenever its
        /// resolved style changes, and cheap enough to call when it has not.
        ///
        /// A group moving between two non-None styles -- Selected to Contested,
        /// say -- keeps the ID it already has. That is the stability guarantee:
        /// an ID is constant for as long as a group is continuously outlined.
        /// Colour is looked up by style index rather than by ID, so recycling an
        /// ID later can never change how anything looks.
        /// </summary>
        public void SyncStyle(IOutlineGroup group)
        {
            if (group == null) return;

            bool wantsOutline = group.Style != OutlineStyle.None;
            bool holdsId = group.OutlineId != OutlineIdAllocator.None;

            if (wantsOutline == holdsId) return;

            if (wantsOutline)
            {
                int id = allocator.Rent();

                // Pool exhausted. Drop this group -- it simply goes un-outlined
                // this frame and will try again on its next style change. The
                // alternative, sharing an ID with another group, would delete
                // the line between them and draw a confident lie.
                if (id == OutlineIdAllocator.None) return;

                group.OutlineId = id;
                active.Add(group);
                return;
            }

            allocator.Return(group.OutlineId);
            group.OutlineId = OutlineIdAllocator.None;
            active.Remove(group);
        }

        /// <summary>
        /// Drops a group entirely, releasing its ID if it holds one. For
        /// OnDisable and OnDestroy. A no-op for a group that was never
        /// outlined, so it is always safe to call.
        /// </summary>
        public void Remove(IOutlineGroup group)
        {
            if (group == null) return;
            if (group.OutlineId == OutlineIdAllocator.None) return;

            allocator.Return(group.OutlineId);
            group.OutlineId = OutlineIdAllocator.None;
            active.Remove(group);
        }

        /// <summary>Releases everything. For scene teardown and test isolation.</summary>
        public void Clear()
        {
            for (int i = 0; i < active.Count; i++)
            {
                if (active[i] != null) active[i].OutlineId = OutlineIdAllocator.None;
            }

            active.Clear();
            allocator.Clear();
        }
    }
}
