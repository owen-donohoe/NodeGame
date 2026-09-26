using System;

namespace NodeWar.Lobby
{
    /// <summary>A Home visit may ask once; leaving it retires any pending dialog.</summary>
    public sealed class AccountLinkPrompt
    {
        private readonly AccountFlow flow;
        private readonly Func<bool> canShow;
        private readonly Action<string> say;
        private int visit;
        private bool checking;

        public AccountLinkPrompt(AccountFlow flow, Func<bool> canShow, Action<string> say)
        {
            this.flow = flow;
            this.canShow = canShow;
            this.say = say;
        }

        public async void Show()
        {
            int thisVisit = ++visit;
            PlayerProfile profile = PlayerProfile.Instance;
            if (profile == null || profile.AccountLinkPromptShown || flow.Busy || !canShow()) return;

            Func<bool> isActive = () => thisVisit == visit && canShow();
            // The automatic check stays quiet; once the player starts linking,
            // Home uses the lobby's toast for the same feedback Settings shows.
            Action onChanged = () =>
            {
                if (isActive() && flow.Busy
                    && (flow.BusyText == "Linking account..." || flow.BusyText == "Switching account..."))
                    say(flow.BusyText);
            };
            checking = true;
            flow.Changed += onChanged;
            try
            {
                await flow.CheckLinkPromptAsync(profile, isActive);
                if (isActive() && !string.IsNullOrEmpty(flow.Message)) say(flow.Message);
            }
            finally
            {
                flow.Changed -= onChanged;
                checking = false;
            }
        }

        public void Hide()
        {
            visit++;
            if (checking) flow.CloseDialog();
        }
    }
}
