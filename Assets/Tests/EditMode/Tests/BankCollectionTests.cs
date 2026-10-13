using System;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Tests
{
    public class BankCollectionTests
    {
        [Test] public void PresentationReasonsAgreeWithEligibility()
        {
            var b=Configure();
            for(int fixture=0;fixture<6;fixture++) {
                var s=Board(b); s.nodes[1].collectRequested=true;
                if(fixture==1) s.nodes[1].claimBar--;
                if(fixture==2) s.nodes[1].ownerID=-1;
                if(fixture==3) s.villagers=new[] { TestBoardFactory.MakeIdleVillager(0,1,1,b) };
                if(fixture==4) s.players[0].food=b.foodCap;
                if(fixture==5) s.nodes[1].bankFood=0;
                int hash=SimulationStateHasher.ComputeHash(s);
                var reason=BankRules.CollectionState(s,s.nodes[1],b);
                Assert.AreEqual(BankRules.Locked(s,s.nodes[1],b),reason==CollectState.Locked || reason==CollectState.Neutral);
                Assert.AreEqual(s.nodes[1].ownerID==0 && BankRules.Total(s.nodes[1])>0,BankRules.CanCollect(s,Collect()));
                Assert.AreEqual(hash,SimulationStateHasher.ComputeHash(s));
            }
        }
        private static GameBalanceData Configure()
        {
            var b = GameBalanceData.Default();
            b.tempoStageTicks = b.tempoProductionPercent = b.tempoClaimPercent = b.tempoRespawnPercent = null;
            b.captureBonusPercentPerStep = 0;
            b.decrementMultiplier = 1; // one civilian advances exactly baseClaimPerTick
            MatchFactory.Configure(b, BoardFixtures.LandGrid3x3());
            return b;
        }
        private static SimulationState Board(GameBalanceData b, int food = 5)
        {
            var s = TestBoardFactory.BuildThreeNodeBoard(b);
            s.nodes[1].districtType = s.nodes[1].baseDistrictType = DistrictType.Storehouse;
            s.nodes[1].ownerID = 0; s.nodes[1].claimBar = b.claimThreshold;
            s.nodes[1].bankProductionRemaining = 1000; // isolate collection from new output
            s.nodes[1].bankFood = food;
            s.villagers = new VillagerData[0];
            return s;
        }
        private static GameCommand Collect(int value = 1, int owner = 0) => new GameCommand
        { type = (CommandType)9, playerID = owner, villagerID = -1, targetNodeID = 1, issuedOnTick = 123, value = value };
        private static void Tick(SimulationState s, int count = 1)
        {
            for (int i = 0; i < count; i++) {
                GameSimulation.SimulateTick(s);

            }
        }
        private static int Fold(int h, SimulationState s) => unchecked(h * 31 + SimulationStateHasher.ComputeHash(s));
        private static void Repeat(Func<int> run, bool determinism)
        { int hash = run(); if (determinism) Assert.AreEqual(hash, run()); }
        private static void Empty(SimulationState s)
        { Assert.AreEqual(0, BankRules.Total(s.nodes[1])); Assert.AreEqual(0, s.nodes[1].collectProgress); Assert.IsFalse(s.nodes[1].collectRequested); }

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void FiveUnitsDrainOnFourSevenTenThirteenSixteen(bool determinism) => Repeat(() => {
            int hash = 0;
            foreach (int tempo in new[] { 100, 200 }) {
                var b = Configure(); b.tempoStageTicks = new[] { 1 }; b.tempoProductionPercent = new[] { tempo };
                MatchFactory.Configure(b, BoardFixtures.LandGrid3x3()); var s = Board(b);
                CommandProcessor.ProcessCommand(s, Collect()); Assert.AreEqual(0, s.players[0].food); Assert.AreEqual(5, s.nodes[1].bankFood);
                for (int t = 1; t <= 16; t++) {
                    Tick(s); int paid = t * 5 / 16;
                    Assert.AreEqual(paid, s.players[0].food, "tick " + t); Assert.AreEqual(5 - paid, s.nodes[1].bankFood);
                    Assert.AreEqual(t == 16 ? 0 : t * 5 % 16, s.nodes[1].collectProgress);
                }
                Empty(s); hash = Fold(hash, s);
            }
            return hash;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void LockPausesUntilRestoreCompletes(bool determinism) => Repeat(() => {
            int hash = 0;
            for (int cause = 0; cause < 3; cause++) {
                var b = Configure(); var s = Board(b); s.nodes[1].collectProgress = 10; s.nodes[1].collectRequested = true;
                if (cause == 0) {
                    s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 1, 1, b) };
                    s.villagers[0].state = VillagerState.Moving; s.villagers[0].movePath = new[] { 1, 2 }; s.villagers[0].targetNodeID = 2;
                    s.villagers[0].moveSpeedTicks = 100;
                }
                if (cause == 1) s.nodes[1].claimBar -= b.baseClaimPerTick;
                if (cause == 2) { s.nodes[1].ownerID = -1; s.nodes[1].claimBar = 0; }
                Assert.IsTrue(BankRules.Locked(s, s.nodes[1], b)); Tick(s, 2);
                Assert.AreEqual(10, s.nodes[1].collectProgress); Assert.AreEqual(5, s.nodes[1].bankFood);
                if (cause != 2) {
                    s.nodes[1].claimBar = b.claimThreshold - b.baseClaimPerTick;
                    s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 0, 1, b) };
                    Tick(s); Assert.AreEqual(b.claimThreshold, s.nodes[1].claimBar); Assert.AreEqual(15, s.nodes[1].collectProgress);
                    Tick(s); Assert.AreEqual(1, s.players[0].food); Assert.AreEqual(4, s.nodes[1].collectProgress);
                }
                hash = Fold(hash, s);
            }
            return hash;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void PassingVillagerDoesNotBypassDwell(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b);
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 0, 1, b) };
            CommandProcessor.ProcessCommand(s, new GameCommand { type = CommandType.Move, playerID = 0, villagerID = 0, targetNodeID = 2 });
            Tick(s); Assert.AreEqual(0, s.nodes[1].collectProgress); Assert.AreEqual(0, s.players[0].food);
            int hash = Fold(0, s);
            for (int count = 1; count <= 2; count++) {
                s = Board(Configure()); s.villagers = new VillagerData[count];
                for (int i = 0; i < count; i++) s.villagers[i] = TestBoardFactory.MakeIdleVillager(i, 0, 1, b);
                Tick(s, 3); Assert.AreEqual(0, s.players[0].food); Tick(s); Assert.AreEqual(1, s.players[0].food);
                Assert.AreEqual(4, s.nodes[1].bankFood); hash = Fold(hash, s);
            }
            // A worker also collects, while its ordinary timer stays independent.
            s = Board(Configure()); s.nodes[1].districtType = DistrictType.Farm;
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 0, 1, b) };
            Tick(s, 4); Assert.AreEqual(VillagerState.Working, s.villagers[0].state); Assert.AreEqual(1, s.players[0].food);
            return Fold(hash, s);
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void NoCollectorResetsProgress_RemoteRequestPersists(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); s.nodes[1].collectProgress = 15;
            Tick(s); Assert.AreEqual(0, s.nodes[1].collectProgress);
            CommandProcessor.ProcessCommand(s, Collect()); s.nodes[1].collectProgress = 15;
            Tick(s); Assert.AreEqual(1, s.players[0].food); Assert.AreEqual(4, s.nodes[1].collectProgress); Assert.IsTrue(s.nodes[1].collectRequested);
            CommandProcessor.ProcessCommand(s, Collect(0)); Assert.AreEqual(0, s.nodes[1].collectProgress); Assert.IsFalse(s.nodes[1].collectRequested);
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 0, 1, b) };
            CommandProcessor.ProcessCommand(s, Collect()); s.nodes[1].collectProgress = 15;
            CommandProcessor.ProcessCommand(s, Collect(0)); Assert.AreEqual(15, s.nodes[1].collectProgress);
            Tick(s); Assert.AreEqual(2, s.players[0].food); Assert.AreEqual(4, s.nodes[1].collectProgress);
            s.villagers = new VillagerData[0]; Tick(s); Assert.AreEqual(0, s.nodes[1].collectProgress);
            return Fold(0, s);
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void CapsSkipResourcesWithoutDestroyingBank(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b, 1); s.nodes[1].bankMaterials = s.nodes[1].bankMetal = 1;
            s.players[0].food = b.foodCap; CommandProcessor.ProcessCommand(s, Collect());
            Tick(s, 4); Assert.AreEqual(1, s.players[0].materials); Assert.AreEqual(1, s.nodes[1].bankFood); Assert.AreEqual(0, s.nodes[1].bankMaterials);
            Tick(s, 3); Assert.AreEqual(1, s.players[0].metal); Assert.AreEqual(1, s.nodes[1].bankFood);
            Tick(s, 5); Assert.AreEqual(3, s.nodes[1].collectProgress); Assert.AreEqual(1, s.nodes[1].bankFood);
            s.players[0].food--; Tick(s, 3); Empty(s); Assert.AreEqual(b.foodCap, s.players[0].food);
            int hash = Fold(0, s);
            s = Board(Configure(), 1); s.nodes[1].bankMaterials = s.nodes[1].bankMetal = 1;
            s.players[0].food = b.foodCap; s.players[0].materials = b.materialsCap; s.players[0].metal = b.metalCap;
            s.nodes[1].collectProgress = 10; CommandProcessor.ProcessCommand(s, Collect()); Tick(s, 4);
            Assert.AreEqual(10, s.nodes[1].collectProgress); Assert.AreEqual(3, BankRules.Total(s.nodes[1]));
            // Zero pool caps are uncapped, just as in PayBank.
            b.foodCap = b.materialsCap = b.metalCap = 0; MatchFactory.Configure(b, BoardFixtures.LandGrid3x3());
            Tick(s, 2); Assert.AreEqual(31, s.players[0].food); Assert.AreEqual(0, s.nodes[1].bankFood);
            return Fold(hash, s);
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void CaptureTakesOnlyRemainingBank(bool determinism) => Repeat(() => {
            int hash = 0;
            for (int overflow = 0; overflow < 2; overflow++) {
                var b = Configure(); var s = Board(b); CommandProcessor.ProcessCommand(s, Collect()); Tick(s, 4);
                Assert.AreEqual(1, s.players[0].food); Assert.AreEqual(4, s.nodes[1].bankFood);
                s.players[1].food = overflow == 1 ? b.foodCap - 1 : 0;
                s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 1, 1, b) };
                s.nodes[1].districtHealth = 0; s.nodes[1].claimBar = -b.claimThreshold + 1;
                Tick(s); Assert.AreEqual(overflow == 1 ? b.foodCap : 4, s.players[1].food);
                Assert.AreEqual(1, s.players[0].food); Empty(s);
                Tick(s); Assert.AreEqual(overflow == 1 ? b.foodCap : 4, s.players[1].food); hash = Fold(hash, s);
            }
            return hash;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void Collect_RoundTripsStartCancelAndRejectsNonOwner(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b);
            foreach (var c in new[] { Collect(1, 1), Collect(0, 1), Collect(2), Collect(-1), Collect(1, -1), Collect(1, 2) }) {
                int h = SimulationStateHasher.ComputeHash(s); CommandProcessor.ProcessCommand(s, c); Assert.AreEqual(h, SimulationStateHasher.ComputeHash(s));
            }
            foreach (int invalid in new[] { -1, 3 }) {
                var c = Collect(); c.targetNodeID = invalid; int h = SimulationStateHasher.ComputeHash(s);
                CommandProcessor.ProcessCommand(s, c); Assert.AreEqual(h, SimulationStateHasher.ComputeHash(s));
            }
            var bodyCommand = Collect(); bodyCommand.villagerID = 0; int before = SimulationStateHasher.ComputeHash(s);
            CommandProcessor.ProcessCommand(s, bodyCommand); Assert.AreEqual(before, SimulationStateHasher.ComputeHash(s));
            CommandProcessor.ProcessCommand(s, Collect()); Assert.IsTrue(s.nodes[1].collectRequested);
            CommandProcessor.ProcessCommand(s, Collect(0)); Assert.IsFalse(s.nodes[1].collectRequested);
            return Fold(0, s); // six-field wire and MatchLog assertions live in their owning projects
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void CollectWhileLocked_AcceptedAndPaused(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b); s.nodes[1].claimBar--; s.nodes[1].collectProgress = 10;
            CommandProcessor.ProcessCommand(s, Collect()); Assert.IsTrue(s.nodes[1].collectRequested);
            Tick(s, 3); Assert.AreEqual(10, s.nodes[1].collectProgress); Assert.AreEqual(5, s.nodes[1].bankFood);
            CommandProcessor.ProcessCommand(s, Collect(0)); Assert.IsFalse(s.nodes[1].collectRequested); Assert.AreEqual(0, s.nodes[1].collectProgress);
            return Fold(0, s);
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void CollectEmptyOrNonOwner_NoHashChange(bool determinism) => Repeat(() => {
            int hash = 0;
            for (int food = 0; food <= 1; food++) for (int owner = -1; owner <= 1; owner++) {
                var s = Board(Configure(), food); s.nodes[1].ownerID = owner;
                s.nodes[1].collectProgress = 10; s.nodes[1].collectRequested = true;
                for (int player = 0; player <= 1; player++) for (int value = 0; value <= 1; value++) {
                    if (food != 0 && player == owner) continue;
                    int h = SimulationStateHasher.ComputeHash(s); CommandProcessor.ProcessCommand(s, Collect(value, player));
                    Assert.AreEqual(h, SimulationStateHasher.ComputeHash(s));
                }
                hash = Fold(hash, s);
            }
            return hash;
        }, determinism);

        [TestCase(false)] [TestCase(true, TestName = "{m}_Determinism")]
        public void EnemyOnNode_HealthyStorehouseStillProduces(bool determinism) => Repeat(() => {
            var b = Configure(); var s = Board(b, 0); s.nodes[1].bankProductionRemaining = 1; s.nodes[1].districtHealth = 3000;
            s.nodes[1].collectProgress = 10; s.nodes[1].collectRequested = true;
            s.villagers = new[] { TestBoardFactory.MakeIdleVillager(0, 1, 1, b) };
            s.villagers[0].state = VillagerState.Moving; s.villagers[0].movePath = new[] { 1, 2 }; s.villagers[0].targetNodeID = 2;
            s.villagers[0].moveSpeedTicks = 100;
            Tick(s); Assert.AreEqual(1, s.nodes[1].bankFood); Assert.AreEqual(10, s.nodes[1].collectProgress);
            Assert.AreEqual(0, s.players[0].food); Assert.AreEqual(80, s.nodes[1].bankProductionRemaining);
            return Fold(0, s);
        }, determinism);

    }
}
