using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    internal static class LobbyScrollbars
    {
        public static void Hide(VisualElement root)
        {
            if (root == null) return;
            if (root is ScrollView scroll) HideScroll(scroll);
            root.Query<ScrollView>().ForEach(HideScroll);
        }

        public static void Reset(VisualElement root)
        {
            if (root is ScrollView scroll) scroll.scrollOffset = Vector2.zero;
            root.Query<ScrollView>().ForEach(view => view.scrollOffset = Vector2.zero);
        }

        private static void Clamp(ScrollView scroll)
        {
            if (LobbyMouseDrag.IsScrolling(scroll)) return;
            var content = scroll.contentContainer.layout;
            var viewport = scroll.contentViewport.layout;
            var offset = scroll.scrollOffset;
            scroll.scrollOffset = new Vector2(
                LobbyDragMath.ClampOffset(offset.x, content.width, viewport.width),
                LobbyDragMath.ClampOffset(offset.y, content.height, viewport.height));
        }

        internal static void ConfigureMotion(ScrollView scroll)
        {
            // Root pointer handling owns travel and inertia for both input devices.
            // Unrestricted lets that owner display resisted offsets without Toolkit's second spring.
            scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Unrestricted;
            scroll.scrollDecelerationRate = 0;
        }

        private static void HideScroll(ScrollView scroll)
        {
            if (!scroll.ClassListContains("lb-scroll-bounds"))
            {
                scroll.AddToClassList("lb-scroll-bounds");
                scroll.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => Clamp(scroll));
                scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => Clamp(scroll));
            }
            ConfigureMotion(scroll);
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        }
    }
}
