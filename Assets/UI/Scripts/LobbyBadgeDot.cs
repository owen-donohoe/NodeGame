using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    public sealed class LobbyBadgeDot : VisualElement
    {
        private readonly BadgeBoard board;
        private readonly string key;

        public LobbyBadgeDot(BadgeBoard board, string key)
        {
            this.board = board;
            this.key = key;
            AddToClassList("lb-badge-dot");
            AddToClassList("mk-tab__badge");
            pickingMode = PickingMode.Ignore;
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                if (board != null) { board.Changed -= Refresh; board.Changed += Refresh; }
                Refresh();
            });
            RegisterCallback<DetachFromPanelEvent>(_ => { if (board != null) board.Changed -= Refresh; });
            Refresh();
        }

        private void Refresh()
        {
            int count = board?.Count(key) ?? 0;
            style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            tooltip = count.ToString();
        }
    }
}
