using System;
using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class StickyOrderTests
    {
        private static GameBalanceData balance;
        private static SimulationState Board(bool reachable = true)
        {
            balance = GameBalanceData.Default();
            balance.breachBarDecayPerTick = 0;
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);
            var s = TestBoardFactory.BuildSquareBoard(balance);
            // Line 0 -> Farm1 -> Farm2 -> enemy Core3; all fixtures fresh.
            for (int i = 0; i < 4; i++)
            {
                s.nodes[i].links = i == 0 ? new[] { new Link { toNodeID = 1, travelWeight = 1 } } :
                    i == 3 ? new[] { new Link { toNodeID = 2, travelWeight = 1 } } :
                    new[] { new Link { toNodeID = i - 1, travelWeight = 1 }, new Link { toNodeID = i + 1, travelWeight = 1 } };
            }
            if (!reachable) s.nodes[1].links = new[] { new Link { toNodeID = 0, travelWeight = 1 } };
            s.nodes[1].districtType = s.nodes[2].districtType = DistrictType.Farm;
            s.nodes[1].ownerID = s.nodes[2].ownerID = 0;
            s.nodes[1].claimBar = s.nodes[2].claimBar = balance.claimThreshold;
            s.villagers[1].state = VillagerState.Dead;
            s.villagers[1].respawnTicksRemaining = 1000;
            return s;
        }
        private static void Order(SimulationState s, int target, int player = 0) =>
            CommandProcessor.ProcessCommand(s, new GameCommand { type = CommandType.Move, playerID = player, villagerID = 0, targetNodeID = target });
        private static void Fight(SimulationState s, int node, int target)
        {
            s.villagers[0].currentNodeID = node;
            s.villagers[0].state = VillagerState.Fighting;
            s.villagers[0].targetNodeID = target;
            s.villagers[0].movePath = new[] { 0, 1, 2 };
            s.villagers[0].movePathIndex = 1;
            s.villagers[0].suit = SuitType.Farmer;
            s.villagers[0].attackCooldownRemaining = 7;
        }
        private static void Enemy(SimulationState s, int node)
        {
            s.villagers[1].currentNodeID = node;
            s.villagers[1].state = VillagerState.Fighting;
            s.villagers[1].hp = balance.baseHP;
            s.villagers[1].attackCooldownRemaining = 100;
        }
        private static void CheckRoute(SimulationState s, int target, params int[] route)
        {
            Assert.AreEqual(target, s.villagers[0].targetNodeID);
            Assert.AreEqual(VillagerState.Moving, s.villagers[0].state);
            CollectionAssert.AreEqual(route, s.villagers[0].movePath);
            Assert.AreEqual(0, s.villagers[0].moveProgress);
        }
        private static void Determinism(Func<SimulationState> scenario)
        {
            int a = SimulationStateHasher.ComputeHash(scenario());
            int b = SimulationStateHasher.ComputeHash(scenario());
            Assert.AreEqual(a, b);
        }
        private static SimulationState PendingAtUnreachableDestination(int currentOwner)
        {
            var s = Board(false);
            balance.baseClaimPerTick = 10;
            for (int i = 0; i < balance.districtStats.Length; i++)
                if (balance.districtStats[i].districtType == DistrictType.Farm)
                    balance.districtStats[i].productionTicks = 1;
            GameSimulation.SetBalance(balance);
            CommandProcessor.SetBalance(balance);
            s.nodes[0].districtType = currentOwner == 0 ? DistrictType.Farm : DistrictType.None;
            s.nodes[0].ownerID = currentOwner;
            s.nodes[0].claimBar = currentOwner == -1 ? 0 : currentOwner == 0 ? balance.claimThreshold : -balance.claimThreshold;
            s.villagers[0].state = VillagerState.Idle;
            s.villagers[0].suit = SuitType.None;
            // A dangling unreachable target (an order cannot create one since D48) is
            // dropped at the resume pass; the node then resolves like any arrival,
            // contributing from the next tick only.
            s.villagers[0].targetNodeID = 2;
            s.players[0].food = 7;
            int food = s.players[0].food;
            int bar = s.nodes[0].claimBar;
            GameSimulation.SimulateTick(s);
            Assert.AreEqual(food, s.players[0].food, "Work resolved at resume cannot produce that tick");
            Assert.AreEqual(bar, s.nodes[0].claimBar, "Claiming resolved at resume cannot claim that tick");
            Assert.AreEqual(currentOwner, s.nodes[0].ownerID);
            Assert.AreEqual(-1, s.villagers[0].targetNodeID);
            Assert.AreEqual(0, s.villagers[0].currentNodeID);
            Assert.IsEmpty(s.villagers[0].movePath);
            Assert.AreEqual(currentOwner == 0 ? VillagerState.Working : VillagerState.Claiming, s.villagers[0].state);
            return s;
        }
        [Test] public void DanglingUnreachableTarget_AtOwnedFarm_ClearsThenWorks() => PendingAtUnreachableDestination(0);
        [Test] public void DanglingUnreachableTarget_AtOwnedFarm_ClearsThenWorks_Determinism() => Determinism(() => PendingAtUnreachableDestination(0));
        [Test] public void DanglingUnreachableTarget_AtNeutralNode_ClearsThenClaims() => PendingAtUnreachableDestination(-1);
        [Test] public void DanglingUnreachableTarget_AtNeutralNode_ClearsThenClaims_Determinism() => Determinism(() => PendingAtUnreachableDestination(-1));
        [Test] public void DanglingUnreachableTarget_AtOpposingNode_ClearsThenClaims() => PendingAtUnreachableDestination(1);
        [Test] public void DanglingUnreachableTarget_AtOpposingNode_ClearsThenClaims_Determinism() => Determinism(() => PendingAtUnreachableDestination(1));
        private static SimulationState Intermediate()
        {
            var s = Board(); Fight(s, 1, 2); Enemy(s, 1);
            s.villagers[0].attackCooldownRemaining = 1;
            s.villagers[1].hp = 1;
            int claim = s.nodes[1].claimBar;
            GameSimulation.SimulateTick(s);
            CheckRoute(s, 2, 1, 2);
            Assert.AreEqual(0, s.players[0].food);
            Assert.AreEqual(claim, s.nodes[1].claimBar);
            GameSimulation.SimulateTick(s);
            Assert.AreEqual(1, s.villagers[0].moveProgress);
            return s;
        }
        private static SimulationState Ownership()
        {
            SimulationState result = null;
            foreach (bool nowFriendly in new[] { true, false })
            {
                var s = Board();
                s.villagers[0].currentNodeID = 1;
                s.nodes[2].ownerID = nowFriendly ? 1 : 0;
                s.nodes[2].claimBar = nowFriendly ? -balance.claimThreshold : balance.claimThreshold;
                Order(s, 2);
                GameSimulation.SimulateTick(s);
                s.nodes[2].ownerID = nowFriendly ? 0 : 1;
                s.nodes[2].claimBar = nowFriendly ? balance.claimThreshold : -balance.claimThreshold;
                Assert.AreEqual(2, s.villagers[0].targetNodeID);
                for (int i = 1; i < balance.baseMoveSpeedTicks; i++) GameSimulation.SimulateTick(s);
                Assert.AreEqual(nowFriendly ? VillagerState.Working : VillagerState.Claiming, s.villagers[0].state);
                Assert.AreEqual(-1, s.villagers[0].targetNodeID);
                result = s;
            }
            return result;
        }
        private static SimulationState ContestedDestination()
        {
            var s = Board(); s.villagers[0].currentNodeID = 1; Enemy(s, 2);
            Order(s, 2);
            for (int i = 0; i < balance.baseMoveSpeedTicks; i++) GameSimulation.SimulateTick(s);
            Assert.AreEqual(2, s.villagers[0].currentNodeID);
            Assert.AreEqual(VillagerState.Fighting, s.villagers[0].state);
            Assert.AreEqual(2, s.villagers[0].targetNodeID);
            Assert.AreEqual(0, s.players[0].food);
            s.villagers[0].attackCooldownRemaining = 1; s.villagers[1].hp = 1;
            GameSimulation.SimulateTick(s);
            Assert.AreEqual(VillagerState.Working, s.villagers[0].state);
            Assert.AreEqual(-1, s.villagers[0].targetNodeID);
            Assert.AreEqual(0, s.players[0].food);
            Assert.AreEqual(s.villagers[0].productionTicksMax, s.villagers[0].productionTicksRemaining);
            return s;
        }
        [Test] public void ContestedDestination_RetainsIntentUntilArrivalActionBegins() => ContestedDestination();
        [Test] public void ContestedDestination_RetainsIntentUntilArrivalActionBegins_Determinism() => Determinism(ContestedDestination);
        // D48: an order with no route is refused outright and changes nothing --
        // standing, mid-leg or fighting -- matching the order preview.
        private static void AssertRefused(SimulationState s, int target)
        {
            int before = SimulationStateHasher.ComputeHash(s);
            Order(s, target);
            Assert.AreEqual(before, SimulationStateHasher.ComputeHash(s), "A move with no route changes nothing");
        }
        private static SimulationState Unreachable()
        {
            var s = Board(false);
            s.villagers[0].state = VillagerState.Idle;
            AssertRefused(s, 2);
            Assert.AreEqual(-1, s.villagers[0].targetNodeID);
            Order(s, 1);
            CheckRoute(s, 1, 0, 1);
            AssertRefused(s, 2); // on the leg before any progress
            CheckRoute(s, 1, 0, 1);
            GameSimulation.SimulateTick(s);
            AssertRefused(s, 2); // mid-leg, crossing already paid
            Assert.AreEqual(1, s.villagers[0].targetNodeID);
            Assert.AreEqual(1, s.villagers[0].moveProgress);
            var fight = Board(false); Fight(fight, 1, -1); Enemy(fight, 1);
            AssertRefused(fight, 2); // fighting
            Assert.AreEqual(-1, fight.villagers[0].targetNodeID);
            Assert.AreEqual(7, fight.villagers[0].attackCooldownRemaining);
            return s;
        }
        private static SimulationState OverrideFight()
        {
            var s = Board(); Fight(s, 1, 2); Enemy(s, 1); Order(s, 3);
            Assert.AreEqual(VillagerState.Fighting, s.villagers[0].state);
            Assert.AreEqual(7, s.villagers[0].attackCooldownRemaining);
            Assert.AreEqual(3, s.villagers[0].targetNodeID);
            s.villagers[1].hp = 0; GameSimulation.SimulateTick(s);
            CheckRoute(s, 3, 1, 2, 3); return s;
        }
        private static SimulationState Cancel()
        {
            var s = Board(); Fight(s, 1, 2); Enemy(s, 1); Order(s, 1);
            Assert.AreEqual(-1, s.villagers[0].targetNodeID);
            Assert.AreEqual(VillagerState.Fighting, s.villagers[0].state);
            Assert.AreEqual(7, s.villagers[0].attackCooldownRemaining);
            s.villagers[1].hp = 0; GameSimulation.SimulateTick(s);
            Assert.AreEqual(VillagerState.Working, s.villagers[0].state);
            Assert.AreEqual(0, s.players[0].food); return s;
        }
        private static SimulationState InterruptedBreach()
        {
            var s = Board(); balance.captureBonusPercentPerStep = 0; GameSimulation.SetBalance(balance);
            s.villagers[0].currentNodeID = 2; Order(s, 3);
            for (int i = 0; i < balance.baseMoveSpeedTicks; i++) GameSimulation.SimulateTick(s);
            Assert.AreEqual(VillagerState.Breaching, s.villagers[0].state);
            Assert.AreEqual(3, s.villagers[0].targetNodeID);
            Enemy(s, 3); GameSimulation.SimulateTick(s);
            Assert.AreEqual(VillagerState.Fighting, s.villagers[0].state);
            Assert.AreEqual(3, s.villagers[0].targetNodeID);
            int bar = s.players[1].breachBar;
            s.villagers[0].attackCooldownRemaining = 1; s.villagers[1].hp = 1;
            GameSimulation.SimulateTick(s);
            Assert.AreEqual(VillagerState.Breaching, s.villagers[0].state);
            Assert.AreEqual(bar, s.players[1].breachBar);
            Assert.AreEqual(3, s.villagers[0].targetNodeID);
            GameSimulation.SimulateTick(s);
            Assert.AreEqual(bar + balance.breachSwarmRate[0], s.players[1].breachBar);
            return s;
        }
        private static SimulationState Away()
        {
            var s = Board(); balance.breachBarDecayPerTick = 5; GameSimulation.SetBalance(balance);
            s.villagers[0].currentNodeID = 3; s.villagers[0].state = VillagerState.Breaching;
            s.villagers[0].targetNodeID = 3; s.players[1].breachBar = 50;
            Order(s, 2); CheckRoute(s, 2, 3, 2);
            GameSimulation.SimulateTick(s); Assert.AreEqual(45, s.players[1].breachBar);
            s.villagers[0].hp = 0; GameSimulation.SimulateTick(s);
            Assert.AreEqual(-1, s.villagers[0].targetNodeID);
            var consumed = Board(); consumed.villagers[0].currentNodeID = 3;
            consumed.villagers[0].state = VillagerState.Breaching; consumed.villagers[0].targetNodeID = 3;
            consumed.players[1].breachBar = balance.breachBarMax - balance.breachSwarmRate[0];
            GameSimulation.SimulateTick(consumed);
            Assert.IsTrue(consumed.villagers[0].isConsumed);
            Assert.AreEqual(-1, consumed.villagers[0].targetNodeID); return consumed;
        }
        private static SimulationState Terminal()
        {
            var s = Board(); s.players[1].coreNodeID = 2;
            s.nodes[2].districtType = DistrictType.Core; s.nodes[2].ownerID = 1;
            s.nodes[3].districtType = DistrictType.None;
            CollectionAssert.IsEmpty(Pathfinding.FindPath(s, 0, 0, 3, moveSpeedTicks: 4));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Pathfinding.FindPath(s, 0, 0, 2, moveSpeedTicks: 4));
            CollectionAssert.AreEqual(new[] { 2, 3 }, Pathfinding.FindPath(s, 0, 2, 3, moveSpeedTicks: 4));
            // Beyond the enemy Core there is no route, so the order is refused.
            Order(s, 3); Assert.AreEqual(-1, s.villagers[0].targetNodeID);
            Assert.AreEqual(VillagerState.Idle, s.villagers[0].state); return s;
        }
        [Test] public void BreachInterrupted_RetainsCoreIntentAndResumesNextTick() => InterruptedBreach();
        [Test] public void BreachInterrupted_RetainsCoreIntentAndResumesNextTick_Determinism() => Determinism(InterruptedBreach);
        [Test] public void BreachOverrideAway_StopsContribution() => Away();
        [Test] public void BreachOverrideAway_StopsContribution_Determinism() => Determinism(Away);
        [Test] public void EnemyCore_IsTerminalButCanBeExited() => Terminal();
        [Test] public void EnemyCore_IsTerminalButCanBeExited_Determinism() => Determinism(Terminal);
        [Test] public void FightAtIntermediateFarm_ResumesToOriginalDestination() => Intermediate();
        [Test] public void FightAtIntermediateFarm_ResumesToOriginalDestination_Determinism() => Determinism(Intermediate);
        [Test] public void OverrideDuringFight_ChangesIntentWithoutFreeAttackReset() => OverrideFight();
        [Test] public void OverrideDuringFight_ChangesIntentWithoutFreeAttackReset_Determinism() => Determinism(OverrideFight);
        [Test] public void OverrideToCurrentNode_CancelsPendingIntent() => Cancel();
        [Test] public void OverrideToCurrentNode_CancelsPendingIntent_Determinism() => Determinism(Cancel);
        [Test] public void TargetOwnershipFlip_UsesCurrentArrivalAction() => Ownership();
        [Test] public void TargetOwnershipFlip_UsesCurrentArrivalAction_Determinism() => Determinism(Ownership);
        [Test] public void UnreachableMove_IsRefusedAndStateUnchanged() => Unreachable();
        [Test] public void UnreachableMove_IsRefusedAndStateUnchanged_Determinism() => Determinism(Unreachable);
    }
}
