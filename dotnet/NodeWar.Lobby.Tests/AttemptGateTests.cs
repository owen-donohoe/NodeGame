using NodeWar.Network;
using NUnit.Framework;

namespace NodeWar.Lobby.Tests
{
    public class AttemptGateTests
    {
        [Test]
        public void LatestAttemptIsCurrentUntilInvalidated()
        {
            var gate = new AttemptGate();
            int a = gate.Begin();
            Assert.That(gate.IsCurrent(a), Is.True);
            gate.Invalidate();
            Assert.That(gate.IsCurrent(a), Is.False);
        }

        [Test]
        public void NewerAttemptSupersedesOlderOne()
        {
            var gate = new AttemptGate();
            int a = gate.Begin(), b = gate.Begin();
            Assert.That(gate.IsCurrent(a), Is.False);
            Assert.That(gate.IsCurrent(b), Is.True);
        }

        [Test]
        public void NothingIsCurrentBeforeTheFirstAttempt()
        {
            var gate = new AttemptGate();
            Assert.That(gate.IsCurrent(0), Is.False);
            Assert.That(gate.IsCurrent(-1), Is.False);
        }
    }
}
