using NUnit.Framework;
namespace NodeWar.Lobby.Tests
{
    public class QueueSearchPolicyTests
    {
        [TestCase(LobbyMode.Ranked1v1, false, false)]
        [TestCase(LobbyMode.Team2v2, true, true)]
        [TestCase(LobbyMode.FreeForAll4, true, false)]
        public void QueuedModesChooseCorrectPath(LobbyMode mode, bool fake, bool invite)
        {
            Assert.That(QueueSearchPolicy.IsFake(mode), Is.EqualTo(fake));
            Assert.That(QueueSearchPolicy.CanInvite(mode), Is.EqualTo(invite));
            Assert.That(new LobbyModeSelection(mode).LaunchStrategy, Is.EqualTo(ModeLaunchStrategy.Queue));
        }
        [TestCase(LobbyMode.Team2v2)][TestCase(LobbyMode.FreeForAll4)]
        public void FakeModesOfferTheRealLocalBotFallbackAtTheSharedDelay(LobbyMode mode)
        {
            int delay = (int)NodeWar.Backend.RankedQueuePresenter.BotOfferSeconds;
            Assert.That(QueueSearchPolicy.OfferDue(mode, delay - 1), Is.False);
            Assert.That(QueueSearchPolicy.OfferDue(mode, delay), Is.True);
        }
        [TestCase(0, "00:00")][TestCase(65, "01:05")][TestCase(600, "10:00")][TestCase(3600, "1:00:00")][TestCase(3665, "1:01:05")]
        public void FormatsElapsedTime(int seconds, string expected) => Assert.That(QueueSearchPolicy.Elapsed(seconds), Is.EqualTo(expected));
    }
}
