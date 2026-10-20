using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The game mode page: a full-screen push page holding a horizontal pager of Ranked, Unranked and Other.
    /// The category switch and horizontal swipes drive one spring; the Private 1v1 toggle is pinned below.
    /// </summary>
    public sealed class LobbyModePicker : LobbyPushPage
    {
        private readonly LobbyToggle privateToggle = new LobbyToggle();
        private readonly SegmentedSwitch tabs;
        private readonly VisualElement viewport = new VisualElement { name = "mode-pager" };
        private readonly VisualElement[] pages = new VisualElement[3];
        private LobbyMotion.Spring position;
        private LobbyModeSelection selection;
        private int current, pointer = -1;
        private bool dragging;
        private Vector2 start;
        private long startedAt;
        private float startPosition;
        private double lastTime;
        public event Action<LobbyModeSelection> Selected;

        public LobbyModePicker(VisualElement host, LobbyOverlayRouter router)
            : base("game-mode-page", null, "")
        {
            Root.Clear();
            Root.AddToClassList("lb-modepage"); Root.AddToClassList("lb-no-swipe");
            Root.styleSheets.Add(Resources.Load<StyleSheet>("Lobby/ModePicker"));
            host.Add(Root);

            var header = new VisualElement(); header.AddToClassList("lb-push__header");
            var back = new Button(Close) { name = "push-back", tooltip = "Back" };
            back.AddToClassList("ui-reset-button"); back.AddToClassList("lb-back");
            back.Add(new LobbyIcon(LobbyIconKind.Back, LobbyIconContext.PageHeader) { pickingMode = PickingMode.Ignore });
            header.Add(back); header.Add(GuildViews.Text("Game mode", "lb-push__title")); Root.Add(header);

            var faces = new Button[3];
            for (int i = 0; i < faces.Length; i++) faces[i] = new Button { text = LobbyModeCategories.Title(LobbyModeCategories.All[i]) };
            tabs = new SegmentedSwitch(faces); tabs.AddToClassList("lb-modepage__tabs"); tabs.Changed += index => GoTo(index, true);
            Root.Add(tabs);

            viewport.AddToClassList("lb-modepage__pager"); Root.Add(viewport);
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i] = new VisualElement(); pages[i].AddToClassList("lb-modepage__page");
                var grid = ModeGrid("mode-grid-" + i); pages[i].Add(grid);
                foreach (var mode in LobbyModeCategories.ModesIn(LobbyModeCategories.All[i]))
                    AddCard(grid, mode);
                viewport.Add(pages[i]);
            }
            viewport.RegisterCallback<GeometryChangedEvent>(_ => Apply());
            viewport.RegisterCallback<PointerDownEvent>(Down);
            viewport.RegisterCallback<PointerMoveEvent>(Move);
            viewport.RegisterCallback<PointerUpEvent>(Up);
            viewport.RegisterCallback<PointerCancelEvent>(_ => Release(0, 0, 0));

            var row = new Button(privateToggle.Flip) { name = "mode-private" };
            row.AddToClassList("ui-reset-button"); row.AddToClassList("lb-modepage__private");
            row.Add(GuildViews.Text("Private 1v1")); row.Add(privateToggle); Root.Add(row);
            privateToggle.Changed += value =>
            {
                selection = new LobbyModeSelection(selection.Mode, value);
                Selected?.Invoke(selection);
            };

            Opened += () =>
            {
                Root.BringToFront();
                router.Open(new LobbyOverlay("game-mode", OverlayKind.Fullscreen, true, true), Close);
            };
            Closed += () => router.Closed("game-mode");
            Root.RegisterCallback<AttachToPanelEvent>(_ => lastTime = Time.realtimeSinceStartupAsDouble);
            Root.schedule.Execute(Tick).Every(16);
        }

        private void AddCard(VisualElement grid, LobbyMode mode)
        {
            var card = ModeCard(new LobbyModeSelection(mode), () =>
            {
                selection = new LobbyModeSelection(mode, privateToggle.Value);
                Selected?.Invoke(selection); Close();
            });
            card.userData = mode; grid.Add(card);
        }

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
        {
            var display = new HomeModeDisplay(mode);
            var card = new Button(selected); card.AddToClassList("mk-btn"); card.AddToClassList("g2-mode-card");
            var bar = new VisualElement(); bar.AddToClassList("g2-mode-colour");
            if (ColorUtility.TryParseHtmlString(display.Colour, out Color colour)) bar.style.backgroundColor = colour;
            card.Add(bar); card.Add(new LobbyIcon(display.Icon, LobbyIconContext.Home));
            card.Add(GuildViews.Text(display.Name, "mk-h3")); return card;
        }

        public void Open(LobbyModeSelection chosen)
        {
            selection = chosen;
            Root.Query<Button>(className: "g2-mode-card").ForEach(card => card.EnableInClassList("g2-mode-card--on", card.userData is LobbyMode mode && mode == chosen.Mode));
            privateToggle.SetValueWithoutNotify(chosen.Private);
            GoTo(LobbyModeCategories.IndexOf(LobbyModeCategories.Of(chosen)), false);
            Open();
        }

        private void GoTo(int index, bool animate)
        {
            current = Mathf.Clamp(index, 0, pages.Length - 1);
            tabs.Select(current);
            position.Target = current;
            if (!animate) { position = new LobbyMotion.Spring(current); Apply(); }
        }

        private void Tick()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            float dt = (float)(now - lastTime); lastTime = now;
            if (dragging || position.Settled) return;
            position.Step(dt); Apply();
        }

        private void Apply()
        {
            float width = viewport.layout.width;
            if (float.IsNaN(width) || width <= 0) return;
            for (int i = 0; i < pages.Length; i++) pages[i].style.translate = new Translate((i - position.Position) * width, 0);
        }

        private void Down(PointerDownEvent evt)
        {
            if (!evt.isPrimary || (evt.pointerType == UnityEngine.UIElements.PointerType.mouse && evt.button != 0)) return;
            pointer = evt.pointerId; start = evt.position; startedAt = evt.timestamp; startPosition = position.Position;
            dragging = false;
        }

        private void Move(PointerMoveEvent evt)
        {
            if (evt.pointerId != pointer) return;
            Vector2 delta = (Vector2)evt.position - start;
            if (!dragging)
            {
                var axis = SwipeDecision.Lock(delta.x, delta.y);
                if (axis == SwipeAxis.Pending) return;
                if (axis == SwipeAxis.Vertical) { pointer = -1; return; }
                dragging = true; LobbyMouseDrag.Capture(viewport, pointer);
            }
            float raw = startPosition - delta.x / Mathf.Max(1f, viewport.layout.width);
            float bounded = Mathf.Clamp(raw, 0f, pages.Length - 1);
            position.Position = bounded + (raw - bounded) * 0.35f; position.Velocity = 0f;
            Apply(); evt.StopPropagation();
        }

        private void Up(PointerUpEvent evt)
        {
            if (evt.pointerId != pointer) return;
            Vector2 delta = (Vector2)evt.position - start;
            Release(delta.x, delta.y, evt.timestamp - startedAt);
        }

        private void Release(float dx, float dy, long elapsed)
        {
            int id = pointer; pointer = -1;
            if (id >= 0 && viewport.HasPointerCapture(id)) viewport.ReleasePointer(id);
            if (!dragging) return;
            dragging = false;
            int offset = SwipeDecision.Decide(dx, dy, elapsed, false, false, SwipeAxis.Horizontal);
            GoTo(current + offset, true);
        }
    }
}
