using NUnit.Framework;
using NodeWar.Backend;

namespace NodeWar.Lobby.Tests
{
    public class CrateOpeningSequenceTests
    {
        [TestCase(CrateRewardKind.Coins, RevealStyle.Card)]
        [TestCase(CrateRewardKind.Premium, RevealStyle.Card)]
        [TestCase(CrateRewardKind.District, RevealStyle.District)]
        [TestCase(CrateRewardKind.Suit, RevealStyle.Suit)]
        [TestCase(CrateRewardKind.Skin, RevealStyle.Skin)]
        public void RewardKind_SelectsReveal(CrateRewardKind kind, RevealStyle style)
            => Assert.AreEqual(style, CrateOpeningSequence.StyleFor(kind));

        [Test]
        public void Sequence_ShowsEveryActualReward_AndAcceptsOnlySummary()
        {
            var rewards = new[] { new CrateReward(CrateRewardKind.Coins, "", 50),
                new CrateReward(CrateRewardKind.District, "farm", 1), new CrateReward(CrateRewardKind.Skin, "hat", 1) };
            var sequence = new CrateOpeningSequence(rewards);
            rewards[0] = default;
            Assert.AreEqual(50, sequence.RewardAt(0).Amount);
            Assert.IsFalse(sequence.Accept());
            sequence.Tap();
            Assert.AreEqual(CrateOpeningState.Landing, sequence.State);
            sequence.Advance(CrateOpeningSequence.LandingSeconds / 2);
            Assert.AreEqual(0.5, sequence.Progress);
            sequence.Advance(100);
            Assert.AreEqual(CrateOpeningState.Ready, sequence.State);
            sequence.Advance(100);
            Assert.AreEqual(CrateOpeningState.Ready, sequence.State);
            Assert.IsFalse(sequence.Accept());
            sequence.Tap();
            Assert.AreEqual(CrateOpeningState.Opening, sequence.State);
            sequence.Tap();
            Assert.IsFalse(sequence.Accept());
            sequence.Advance(100);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(CrateOpeningState.Revealing, sequence.State);
                Assert.AreEqual(i, sequence.RewardIndex);
                Assert.IsFalse(sequence.Accept());
                sequence.Tap(); // Finishes this reward, without changing its identity.
                Assert.AreEqual(i, sequence.RewardIndex);
                Assert.AreEqual(1, sequence.Progress);
                sequence.Tap();
            }
            Assert.IsTrue(sequence.CanAccept);
            sequence.Tap();
            sequence.Advance(100);
            Assert.AreEqual(CrateOpeningState.Summary, sequence.State);
            Assert.IsTrue(sequence.Accept());
            sequence.Tap();
            sequence.Advance(100);
            Assert.AreEqual(CrateOpeningState.Done, sequence.State);
            Assert.IsFalse(sequence.Accept());
        }

        [Test]
        public void TimeFinishesAnimation_WithoutSkippingAReward()
        {
            var sequence = new CrateOpeningSequence(new[] { new CrateReward(CrateRewardKind.Suit, "guard", 1) });
            sequence.Advance(1); sequence.Tap(); sequence.Advance(1);
            sequence.Advance(9999);
            Assert.AreEqual(CrateOpeningState.Revealing, sequence.State);
            Assert.AreEqual(0, sequence.RewardIndex);
            Assert.AreEqual(1, sequence.Progress);
            sequence.Tap();
            Assert.AreEqual(CrateOpeningState.Summary, sequence.State);
        }

        [Test]
        public void EmptyRewards_StillEndWithAccept_AndInvalidTimeIsIgnored()
        {
            var sequence = new CrateOpeningSequence(null);
            foreach (double delta in new[] { -1d, 0d, double.NaN, double.PositiveInfinity }) sequence.Advance(delta);
            Assert.AreEqual(CrateOpeningState.Landing, sequence.State);
            Assert.AreEqual(0, sequence.Progress);
            sequence.Advance(1); sequence.Tap(); sequence.Advance(1);
            Assert.AreEqual(CrateOpeningState.Summary, sequence.State);
            Assert.AreEqual(-1, sequence.RewardIndex);
            Assert.IsTrue(sequence.Accept());
        }
    }
}
