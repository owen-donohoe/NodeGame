using System;
using System.Threading.Tasks;
using NodeWar.Backend;

namespace NodeWar.Lobby
{
    /// <summary>Shared account actions. Identity always comes from the backend.</summary>
    public sealed class AccountFlow
    {
        private Task idle = Task.CompletedTask;

        public bool Busy { get; private set; }
        public string BusyText { get; private set; } = "";
        public string Message { get; private set; } = "";
        public event Action Changed;

        public async Task EnsureAsync()
        {
            await idle;
            await RunAsync("Checking account...", async () =>
            {
                await BackendServices.Account.EnsureSignedInAsync();
            });
        }

        public Task LinkAsync()
        {
            return RunAsync("Linking account...", async () =>
                ShowResult(await BackendServices.Account.LinkAsync()));
        }

        public Task SignInAsync()
        {
            return RunAsync("Signing in...", async () =>
                ShowResult(await BackendServices.Account.SignInWithAccountAsync()));
        }

        public Task SignOutAsync()
        {
            return RunAsync("Signing out...", () => BackendServices.Account.SignOutAsync());
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
