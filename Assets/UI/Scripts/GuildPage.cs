using UnityEngine;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    public sealed class GuildPage : LobbyPage
    {
        public GuildPage() : base(LobbyPageID.Guild, Build()) { }

        private static VisualElement Build()
        {
            var root = new VisualElement();
            var layout = Resources.Load<VisualTreeAsset>("Lobby/GuildPage");
            if (layout != null) layout.CloneTree(root);
            else root.Add(new Label("Guild"));
            var state = new AsyncStateView();
            state.Render(new AsyncState().Reduce(AsyncResult.Empty, 0), 0, "You're not in a guild");
            (root.Q<VisualElement>("guild-state") ?? root).Add(state);
            return root;
        }
    }
}
