using System;
using NodeWar.Backend;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The default, deliberately plain <see cref="IRankedQueueView"/>: a status
    /// line, an elapsed timer, and a Cancel button. Built in code rather than
    /// its own UXML because there is nothing here worth hand-laying-out yet.
    ///
    /// This is one implementation of the interface, not the interface itself.
    /// A redesign swaps the single place PlayPopup constructs this class for
    /// another IRankedQueueView - RankedQueuePresenter and IRankedQueueService
    /// never change.
    /// </summary>
    public sealed class RankedQueueViewElement : VisualElement, IRankedQueueView
    {
        private readonly Label statusLabel;
        private readonly Label timerLabel;
        private readonly Button cancelButton;

        public event Action CancelRequested;

        public RankedQueueViewElement()
        {
            AddToClassList("lb-ranked");
            pickingMode = PickingMode.Ignore;

            statusLabel = new Label();
            statusLabel.AddToClassList("lb-ranked__status");
            Add(statusLabel);

            timerLabel = new Label();
            timerLabel.AddToClassList("lb-ranked__timer");
            Add(timerLabel);

            cancelButton = new Button(() => CancelRequested?.Invoke());
            cancelButton.text = "Cancel";
            cancelButton.AddToClassList("ui-reset-button");
            cancelButton.AddToClassList("lb-ranked__cancel");
            Add(cancelButton);

            ShowIdle();
        }

        public void ShowIdle()
        {
            statusLabel.text = "Ready to search for a ranked match.";
            timerLabel.text = "";
            SetVisible(timerLabel, false);
            SetVisible(cancelButton, false);
        }

        public void ShowSearching(int elapsedSeconds)
        {
            statusLabel.text = "Searching for a match...";
            timerLabel.text = FormatElapsed(elapsedSeconds);
            SetVisible(timerLabel, true);
            SetVisible(cancelButton, true);
        }

        public void ShowFound(string matchId)
        {
            statusLabel.text = "Match found — connecting comes next.";
            SetVisible(timerLabel, false);
            SetVisible(cancelButton, false);
        }

        public void ShowFailed(string message)
        {
            statusLabel.text = "Couldn't find a match: " + message;
            SetVisible(timerLabel, false);
            SetVisible(cancelButton, false);
        }

        public void ShowCancelled()
        {
            statusLabel.text = "Search cancelled.";
            SetVisible(timerLabel, false);
            SetVisible(cancelButton, false);
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static string FormatElapsed(int seconds)
        {
            if (seconds < 0) seconds = 0;
            int minutes = seconds / 60;
            int rest = seconds % 60;
            return minutes + ":" + rest.ToString("00");
        }
    }
}
