using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NodeWar.Backend;
using NodeWar.Simulation;
using NodeWar.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The suit tree (docs/suit-tree-spec.md section 6), drawn over the Workshop: arena
    /// tabs, a strip of suits, the selected suit's tree as nodes on a graph with one
    /// labelled band per arena gate, and an info panel in the lobby's bottom sheet.
    ///
    /// What each node is and what it says comes from SuitTreeModel, which has no
    /// UnityEngine and is tested; this class only draws it. It owns no rules and writes
    /// nothing: Equip goes back to the Workshop's own equip path through a callback, and
    /// the view re-reads the player state it is handed afterwards. Unlock is shown
    /// disabled, because no currency exists yet.
    ///
    /// The element skeleton is authored in WorkshopPage.uxml; the tabs, strip, bands,
    /// nodes and stat rows are built here. BandHeight and the node geometry below match
    /// SuitTree.uss, and edge endpoints are computed from them rather than measured.
    /// </summary>
    public sealed class SuitTreeView
    {
        private const float BandHeight = 148f;
        private const float NodeTop = 20f;
        private const float DiscRadius = 36f;
        private float discSize = DiscRadius * 2f;
        private float lineWidth = 4f;
        private Color edgeIdle = EdgeIdle, edgeOwned = EdgeOwned;
        private const float EdgeGap = 5f;
        private const float SheetClearance = 260f;

        private static readonly Color EdgeIdle = new Color(0.59f, 0.50f, 0.37f, 1f);
        private static readonly Color EdgeOwned = new Color(0.87f, 0.67f, 0.16f, 1f);

        private readonly VisualElement page;
        private readonly VisualElement tabs;
        private readonly VisualElement strip;
        private readonly ScrollView scroll;
        private readonly VisualElement canvas;
        private readonly VisualElement info;
        private readonly Label infoTitle;
        private readonly Label infoRole;
        private readonly VisualElement infoStats;
        private readonly Label infoFits;
        private readonly Label infoHow;
        private readonly Button infoPrimary;
        private readonly LobbySheet sheet;
        private readonly Func<PlayerState> getState;
        private readonly Func<GameBalanceData> getBalance;
        private readonly Func<string, string> describe;
        private readonly Func<string, string, Task<bool>> equip;

        private int tabArena = -1; // -1 is "All"
        private string selectedSuit;
        private int selectedVariant = -1;
        private bool busy;

        public bool IsOpen { get; private set; }

        public SuitTreeView(VisualElement pageRoot, LobbySheet sheet, Func<PlayerState> getState,
            Func<GameBalanceData> getBalance, Func<string, string> describe,
            Func<string, string, Task<bool>> equip)
        {
            this.sheet = sheet;
            this.getState = getState;
            this.getBalance = getBalance;
            this.describe = describe;
            this.equip = equip;

            page = pageRoot.Q<VisualElement>("suittree-page");
            tabs = pageRoot.Q<VisualElement>("suittree-tabs");
            strip = pageRoot.Q<VisualElement>("suittree-strip");
            scroll = pageRoot.Q<ScrollView>("suittree-scroll");
            canvas = pageRoot.Q<VisualElement>("suittree-canvas");
            if (canvas != null) canvas.RegisterCallback<CustomStyleResolvedEvent>(OnTreeStyle);
            info = LobbySheet.Lift(pageRoot, "suittree-info");
            if (info != null)
            {
                infoTitle = info.Q<Label>("suittree-info-title");
                infoRole = info.Q<Label>("suittree-info-role");
                infoStats = info.Q<VisualElement>("suittree-info-stats");
                infoFits = info.Q<Label>("suittree-info-fits");
                infoHow = info.Q<Label>("suittree-info-how");
                infoPrimary = info.Q<Button>("suittree-info-primary");
            }

            Button back = pageRoot.Q<Button>("suittree-back");
            if (back != null) back.clicked += Close;
            if (infoPrimary != null) infoPrimary.clicked += () => _ = OnPrimaryAsync();
            if (sheet != null) sheet.Closed += OnSheetClosed;
            if (scroll != null) scroll.contentContainer.style.paddingBottom = SheetClearance;
        }

        /// <summary>False when the layout lacks the overlay, so the Workshop can hide its button.</summary>
        public bool IsWired => page != null && tabs != null && strip != null && canvas != null && info != null;

        public void Open(string preferredBaseId)
        {
            if (!IsWired) return;
            IsOpen = true;
            tabArena = -1;
            List<string> all = SuitTreeModel.TreeSuits();
            selectedSuit = all.Contains(preferredBaseId) ? preferredBaseId : (all.Count > 0 ? all[0] : null);
            selectedVariant = -1;
            page.AddToClassList("st-page--open");
            Render();
            if (scroll != null) scroll.scrollOffset = Vector2.zero;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            selectedVariant = -1;
            if (page != null) page.RemoveFromClassList("st-page--open");
            if (sheet != null && sheet.IsShowing(info)) sheet.Close();
        }

        /// <summary>Re-reads the player state; the Workshop calls this after anything changes it.</summary>
        public void Refresh()
        {
            if (IsOpen) Render();
        }

        // ===== RENDER =====

        private void Render()
        {
            RenderTabs();
            RenderStrip();
            RenderTree();
            RenderInfo();
        }

        private List<string> VisibleSuits() =>
            tabArena < 0 ? SuitTreeModel.TreeSuits() : SuitTreeModel.SuitsDebutingAt(tabArena);

        private void SetTab(int arena)
        {
            tabArena = arena;
            List<string> visible = VisibleSuits();
            if (!visible.Contains(selectedSuit) && visible.Count > 0) SelectSuit(visible[0]);
            else Render();
        }

        private void RenderTabs()
        {
            tabs.Clear();
            AddTab("All", -1);
            foreach (int arena in SuitTreeModel.DebutArenas()) AddTab("Arena " + arena, arena);
        }

        private void AddTab(string text, int arena)
        {
            Button tab = new Button { text = text };
            tab.AddToClassList("ui-reset-button");
            tab.AddToClassList("st-tab");
            tab.AddToClassList("ui-w600");
            tab.EnableInClassList("st-tab--on", tabArena == arena);
            tab.clicked += () => SetTab(arena);
            tabs.Add(tab);
        }

        private void RenderStrip()
        {
            strip.Clear();
            foreach (string baseId in VisibleSuits())
            {
                string captured = baseId;
                string name = SuitTreeModel.SuitName(baseId);
                Button suit = new Button();
                suit.AddToClassList("ui-reset-button");
                suit.AddToClassList("st-suit");
                suit.EnableInClassList("st-suit--on", baseId == selectedSuit);
                suit.Add(MakeLabel(name.Length > 0 ? name.Substring(0, 1) : "?", "st-suit__letter", "ui-w600"));
                suit.Add(MakeLabel(name, "st-suit__name", "ui-w500"));
                suit.clicked += () => SelectSuit(captured);
                strip.Add(suit);
            }
        }

        private void SelectSuit(string baseId)
        {
            selectedSuit = baseId;
            selectedVariant = -1;
            if (sheet != null && sheet.IsShowing(info)) sheet.Close();
            Render();
        }

        private void RenderTree()
        {
            canvas.Clear();
            if (selectedSuit == null) return;

            SuitNodeView[] nodes = SuitTreeModel.Nodes(getState(), selectedSuit);
            SuitNodeLayout[] layout = SuitTreeModel.Layout(selectedSuit);
            List<int> bandArenas = SuitTreeModel.BandArenas(selectedSuit);

            canvas.style.height = bandArenas.Count * BandHeight;

            for (int b = 0; b < bandArenas.Count; b++)
            {
                VisualElement band = new VisualElement();
                band.AddToClassList("st-band");
                band.EnableInClassList("st-band--odd", b % 2 == 1);
                band.style.top = b * BandHeight;
                band.pickingMode = PickingMode.Ignore;
                band.Add(MakeLabel("Arena " + bandArenas[b], "st-band__label", "ui-w600"));
                canvas.Add(band);
            }

            VisualElement edges = new VisualElement();
            edges.AddToClassList("st-edges");
            edges.pickingMode = PickingMode.Ignore;
            edges.generateVisualContent += ctx => DrawEdges(ctx, edges, nodes, layout);
            edges.RegisterCallback<GeometryChangedEvent>(_ => edges.MarkDirtyRepaint());
            canvas.Add(edges);

            for (int i = 0; i < nodes.Length; i++) canvas.Add(BuildNode(nodes[i], layout[i]));
        }

        private VisualElement BuildNode(SuitNodeView node, SuitNodeLayout place)
        {
            Button button = new Button();
            button.AddToClassList("ui-reset-button");
            button.AddToClassList("st-node");
            button.AddToClassList("st-node--" + node.State.ToString().ToLowerInvariant());
            button.EnableInClassList("st-node--selected", node.Variant == selectedVariant);
            button.style.left = Length.Percent(place.X * 100f);
            button.style.top = place.Band * BandHeight + NodeTop;

            VisualElement ring = new VisualElement();
            ring.AddToClassList("st-node__ring");
            ring.pickingMode = PickingMode.Ignore;
            button.Add(ring);

            VisualElement disc = new VisualElement();
            disc.AddToClassList("st-node__disc");
            disc.pickingMode = PickingMode.Ignore;
            LobbyIcon glyph = new LobbyIcon(GlyphFor(node.State));
            glyph.AddToClassList("lb-icon");
            glyph.AddToClassList("st-node__glyph");
            glyph.pickingMode = PickingMode.Ignore;
            disc.Add(glyph);
            button.Add(disc);

            button.Add(MakeLabel(SuitTreeModel.NodeName(node.BaseId, node.Variant), "st-node__name", "ui-w600"));
            button.Add(MakeLabel(StateWord(node), "st-node__state", "ui-w500"));

            int variant = node.Variant;
            button.clicked += () => SelectNode(variant);
            ApplyNodeSize(button);
            return button;
        }

        // Shape carries the state as well as colour: see SuitTree.uss.
        private static LobbyIconKind GlyphFor(SuitNodeState state)
        {
            switch (state)
            {
                case SuitNodeState.Equipped: return LobbyIconKind.Diamond;
                case SuitNodeState.Owned: return LobbyIconKind.Pip;
                case SuitNodeState.Available: return LobbyIconKind.Spark;
                default: return LobbyIconKind.Lock;
            }
        }

        private static string StateWord(SuitNodeView node)
        {
            switch (node.State)
            {
                case SuitNodeState.Equipped: return "Equipped";
                case SuitNodeState.Owned: return "Owned";
                case SuitNodeState.Available: return "Available";
                default: return node.Owned ? "Owned, arena " + node.Arena : "Arena " + node.Arena;
            }
        }

        private void DrawEdges(MeshGenerationContext ctx, VisualElement host, SuitNodeView[] nodes,
            SuitNodeLayout[] layout)
        {
            float width = host.contentRect.width;
            if (width <= 0f) return;
            Painter2D painter = ctx.painter2D;
            painter.lineWidth = lineWidth;
            painter.lineCap = LineCap.Round;

            for (int i = 0; i < layout.Length; i++)
            {
                if (layout[i].ParentVariant < 0) continue;
                int parent = Array.FindIndex(layout, l => l.Variant == layout[i].ParentVariant);
                if (parent < 0) continue;

                Vector2 from = Center(layout[parent], width);
                Vector2 to = Center(layout[i], width);
                Vector2 direction = (to - from).normalized;
                painter.strokeColor = nodes[i].Owned ? edgeOwned : edgeIdle;
                painter.BeginPath();
                float inset = Mathf.Min(discSize * 0.5f + EdgeGap, Vector2.Distance(from, to) * 0.5f);
                painter.MoveTo(from + direction * inset);
                painter.LineTo(to - direction * inset);
                painter.Stroke();
            }
        }

        private Vector2 Center(SuitNodeLayout place, float width) =>
            new Vector2(place.X * width, place.Band * BandHeight + NodeTop + discSize * 0.5f);

        private void OnTreeStyle(CustomStyleResolvedEvent evt)
        {
            var style = evt.customStyle;
            edgeIdle = EdgeIdle; edgeOwned = EdgeOwned;
            ResourceRingColors.TryRead(style, "--tree-edge-color", ref edgeIdle);
            ResourceRingColors.TryRead(style, "--tree-edge-owned-color", ref edgeOwned);
            lineWidth = style.TryGetValue(new CustomStyleProperty<float>("--tree-line-width"), out float width)
                ? UiArtMath.Geometry(width, 4f, 1f, 16f) : 4f;
            discSize = style.TryGetValue(new CustomStyleProperty<float>("--tree-disc-size"), out float size)
                ? UiArtMath.Geometry(size, 72f, 24f, 92f) : 72f;
            canvas.Query<Button>(className: "st-node").ForEach(ApplyNodeSize);
            canvas.Query<VisualElement>(className: "st-edges").ForEach(e => e.MarkDirtyRepaint());
        }

        private void ApplyNodeSize(Button button)
        {
            var disc = button.Q<VisualElement>(className: "st-node__disc");
            if (disc != null)
            {
                disc.style.width = disc.style.height = discSize;
                float radius = discSize * (button.ClassListContains("st-node--available") ? 0.25f : 0.5f);
                disc.style.borderTopLeftRadius = disc.style.borderTopRightRadius = radius;
                disc.style.borderBottomLeftRadius = disc.style.borderBottomRightRadius = radius;
            }
            var ring = button.Q<VisualElement>(className: "st-node__ring");
            if (ring != null)
            {
                ring.style.width = ring.style.height = discSize + 20f;
                ring.style.left = (112f - discSize - 20f) * 0.5f;
                ring.style.borderTopLeftRadius = ring.style.borderTopRightRadius = (discSize + 20f) * 0.5f;
                ring.style.borderBottomLeftRadius = ring.style.borderBottomRightRadius = (discSize + 20f) * 0.5f;
            }
        }

        private static Label MakeLabel(string text, string styleClass, string weightClass)
        {
            Label label = new Label(text);
            label.AddToClassList(styleClass);
            label.AddToClassList(weightClass);
            label.pickingMode = PickingMode.Ignore;
            return label;
        }

        // ===== INFO PANEL =====

        private void SelectNode(int variant)
        {
            selectedVariant = variant;
            RenderTree();
            RenderInfo();
            if (sheet != null && info != null) sheet.Open(info);
        }

        private void OnSheetClosed(VisualElement content)
        {
            if (content != info) return;
            selectedVariant = -1;
            if (IsOpen) RenderTree();
        }

        private SuitNodeView SelectedNode(out SuitNodeView[] all)
        {
            all = selectedSuit == null ? new SuitNodeView[0] : SuitTreeModel.Nodes(getState(), selectedSuit);
            foreach (var node in all)
                if (node.Variant == selectedVariant) return node;
            return null;
        }

        private void RenderInfo()
        {
            SuitNodeView node = SelectedNode(out SuitNodeView[] all);
            if (node == null || infoTitle == null) return;

            string suitName = SuitTreeModel.SuitName(node.BaseId);
            GameBalanceData balance = getBalance();
            StatRow[] rows = SuitTreeModel.StatRows(balance, node.BaseId, node.Variant, node.ParentVariant);

            infoTitle.text = SuitTreeModel.NodeName(node.BaseId, node.Variant) + "  -  arena " + node.Arena;
            infoRole.text = SuitTreeModel.RoleLine(rows, node.IsRoot, suitName, describe != null ? describe(node.BaseId) : null);

            infoStats.Clear();
            foreach (StatRow row in rows) infoStats.Add(BuildStatRow(row));

            List<DistrictType> districts = SuitTreeModel.EquipDistricts(balance, node.BaseId);
            infoFits.text = districts.Count == 0
                ? "Equips at: no district"
                : "Equips at: " + string.Join(", ", districts);
            infoHow.text = "How to get it: " + SuitTreeModel.HowToGet(node, all);

            SuitNodeButton button = SuitTreeModel.Button(node);
            infoPrimary.text = busy ? "Saving..." : button.Label;
            infoPrimary.SetEnabled(button.Enabled && !busy);
        }

        private static VisualElement BuildStatRow(StatRow row)
        {
            VisualElement line = new VisualElement();
            line.AddToClassList("st-stat");
            line.pickingMode = PickingMode.Ignore;
            line.Add(MakeLabel(row.Label, "st-stat__label", "ui-w500"));
            line.Add(MakeLabel(row.Value.ToString(), "st-stat__value", "ui-w600"));

            string delta = row.HasParent
                ? row.Trend == StatTrend.Same ? row.DeltaText
                    : row.DeltaText + (row.Trend == StatTrend.Better ? " better" : " worse")
                : "";
            Label deltaLabel = MakeLabel(delta, "st-stat__delta", "ui-w600");
            deltaLabel.EnableInClassList("st-stat__delta--better", row.Trend == StatTrend.Better);
            deltaLabel.EnableInClassList("st-stat__delta--worse", row.Trend == StatTrend.Worse);
            line.Add(deltaLabel);
            return line;
        }

        private async Task OnPrimaryAsync()
        {
            SuitNodeView node = SelectedNode(out _);
            if (node == null || busy) return;
            SuitNodeButton button = SuitTreeModel.Button(node);
            if (!button.Enabled) return;

            if (button.Action == SuitNodeAction.Equip)
            {
                busy = true;
                RenderInfo();
                // The Workshop reports a failed save itself, so a refusal needs no second toast.
                try { await equip(node.BaseId, node.Id); }
                catch (Exception) { }
                busy = false;
                if (!IsOpen) return;
                Render();
            }
        }
    }
}
