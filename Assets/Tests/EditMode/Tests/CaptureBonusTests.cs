using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class CaptureBonusTests
    {
        private static int Net()
        {
            int hash = 0;
            foreach (int attacker in new[] { 0, 1 }) foreach (bool opposite in new[] { false, true })
            for (int i = 0; i < 3; i++)
            {
                var b = CoreRulesFixture.Balance(); var s = CoreRulesFixture.Board(b);
                int[] friends = { 0, 2, 4 }, enemies = { 2, 1, 0 }, rates = opposite ? new[] { 40, 50, 60 } : new[] { 10, 12, 15 };
                CoreRulesFixture.Neighbours(s, 0, friends[i], enemies[i], attacker);
                int sign = attacker == 0 ? 1 : -1; int start = opposite ? -sign * 500 : 0;
                s.nodes[0].claimBar = start; s.villagers = new[] { CoreRulesFixture.Body(b, 0, attacker, 0, VillagerState.Claiming) };
                GameSimulation.SimulateTick(s); Assert.AreEqual(start + sign * rates[i], s.nodes[0].claimBar); hash = CoreRulesFixture.Fold(hash, s);
            }
            return hash;
        }
        private static int Neutral()
        {
            var b = CoreRulesFixture.Balance(); var s = CoreRulesFixture.Board(b); CoreRulesFixture.Neighbours(s, 0, 2, 1);
            s.villagers = new[] { CoreRulesFixture.Body(b, 0, 0, 0, VillagerState.Claiming) };
            GameSimulation.SimulateTick(s); Assert.AreEqual(12, s.nodes[0].claimBar); return CoreRulesFixture.Fold(0, s);
        }
        private static int Lake()
        {
            int hash = 0;
            for (int mode = 0; mode < 3; mode++)
            {
                var b = CoreRulesFixture.Balance(); var s = CoreRulesFixture.Board(b);
                s.nodes[1].ownerID = 0; s.nodes[1].terrain = TerrainType.Lake; s.nodes[1].districtType = mode == 1 ? DistrictType.Pier : DistrictType.None;
                s.nodes[0].gridX = 0; s.nodes[1].gridX = 2;
                if (mode == 1) s.nodes[0].links = new[] { new Link { toNodeID = 1, travelWeight = 1 } };
                if (mode == 2) { s.nodes[0].links = new[] { new Link { toNodeID = 2, travelWeight = 1 } }; s.nodes[2].links = new[] { new Link { toNodeID = 1, travelWeight = 1 } }; }
                s.villagers = new[] { CoreRulesFixture.Body(b, 0, 0, 0, VillagerState.Claiming) };
                GameSimulation.SimulateTick(s); Assert.AreEqual(mode == 1 ? 12 : 10, s.nodes[0].claimBar); hash = CoreRulesFixture.Fold(hash, s);
            }
            return hash;
        }
        private static int Floors()
        {
            var b = CoreRulesFixture.Balance(125); b.baseClaimPerTick = 3; var s = CoreRulesFixture.Board(b);
            CoreRulesFixture.Neighbours(s, 0, 1, 0); s.nodes[0].claimBar = -500; s.villagers = new VillagerData[5];
            for (int i = 0; i < 5; i++) s.villagers[i] = CoreRulesFixture.Body(b, i, 0, 0, VillagerState.Claiming);
            GameSimulation.SimulateTick(s); Assert.AreEqual(-425, s.nodes[0].claimBar); return CoreRulesFixture.Fold(0, s);
        }
        private static int Minimum()
        {
            var b = CoreRulesFixture.Balance(1); b.baseClaimPerTick = 1; var s = CoreRulesFixture.Board(b);
            GameSimulation.SimulateTick(s); Assert.AreEqual(0, s.nodes[0].claimBar);
            s.villagers = new[] { CoreRulesFixture.Body(b, 0, 0, 0, VillagerState.Claiming) };
            GameSimulation.SimulateTick(s); Assert.AreEqual(1, s.nodes[0].claimBar); return CoreRulesFixture.Fold(0, s);
        }
        private static int LargeRate()
        {
            int hash = 0;
            foreach (int attacker in new[] { 0, 1 })
            {
                var b = CoreRulesFixture.Balance(); b.baseClaimPerTick = b.claimThreshold = int.MaxValue;
                var s = CoreRulesFixture.Board(b); int sign = attacker == 0 ? 1 : -1;
                s.nodes[0].ownerID = 1 - attacker; s.nodes[0].claimBar = -sign * int.MaxValue;
                s.villagers = new VillagerData[4];
                for (int i = 0; i < 4; i++) s.villagers[i] = CoreRulesFixture.Body(b, i, attacker, 0, VillagerState.Claiming);
                GameSimulation.SimulateTick(s);
                Assert.AreEqual(sign * int.MaxValue, s.nodes[0].claimBar);
                Assert.AreEqual(attacker, s.nodes[0].ownerID); hash = CoreRulesFixture.Fold(hash, s);
            }
            return hash;
        }
        [Test] public void LargeClaimRate_ClampsBarWithoutOverflowOrLosingCrossing() => LargeRate();
        [Test] public void LargeClaimRate_ClampsBarWithoutOverflowOrLosingCrossing_Determinism() => CoreRulesFixture.Determinism(LargeRate);
        [Test] public void NetAdjacency_UsesZeroOneTwoSteps() => Net();
        [Test] public void NetAdjacency_UsesZeroOneTwoSteps_Determinism() => CoreRulesFixture.Determinism(Net);
        [Test] public void NeutralTarget_OtherPlayersNeighboursStillResist() => Neutral();
        [Test] public void NeutralTarget_OtherPlayersNeighboursStillResist_Determinism() => CoreRulesFixture.Determinism(Neutral);
        [Test] public void LakeWithoutPier_GivesNoBonus() => Lake();
        [Test] public void LakeWithoutPier_GivesNoBonus_Determinism() => CoreRulesFixture.Determinism(Lake);
        [Test] public void ClaimRate_CapsFourAndFloorsEachStage() => Floors();
        [Test] public void ClaimRate_CapsFourAndFloorsEachStage_Determinism() => CoreRulesFixture.Determinism(Floors);
        [Test] public void PositiveContribution_HasMinimumOneButZeroBodiesStayZero() => Minimum();
        [Test] public void PositiveContribution_HasMinimumOneButZeroBodiesStayZero_Determinism() => CoreRulesFixture.Determinism(Minimum);
    }
}
