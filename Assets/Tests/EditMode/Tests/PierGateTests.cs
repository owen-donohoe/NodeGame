using System;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class PierGateTests
    {
        private static SimulationState Board(int owner = 1, int bar = -80, int bodies = 1)
        {
            var b = GameBalanceData.Default();
            b.baseMoveSpeedTicks = 16; b.baseClaimPerTick = 10; b.claimThreshold = 1000;
            b.decrementMultiplier = 4; b.captureBonusPercentPerStep = 0;
            b.tempoStageTicks = null; b.tempoClaimPercent = null;
            MatchFactory.Configure(b, new BoardConfigData { ownedMultiplier = 50, partiallyOwnedMultiplier = 75, unownedMultiplier = 100, enemyPartiallyOwnedMultiplier = 150, enemyOwnedMultiplier = 200 });
            var s = TestBoardFactory.BuildSquareBoard(b);
            s.nodes[0].links = new[] { new Link { toNodeID = 1, travelWeight = 1 } };
            s.nodes[1].links = new[] { new Link { toNodeID = 0, travelWeight = 1 }, new Link { toNodeID = 2, travelWeight = 1 } };
            s.nodes[2].links = new[] { new Link { toNodeID = 1, travelWeight = 1 }, new Link { toNodeID = 3, travelWeight = 1 } };
            s.nodes[3].links = new[] { new Link { toNodeID = 2, travelWeight = 1 } };
            s.nodes[1].districtType = s.nodes[1].baseDistrictType = DistrictType.Pier;
            s.nodes[1].ownerID = owner; s.nodes[1].claimBar = bar;
            var model = s.villagers[0]; s.villagers = new VillagerData[bodies];
            for (int i = 0; i < bodies; i++)
            {
                s.villagers[i] = model; s.villagers[i].villagerID = i;
                s.villagers[i].currentNodeID = 1; s.villagers[i].previousNodeID = 0;
                s.villagers[i].targetNodeID = 2;
            }
            return s;
        }
        private static void Move(SimulationState s, int destination, int id = 0) =>
            CommandProcessor.ProcessCommand(s, new GameCommand { type = CommandType.Move, playerID = 0,
                villagerID = id, targetNodeID = destination, issuedOnTick = s.tickCount });
        private static void Ticks(SimulationState s, int n) { for (int i = 0; i < n; i++) GameSimulation.SimulateTick(s); }
        private static void Repeat(Func<SimulationState> run) =>
            Assert.AreEqual(SimulationStateHasher.ComputeHash(run()), SimulationStateHasher.ComputeHash(run()));

        private static SimulationState Transit()
        {
            var s = Board(); Ticks(s, 1);
            Assert.AreEqual(-40, s.nodes[1].claimBar); Assert.AreEqual(VillagerState.Claiming, s.villagers[0].state);
            Ticks(s, 1); Assert.AreEqual(0, s.nodes[1].claimBar); Assert.AreEqual(-1, s.nodes[1].ownerID);
            Assert.AreEqual(2, s.villagers[0].targetNodeID); Assert.AreEqual(VillagerState.Moving, s.villagers[0].state);
            Assert.AreEqual(0, s.villagers[0].moveProgress); Ticks(s, 1); Assert.AreEqual(1, s.villagers[0].moveProgress);
            var four = Board(bar: -160, bodies: 4); Ticks(four, 1);
            Assert.AreEqual(0, four.nodes[1].claimBar); Assert.AreEqual(-1, four.nodes[1].ownerID);
            // Arrival during a longer route must stop before crossing the next leg.
            var arrival = Board(bar: -1000); arrival.villagers[0].currentNodeID = 0; Move(arrival, 2);
            Ticks(arrival, 16); Assert.AreEqual(1, arrival.villagers[0].currentNodeID);
            Assert.AreEqual(VillagerState.Claiming, arrival.villagers[0].state); Assert.AreEqual(0, arrival.villagers[0].moveLegDurationTicks);
            return s;
        }
        [Test] public void TransitStopsUntilNeutralThenResumes() => Transit();
        [Test] public void TransitStopsUntilNeutralThenResumes_Determinism() => Repeat(Transit);

        private static SimulationState Destination()
        {
            var s = Board(); s.villagers[0].targetNodeID = 1; Ticks(s, 2);
            Assert.AreEqual(0, s.nodes[1].claimBar); Assert.AreEqual(VillagerState.Claiming, s.villagers[0].state);
            Ticks(s, 100); Assert.AreEqual(1000, s.nodes[1].claimBar); Assert.AreEqual(0, s.nodes[1].ownerID);
            Assert.AreEqual(1, s.villagers[0].currentNodeID); return s;
        }
        [Test] public void DestinationPier_ContinuesToFullOwnership() => Destination();
        [Test] public void DestinationPier_ContinuesToFullOwnership_Determinism() => Repeat(Destination);

        private static SimulationState Garrison()
        {
            var s = Board(); var defender = s.villagers[0]; defender.villagerID = 1; defender.ownerID = 1;
            defender.targetNodeID = -1; defender.hp = 1; defender.attackDamage = 0;
            Array.Resize(ref s.villagers, 2); s.villagers[1] = defender;
            s.villagers[0].attackCooldownMax = 1; s.villagers[0].attackCooldownRemaining = 1;
            Ticks(s, 1); Assert.AreEqual(-80, s.nodes[1].claimBar);
            Assert.AreEqual(VillagerState.Dead, s.villagers[1].state); Assert.AreEqual(-1, s.villagers[1].targetNodeID);
            Assert.AreEqual(VillagerState.Claiming, s.villagers[0].state); Assert.AreEqual(2, s.villagers[0].targetNodeID);
            Ticks(s, 1); Assert.AreEqual(-40, s.nodes[1].claimBar); return s;
        }
        [Test] public void GarrisonForcesCombat_BlockedLinePersists() => Garrison();
        [Test] public void GarrisonForcesCombat_BlockedLinePersists_Determinism() => Repeat(Garrison);

        private static SimulationState Escape()
        {
            var s = Board(bar: -1000, bodies: 5); Ticks(s, 1);
            for (int i = 0; i < 5; i++) { Assert.AreEqual(2, s.villagers[i].targetNodeID); Assert.AreEqual(1, s.villagers[i].currentNodeID); }
            Assert.AreEqual(VillagerState.Idle, s.villagers[4].state); Assert.AreEqual(-840, s.nodes[1].claimBar);
            Move(s, 2, 4); Assert.AreEqual(VillagerState.Idle, s.villagers[4].state);
            Ticks(s, 1); Assert.AreEqual(VillagerState.Idle, s.villagers[4].state); Assert.AreEqual(-680, s.nodes[1].claimBar);
            Garrison(); return s;
        }
        [Test] public void GateEscapePaths_AllBlocked() => Escape();
        [Test] public void GateEscapePaths_AllBlocked_Determinism() => Repeat(Escape);

        private static SimulationState Retreat()
        {
            var s = Board(bar: -1000); Move(s, 2);
            Assert.AreEqual(2, s.villagers[0].targetNodeID); Assert.AreNotEqual(VillagerState.Moving, s.villagers[0].state);
            Move(s, 0); Assert.AreEqual(VillagerState.Moving, s.villagers[0].state);
            Assert.AreEqual(16, s.villagers[0].moveLegDurationTicks); Ticks(s, 16);
            Assert.AreEqual(0, s.villagers[0].currentNodeID); return s;
        }
        [Test] public void GateRetreat_ToPreviousNodeOnly() => Retreat();
        [Test] public void GateRetreat_ToPreviousNodeOnly_Determinism() => Repeat(Retreat);

        private static SimulationState Highway()
        {
            foreach (int owner in new[] { 0, -1, 1 })
            {
                var s = Board(owner, owner == 0 ? 1000 : owner == 1 ? -1000 : 0);
                s.villagers[0].currentNodeID = 0; Move(s, 1);
                int duration = owner == 0 ? 8 : 16; Assert.AreEqual(duration, s.villagers[0].moveLegDurationTicks);
                Ticks(s, duration - 1); Assert.AreEqual(0, s.villagers[0].currentNodeID);
                // Mid-edge ownership changes cannot alter this clock.
                s.nodes[1].ownerID = owner == 0 ? 1 : 0; Ticks(s, 1);
                Assert.AreEqual(1, s.villagers[0].currentNodeID); Assert.AreEqual(0, s.villagers[0].moveLegDurationTicks);
            }
            var land = Board(0, 1000); land.nodes[1].districtType = DistrictType.None;
            land.villagers[0].currentNodeID = 0; Move(land, 1); Assert.AreEqual(16, land.villagers[0].moveLegDurationTicks);
            return land;
        }
        [Test] public void OwnerHighway_HalvesActualTravelOnce() => Highway();
        [Test] public void OwnerHighway_HalvesActualTravelOnce_Determinism() => Repeat(Highway);

        private static SimulationState Reverse()
        {
            var s = Board(0, 1000); s.villagers[0].currentNodeID = 0; Move(s, 1); Ticks(s, 3);
            Move(s, 1); Assert.AreEqual(3, s.villagers[0].moveProgress); Assert.AreEqual(8, s.villagers[0].moveLegDurationTicks);
            Move(s, 0); Assert.AreEqual(5, s.villagers[0].moveProgress); Assert.AreEqual(8, s.villagers[0].moveLegDurationTicks);
            Ticks(s, 2); Assert.AreEqual(VillagerState.Moving, s.villagers[0].state);
            Ticks(s, 1); Assert.AreEqual(VillagerState.Idle, s.villagers[0].state); Assert.AreEqual(0, s.villagers[0].moveLegDurationTicks);
            var combat = Board(0, 1000); combat.villagers[0].currentNodeID = 0; Move(combat, 1); Ticks(combat, 3); Move(combat, 0);
            var enemy = combat.villagers[0]; enemy.ownerID = 1; enemy.villagerID = 1; enemy.state = VillagerState.Idle;
            enemy.movePath = new int[0]; enemy.targetNodeID = -1; enemy.hp = 100;
            Array.Resize(ref combat.villagers, 2); combat.villagers[1] = enemy; Ticks(combat, 1);
            Assert.AreEqual(VillagerState.Fighting, combat.villagers[0].state); Assert.AreEqual(0, combat.villagers[0].moveLegDurationTicks);
            return s;
        }
        [Test] public void ReverseIntoAsymmetricLeg_PaysCoveredTicks() => Reverse();
        [Test] public void ReverseIntoAsymmetricLeg_PaysCoveredTicks_Determinism() => Repeat(Reverse);

        private static SimulationState GateCapById()
        {
            var s = Board(bar: -1000, bodies: 5);
            for (int i = 0; i < 5; i++) s.villagers[i].villagerID = 8 - 2 * i;
            Ticks(s, 1); Assert.AreEqual(-840, s.nodes[1].claimBar);
            Assert.AreEqual(VillagerState.Idle, s.villagers[0].state);
            for (int i = 1; i < 5; i++) Assert.AreEqual(VillagerState.Claiming, s.villagers[i].state);
            return s;
        }
        [Test] public void GateCap_LowestVillagerIdsClaim() => GateCapById();
        [Test] public void GateCap_LowestVillagerIdsClaim_Determinism() => Repeat(GateCapById);

        private static SimulationState OddHighway()
        {
            var s = Board(0, 1000); s.villagers[0].currentNodeID = 0;
            s.villagers[0].moveSpeedTicks = 3; Move(s, 2);
            Assert.AreEqual(2, s.villagers[0].moveLegDurationTicks); Ticks(s, 2);
            Assert.AreEqual(1, s.villagers[0].currentNodeID); Assert.AreEqual(3, s.villagers[0].moveLegDurationTicks);
            var one = Board(0, 1000); one.villagers[0].currentNodeID = 0; one.villagers[0].moveSpeedTicks = 1;
            Move(one, 1); Assert.AreEqual(1, one.villagers[0].moveLegDurationTicks);
            var copy = new SimulationState(); copy.CopyFrom(s); Assert.AreEqual(3, copy.villagers[0].moveLegDurationTicks);
            copy.villagers[0].moveLegDurationTicks = 20; Assert.AreEqual(3, s.villagers[0].moveLegDurationTicks);
            return s;
        }
        [Test] public void Highway_CeilingMinimumAndNextLegLatch() => OddHighway();
        [Test] public void Highway_CeilingMinimumAndNextLegLatch_Determinism() => Repeat(OddHighway);

        private static SimulationState GateCostSnapshot()
        {
            var s = Routing(); s.nodes[2].ownerID = -1; s.nodes[1].ownerID = 1; s.nodes[1].claimBar = -81;
            s.nodes[0].links[1].travelWeight = 2; s.nodes[2].links[0].travelWeight = 3;
            // Speed 1: gate 2 + ceil(81/40)=3 ties detour5; lower ID wins.
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, Pathfinding.FindPath(s, 0, 0, 3, 1));
            s.nodes[1].claimBar = -121; // ceil(121/40)=4, now detour wins
            CollectionAssert.AreEqual(new[] { 0, 2, 3 }, Pathfinding.FindPath(s, 0, 0, 3, 1));
            var b = GameBalanceData.Default(); b.baseClaimPerTick = 10;
            b.captureBonusPercentPerStep = 100; b.captureBonusMaxSteps = 1;
            b.tempoStageTicks = b.tempoClaimPercent = null;
            GameSimulation.SetBalance(b); CommandProcessor.SetBalance(b);
            s.nodes[1].links = new[] { new Link { toNodeID = 0, travelWeight = 1 }, new Link { toNodeID = 3, travelWeight = 1 } };
            s.nodes[0].ownerID = 0; s.nodes[2].ownerID = -1;
            // Current friendly frontier doubles the rate to80, delay2: gate wins.
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, Pathfinding.FindPath(s, 0, 0, 3, 1));
            // Current enemy Fortress support removes frontier bonus and resists at50%.
            s.nodes[3].districtType = DistrictType.Fortress; s.nodes[3].ownerID = 1;
            s.nodes[3].fortressLevel = 3; s.nodes[3].links = new[] { new Link { toNodeID = 1, travelWeight = 1 } };
            CollectionAssert.AreEqual(new[] { 0, 2, 3 }, Pathfinding.FindPath(s, 0, 0, 3, 1));
            return s;
        }
        [Test] public void GateCost_CeilingAndCurrentFrontierResistance() => GateCostSnapshot();
        [Test] public void GateCost_CeilingAndCurrentFrontierResistance_Determinism() => Repeat(GateCostSnapshot);

        private static SimulationState Routing()
        {
            var s = Board(bar: -800); // lone rate 40 => 20 gate ticks
            s.nodes[0].links = new[] { new Link { toNodeID = 1, travelWeight = 1 }, new Link { toNodeID = 2, travelWeight = 2 } };
            s.nodes[1].links = new[] { new Link { toNodeID = 3, travelWeight = 1 } };
            s.nodes[2].links = new[] { new Link { toNodeID = 3, travelWeight = 2 } };
            s.nodes[3].districtType = DistrictType.None; s.nodes[3].ownerID = -1; s.nodes[3].claimBar = 0;
            s.players[1].coreNodeID = 0;
            Pathfinding.EnemyOwnedMultiplier = 100;
            // Physical gate route 16 + delay 20, versus physical detour 32.
            CollectionAssert.AreEqual(new[] { 0, 2, 3 }, Pathfinding.FindPath(s, 0, 0, 3, 8));
            s.nodes[1].claimBar = -320; // physical 16 + delay 8 wins
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, Pathfinding.FindPath(s, 0, 0, 3, 8));
            // A faster unit pays a larger relative gate penalty and chooses the detour.
            CollectionAssert.AreEqual(new[] { 0, 2, 3 }, Pathfinding.FindPath(s, 0, 0, 3, 2));
            // No gate delay on the start node, even though its bar remains enemy-owned.
            CollectionAssert.AreEqual(new[] { 1, 3 }, Pathfinding.FindPath(s, 0, 1, 3, 8));
            s.nodes[1].ownerID = -1; s.nodes[1].claimBar = 0;
            s.nodes[0].links[1].travelWeight = 1; s.nodes[2].links[0].travelWeight = 1;
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, Pathfinding.FindPath(s, 0, 0, 3, 8));
            // Into an own Pier: 8 physical *100%; into owned land: 16 physical *50%.
            s.nodes[1].ownerID = 0; s.nodes[2].ownerID = 0;
            CollectionAssert.AreEqual(new[] { 0, 1, 3 }, Pathfinding.FindPath(s, 0, 0, 3, 16));
            return s;
        }
        [Test] public void Routing_DetourBeatsGateCostAndTiesUseNodeId() => Routing();
        [Test] public void Routing_DetourBeatsGateCostAndTiesUseNodeId_Determinism() => Repeat(Routing);
    }
}
