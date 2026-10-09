using System;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Tests
{
    public class StructureAttackTests
    {
        private static GameBalanceData Configure(int rate = 10)
        {
            var b = GameBalanceData.Default();
            b.baseClaimPerTick = rate; b.claimThreshold = 1000; b.decrementMultiplier = 1;
            b.captureBonusPercentPerStep = 0; b.tempoStageTicks = null;
            b.tempoClaimPercent = null; b.healIntervalTicks = 1000;
            GameSimulation.SetBalance(b); CommandProcessor.SetBalance(b);
            return b;
        }
        private static SimulationState Board(GameBalanceData b, SuitType suit = SuitType.Warrior)
        {
            var s = TestBoardFactory.BuildThreeNodeBoard(b);
            s.players[0].nextBreacherID = s.players[1].nextBreacherID = -1;
            s.nodes[1].districtType = s.nodes[1].baseDistrictType = DistrictType.Fortress;
            s.nodes[1].ownerID = 1; s.nodes[1].claimBar = -1000;
            s.nodes[1].fortressLevel = 1;
            s.nodes[1].structureKind = StructureKind.Fortification; s.nodes[1].structureHP = 16;
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 0, 1, b) };
            s.villagers[0].suit = suit;
            return s;
        }
        private static void Tick(SimulationState s, int count = 1)
        { for (int i = 0; i < count; i++) GameSimulation.SimulateTick(s); }
        private static void Move(SimulationState s, int node) => CommandProcessor.ProcessCommand(s,
            new GameCommand { type = CommandType.Move, playerID = 0, villagerID = 0, targetNodeID = node, issuedOnTick = s.tickCount });
        private static void Repeat(Func<SimulationState> run, bool determinism)
        {
            int hash = SimulationStateHasher.ComputeHash(run());
            if (determinism) Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(run()));
        }

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void OneAttacker_DestroysHp16OnTick16(bool determinism) => Repeat(() => {
            var s = Board(Configure()); Tick(s, 15);
            Assert.AreEqual(1, s.nodes[1].structureHP);
            Assert.AreEqual(VillagerState.AttackingStructure, s.villagers[0].state);
            Tick(s); Assert.AreEqual(0, s.nodes[1].structureHP);
            Assert.AreEqual(StructureKind.None, s.nodes[1].structureKind);
            Assert.AreEqual(0, s.nodes[1].fortressLevel);
            Assert.AreEqual(1, s.nodes[1].ownerID); Assert.AreEqual(-1000, s.nodes[1].claimBar);
            Assert.AreEqual(DistrictType.Fortress, s.nodes[1].districtType);
            Assert.AreEqual(VillagerState.Idle, s.villagers[0].state);
            Assert.Greater(s.villagers[0].hp, 0); Assert.IsFalse(s.villagers[0].isConsumed);
            Tick(s); Assert.AreEqual(-990, s.nodes[1].claimBar);
            Assert.AreEqual(VillagerState.Claiming, s.villagers[0].state); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void SuitVerb_UsesExplicitRule(bool determinism) => Repeat(() => {
            SimulationState s = null;
            foreach (var suit in new[] { SuitType.Warrior, SuitType.Guardian, SuitType.Scout, SuitType.Berserker, SuitType.None, SuitType.Farmer, SuitType.Medic })
            {
                s = Board(Configure(), suit); Tick(s);
                bool attacks = suit == SuitType.Warrior || suit == SuitType.Guardian || suit == SuitType.Scout || suit == SuitType.Berserker;
                Assert.AreEqual(attacks ? 15 : 16, s.nodes[1].structureHP, suit.ToString());
                Assert.AreEqual(attacks ? VillagerState.AttackingStructure : VillagerState.Claiming, s.villagers[0].state);
                Assert.AreEqual(attacks ? -1000 : -992, s.nodes[1].claimBar);
            }
            s = Board(Configure()); s.nodes[1].ownerID = 0; s.nodes[1].claimBar = 1000; Tick(s);
            Assert.AreEqual(16, s.nodes[1].structureHP); Assert.AreEqual(VillagerState.Idle, s.villagers[0].state);
            Move(s, 2); Tick(s, 4); // Enemy core always enters the breach channel.
            Assert.AreEqual(VillagerState.Breaching, s.villagers[0].state);
            Assert.Greater(s.players[1].breachBar, 0); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void DefenderInterruptsAndResumeDefersDamage(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); Array.Resize(ref s.villagers, 2);
            s.villagers[1] = TestBoardFactory.MakeIdleVillager(1, 1, 1, b);
            s.villagers[1].hp = 1; s.villagers[0].attackCooldownMax = 2;
            Tick(s); Assert.AreEqual(VillagerState.Fighting, s.villagers[0].state);
            Assert.AreEqual(16, s.nodes[1].structureHP);
            Tick(s); Assert.AreEqual(VillagerState.Dead, s.villagers[1].state);
            Assert.AreEqual(VillagerState.AttackingStructure, s.villagers[0].state);
            Assert.AreEqual(16, s.nodes[1].structureHP);
            Tick(s); Assert.AreEqual(15, s.nodes[1].structureHP); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void StructureAndClaimCompletion_DoesNotDoubleAct(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); s.nodes[1].structureHP = 1;
            s.nodes[1].ownerID = -1; s.nodes[1].claimBar = 990;
            // A neutral Fortification cannot be attacked: test direct capture of an enemy
            // whose bar is already within one civilian step of the positive threshold.
            s.nodes[1].ownerID = 1; s.nodes[1].claimBar = 992;
            Array.Resize(ref s.villagers, 2); s.villagers[1] = TestBoardFactory.MakeIdleVillager(1, 0, 1, b);
            Tick(s); Assert.AreEqual(0, s.nodes[1].structureHP); Assert.AreEqual(0, s.nodes[1].ownerID);
            Assert.AreEqual(1000, s.nodes[1].claimBar); Assert.AreEqual(VillagerState.Idle, s.villagers[0].state);
            // Without a civilian the attack cannot advance even a nearly completed bar.
            s = Board(Configure()); s.nodes[1].structureHP = 1; s.nodes[1].claimBar = 992;
            Tick(s); Assert.AreEqual(992, s.nodes[1].claimBar); Assert.AreEqual(1, s.nodes[1].ownerID); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void StickyTransit_IgnoresOrdinaryStructure(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); s.villagers[0].currentNodeID = 0;
            Move(s, 2); Tick(s, 4); Assert.AreEqual(VillagerState.Moving, s.villagers[0].state);
            Assert.AreEqual(2, s.villagers[0].targetNodeID); Assert.AreEqual(16, s.nodes[1].structureHP);
            Move(s, 1); Tick(s); Assert.AreEqual(VillagerState.AttackingStructure, s.villagers[0].state);
            Assert.AreEqual(15, s.nodes[1].structureHP);
            Move(s, 0); Tick(s); Assert.AreEqual(VillagerState.Moving, s.villagers[0].state);
            Assert.AreEqual(15, s.nodes[1].structureHP);
            // An override during person combat preserves the clock and resumes the new route.
            s = Board(Configure()); Array.Resize(ref s.villagers, 2);
            s.villagers[1] = TestBoardFactory.MakeIdleVillager(1, 1, 1, b); s.villagers[1].hp = 1;
            s.villagers[0].attackCooldownMax = 2; Tick(s);
            int cooldown = s.villagers[0].attackCooldownRemaining; Move(s, 0);
            Assert.AreEqual(cooldown, s.villagers[0].attackCooldownRemaining);
            Assert.AreEqual(VillagerState.Fighting, s.villagers[0].state); Tick(s);
            Assert.AreEqual(VillagerState.Moving, s.villagers[0].state); Assert.AreEqual(0, s.villagers[0].targetNodeID);
            Assert.AreEqual(16, s.nodes[1].structureHP); return s;
        }, determinism);

        [TestCase(0, false, false)] [TestCase(0, false, true, TestName = "{m}_Determinism{a}")]
        [TestCase(1, false, false)] [TestCase(1, false, true, TestName = "{m}_Determinism{a}")]
        [TestCase(0, true, false)] [TestCase(0, true, true, TestName = "{m}_Determinism{a}")]
        [TestCase(1, true, false)] [TestCase(1, true, true, TestName = "{m}_Determinism{a}")]
        public void OwnershipTransition_IsCentralised(int owner, bool fullClaim, bool determinism) => Repeat(() => {
            var b = Configure(fullClaim ? 100000 : 10); var s = Board(b, SuitType.None);
            s.nodes[1].ownerID = owner; s.nodes[1].claimBar = owner == 0 ? 1 : -1;
            s.nodes[1].fortressLevel = 2; s.nodes[1].structureHP = 9;
            s.nodes[1].recruitReadyTick = 50; s.nodes[1].autoRecruit = true;
            s.villagers[0].ownerID = 1 - owner; Tick(s);
            Assert.AreEqual(fullClaim ? 1 - owner : -1, s.nodes[1].ownerID);
            Assert.AreEqual(0, s.nodes[1].fortressLevel); Assert.AreEqual(StructureKind.None, s.nodes[1].structureKind);
            Assert.AreEqual(0, s.nodes[1].structureHP); Assert.AreEqual(0, s.nodes[1].recruitReadyTick);
            Assert.IsFalse(s.nodes[1].autoRecruit); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void ExcessSoldiersClaim_LowestFourAttack(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); s.villagers = new VillagerData[7];
            // Deliberately shuffled storage: selection is by villager ID, not array index.
            int[] ids = { 12, 8, 2, 10, 6, 4, 14 };
            for (int i = 0; i < ids.Length; i++) { s.villagers[i] = TestBoardFactory.MakeIdleVillager(ids[i], 0, 1, b); s.villagers[i].suit = i == 6 ? SuitType.Medic : SuitType.Warrior; }
            Tick(s); Assert.AreEqual(12, s.nodes[1].structureHP); Assert.AreEqual(-976, s.nodes[1].claimBar);
            foreach (var v in s.villagers) Assert.AreEqual(v.villagerID <= 8 ? VillagerState.AttackingStructure : VillagerState.Claiming, v.state);
            return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void FortificationRebuy_StartsAtLevelOne(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); s.nodes[1].fortressLevel = 3; s.nodes[1].structureHP = 1;
            Tick(s); Assert.AreEqual(0, s.nodes[1].fortressLevel); Assert.AreEqual(0, s.nodes[1].structureHP);
            s.villagers = new VillagerData[0]; s.players[1].materials = 30;
            var command = new GameCommand { type = CommandType.UpgradeFortress, playerID = 1, targetNodeID = 1, villagerID = -1, value = 0 };
            CommandProcessor.ProcessCommand(s, command); Assert.AreEqual(26, s.players[1].materials);
            Assert.AreEqual(1, s.nodes[1].fortressLevel); Assert.AreEqual(16, s.nodes[1].structureHP);
            Assert.AreEqual(StructureKind.Fortification, s.nodes[1].structureKind);
            s.nodes[1].structureHP = 5; CommandProcessor.ProcessCommand(s, command);
            Assert.AreEqual(18, s.players[1].materials); Assert.AreEqual(2, s.nodes[1].fortressLevel);
            Assert.AreEqual(5, s.nodes[1].structureHP); CommandProcessor.ProcessCommand(s, command);
            Assert.AreEqual(3, s.nodes[1].fortressLevel); Assert.AreEqual(5, s.nodes[1].structureHP); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void DestructionAura_UsesTickStartAndNoStructuralResistance(bool determinism) => Repeat(() => {
            var b = Configure(100); var s = Board(b); s.nodes[1].fortressLevel = 3; s.nodes[1].structureHP = 1;
            s.nodes[2].districtType = DistrictType.None; s.nodes[2].claimBar = -1000;
            Array.Resize(ref s.villagers, 2); s.villagers[1] = TestBoardFactory.MakeIdleVillager(1, 0, 2, b);
            Tick(s); Assert.AreEqual(0, s.nodes[1].structureHP); Assert.AreEqual(-934, s.nodes[2].claimBar);
            Tick(s); Assert.AreEqual(-834, s.nodes[2].claimBar); return s;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void StructureFields_HashCopyAndRollback(bool determinism) => Repeat(() => {
            var s = Board(Configure()); var copy = new SimulationState(); copy.CopyFrom(s);
            int hash = SimulationStateHasher.ComputeHash(s); Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(copy));
            Assert.AreEqual(StructureKind.Fortification, copy.nodes[1].structureKind); Assert.AreEqual(16, copy.nodes[1].structureHP);
            copy.nodes[1].structureKind = StructureKind.Minion; Assert.AreNotEqual(hash, SimulationStateHasher.ComputeHash(copy));
            copy.CopyFrom(s); copy.nodes[1].structureHP = 9; Assert.AreNotEqual(hash, SimulationStateHasher.ComputeHash(copy));
            Assert.AreEqual(16, s.nodes[1].structureHP); Assert.AreEqual(StructureKind.Fortification, s.nodes[1].structureKind);
            copy.CopyFrom(s); Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(copy)); return s;
        }, determinism);
    }
}
