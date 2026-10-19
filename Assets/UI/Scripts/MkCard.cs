using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    [UxmlElement]
    public partial class MkCard : VisualElement
    {
        public MkCard() { AddToClassList("mk-card"); }
    }
}
