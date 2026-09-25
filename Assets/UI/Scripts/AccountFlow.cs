using System;
using System.Threading.Tasks;
using NodeWar.Backend;
using UnityEngine.UIElements;

namespace NodeWar.Lobby
{
    /// <summary>Shared account actions. Identity always comes from the backend.</summary>
    public sealed class AccountFlow
    {
        private Task idle = Task.CompletedTask;
        private readonly LobbySheet sheet;
        private VisualElement dialog;

        public bool Busy { get; private set; }
        public string BusyText { get; private set; } = "";
        public string Message { get; private set; } = "";
        public event Action Changed;

        public AccountFlow(LobbySheet sheet)
        {
            this.sheet = sheet;
        }

        public async Task EnsureAsync()
        {
            await idle;
            await RunAsync("Checking account...", async () =>
            {
                await BackendServices.Account.EnsureSignedInAsync();
            });
        }

        public Task LinkAsync(Func<bool> isActive)
        {
            return RunAsync("Linking account...", async () =>
            {
                AccountInfo guest = BackendServices.Account.Current;
                LinkResult result = await BackendServices.Account.LinkAsync();
                if (result != LinkResult.AlreadyLinkedElsewhere)
                {
                    ShowResult(result);
                    return;
                }

                if (!CanContinue(isActive, guest)) return;
                SetBusyText("Choose an account");
                bool switchAccount = await ConfirmAsync("account-conflict",
                    "That Unity account already has its own Node War progress.",
                    "Switch to that account", "This device's guest progress will be abandoned");
                if (!switchAccount)
                {
                    Message = "Account switch cancelled";
                    return;
                }
                if (!CanContinue(isActive, guest)) return;
                SetBusyText("Switching account...");
                await BackendServices.Account.SwitchToLinkedAccountAsync();
            });
        }

        public Task SignInAsync(Func<bool> isActive)
        {
            return RunAsync("Signing in...", async () =>
            {
                AccountInfo previous = BackendServices.Account.Current;
                if (previous.Status == AccountStatus.Guest)
                {
                    SetBusyText("Checking guest progress...");
                    PlayerState state = await BackendServices.PlayerState.GetAsync();
                    if (!CanContinue(isActive, previous)) return;
                    if (state == null)
                    {
                        Message = "Couldn't reach the server";
                        return;
                    }
                    if (LinkPromptPolicy.HasProgress(state))
                    {
                        SetBusyText("Confirm sign-in");
                        bool signIn = await ConfirmAsync("account-sign-in-warning",
                            "Your guest progress on this device will be abandoned.",
                            "Sign in to another account");
                        if (!signIn)
                        {
                            Message = "Sign-in cancelled";
                            return;
                        }
                    }
                }
                if (!CanContinue(isActive, previous)) return;
                SetBusyText("Signing in...");
                ShowResult(await BackendServices.Account.SignInWithAccountAsync());
            });
        }

        public Task SignOutAsync()
        {
            return RunAsync("Signing out...", () => BackendServices.Account.SignOutAsync());
        }

        private static bool CanContinue(Func<bool> isActive, AccountInfo previous)
        {
            AccountInfo current = BackendServices.Account.Current;
            return isActive() && current.Status == previous.Status && current.PlayerId == previous.PlayerId;
        }

        private void SetBusyText(string text)
        {
            BusyText = text;
            Changed?.Invoke();
        }

        public void CloseDialog()
        {
            if (sheet.IsShowing(dialog)) sheet.Close();
        }

        /// <summary>The shared sheet owns dismissal, including its scrim and handle.</summary>
        private async Task<bool> ConfirmAsync(string name, string title, string confirm,
            string subtitle = null)
        {
            if (sheet.IsOpen) return false;

            VisualElement content = new VisualElement { name = name };
            content.AddToClassList("lb-account-dialog");
            Label heading = new Label(title) { name = name + "-title" };
            heading.AddToClassList("ui-sheet__title");
            heading.AddToClassList("ui-w600");
            content.Add(heading);

            var completion = new TaskCompletionSource<bool>();
            bool accepted = false;
            Button accept = DialogButton(name + "-confirm", confirm, "ui-button--gold");
            if (subtitle != null)
            {
                Label note = new Label(subtitle) { name = name + "-subtitle", pickingMode = PickingMode.Ignore };
                note.AddToClassList("lb-account-dialog__subtitle");
                accept.Add(note);
            }
            accept.clicked += () => { accepted = true; sheet.Close(); };
            content.Add(accept);
            Button cancel = DialogButton(name + "-cancel", "Cancel", "ui-button--quiet");
            cancel.clicked += sheet.Close;
            content.Add(cancel);

            Action<VisualElement> onClosed = closed =>
            {
                if (closed == content) completion.TrySetResult(accepted);
            };
            dialog = content;
            sheet.Closed += onClosed;
            try
            {
                sheet.Open(content);
                if (!sheet.IsShowing(content)) return false;
                cancel.Focus();
                return await completion.Task;
            }
            finally
            {
                sheet.Closed -= onClosed;
                if (dialog == content) dialog = null;
            }
        }

        private static Button DialogButton(string name, string text, string style)
        {
            Button button = new Button { name = name };
            button.AddToClassList("ui-reset-button");
            button.AddToClassList("ui-button");
            button.AddToClassList(style);
            button.AddToClassList("ui-button-wrap");
            button.AddToClassList("lb-account-dialog__choice");
            Label label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("ui-w600");
            button.Add(label);
            return button;
        }

        private void ShowResult(LinkResult result)
        {
            switch (result)
            {
                case LinkResult.Cancelled: Message = "Sign-in cancelled"; break;
                case LinkResult.Failed: Message = "Couldn't sign in. Please try again"; break;
                case LinkResult.AlreadyLinkedElsewhere:
                    Message = "That Unity account already has its own Node War progress.";
                    break;
            }
        }

        private async Task RunAsync(string busyText, Func<Task> action)
        {
            if (Busy) return;

            var completion = new TaskCompletionSource<bool>();
            idle = completion.Task;
            Busy = true;
            BusyText = busyText;
            Message = "";
            Changed?.Invoke();
            try
            {
                await action();
            }
            catch (Exception)
            {
                Message = "Couldn't reach the server";
            }
            finally
            {
                Busy = false;
                Changed?.Invoke();
                completion.TrySetResult(true);
            }
        }
    }
}
