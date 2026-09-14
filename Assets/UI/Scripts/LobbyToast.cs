using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// One short message over the lobby, gone after a moment.
    ///
    /// This is how a control that cannot act yet says why, instead of doing
    /// nothing. A second message replaces the first rather than queueing.
    /// </summary>
    public class LobbyToast
    {
        private const long VisibleMs = 1700;

        private readonly Label label;
        private IVisualElementScheduledItem hideItem;

        public LobbyToast(Label label)
        {
            this.label = label;
        }

        public void Show(string message)
        {
            if (label == null) return;

            label.text = message;
            label.AddToClassList("lb-toast--on");

            if (hideItem != null) hideItem.Pause();
            hideItem = label.schedule.Execute(() => label.RemoveFromClassList("lb-toast--on"));
            hideItem.ExecuteLater(VisibleMs);
        }
    }
}
