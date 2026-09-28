using System;
using NodeWar.Backend;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>
    /// The default, deliberately plain <see cref="IRankedQueueView"/>: a status
    /// line, an elapsed timer, and up to two buttons. Built in code rather than
    /// its own UXML because there is nothing here worth hand-laying-out yet.
    ///
    /// This is one implementation of the interface, not the interface itself.
    /// A redesign swaps the single place PlayPopup constructs this class for
    /// another IRankedQueueView - RankedQueuePresenter and IRankedQueueService
    /// never change.
    ///
    /// The secondary button (Cancel / Back) always raises CancelRequested; the
    /// presenter treats it as "stop" while an attempt runs, and PlayPopup as
    /// "back to the mode list" once it has ended. The primary button is the
    /// bot offer while searching, and Forfeit on the forfeit prompt.
    /// </summary>
    public sealed class RankedQueueViewElement : VisualElement, IRankedQueueView
    {
        private readonly Label statusLabel;
        private readonly Label timerLabel;
        private readonly Button primaryButton;
        private readonly Button cancelButton;

        private Action primaryAction;
        private bool botOffered;

        public event Action CancelRequested;
        public event Action ForfeitConfirmed;
        public event Action BotAccepted;

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

            primaryButton = new Button(() => primaryAction?.Invoke());
            primaryButton.AddToClassList("ui-reset-button");
            primaryButton.AddToClassList("lb-ranked__cancel");
            Add(primaryButton);

            cancelButton = new Button(() => CancelRequested?.Invoke());
            cancelButton.AddToClassList("ui-reset-button");
            cancelButton.AddToClassList("lb-ranked__cancel");
            Add(cancelButton);

            ShowIdle();
        }

        public void ShowIdle()
        {
            Show("Ready to search for a ranked match.", null, null, null, null);
        }

        public void ShowSearching(int elapsedSeconds)
        {
            // Called every frame while searching; the bot offer stays up once made.
            if (botOffered)
                Show("Searching for a match...", FormatElapsed(elapsedSeconds), "Play a bot instead",
                     () => BotAccepted?.Invoke(), "Cancel", keepBotOffer: true);
            else
                Show("Searching for a match...", FormatElapsed(elapsedSeconds), null, null, "Cancel");
        }

        public void ShowBotOffer()
        {
            botOffered = true;
        }

        public void ShowFound(string matchId)
        {
            Show("Match found.", null, null, null, null);
        }

        public void ShowConnecting()
        {
            Show("Match found. Connecting to your opponent...", null, null, null, "Cancel");
        }

        public void ShowRequeueing(string message)
        {
            Show(message, null, null, null, "Cancel");
        }

        public void ShowForfeitPrompt()
        {
            Show("You left a match in progress. Forfeit it to queue again?", null, "Forfeit",
                 () => ForfeitConfirmed?.Invoke(), "Back");
        }

        public void ShowWaitingForResult(int seconds)
        {
            Show("Waiting for your last match's result.", FormatElapsed(seconds), null, null, null);
        }

        public void ShowFailed(string message)
        {
            Show("Couldn't find a match: " + message, null, null, null, "Back");
        }

        public void ShowCancelled()
        {
            Show("Search cancelled.", null, null, null, "Back");
        }

        private void Show(string status, string timer, string primary, Action onPrimary, string cancel,
                          bool keepBotOffer = false)
        {
            if (!keepBotOffer) botOffered = false;

            statusLabel.text = status;
            timerLabel.text = timer ?? "";
            SetVisible(timerLabel, timer != null);

            primaryAction = onPrimary;
            primaryButton.text = primary ?? "";
            SetVisible(primaryButton, primary != null);

            cancelButton.text = cancel ?? "";
            SetVisible(cancelButton, cancel != null);
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
