using System;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    [UxmlElement]
    public partial class MkSheet : VisualElement
    {
        public VisualElement Body { get; private set; } = new ScrollView();
        public override VisualElement contentContainer => Body.contentContainer;
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
        private IVisualElementScheduledItem hideLater;

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
            RegisterCallback<PointerDownEvent>(evt => { if (!IsOpen) evt.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
            RegisterCallback<ClickEvent>(evt => { if (!IsOpen) evt.StopImmediatePropagation(); }, TrickleDown.TrickleDown);
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

        public void SetScrollable(bool value)
        {
            if ((Body is ScrollView) == value) return;
            var previous = Body;
            Body = value ? new ScrollView() : new VisualElement();
            Body.name = previous.name;
            Body.style.minHeight = 0; Body.style.flexShrink = 1; Body.style.overflow = Overflow.Hidden;
            foreach (var child in new System.Collections.Generic.List<VisualElement>(previous.Children())) Body.Add(child);
            previous.RemoveFromHierarchy(); Surface.Add(Body);
        }
        public void BindRouter(LobbyOverlayRouter value, string id) { router = value; route = id; }
        public void Open(bool large = false)
        {
            Surface.EnableInClassList("mk-bigsheet", large);
            LobbyScrollbars.Hide(Body);
            hideLater?.Pause();
            style.display = DisplayStyle.Flex;
            pickingMode = PickingMode.Position;
            Surface.pickingMode = Scrim.pickingMode = PickingMode.Position;
            schedule.Execute(() => { if (IsOpen) Surface.AddToClassList("mk-sheet--open"); }).ExecuteLater(16);
            if (IsOpen) return;
            IsOpen = true;
            if (router != null) router.Open(new LobbyOverlay(route, OverlayKind.Sheet, true), Close);
            Opened?.Invoke();
        }
        public void Close()
        {
            if (!IsOpen) return;
            Release(); IsOpen = false;
            Surface.RemoveFromClassList("mk-sheet--open");
            pickingMode = Surface.pickingMode = Scrim.pickingMode = PickingMode.Ignore;
            hideLater = schedule.Execute(() => style.display = DisplayStyle.None);
            hideLater.ExecuteLater(280);
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
