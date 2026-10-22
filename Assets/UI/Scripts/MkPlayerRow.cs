using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    [UxmlElement]
    public partial class MkPlayerRow : VisualElement
    {
        public Label Avatar { get; } = new Label();
        public Label NameLabel { get; } = new Label();
        public VisualElement StatusDot { get; } = new VisualElement();
        public Label StatusLabel { get; } = new Label();
        public VisualElement Trailing { get; } = new VisualElement();

        public MkPlayerRow()
        {
            AddToClassList("mk-friend");
            Avatar.AddToClassList("mk-friend__avatar"); Add(Avatar);
            var body = new VisualElement(); body.AddToClassList("mk-friend__body"); Add(body);
            NameLabel.AddToClassList("mk-friend__name"); NameLabel.enableRichText = false; body.Add(NameLabel);
            var status = new VisualElement(); status.AddToClassList("mk-friend__status"); body.Add(status);
            StatusDot.AddToClassList("mk-friend__dot"); status.Add(StatusDot);
            StatusLabel.enableRichText = false; status.Add(StatusLabel);
            Trailing.AddToClassList("mk-friend__trailing"); Add(Trailing);
        }

        public MkPlayerRow(string handle, string status) : this() { SetPlayer(handle, status); }
        public void SetPlayer(string playerName, string tag, string status) => SetPlayer(playerName + "#" + tag, status);
        public void SetPlayer(string handle, string status)
        {
            NameLabel.text = handle ?? "";
            Avatar.text = string.IsNullOrEmpty(handle) ? "?" : handle.Substring(0, 1).ToUpperInvariant();
            StatusLabel.text = status ?? "";
        }
    }
}
