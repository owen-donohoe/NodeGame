using System;
using UnityEngine;
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
        public VisualElement Header { get; } = new VisualElement();
        public VisualElement Footer { get; } = new VisualElement();
        public Button CloseButton { get; }
        public bool IsOpen { get; private set; }
        public event Action Opened;
        public event Action Closed;
        private int pointer = -1;
        private float startY;
        private LobbyOverlayRouter router;
        private string route;
        private readonly LobbyElementMotion motion;
        private int openRevision;
        private readonly IVisualElementScheduledItem sizeTick;
        private LobbyMotion.Spring height;
        private double sizeSampled;
        private bool heightPlaced;
        private Label parkedHeading;
        private VisualElement headingOwner;
        private int headingIndex;

        public MkSheet()
        {
            style.position = Position.Absolute;
            style.left = style.right = style.top = style.bottom = 0;
            AddToClassList("lb-no-swipe");
            Scrim.AddToClassList("mk-scrim"); hierarchy.Add(Scrim);
            Surface.AddToClassList("mk-sheet"); hierarchy.Add(Surface);
            Handle.AddToClassList("mk-handle-hit"); Surface.Add(Handle);
            var grip = new VisualElement(); grip.AddToClassList("mk-handle"); Handle.Add(grip);
            CloseButton = LobbyCloseButton.Create(Close); CloseButton.name = "sheet-close";
            CloseButton.AddToClassList("mk-sheet__close"); Handle.Add(CloseButton);
            CloseButton.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            Header.style.flexShrink = 0; Footer.style.flexShrink = 0;
            Header.AddToClassList("mk-sheet-header"); Footer.AddToClassList("mk-sheet-footer");
            Surface.Add(Header); Body.style.minHeight = 0; Body.style.flexShrink = 1; Surface.Add(Body); Surface.Add(Footer);
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
            motion = new LobbyElementMotion(Surface, Scrim, vertical: true);
            motion.Set(0f, 0f, LobbyMotion.ClosedScale);
            sizeTick = schedule.Execute(FitContent).Every(16); sizeTick.Pause();
        }

        public void SetScrollable(bool value)
        {
            if ((Body is ScrollView) == value) return;
            var previous = Body;
            Body = value ? new ScrollView() : new VisualElement();
            Body.name = previous.name;
            Body.style.minHeight = 0; Body.style.flexShrink = 1; Body.style.overflow = Overflow.Hidden;
            foreach (var child in new System.Collections.Generic.List<VisualElement>(previous.Children())) Body.Add(child);
            previous.RemoveFromHierarchy(); Surface.Insert(Surface.IndexOf(Footer), Body);
        }
        public void BindRouter(LobbyOverlayRouter value, string id) { router = value; route = id; }
        public void Open(bool large = false)
        {
            if (router != null && !router.Allows(route)) return;
            Surface.EnableInClassList("mk-bigsheet", large);
            bool item = Surface.ClassListContains("ws-item-sheet");
            if (item) { Surface.style.height = Length.Percent(78); Surface.style.maxHeight = Length.Percent(78); }
            else { Surface.style.maxHeight = Length.Percent(85); sizeSampled = Time.unscaledTimeAsDouble; sizeTick.Resume(); }
            LobbyScrollbars.Hide(Body);
            int revision = ++openRevision;
            // Never paint the previous item's offset. Reset again after layout,
            // when Unity has recomputed the new content and scroller bounds.
            Body.style.visibility = Visibility.Hidden;
            LobbyScrollbars.Reset(Body);
            // Scheduling from the next update guarantees a layout pass between
            // rebuilding the content and revealing it, even if its size is unchanged.
            Body.schedule.Execute(() => Body.schedule.Execute(() =>
            {
                if (!IsOpen || revision != openRevision) return;
                LobbyScrollbars.Reset(Body);
                Body.style.visibility = StyleKeyword.Null;
            }).StartingIn(1)).StartingIn(1);
            style.display = DisplayStyle.Flex;
            SetEnabled(true);
            pickingMode = PickingMode.Position;
            Surface.pickingMode = Scrim.pickingMode = PickingMode.Position;
            if (IsOpen) return;
            IsOpen = true;
            motion.Open();
            if (router != null) router.Open(new LobbyOverlay(route, OverlayKind.Sheet, true), Close);
            Opened?.Invoke();
        }
        public void Close()
        {
            if (!IsOpen) return;
            Release(); IsOpen = false; sizeTick.Pause(); heightPlaced = false; RestoreHeading();
            SetEnabled(false);
            pickingMode = Surface.pickingMode = Scrim.pickingMode = PickingMode.Ignore;
            motion.CloseDown(() => style.display = DisplayStyle.None);
            if (router != null) router.Closed(route);
            Closed?.Invoke();
        }
        private void RestoreHeading()
        {
            if (parkedHeading != null && headingOwner != null)
                headingOwner.Insert(Mathf.Min(headingIndex, headingOwner.childCount), parkedHeading);
            parkedHeading = null; headingOwner = null;
        }
        private void FitContent()
        {
            if (!IsOpen || panel == null || Surface.ClassListContains("ws-item-sheet")) return;
            var body = Body.contentContainer;
            VisualElement first = body.childCount > 0 ? body[0] : null;
            var heading = first as Label;
            if (heading == null && first != null && first.childCount > 0) heading = first[0] as Label;
            if (heading != null && heading.ClassListContains("mk-h2"))
            {
                if (parkedHeading != null) { parkedHeading.RemoveFromHierarchy(); parkedHeading = null; }
                headingOwner = heading.parent; headingIndex = headingOwner.IndexOf(heading); parkedHeading = heading;
                Header.Add(heading);
            }
            float contentHeight = body.layout.height;
            if (float.IsNaN(contentHeight) || contentHeight <= 0) return;
            var style = Surface.resolvedStyle;
            float chrome = Handle.layout.height + Handle.resolvedStyle.marginTop + Header.layout.height + Footer.layout.height
                + style.paddingTop + style.paddingBottom + style.borderTopWidth + style.borderBottomWidth;
            float target = Mathf.Min(Mathf.Max(240, contentHeight + chrome), worldBound.height * 0.85f);
            if (float.IsNaN(target) || target <= 0) return;
            if (!heightPlaced) { height = new LobbyMotion.Spring(target); heightPlaced = true; }
            height.Target = target;
            double now = Time.unscaledTimeAsDouble;
            if (LobbyMotion.Reduced) { height.Position = target; height.Velocity = 0; }
            else height.Advance((float)(now - sizeSampled), LobbyMotion.PopDamping);
            sizeSampled = now; Surface.style.height = height.Position;
        }
        private void Release()
        {
            int id = pointer; pointer = -1;
            if (id >= 0 && Handle.HasPointerCapture(id)) Handle.ReleasePointer(id);
        }
    }
}
