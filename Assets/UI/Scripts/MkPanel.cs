using System;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>The single lobby close control for centered panels and bottom sheets.</summary>
    internal static class LobbyCloseButton
    {
        public static Button Create(Action close)
        {
            var button = new Button(close) { text = "", tooltip = "Close" };
            button.AddToClassList("ui-reset-button"); button.AddToClassList("mk-close");
            return button;
        }
    }

    /// <summary>A fixed modal card; only lists placed inside Body may scroll.</summary>
    [UxmlElement]
    public partial class MkPanel : VisualElement
    {
        public VisualElement Surface { get; } = new VisualElement();
        public VisualElement Scrim { get; } = new VisualElement();
        public VisualElement Body { get; } = new VisualElement();
        public Label Title { get; } = new Label { enableRichText = false };
        public Button CloseButton { get; }
        public override VisualElement contentContainer => Body;
        public bool IsOpen { get; private set; }
        public event Action Opened;
        public event Action Closed;
        private LobbyOverlayRouter router;
        private string route;
        private readonly LobbyElementMotion motion;

        public MkPanel()
        {
            AddToClassList("mk-panel-host"); AddToClassList("lb-no-swipe");
            Scrim.AddToClassList("mk-scrim"); hierarchy.Add(Scrim);
            Surface.AddToClassList("mk-panel"); hierarchy.Add(Surface);
            var header = new VisualElement(); header.AddToClassList("mk-panel__header");
            Title.AddToClassList("mk-h2"); Title.AddToClassList("mk-ellipsis"); header.Add(Title);
            CloseButton = LobbyCloseButton.Create(Close); CloseButton.name = "panel-close";
            CloseButton.AddToClassList("mk-panel__close"); header.Add(CloseButton);
            Surface.Add(header); Body.AddToClassList("mk-panel__body"); Surface.Add(Body);
            Scrim.RegisterCallback<PointerDownEvent>(evt => { Close(); evt.StopPropagation(); });
            style.display = DisplayStyle.None;
            motion = new LobbyElementMotion(Surface, Scrim);
            motion.Set(0f, 0f, LobbyMotion.ClosedScale);
        }
        public void BindRouter(LobbyOverlayRouter value, string id) { router = value; route = id; }
        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true; style.display = DisplayStyle.Flex; BringToFront();
            LobbyScrollbars.Hide(Body);
            if (router != null) router.Open(new LobbyOverlay(route, OverlayKind.Panel, true), Close);
            Opened?.Invoke();
            SetEnabled(true);
            motion.Open();
        }
        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false; SetEnabled(false);
            motion.Close(() => style.display = DisplayStyle.None);
            if (router != null) router.Closed(route);
            Closed?.Invoke();
        }
    }
}
