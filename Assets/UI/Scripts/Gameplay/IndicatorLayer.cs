using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using NodeWar.Lobby;
using NodeWar.View;

namespace NodeWar.UI
{
    /// <summary>
    /// Draws IndicatorDirector's list over the board: an icon over each subject
    /// while it is in the middle of the view, and at the edge of the board with
    /// an arrow while it is not.
    ///
    /// TWO RECTANGLES, ON PURPOSE. Whether a subject is "in view" is measured
    /// against the board the player can see -- the HUD's board space, less the
    /// node sheet while it is open. Where an edge icon may sit is that same
    /// area inset from the control docks, so an arrow never lands on the zoom
    /// handle or the gear. Measuring the zone against the inset area instead
    /// would pull the player's centre of attention off the centre of the screen.
    ///
    /// PICKING IS LOAD-BEARING, as everywhere on this surface. The layer and
    /// every in-view icon ignore the pointer, so they can sit over the board
    /// without stealing a tap. Only a visible edge icon picks, and a tap on one
    /// moves the camera to its subject. PointerGestureSource asks the
    /// EventSystem whether a press landed on UI, so an edge icon also stops that
    /// press reaching the board beneath it.
    ///
    /// MOTION IS USS. An icon arrives by adding .ind--in, whose transition is
    /// ease-out-back, and leaves by removing it, falling back to .ind's ease-in:
    /// the transition in force is always the target style's, so the same scale
    /// property overshoots on the way in and tapers on the way out.
    /// </summary>
    public class IndicatorLayer
    {
        // The element box, which is also the tap target, so no smaller than a
        // fingertip on the 390-wide reference frame.
        private const float Box = 44f;

        // How far an edge icon's centre stays inside the clamp rect: half the
        // box, so it never hangs off.
        private const float EdgeMargin = Box * 0.5f;

        // Kept clear between an edge icon and a dock or the sheet.
        private const float DockGap = 6f;

        // A new element has no computed scale to transition from until it has
        // been styled once, so .ind--in is added a frame after it is created.
        private const long PopDelayMs = 16;

        // Long enough for the ease-in taper in HUD.uss to finish before the
        // element is recycled.
        private const long RecycleAfterMs = 220;

        private readonly VisualElement layer;
        private readonly VisualElement boardSpace;
        private readonly List<VisualElement> rightDocks = new List<VisualElement>();
        private readonly List<VisualElement> leftDocks = new List<VisualElement>();
        private System.Func<VisualElement> openSheet;

        private IndicatorDirector director;
        private NodeWar.Core.CameraController cameraController;
        private Camera cam;
        private bool suppressed;

        private readonly Dictionary<int, Entry> entries = new Dictionary<int, Entry>();
        private readonly Stack<Entry> pool = new Stack<Entry>();
        private readonly List<int> gone = new List<int>();

        // Edge clustering scratch, grown on demand.
        private Entry[] edge = new Entry[16];
        private float[] xs = new float[16];
        private float[] ys = new float[16];
        private int[] priorities = new int[16];
        private int[] order = new int[16];
        private int[] leaderOf = new int[16];

        private class Entry
        {
            public VisualElement root;
            public VisualElement pointer;
            public LobbyIcon icon;
            public Label count;

            public ActiveIndicator model;
            public IndicatorZone zone = IndicatorZone.Edge;
            public Vector3 ground;

            public bool ready;
            public bool visible;
            public bool small;
            public bool pointing;
            public bool tappable;
            public float x;
            public float y;
            public float angle;
            public int followers;

            public string kindClass;
            public string tierClass;

            // Bumped on every acquire and retire, so a timer scheduled for one
            // life of this element can tell it no longer applies to the next.
            public int generation;
        }

        public IndicatorLayer(VisualElement hudRoot)
        {
            layer = new VisualElement();
            layer.name = "hud-indicators";
            layer.AddToClassList("ind-layer");
            layer.pickingMode = PickingMode.Ignore;

            // First child, so every readout, dock, sheet and card on the HUD
            // draws over the indicators rather than under them.
            hudRoot.Insert(0, layer);

            boardSpace = hudRoot.Q<VisualElement>(className: "hud__board-space");
        }

        /// <summary>A control hugging the right edge. Edge icons stay to its left.</summary>
        public void AvoidRight(VisualElement dock)
        {
            if (dock != null) rightDocks.Add(dock);
        }

        /// <summary>A control hugging the left edge. Edge icons stay to its right.</summary>
        public void AvoidLeft(VisualElement dock)
        {
            if (dock != null) leftDocks.Add(dock);
        }

        /// <summary>Answers with the node sheet's panel while it is open, or null.</summary>
        public void SetSheet(System.Func<VisualElement> openSheetQuery)
        {
            openSheet = openSheetQuery;
        }

        public void Bind(IndicatorDirector indicatorDirector)
        {
            director = indicatorDirector;
        }

        public void SetCameraController(NodeWar.Core.CameraController controller)
        {
            cameraController = controller;
        }

        /// <summary>Reduced motion: the pop arrives without its overshoot.</summary>
        public void SetCalm(bool calm)
        {
            layer.EnableInClassList("ind-layer--calm", calm);
        }

        /// <summary>
        /// The match is over. Everything tapers away and nothing new appears;
        /// the end card is the only thing worth reading now.
        /// </summary>
        public void Suppress()
        {
            if (suppressed) return;
            suppressed = true;

            gone.Clear();
            foreach (KeyValuePair<int, Entry> pair in entries) gone.Add(pair.Key);
            for (int i = 0; i < gone.Count; i++) Retire(gone[i]);
        }

        // ===== PER FRAME =====

        public void LateUpdate()
        {
            if (suppressed || director == null || layer.panel == null) return;

            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            if (!TryBoardRects(out PlacementRect zoneRect, out PlacementRect clampRect)) return;

            IndicatorSettings settings = director.Settings;
            IReadOnlyList<ActiveIndicator> active = director.Active;

            // Anything the director no longer lists leaves.
            gone.Clear();
            foreach (KeyValuePair<int, Entry> pair in entries)
            {
                if (!Lists(active, pair.Key)) gone.Add(pair.Key);
            }
            for (int i = 0; i < gone.Count; i++) Retire(gone[i]);

            int edgeCount = 0;

            for (int i = 0; i < active.Count; i++)
            {
                ActiveIndicator model = active[i];
                if (!model.shown) continue;

                if (!entries.TryGetValue(model.id, out Entry entry))
                {
                    entry = Acquire(model);
                    entries[model.id] = entry;
                }

                if (!Place(entry, settings, zoneRect, clampRect)) continue;

                if (entry.visible && entry.zone == IndicatorZone.Edge)
                {
                    EnsureEdgeCapacity(edgeCount + 1);
                    edge[edgeCount] = entry;
                    xs[edgeCount] = entry.x;
                    ys[edgeCount] = entry.y;
                    priorities[edgeCount] = Priority(settings, model);
                    edgeCount++;
                }
            }

            // Edge icons that would overlap merge into the most important, and
            // past the cap the least important go.
            IndicatorPlacement.Cluster(xs, ys, priorities, edgeCount,
                                       settings.mergeRadius, settings.maxAtEdge, order, leaderOf);

            for (int i = 0; i < edgeCount; i++) edge[i].followers = 0;

            for (int i = 0; i < edgeCount; i++)
            {
                int leader = leaderOf[i];
                if (leader == i) continue;

                edge[i].visible = false;
                if (leader >= 0) edge[leader].followers++;
            }

            foreach (KeyValuePair<int, Entry> pair in entries) Apply(pair.Value);
        }

        /// <summary>
        /// Projects the subject and decides its form and position. False when it
        /// has nothing to anchor to this frame, in which case it is hidden.
        /// </summary>
        private bool Place(Entry entry, IndicatorSettings settings,
                           PlacementRect zoneRect, PlacementRect clampRect)
        {
            entry.followers = 0;

            if (!director.TryGetAnchor(entry.model, out Vector3 world, out Vector3 ground))
            {
                entry.visible = false;
                return false;
            }

            entry.ground = ground;

            Vector3 screen = cam.WorldToScreenPoint(world);
            bool behind = screen.z < 0f;

            // Screen space is y-up and the panel is y-down.
            Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(layer.panel,
                new Vector2(screen.x, Screen.height - screen.y));

            IndicatorKindSettings rules = settings.For(entry.model.kind);

            entry.zone = IndicatorPlacement.Classify(panelPoint.x, panelPoint.y, behind, zoneRect,
                                                     settings.innerEnter, settings.innerExit, entry.zone);

            if (entry.zone == IndicatorZone.Inner)
            {
                entry.visible = rules.inView != InViewForm.None;
                entry.small = rules.inView == InViewForm.Small;
                entry.x = panelPoint.x;
                entry.y = panelPoint.y;
                entry.pointing = false;
                entry.tappable = false;
                return true;
            }

            IndicatorPlacement.EdgePosition(panelPoint.x, panelPoint.y, behind, clampRect, EdgeMargin,
                                            out entry.x, out entry.y, out entry.pointing, out entry.angle);

            entry.visible = rules.showAtEdge;
            entry.small = false;
            entry.tappable = true;
            return true;
        }

        private void Apply(Entry entry)
        {
            VisualElement root = entry.root;

            root.style.translate = new Translate(entry.x, entry.y);

            bool on = entry.visible && entry.ready;
            root.EnableInClassList("ind--in", on);
            root.EnableInClassList("ind--small", entry.small);
            root.EnableInClassList("ind--pointing", entry.pointing && on);
            root.EnableInClassList("ind--lost", entry.model.lost);
            root.EnableInClassList("ind--grouped", entry.followers > 0 && on);

            if (entry.pointing) entry.pointer.style.rotate = new Rotate(entry.angle);

            if (entry.followers > 0) entry.count.text = (entry.followers + 1).ToString();

            root.pickingMode = on && entry.tappable ? PickingMode.Position : PickingMode.Ignore;
        }

        // ===== GEOMETRY =====

        /// <summary>
        /// zoneRect is the board the player can see; clampRect is that, less the
        /// docks. Both in panel space. False before the HUD has laid out.
        /// </summary>
        private bool TryBoardRects(out PlacementRect zoneRect, out PlacementRect clampRect)
        {
            Rect board = boardSpace != null ? boardSpace.worldBound : layer.worldBound;
            if (!Valid(board))
            {
                board = layer.worldBound;
                if (!Valid(board))
                {
                    zoneRect = clampRect = default;
                    return false;
                }
            }

            zoneRect = new PlacementRect(board.xMin, board.yMin, board.xMax, board.yMax);

            VisualElement sheet = openSheet != null ? openSheet() : null;
            if (sheet != null && Valid(sheet.worldBound))
                zoneRect.yMax = Mathf.Min(zoneRect.yMax, sheet.worldBound.yMin - DockGap);

            clampRect = zoneRect;

            for (int i = 0; i < rightDocks.Count; i++)
            {
                Rect dock = rightDocks[i].worldBound;
                if (Valid(dock)) clampRect.xMax = Mathf.Min(clampRect.xMax, dock.xMin - DockGap);
            }

            for (int i = 0; i < leftDocks.Count; i++)
            {
                Rect dock = leftDocks[i].worldBound;
                if (Valid(dock)) clampRect.xMin = Mathf.Max(clampRect.xMin, dock.xMax + DockGap);
            }

            return zoneRect.Width > 1f && zoneRect.Height > 1f;
        }

        private static bool Valid(Rect r)
        {
            return !float.IsNaN(r.x) && !float.IsNaN(r.y) && r.width > 0f && r.height > 0f;
        }

        // Tier dominates; a node that has just fallen beats anything else of its tier.
        private static int Priority(IndicatorSettings settings, ActiveIndicator model)
        {
            return (int)settings.For(model.kind).tier * 10 + (model.lost ? 5 : 0);
        }

        // ===== ELEMENTS =====

        private Entry Acquire(ActiveIndicator model)
        {
            Entry entry = pool.Count > 0 ? pool.Pop() : Build();

            entry.model = model;
            entry.zone = IndicatorZone.Edge;
            entry.ready = false;
            entry.visible = false;
            entry.followers = 0;
            int life = ++entry.generation;

            SwapClass(entry.root, ref entry.kindClass, KindClass(model.kind));
            SwapClass(entry.root, ref entry.tierClass,
                      TierClass(director.Settings.For(model.kind).tier));
            entry.icon.Kind = IconFor(model.kind);

            layer.Add(entry.root);

            // On the layer's scheduler, never the element's own. An element's
            // timers pause while it is detached and resume when it is attached
            // again, so a timer on a recycled element fires into its next life.
            layer.schedule.Execute(() =>
            {
                if (entry.generation == life) entry.ready = true;
            }).ExecuteLater(PopDelayMs);

            return entry;
        }

        /// <summary>Starts the taper and recycles the element once it has finished.</summary>
        private void Retire(int id)
        {
            if (!entries.TryGetValue(id, out Entry entry)) return;
            entries.Remove(id);

            entry.visible = false;
            entry.model = null;
            int life = ++entry.generation;

            VisualElement root = entry.root;
            root.RemoveFromClassList("ind--in");
            root.RemoveFromClassList("ind--pointing");
            root.RemoveFromClassList("ind--grouped");
            root.pickingMode = PickingMode.Ignore;

            // The layer's scheduler, for the reason given in Acquire, and the
            // generation check so this can only ever recycle the life it retired.
            layer.schedule.Execute(() =>
            {
                if (entry.generation != life) return;

                root.RemoveFromHierarchy();
                root.RemoveFromClassList("ind--lost");
                root.RemoveFromClassList("ind--small");
                pool.Push(entry);
            }).ExecuteLater(RecycleAfterMs);
        }

        private Entry Build()
        {
            Entry entry = new Entry();

            VisualElement root = new VisualElement();
            root.AddToClassList("ind");
            root.pickingMode = PickingMode.Ignore;
            root.usageHints = UsageHints.DynamicTransform;

            // Rotated as a whole about the icon's centre, so the triangle inside
            // it swings round the bubble to face the subject.
            VisualElement pointer = new VisualElement();
            pointer.AddToClassList("ind__pointer");
            pointer.pickingMode = PickingMode.Ignore;

            LobbyIcon pointerIcon = new LobbyIcon(LobbyIconKind.Pointer);
            pointerIcon.AddToClassList("ind__pointer-icon");
            pointer.Add(pointerIcon);

            VisualElement bubble = new VisualElement();
            bubble.AddToClassList("ind__bubble");
            bubble.pickingMode = PickingMode.Ignore;

            LobbyIcon icon = new LobbyIcon();
            icon.AddToClassList("ind__icon");
            bubble.Add(icon);

            Label count = new Label();
            count.AddToClassList("ind__count");
            count.AddToClassList("ui-w700");
            count.pickingMode = PickingMode.Ignore;

            root.Add(pointer);
            root.Add(bubble);
            root.Add(count);

            entry.root = root;
            entry.pointer = pointer;
            entry.icon = icon;
            entry.count = count;

            root.RegisterCallback<ClickEvent>(evt =>
            {
                if (entry.model == null || !entry.tappable) return;

                if (cameraController != null)
                    cameraController.FocusOnWorldPoint(entry.ground);

                evt.StopPropagation();
            });

            return entry;
        }

        private void EnsureEdgeCapacity(int needed)
        {
            if (edge.Length >= needed) return;

            int size = Mathf.Max(needed, edge.Length * 2);
            System.Array.Resize(ref edge, size);
            System.Array.Resize(ref xs, size);
            System.Array.Resize(ref ys, size);
            System.Array.Resize(ref priorities, size);
            System.Array.Resize(ref order, size);
            System.Array.Resize(ref leaderOf, size);
        }

        private static bool Lists(IReadOnlyList<ActiveIndicator> active, int id)
        {
            for (int i = 0; i < active.Count; i++)
                if (active[i].id == id && active[i].shown) return true;
            return false;
        }

        private static void SwapClass(VisualElement element, ref string current, string next)
        {
            if (current == next) return;
            if (current != null) element.RemoveFromClassList(current);
            current = next;
            element.AddToClassList(next);
        }

        private static string KindClass(IndicatorKind kind)
        {
            switch (kind)
            {
                case IndicatorKind.Battle: return "ind--battle";
                case IndicatorKind.NodeUnderAttack: return "ind--attack";
                case IndicatorKind.NodeContested: return "ind--contested";
                case IndicatorKind.ThreatToCore: return "ind--threat";
                case IndicatorKind.ThreatToTerritory: return "ind--incursion";
                case IndicatorKind.Idle: return "ind--idle";
                case IndicatorKind.Respawn: return "ind--respawn";
                default: return "ind--effect";
            }
        }

        private static string TierClass(IndicatorTier tier)
        {
            switch (tier)
            {
                case IndicatorTier.High: return "ind--high";
                case IndicatorTier.Medium: return "ind--medium";
                default: return "ind--low";
            }
        }

        /// <summary>
        /// The one table from indicator to picture. The partial and full
        /// captures share an icon, as asked: tier is what tells them apart.
        /// </summary>
        private static LobbyIconKind IconFor(IndicatorKind kind)
        {
            switch (kind)
            {
                case IndicatorKind.Battle: return LobbyIconKind.Swords;
                case IndicatorKind.NodeUnderAttack: return LobbyIconKind.Capture;
                case IndicatorKind.NodeContested: return LobbyIconKind.Capture;
                case IndicatorKind.ThreatToCore: return LobbyIconKind.Alert;
                case IndicatorKind.ThreatToTerritory: return LobbyIconKind.Alert;
                case IndicatorKind.Idle: return LobbyIconKind.Sleep;
                case IndicatorKind.Respawn: return LobbyIconKind.Respawn;
                default: return LobbyIconKind.Spark;
            }
        }
    }
}
