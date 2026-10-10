using System;
using System.Linq;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.BalanceRig
{
    public class ArenaLoadoutRigTests
    {
        private static RigSetup Mixed()
        {
            var s = RigSetupLoader.Load(null, null, "Barracks,Forge");
            s.effectiveDraftPools = new[] {
                new[] { DistrictType.Farm, DistrictType.Mine, DistrictType.Village, DistrictType.Barracks, DistrictType.Forge },
                new[] { DistrictType.Barracks, DistrictType.Forge, DistrictType.Town, DistrictType.Fortress, DistrictType.Workshop } };
            s.playerSetups = new[] {
                new PlayerSetup { suits = new[] { (int)SuitType.Warrior } },
                new PlayerSetup { suits = new[] { (int)SuitType.Guardian, (int)SuitType.Scout } } };
            return s;
        }

        [Test]
        public void MixedPools_ExactlyFivePerSide()
        {
            var s = Mixed();
            var draft = MatchRunner.Prepare(s, 7).draft;
            for (int p = 0; p < 2; p++)
                CollectionAssert.AreEquivalent(s.effectiveDraftPools[p], draft.Where(d => d.playerID == p).Select(d => d.districtType));
        }

        [Test]
        public void SeatSwap_ModesWithoutBoardHashChange()
        {
            var s = Mixed();
            int hash = BoardHasher.Hash(s.board);
            var first = MatchRunner.Prepare(s, 7);
            var swapped = MatchRunner.SwapSeats(first);
            CollectionAssert.AreEqual(s.effectiveDraftPools[1], swapped.setup.effectiveDraftPools[0]);
            CollectionAssert.AreEqual(s.effectiveDraftPools[0], swapped.setup.effectiveDraftPools[1]);
            CollectionAssert.AreEqual(first.players[1].suits, swapped.players[0].suits);
            Assert.That(BoardHasher.Hash(s.board), Is.EqualTo(hash));
            Assert.That(BoardHasher.Hash(swapped.setup.board), Is.EqualTo(hash));
        }

        [Test]
        public void ExplicitSetup_NoImplicitWarrior()
        {
            var match = MatchRunner.Prepare(Mixed(), 7);
            var s = MatchFactory.Build(match.setup.balance, match.setup.board, match.draft, match.players);
            CollectionAssert.AreEqual(match.players[1].suits, s.players[1].draftedSuits);
            Assert.That(s.players[1].draftedSuits, Does.Not.Contain((int)SuitType.Warrior));
            Assert.That(s.villagers.All(v => v.suit == SuitType.None), Is.True);
        }

        [Test]
        public void LegacyDefaults_Unchanged()
        {
            var s = RigSetupLoader.Load(null, null, "Barracks,Forge");
            Assert.That(s.effectiveDraftPools, Is.Null);
            var m = MatchRunner.Prepare(s, 7);
            for (int p = 0; p < 2; p++) {
                CollectionAssert.AreEqual(new[] { (int)SuitType.Warrior }, m.players[p].suits);
                CollectionAssert.AreEquivalent(s.baseDraft[p].Concat(s.loadoutNodes),
                    m.draft.Where(d => d.playerID == p).Select(d => d.districtType));
            }
        }

        [Test]
        public void ExplicitEmptyPool_IsConsumedExactly()
        {
            var s = Mixed();
            s.effectiveDraftPools[1] = Array.Empty<DistrictType>();
            Assert.That(MatchRunner.Prepare(s, 7).draft.Any(d => d.playerID == 1), Is.False);
        }
    }
}
