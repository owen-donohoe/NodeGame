using System.Threading.Tasks;
using NodeWar.Backend;
using NUnit.Framework;

namespace NodeWar.Cloud.Tests
{
    public class AccountTests
    {
        [Test]
        public async Task Fake_StartsAsGuest_ThenLinks()
        {
            var account = new LocalAccountService();
            int changes = 0;
            account.Changed += _ => changes++;

            await account.EnsureSignedInAsync();
            string guestId = account.Current.PlayerId;
            LinkResult result = await account.LinkAsync();

            Assert.That(result, Is.EqualTo(LinkResult.Linked));
            Assert.That(account.Current.Status, Is.EqualTo(AccountStatus.Linked));
            Assert.That(account.Current.PlayerId, Is.EqualTo(guestId), "linking keeps the same player");
            Assert.That(changes, Is.EqualTo(2));
        }

        [Test]
        public async Task Fake_Conflict_LeavesGuestUntilSwitch()
        {
            var account = new LocalAccountService { SimulateConflict = true };
            await account.EnsureSignedInAsync();
            string guestId = account.Current.PlayerId;

            LinkResult result = await account.LinkAsync();
            Assert.That(result, Is.EqualTo(LinkResult.AlreadyLinkedElsewhere));
            Assert.That(account.Current.Status, Is.EqualTo(AccountStatus.Guest));

            await account.SwitchToLinkedAccountAsync();
            Assert.That(account.Current.Status, Is.EqualTo(AccountStatus.Linked));
            Assert.That(account.Current.PlayerId, Is.Not.EqualTo(guestId), "switching abandons the guest");
        }

        [Test]
        public async Task Fake_SignOut_ThenSignInWithAccount()
        {
            var account = new LocalAccountService();
            await account.EnsureSignedInAsync();
            await account.SignOutAsync();
            Assert.That(account.Current.Status, Is.EqualTo(AccountStatus.SignedOut));
            Assert.That(account.Current.PlayerId, Is.Null);

            LinkResult result = await account.SignInWithAccountAsync();
            Assert.That(result, Is.EqualTo(LinkResult.Linked));
            Assert.That(account.Current.Status, Is.EqualTo(AccountStatus.Linked));
        }

        private static AccountInfo Guest => new AccountInfo { Status = AccountStatus.Guest, PlayerId = "p" };

        [Test]
        public async Task LinkPrompt_NotShownWithoutProgress()
        {
            PlayerState fresh = await new LocalPlayerStateService().GetAsync();
            Assert.That(LinkPromptPolicy.ShouldPrompt(Guest, fresh, alreadyShown: false), Is.False);
        }

        [Test]
        public async Task LinkPrompt_ShownOnceAGuestHasPlayedOrUnlocked()
        {
            PlayerState played = await new LocalPlayerStateService().GetAsync();
            played.History.MatchIds.Add("m1");
            PlayerState unlocked = await new LocalPlayerStateService().GetAsync();
            unlocked.Inventory.OwnedVariants.Add("suit.warrior.era1");

            Assert.That(LinkPromptPolicy.ShouldPrompt(Guest, played, alreadyShown: false), Is.True);
            Assert.That(LinkPromptPolicy.ShouldPrompt(Guest, unlocked, alreadyShown: false), Is.True);
            Assert.That(LinkPromptPolicy.ShouldPrompt(Guest, played, alreadyShown: true), Is.False);
        }

        [Test]
        public async Task LinkPrompt_NeverForLinkedPlayers()
        {
            PlayerState played = await new LocalPlayerStateService().GetAsync();
            played.History.MatchIds.Add("m1");
            var linked = new AccountInfo { Status = AccountStatus.Linked, PlayerId = "p" };

            Assert.That(LinkPromptPolicy.ShouldPrompt(linked, played, alreadyShown: false), Is.False);
        }
    }
}
