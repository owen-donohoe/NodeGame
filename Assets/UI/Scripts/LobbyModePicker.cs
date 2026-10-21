using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>One elastic strip of two-row column groups, with a spring snap after scrolling rests.</summary>
    public sealed class LobbyModePicker : LobbyPushPage
    {
        private const float Gap = 12, GroupGap = 24, GroupFrame = 28;
        private readonly LobbyToggle privateToggle = new LobbyToggle();
        private readonly ScrollView strip = new ScrollView(ScrollViewMode.Horizontal);
        private readonly VisualElement[] groups = new VisualElement[3];
        private LobbyModeSelection selection;
        private LobbyMotion.Spring snap;
        private float columnWidth, previousOffset;
        private double lastTime, idleSince;
        private bool held, snapping, placing;
        public event Action<LobbyModeSelection> Selected;

        public LobbyModePicker(VisualElement host, LobbyOverlayRouter router)
            : base("game-mode-page", null, "")
        {
            Root.Clear(); Root.AddToClassList("lb-modepage"); Root.AddToClassList("lb-no-swipe");
            Root.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/ModePicker")); host.Add(Root);
            var header = new VisualElement(); header.AddToClassList("lb-push__header");
            var back = new Button(Close) { name = "push-back", tooltip = "Back" };
            back.AddToClassList("ui-reset-button"); back.AddToClassList("lb-back");
            back.Add(new LobbyIcon(LobbyIconKind.Back, LobbyIconContext.PageHeader) { pickingMode = PickingMode.Ignore });
            header.Add(back); header.Add(GuildViews.Text("Game mode", "lb-push__title")); Root.Add(header);

            var middle = new VisualElement(); middle.AddToClassList("lb-modepage__middle"); Root.Add(middle);
            strip.name = "mode-strip"; strip.AddToClassList("lb-modepage__strip");
            strip.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            strip.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            strip.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            strip.contentContainer.style.flexDirection = FlexDirection.Row;
            middle.Add(strip);
            for (int i = 0; i < groups.Length; i++)
            {
                var category = LobbyModeCategories.All[i];
                var modes = LobbyModeCategories.ModesIn(category);
                var group = new VisualElement(); group.AddToClassList("mk-card"); group.AddToClassList("lb-modepage__group");
                group.AddToClassList("lb-modepage__group--" + category.ToString().ToLowerInvariant());
                groups[i] = group; strip.Add(group);
                group.Add(GuildViews.Text(LobbyModeCategories.Title(category), "lb-modepage__group-title"));
                var columns = new VisualElement(); columns.AddToClassList("lb-modepage__columns"); group.Add(columns);
                for (int c = 0; c < LobbyModeLayout.Columns(modes.Length); c++)
                {
                    var column = new VisualElement(); column.AddToClassList("lb-modepage__column"); columns.Add(column);
                    for (int r = 0; r < LobbyModeLayout.Rows; r++)
                    {
                        if (LobbyModeLayout.IsEmpty(modes.Length, c, r))
                        {
                            var empty = new VisualElement { pickingMode = PickingMode.Ignore };
                            empty.AddToClassList("g2-mode-card"); empty.AddToClassList("g2-mode-card--empty"); column.Add(empty);
                        }
                        else AddCard(column, modes[LobbyModeLayout.ModeIndex(c, r)]);
                    }
                }
            }
            strip.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => LayoutGroups());
            Root.RegisterCallback<PointerDownEvent>(evt => { held = evt.pointerType != UnityEngine.UIElements.PointerType.mouse; snapping = false; }, TrickleDown.TrickleDown);
            Root.RegisterCallback<PointerUpEvent>(_ => { held = false; idleSince = Time.realtimeSinceStartupAsDouble; }, TrickleDown.TrickleDown);
            Root.RegisterCallback<PointerCancelEvent>(_ => held = false, TrickleDown.TrickleDown);
            Root.RegisterCallback<WheelEvent>(_ => { snapping = false; idleSince = Time.realtimeSinceStartupAsDouble; }, TrickleDown.TrickleDown);
            var row = new Button(privateToggle.Flip) { name = "mode-private" };
            row.AddToClassList("ui-reset-button"); row.AddToClassList("lb-modepage__private");
            row.Add(GuildViews.Text("Private")); row.Add(privateToggle); Root.Add(row);
            privateToggle.Changed += value =>
            {
                selection = new LobbyModeSelection(selection.Mode, value);
                RefreshCards(); Selected?.Invoke(selection);
            };
            Opened += () =>
            {
                Root.BringToFront(); router.Open(new LobbyOverlay("game-mode", OverlayKind.Fullscreen, true, true), Close);
            };
            Closed += () => { held = snapping = false; router.Closed("game-mode"); };
            Root.schedule.Execute(Tick).Every(16);
        }

        private void AddCard(VisualElement column, LobbyMode mode)
        {
            var card = ModeCard(ModeDefinition.For(mode), () =>
            {
                if (!ModeDefinition.For(mode).CanSelect(privateToggle.Value)) return;
                selection = new LobbyModeSelection(mode, privateToggle.Value);
                Selected?.Invoke(selection); Close();
            });
            card.userData = mode;
            var meta = GuildViews.Text("Off in private", "lb-modepage__private-meta"); card.Add(meta);
            column.Add(card);
        }

        // Shared with the existing Multiplayer panel.
        internal static VisualElement ModeGrid(string name)
        {
            var grid = new VisualElement { name = name }; grid.AddToClassList("g2-mode-grid");
            grid.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                if (evt.newRect.width <= 16) return;
                for (int i = 0; i < grid.childCount; i++) grid[i].style.width = (evt.newRect.width - 16f) / 2f;
            });
            return grid;
        }
        internal static Button ModeCard(LobbyModeSelection mode, Action selected)
            => ModeCard(mode.Definition, selected);
        private static Button ModeCard(ModeDefinition mode, Action selected)
        {
            var card = new Button(selected); card.AddToClassList("ui-reset-button"); card.AddToClassList("mk-btn"); card.AddToClassList("g2-mode-card");
            card.Add(new LobbyIcon(mode.Icon, LobbyIconContext.Home));
            card.Add(GuildViews.Text(mode.DisplayName, "mk-h3")); return card;
        }
        private void RefreshCards()
        {
            Root.Query<Button>(className: "g2-mode-card").ForEach(card =>
            {
                if (!(card.userData is LobbyMode mode)) return;
                string reason = ModeDefinition.For(mode).DisabledReason(selection.Private);
                bool off = reason != null;
                card.SetEnabled(!off); card.EnableInClassList("g2-mode-card--on", mode == selection.Mode);
                var meta = card.Q<Label>(className: "lb-modepage__private-meta");
                meta.text = reason;
                meta.style.display = off ? DisplayStyle.Flex : DisplayStyle.None;
            });
        }
        public void Open(LobbyModeSelection chosen)
        {
            selection = chosen; privateToggle.SetValueWithoutNotify(chosen.Private); RefreshCards();
            held = snapping = false; placing = true; Open(); LayoutGroups();
        }
        private void LayoutGroups()
        {
            float width = strip.contentViewport.layout.width;
            if (float.IsNaN(width) || width <= 0) return;
            columnWidth = Mathf.Clamp(width * 0.50f, 160, 264);
            // Ranked sits just left of centre; the next group peeks in immediately.
            float inset = Mathf.Max(12, width * 0.38f - (columnWidth + GroupFrame) / 2);
            strip.contentContainer.style.paddingLeft = inset;
            strip.contentContainer.style.paddingRight = Mathf.Max(12, width - inset - columnWidth - GroupFrame);
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i].style.width = LobbyModeLayout.GroupWidth(LobbyModeCategories.ModesIn(LobbyModeCategories.All[i]).Length, columnWidth, Gap) + GroupFrame;
                groups[i].Query(className: "lb-modepage__column").ForEach(c => c.style.width = columnWidth);
            }
            if (placing)
            {
                // Wait for content widths to reach Yoga before assigning scroll offset.
                Root.schedule.Execute(() =>
                {
                    strip.scrollOffset = Vector2.zero;
                    previousOffset = strip.scrollOffset.x; idleSince = Time.realtimeSinceStartupAsDouble; placing = false;
                });
            }
        }
        private void Tick()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float dt = (float)(now - lastTime); lastTime = now;
            if (!IsOpen || held || placing || columnWidth <= 0) return;
            if (LobbyMouseDrag.IsScrolling(strip)) { snapping = false; idleSince = now; previousOffset = strip.scrollOffset.x; return; }
            float offset = strip.scrollOffset.x;
            if (snapping)
            {
                snap.Advance(dt, 1f); strip.scrollOffset = new Vector2(snap.Position, 0);
                if (snap.Settled) { strip.scrollOffset = new Vector2(snap.Target, 0); snapping = false; }
                previousOffset = strip.scrollOffset.x; idleSince = now; return;
            }
            // Let native touch inertia and LobbyMouseDrag finish before owning the offset.
            if (Mathf.Abs(offset - previousOffset) > 0.01f) { previousOffset = offset; idleSince = now; return; }
            if (now - idleSince < 0.2) return;
            float destination = LobbyModeLayout.GroupStart(LobbyModeLayout.NearestGroup(offset, columnWidth, Gap, GroupGap + GroupFrame), columnWidth, Gap, GroupGap + GroupFrame);
            destination = Mathf.Clamp(destination, 0, strip.horizontalScroller.highValue);
            if (Mathf.Abs(destination - offset) < 0.5f) return;
            snap = new LobbyMotion.Spring(offset) { Target = destination }; snapping = true;
        }
    }
}
