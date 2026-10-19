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

        private static void HideScroll(ScrollView scroll)
        {
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        }
    }
}
