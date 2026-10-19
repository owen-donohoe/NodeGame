using System;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    [UxmlElement]
    public partial class MkSheet : VisualElement
    {
        public VisualElement Body { get; } = new ScrollView();
        public VisualElement Surface { get; } = new VisualElement();
        public VisualElement Scrim { get; } = new VisualElement();
        public VisualElement Handle { get; } = new VisualElement();
        public bool IsOpen { get; private set; }
        public event Action Opened;
        public event Action Closed;
        private int pointer = -1;
        private float startY;
        private LobbyOverlayRouter router;
        private string route;

        public MkSheet()
        {
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0;
            AddToClassList("lb-no-swipe");
            Scrim.AddToClassList("mk-scrim"); hierarchy.Add(Scrim);
            Surface.AddToClassList("mk-sheet"); hierarchy.Add(Surface);
            Handle.AddToClassList("mk-handle-hit"); Surface.Add(Handle);
            var grip = new VisualElement(); grip.AddToClassList("mk-handle"); Handle.Add(grip);
            Body.style.minHeight = 0; Body.style.flexShrink = 1; Surface.Add(Body);
            Scrim.RegisterCallback<PointerDownEvent>(evt => { Close(); evt.StopPropagation(); });
            Handle.RegisterCallback<PointerDownEvent>(evt =>
            {
                pointer = evt.pointerId; startY = evt.position.y; Handle.CapturePointer(pointer); evt.StopPropagation();
            });
            Handle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (evt.pointerId == pointer && evt.position.y - startY > 44f) Close();
            });
            Handle.RegisterCallback<PointerUpEvent>(_ => Release());
            Handle.RegisterCallback<PointerCancelEvent>(_ => Release());
            style.display = DisplayStyle.None;
        }

        public void BindRouter(LobbyOverlayRouter value, string id) { router = value; route = id; }
        public void Open(bool large = false)
        {
            Surface.EnableInClassList("mk-bigsheet", large);
            LobbyScrollbars.Hide(Body);
            style.display = DisplayStyle.Flex;
            if (IsOpen) return;
            IsOpen = true;
            if (router != null) router.Open(new LobbyOverlay(route, OverlayKind.Sheet, true), Close);
            Opened?.Invoke();
        }
        public void Close()
        {
            if (!IsOpen) return;
            Release(); IsOpen = false; style.display = DisplayStyle.None;
            if (router != null) router.Closed(route);
            Closed?.Invoke();
        }
        private void Release()
        {
            int id = pointer; pointer = -1;
            if (id >= 0 && Handle.HasPointerCapture(id)) Handle.ReleasePointer(id);
        }
    }
}
