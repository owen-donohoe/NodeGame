using System;
using System.Reflection;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Tests
{
    public class RecruitTests
    {
        private GameBalanceData balance;

        private SimulationState Fixture()
        {
            balance = GameBalanceData.Default();
            MatchFactory.Configure(balance, new BoardConfigData());
            var state = new SimulationState
            {
                players = new[] {
                    new PlayerData { playerID = 0, coreNodeID = 0, food = 30, nextBreacherID = -1 },
                    new PlayerData { playerID = 1, coreNodeID = 3, nextBreacherID = -1 } },
                nodes = new[] { Node(0, DistrictType.Core, 0), Node(1, DistrictType.Village, 0),
                    Node(2, DistrictType.Village, 0), Node(3, DistrictType.Core, 1), Node(4, DistrictType.Farm, 0) },
                villagers = Array.Empty<VillagerData>()
            };
            return state;
        }

        private NodeData Node(int id, DistrictType type, int owner) => new NodeData
        {
            nodeID = id, districtType = type, baseDistrictType = type, ownerID = owner,
            claimBar = owner == 0 ? balance.claimThreshold : -balance.claimThreshold,
            links = Array.Empty<Link>()
        };

        // Reflection lets the test-first commit compile before the DTO fields exist.
        private static int Count(SimulationState s) => Get<int>(s.players[0], "recruitCount");
        private static int Ready(SimulationState s, int node = 1) => Get<int>(s.nodes[node], "recruitReadyTick");
        private static bool Auto(SimulationState s, int node = 1) => Get<bool>(s.nodes[node], "autoRecruit");
        private static T Get<T>(object owner, string name)
        {
            var field = owner.GetType().GetField(name); Assert.IsNotNull(field, name);
            return (T)field.GetValue(owner);
        }
        private static T Set<T>(T owner, string name, object value)
        {
            var field = typeof(T).GetField(name); Assert.IsNotNull(field, name);
            object boxed = owner; field.SetValue(boxed, value); return (T)boxed;
        }
        private static GameCommand Command(int type = 5, int player = 0, int node = 1, int value = 0, int villager = -1)
            => new GameCommand { type = (CommandType)type, playerID = player, targetNodeID = node,
                villagerID = villager, value = value, issuedOnTick = 123 };
        private static void Recruit(SimulationState s) => CommandProcessor.ProcessCommand(s, Command());
        private static void NoOp(SimulationState s, GameCommand command)
        {
            int hash = SimulationStateHasher.ComputeHash(s);
            CommandProcessor.ProcessCommand(s, command);
            Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(s));
        }
        private static VillagerData Body(int id, int owner, int node, VillagerState state = VillagerState.Idle)
            => new VillagerData { villagerID = id, ownerID = owner, currentNodeID = node, targetNodeID = -1,
                combatTargetID = -1, movePath = Array.Empty<int>(), state = state, hp = 5, maxHP = 5,
                attackCooldownRemaining = 100, attackCooldownMax = 100 };

        private SimulationState Run(string scenario)
        {
            var s = Fixture();
            switch (scenario)
            {
                case "Recruit_FirstAndSecondUsePreIncrementCount":
                    s.tickCount = 100; Recruit(s);
                    Assert.AreEqual(24, s.players[0].food); Assert.AreEqual(1, Count(s)); Assert.AreEqual(160, Ready(s));
                    s.tickCount = 159; NoOp(s, Command()); s.tickCount = 160; Recruit(s);
                    Assert.AreEqual(15, s.players[0].food); Assert.AreEqual(2, Count(s)); Assert.AreEqual(250, Ready(s));
                    Assert.AreEqual(2, s.villagers.Length);
                    for (int i = 0; i < 2; i++)
                    {
                        var v = s.villagers[i]; Assert.AreEqual(i, v.villagerID); Assert.AreEqual(0, v.ownerID);
                        Assert.AreEqual(1, v.currentNodeID); Assert.AreEqual(-1, v.targetNodeID);
                        Assert.AreEqual(SuitType.None, v.suit); Assert.AreEqual(VillagerState.Idle, v.state);
                        Assert.AreEqual(balance.baseHP, v.hp); Assert.AreEqual(balance.baseHP, v.maxHP);
                        Assert.AreEqual(balance.baseAttackDamage, v.attackDamage);
                        Assert.AreEqual(balance.baseMoveSpeedTicks, v.moveSpeedTicks);
                        Assert.AreEqual(balance.baseAttackCooldownMax, v.attackCooldownMax);
                        Assert.AreEqual(-1, v.combatTargetID); Assert.IsEmpty(v.movePath);
                        Assert.AreEqual(0, v.moveProgress); Assert.AreEqual(0, v.respawnTicksRemaining);
                    }
                    break;
                case "RecruitCountShared_CooldownsArePerVillage":
                    CommandProcessor.ProcessCommand(s, Command(6, value: 1));
                    CommandProcessor.ProcessCommand(s, Command(6, node: 2, value: 1));
                    GameSimulation.SimulateTick(s);
                    Assert.AreEqual(15, s.players[0].food); Assert.AreEqual(2, Count(s));
                    Assert.AreEqual(61, Ready(s)); Assert.AreEqual(91, Ready(s, 2));
                    Assert.AreEqual(1, s.villagers[0].currentNodeID); Assert.AreEqual(2, s.villagers[1].currentNodeID);
                    break;
                case "PopCap_RefusesWithoutSpendingOrCooldown":
                    s.villagers = new VillagerData[25];
                    for (int i = 0; i < 25; i++) s.villagers[i] = Body(i, 0, 0, VillagerState.Dead);
                    NoOp(s, Command()); s.villagers[0].isConsumed = true; Recruit(s);
                    Assert.AreEqual(26, s.villagers.Length); Assert.AreEqual(25, s.villagers[25].villagerID);
                    Assert.AreEqual(24, s.players[0].food); Assert.AreEqual(1, Count(s));
                    break;
                case "OwnershipLoss_ResetsAutoAndCooldown":
                    for (int owner = 0; owner < 2; owner++)
                    {
                        s.nodes[1].ownerID = owner; s.nodes[1].claimBar = owner == 0 ? 1 : -1;
                        s.nodes[1] = Set(Set(s.nodes[1], "autoRecruit", true), "recruitReadyTick", 200);
                        s.players[0] = Set(s.players[0], "recruitCount", 4);
                        s.villagers = new[] { Body(0, 1 - owner, 1, VillagerState.Claiming) };
                        GameSimulation.SimulateTick(s);
                        Assert.AreEqual(-1, s.nodes[1].ownerID); Assert.IsFalse(Auto(s)); Assert.AreEqual(0, Ready(s));
                        s.nodes[1].claimBar = owner == 0 ? -balance.claimThreshold + 1 : balance.claimThreshold - 1;
                        GameSimulation.SimulateTick(s);
                        Assert.AreEqual(1 - owner, s.nodes[1].ownerID); Assert.IsFalse(Auto(s)); Assert.AreEqual(0, Ready(s));
                        Assert.AreEqual(4, Count(s));
                        // A large claim contribution can replace ownership without a neutral tick.
                        s.nodes[1].ownerID = owner;
                        s.nodes[1].claimBar = owner == 0 ? -balance.claimThreshold + 1 : balance.claimThreshold - 1;
                        s.nodes[1] = Set(Set(s.nodes[1], "autoRecruit", true), "recruitReadyTick", 200);
                        s.villagers[0].state = VillagerState.Claiming;
                        GameSimulation.SimulateTick(s);
                        Assert.AreEqual(1 - owner, s.nodes[1].ownerID); Assert.IsFalse(Auto(s)); Assert.AreEqual(0, Ready(s));
                        Assert.AreEqual(4, Count(s));
                    }
                    break;
                case "AutoWaitsForFood_ThenRunsAfterProduction":
                    s.players[0].food = 5; CommandProcessor.ProcessCommand(s, Command(6, value: 1));
                    GameSimulation.SimulateTick(s); Assert.IsEmpty(s.villagers); Assert.IsTrue(Auto(s));
                    s.villagers = new[] { Body(0, 0, 4, VillagerState.Working) };
                    s.villagers[0].productionTicksMax = 30; s.villagers[0].productionTicksRemaining = 1;
                    GameSimulation.SimulateTick(s);
                    Assert.AreEqual(0, s.players[0].food); Assert.AreEqual(1, Count(s)); Assert.AreEqual(2, s.villagers.Length);
                    Assert.AreEqual(62, Ready(s)); Assert.IsTrue(Auto(s));
                    break;
                case "NoWorkerNeeded_EnemyPresenceRefuses":
                    Recruit(s); Assert.AreEqual(1, s.villagers.Length);
                    s.tickCount = 60; s.villagers = new[] { s.villagers[0], Body(1, 1, 1) };
                    NoOp(s, Command()); s.villagers[1].state = VillagerState.Dead;
                    foreach (var c in new[] { Command(player: -1), Command(player: 2), Command(node: -1),
                        Command(node: 5), Command(node: 0), Command(player: 1), Command(value: 1), Command(villager: 0) }) NoOp(s, c);
                    Recruit(s); Assert.AreEqual(2, Count(s)); Assert.AreEqual(3, s.villagers.Length);
                    break;
                case "ReclaimVillage_NoBonus":
                    s.nodes[1].ownerID = -1; s.nodes[1].claimBar = balance.claimThreshold - 1;
                    s.villagers = new[] { Body(0, 0, 1, VillagerState.Claiming) };
                    GameSimulation.SimulateTick(s); Assert.AreEqual(0, s.nodes[1].ownerID); Assert.AreEqual(1, s.villagers.Length);
                    s.nodes[1].ownerID = -1; s.nodes[1].claimBar = balance.claimThreshold - 1;
                    s.villagers[0].state = VillagerState.Claiming; GameSimulation.SimulateTick(s);
                    Assert.AreEqual(1, s.villagers.Length);
                    break;
                case "NinthRecruit_FitsFoodCap":
                    s.players[0] = Set(s.players[0], "recruitCount", 8); Recruit(s);
                    Assert.AreEqual(0, s.players[0].food); Assert.AreEqual(9, Count(s)); Assert.AreEqual(300, Ready(s));
                    Assert.AreEqual(1, s.villagers.Length);
                    break;
                case "CostAboveFoodCap_IsRefused":
                    s.players[0] = Set(s.players[0], "recruitCount", 9); NoOp(s, Command());
                    Assert.AreEqual(30, s.players[0].food); Assert.AreEqual(9, Count(s)); Assert.AreEqual(0, Ready(s));
                    Assert.IsEmpty(s.villagers); AssertCost(9, 0, true, 33, 330);
                    break;
                case "FoodCap_IsSingleTunable":
                    Assert.AreEqual(30, balance.foodCap); balance.foodCap = 60;
                    MatchFactory.Configure(balance, new BoardConfigData()); s.players[0].food = 60;
                    s.players[0] = Set(s.players[0], "recruitCount", 9); Recruit(s);
                    Assert.AreEqual(27, s.players[0].food); Assert.AreEqual(10, Count(s)); Assert.AreEqual(1, s.villagers.Length);
                    break;
                case "HugeCounter_RefusesOverflow":
                    s.players[0] = Set(s.players[0], "recruitCount", int.MaxValue); NoOp(s, Command());
                    Assert.AreEqual(int.MaxValue, Count(s)); AssertCost(int.MaxValue, 0, false);
                    break;
                case "ReadyTickOverflow_Refuses":
                    s.tickCount = int.MaxValue - 1; NoOp(s, Command()); AssertCost(0, s.tickCount, false);
                    break;
                case "SetAutoRecruit_AbsoluteAndDoesNotRecruit":
                    s.players[0].food = 0; s.nodes[1] = Set(s.nodes[1], "recruitReadyTick", 200);
                    CommandProcessor.ProcessCommand(s, Command(6, value: 1)); Assert.IsTrue(Auto(s));
                    NoOp(s, Command(6, value: 1));
                    foreach (var c in new[] { Command(6, value: 2), Command(6, value: -1), Command(6, villager: 0),
                        Command(6, player: 1), Command(6, player: 2), Command(6, node: -1), Command(6, node: 0) }) NoOp(s, c);
                    Assert.AreEqual(0, Count(s)); Assert.AreEqual(0, s.players[0].food); Assert.IsEmpty(s.villagers);
                    Assert.AreEqual(200, Ready(s)); CommandProcessor.ProcessCommand(s, Command(6)); Assert.IsFalse(Auto(s));
                    break;
                default: Assert.Fail(scenario); break;
            }
            return s;
        }

        private void AssertCost(int count, int tick, bool expected, int cost = 0, int ready = 0)
        {
            var method = typeof(GameBalanceData).GetMethod("TryRecruitCostAndCooldown"); Assert.IsNotNull(method);
            object[] args = { count, tick, 0, 0 };
            Assert.AreEqual(expected, method.Invoke(balance, args));
            if (expected) { Assert.AreEqual(cost, args[2]); Assert.AreEqual(ready, args[3]); }
        }

        [TestCase("recruitBaseCost", 0)] [TestCase("recruitBaseCost", -1)]
        [TestCase("recruitCostPerRecruit", 0)] [TestCase("recruitCostPerRecruit", -1)]
        [TestCase("ticksPerSecond", 0)] [TestCase("ticksPerSecond", -1)]
        public void CostValidation_RefusesNonpositiveTuning(string name, int value)
        {
            Fixture(); balance = Set(balance, name, value); AssertCost(0, 0, false);
        }

        [Test]
        public void CostValidation_RefusesNegativeCountAndOverflowingProducts()
        {
            Fixture(); AssertCost(-1, 0, false);
            balance = Set(balance, "recruitCostPerRecruit", int.MaxValue);
            balance.ticksPerSecond = int.MaxValue; AssertCost(int.MaxValue, 0, false);
            balance = GameBalanceData.Default(); balance.ticksPerSecond = int.MaxValue;
            AssertCost(0, 0, false);
        }

        [TestCase("Recruit_FirstAndSecondUsePreIncrementCount", TestName = "Recruit_FirstAndSecondUsePreIncrementCount")]
        [TestCase("RecruitCountShared_CooldownsArePerVillage", TestName = "RecruitCountShared_CooldownsArePerVillage")]
        [TestCase("PopCap_RefusesWithoutSpendingOrCooldown", TestName = "PopCap_RefusesWithoutSpendingOrCooldown")]
        [TestCase("OwnershipLoss_ResetsAutoAndCooldown", TestName = "OwnershipLoss_ResetsAutoAndCooldown")]
        [TestCase("AutoWaitsForFood_ThenRunsAfterProduction", TestName = "AutoWaitsForFood_ThenRunsAfterProduction")]
        [TestCase("NoWorkerNeeded_EnemyPresenceRefuses", TestName = "NoWorkerNeeded_EnemyPresenceRefuses")]
        [TestCase("ReclaimVillage_NoBonus", TestName = "ReclaimVillage_NoBonus")]
        [TestCase("NinthRecruit_FitsFoodCap", TestName = "NinthRecruit_FitsFoodCap")]
        [TestCase("CostAboveFoodCap_IsRefused", TestName = "CostAboveFoodCap_IsRefused")]
        [TestCase("FoodCap_IsSingleTunable", TestName = "FoodCap_IsSingleTunable")]
        [TestCase("HugeCounter_RefusesOverflow", TestName = "HugeCounter_RefusesOverflow")]
        [TestCase("ReadyTickOverflow_Refuses", TestName = "ReadyTickOverflow_Refuses")]
        [TestCase("SetAutoRecruit_AbsoluteAndDoesNotRecruit", TestName = "SetAutoRecruit_AbsoluteAndDoesNotRecruit")]
        public void Correctness(string scenario) => Run(scenario);

        [TestCase("Recruit_FirstAndSecondUsePreIncrementCount", TestName = "Recruit_FirstAndSecondUsePreIncrementCount_Determinism")]
        [TestCase("RecruitCountShared_CooldownsArePerVillage", TestName = "RecruitCountShared_CooldownsArePerVillage_Determinism")]
        [TestCase("PopCap_RefusesWithoutSpendingOrCooldown", TestName = "PopCap_RefusesWithoutSpendingOrCooldown_Determinism")]
        [TestCase("OwnershipLoss_ResetsAutoAndCooldown", TestName = "OwnershipLoss_ResetsAutoAndCooldown_Determinism")]
        [TestCase("AutoWaitsForFood_ThenRunsAfterProduction", TestName = "AutoWaitsForFood_ThenRunsAfterProduction_Determinism")]
        [TestCase("NoWorkerNeeded_EnemyPresenceRefuses", TestName = "NoWorkerNeeded_EnemyPresenceRefuses_Determinism")]
        [TestCase("ReclaimVillage_NoBonus", TestName = "ReclaimVillage_NoBonus_Determinism")]
        [TestCase("NinthRecruit_FitsFoodCap", TestName = "NinthRecruit_FitsFoodCap_Determinism")]
        [TestCase("CostAboveFoodCap_IsRefused", TestName = "CostAboveFoodCap_IsRefused_Determinism")]
        [TestCase("FoodCap_IsSingleTunable", TestName = "FoodCap_IsSingleTunable_Determinism")]
        [TestCase("HugeCounter_RefusesOverflow", TestName = "HugeCounter_RefusesOverflow_Determinism")]
        [TestCase("ReadyTickOverflow_Refuses", TestName = "ReadyTickOverflow_Refuses_Determinism")]
        [TestCase("SetAutoRecruit_AbsoluteAndDoesNotRecruit", TestName = "SetAutoRecruit_AbsoluteAndDoesNotRecruit_Determinism")]
        public void Correctness_Determinism(string scenario)
        {
            int first = SimulationStateHasher.ComputeHash(Run(scenario));
            Assert.AreEqual(first, SimulationStateHasher.ComputeHash(Run(scenario)));
        }
    }
}
