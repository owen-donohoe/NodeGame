using System;
using System.Collections;
using System.Reflection;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Tests
{
    public class WorkshopMinionTests
    {
        private const DistrictType Workshop = (DistrictType)19;
        private const SuitType Minion = (SuitType)12;
        private GameBalanceData balance;
        private static T Set<T>(T data, string name, int value)
        {
            var field = typeof(T).GetField(name); Assert.IsNotNull(field, name);
            object boxed = data; field.SetValue(boxed, value); return (T)boxed;
        }
        private void Configure() => MatchFactory.Configure(balance, new BoardConfigData());
        private SimulationState Fixture()
        {
            balance = GameBalanceData.Default(); Configure();
            var s = new SimulationState {
                players = new[] { new PlayerData { playerID = 0, coreNodeID = 0, metal = 10, nextBreacherID = -1 },
                    new PlayerData { playerID = 1, coreNodeID = 3, nextBreacherID = -1 } },
                nodes = new NodeData[5], villagers = Array.Empty<VillagerData>() };
            var types = new[] { DistrictType.Core, Workshop, DistrictType.Farm, DistrictType.Core, DistrictType.Storehouse };
            for (int i = 0; i < 5; i++) s.nodes[i] = new NodeData {
                nodeID = i, districtType = types[i], baseDistrictType = types[i], ownerID = i == 3 ? 1 : 0,
                claimBar = i == 3 ? -balance.claimThreshold : balance.claimThreshold, links = Array.Empty<Link>() };
            LinkNodes(s, 1, 2); LinkNodes(s, 2, 3); LinkNodes(s, 2, 4);
            return s;
        }
        private static void LinkNodes(SimulationState s, int a, int b)
        {
            foreach (var pair in new[] { new[] { a, b }, new[] { b, a } }) {
                var links = s.nodes[pair[0]].links; Array.Resize(ref links, links.Length + 1);
                links[links.Length - 1] = new Link { toNodeID = pair[1], travelWeight = 1 }; s.nodes[pair[0]].links = links;
            }
        }
        private static GameCommand Forge(int player = 0, int node = 1, int value = 0, int villager = -1) =>
            new GameCommand { type = (CommandType)8, playerID = player, targetNodeID = node, value = value, villagerID = villager };
        private static void NoOp(SimulationState s, GameCommand c)
        {
            int hash = SimulationStateHasher.ComputeHash(s); CommandProcessor.ProcessCommand(s, c);
            Assert.AreEqual(hash, SimulationStateHasher.ComputeHash(s));
        }
        private static VillagerData Body(int id, int owner, int node, SuitType suit = SuitType.None) =>
            new VillagerData { villagerID = id, ownerID = owner, currentNodeID = node, previousNodeID = node,
                targetNodeID = -1, combatTargetID = -1, movePath = Array.Empty<int>(), suit = suit,
                state = VillagerState.Idle, hp = 8, maxHP = 8, moveSpeedTicks = 2,
                attackDamage = 2, attackCooldownMax = 1, attackCooldownRemaining = 1 };
        private static void Move(SimulationState s, int node) => CommandProcessor.ProcessCommand(s,
            new GameCommand { type = CommandType.Move, playerID = 0, villagerID = 0, targetNodeID = node });
        private static void Ticks(SimulationState s, int n) { for (int i = 0; i < n; i++) GameSimulation.SimulateTick(s); }
        private void Refusal(SimulationState s, string expected, int player = 0, int node = 1)
        {
            var method = typeof(NodeActionRules).GetMethod("CanForgeMinion"); Assert.IsNotNull(method);
            object[] args = { s, balance, player, node, null };
            Assert.IsFalse((bool)method.Invoke(null, args)); Assert.AreEqual(expected, args[4].ToString());
            NoOp(s, Forge(player, node));
        }
        private SimulationState Run(string scenario)
        {
            var s = Fixture();
            switch (scenario) {
                case "ForgeCostCooldownEraAndNoAutomation":
                    Assert.AreEqual("Workshop", Enum.GetName(typeof(DistrictType), 19));
                    Assert.AreEqual("Minion", Enum.GetName(typeof(SuitType), 12));
                    int index = Array.FindIndex(balance.districtStats, d => d.districtType == Workshop && d.era == 5);
                    Assert.GreaterOrEqual(index, 0);
                    balance.districtStats[index] = Set(balance.districtStats[index], "forgeCooldownTicks", 7); Configure();
                    s.nodes[1].districtEra = 5; s.tickCount = 100;
                    CommandProcessor.ProcessCommand(s, Forge());
                    Assert.AreEqual(7, s.players[0].metal); Assert.AreEqual(107, s.nodes[1].recruitReadyTick);
                    Assert.AreEqual(0, s.players[0].recruitCount); Assert.AreEqual(1, s.villagers.Length);
                    var v = s.villagers[0]; Assert.AreEqual(Minion, v.suit); Assert.AreEqual(8, v.hp); Assert.AreEqual(8, v.maxHP);
                    Assert.AreEqual(2, v.moveSpeedTicks); Assert.AreEqual(0, v.attackDamage); Assert.AreEqual(1, v.currentNodeID);
                    Assert.AreEqual(-1, v.targetNodeID); Assert.AreEqual(-1, v.combatTargetID); Assert.IsEmpty(v.movePath);
                    Refusal(s, "Cooldown"); s.tickCount = 107; CommandProcessor.ProcessCommand(s, Forge());
                    Assert.AreEqual(4, s.players[0].metal); Assert.AreEqual(114, s.nodes[1].recruitReadyTick);
                    s.nodes[1].autoRecruit = true; Ticks(s, 10); Assert.AreEqual(2, s.villagers.Length);
                    break;
                case "EveryForgeRefusalIsHashNeutral":
                    Refusal(s, "InvalidPlayer", -1); Refusal(s, "InvalidPlayer", 2);
                    Refusal(s, "InvalidNode", node: -1); Refusal(s, "InvalidNode", node: 5);
                    Refusal(s, "NotWorkshop", node: 2); Refusal(s, "NotOwned", player: 1);
                    NoOp(s, Forge(value: 1)); NoOp(s, Forge(villager: 0));
                    s.villagers = new[] { Body(0, 1, 1) }; Refusal(s, "EnemyPresent");
                    s.villagers[0].state = VillagerState.Dead; s.nodes[1].recruitReadyTick = 1; Refusal(s, "Cooldown");
                    s.nodes[1].recruitReadyTick = 0; s.players[0].metal = 2; Refusal(s, "InsufficientMetal");
                    s.players[0].metal = 10; balance = Set(balance, "minionMetalCost", 0); Configure(); Refusal(s, "InvalidCost");
                    balance = Set(balance, "minionMetalCost", 11); Configure(); Refusal(s, "CostAboveMetalCap");
                    balance = Set(balance, "minionMetalCost", 3); Configure(); s.tickCount = int.MaxValue - 1; Refusal(s, "InvalidCost");
                    break;
                case "PopulationIncludesDeadAndFreesConsumed":
                    balance.maxVillagersPerPlayer = 1; Configure(); s.villagers = new[] { Body(0, 0, 0) };
                    s.villagers[0].state = VillagerState.Dead; Refusal(s, "PopulationCap");
                    s.villagers[0].isConsumed = true; CommandProcessor.ProcessCommand(s, Forge());
                    Assert.AreEqual(2, s.villagers.Length); Assert.AreEqual(1, s.villagers[1].villagerID);
                    Assert.AreEqual(1, NodeActionRules.CountPopulation(s, 0));
                    break;
                case "MoveSpeedNoWorkClaimOrCoreBreach":
                    CommandProcessor.ProcessCommand(s, Forge()); Move(s, 2); Ticks(s, 1);
                    Assert.AreEqual(1, s.villagers[0].currentNodeID); Ticks(s, 1);
                    Assert.AreEqual(2, s.villagers[0].currentNodeID); Assert.AreEqual(VillagerState.Idle, s.villagers[0].state);
                    Assert.AreEqual(Minion, s.villagers[0].suit); Assert.AreEqual(0, s.players[0].food);
                    s.nodes[2].ownerID = 1; s.nodes[2].claimBar = -balance.claimThreshold; Ticks(s, 2);
                    Assert.AreEqual(-balance.claimThreshold, s.nodes[2].claimBar); Assert.AreEqual(VillagerState.Idle, s.villagers[0].state);
                    Move(s, 3); Ticks(s, 4); Assert.AreEqual(3, s.villagers[0].currentNodeID);
                    Assert.AreEqual(VillagerState.Idle, s.villagers[0].state); Assert.AreEqual(0, s.players[1].breachBar);
                    Assert.AreEqual(-1, s.players[1].nextBreacherID); Assert.IsFalse(s.villagers[0].isConsumed);
                    // Legacy Core arrival is also inert.
                    balance.breachBarMax = 0; Configure(); s.villagers[0].currentNodeID = 2; Move(s, 3); Ticks(s, 2);
                    Assert.IsFalse(s.villagers[0].isConsumed); Assert.AreEqual(Minion, s.villagers[0].suit);
                    break;
                case "NeutralNodeAndFriendlyWorkersIgnoreMinion":
                    s.villagers = new[] { Body(0, 0, 2, Minion) }; s.nodes[2].ownerID = -1; s.nodes[2].claimBar = 0;
                    Ticks(s, 2); Assert.AreEqual(0, s.nodes[2].claimBar); Assert.AreEqual(VillagerState.Idle, s.villagers[0].state);
                    s.nodes[2].ownerID = 0; s.nodes[2].districtType = DistrictType.Infirmary; s.nodes[2].districtHealth = 3000;
                    s.villagers = new[] { s.villagers[0], Body(1, 0, 2), Body(2, 0, 2) };
                    Ticks(s, 1); Assert.AreEqual(Minion, s.villagers[0].suit); Assert.AreEqual(VillagerState.Idle, s.villagers[0].state);
                    Assert.AreEqual(2, GameSimulation.CountInfirmaryWorkers(s, 2, 0));
                    Assert.AreEqual(VillagerState.Working, s.villagers[1].state); Assert.AreEqual(VillagerState.Working, s.villagers[2].state);
                    // Minions alone cannot Restore, even on their own node.
                    s.villagers = new[] { s.villagers[0] }; s.nodes[2].claimBar = balance.claimThreshold - 17;
                    Ticks(s, 1); Assert.AreEqual(balance.claimThreshold - 17, s.nodes[2].claimBar);
                    break;
                case "CustomMinionStatsAndForgeInvalidTuning":
                    balance = Set(Set(balance, "minionHP", 11), "minionMoveSpeedTicks", 3); Configure();
                    CommandProcessor.ProcessCommand(s, Forge()); Assert.AreEqual(11, s.villagers[0].hp);
                    Assert.AreEqual(11, s.villagers[0].maxHP); Move(s, 2); Ticks(s, 2);
                    Assert.AreEqual(1, s.villagers[0].currentNodeID); Ticks(s, 1); Assert.AreEqual(2, s.villagers[0].currentNodeID);
                    s.nodes[1].recruitReadyTick = 0;
                    foreach (string name in new[] { "minionHP", "minionMetalCost", "minionMoveSpeedTicks" }) {
                        var original = balance; balance = Set(balance, name, 0); Configure(); Refusal(s, "InvalidCost"); balance = original;
                    }
                    int workshopIndex = Array.FindIndex(balance.districtStats, d => d.districtType == Workshop && d.era == 0);
                    balance.districtStats[workshopIndex] = Set(balance.districtStats[workshopIndex], "forgeCooldownTicks", 0);
                    Configure(); Refusal(s, "InvalidCost");
                    break;
                case "PierGateRetreatAndNoRoute":
                    CommandProcessor.ProcessCommand(s, Forge()); NoOp(s, new GameCommand { type = CommandType.Move, playerID = 0, villagerID = 0, targetNodeID = 0 });
                    s.nodes[2].districtType = DistrictType.Pier; s.nodes[2].ownerID = 1; s.nodes[2].claimBar = -100;
                    Move(s, 3); Ticks(s, 4); Assert.AreEqual(2, s.villagers[0].currentNodeID);
                    Assert.AreEqual(VillagerState.Idle, s.villagers[0].state); Assert.AreEqual(-100, s.nodes[2].claimBar);
                    Assert.AreEqual(3, s.villagers[0].targetNodeID); Move(s, 1); Ticks(s, 2);
                    Assert.AreEqual(1, s.villagers[0].currentNodeID);
                    break;
                case "TargetOnlyCombatDeathConsumptionAndPopulation":
                    CommandProcessor.ProcessCommand(s, Forge()); var minion = s.villagers[0];
                    minion.attackDamage = 99; s.villagers = new[] { minion, Body(1, 1, 1) };
                    Ticks(s, 1); Assert.AreEqual(VillagerState.Fighting, s.villagers[0].state);
                    Assert.AreEqual(6, s.villagers[0].hp); Assert.AreEqual(8, s.villagers[1].hp);
                    Ticks(s, 3); Assert.AreEqual(VillagerState.Dead, s.villagers[0].state); Assert.IsTrue(s.villagers[0].isConsumed);
                    Assert.AreEqual(0, NodeActionRules.CountPopulation(s, 0)); Assert.AreEqual(8, s.villagers[1].hp);
                    s.players[0].food = 30; NoOp(s, new GameCommand { type = CommandType.Respawn, playerID = 0, villagerID = 0 });
                    Ticks(s, balance.respawnTicks + 1); Assert.AreEqual(VillagerState.Dead, s.villagers[0].state);
                    s.villagers[1].isConsumed = true; s.nodes[1].ownerID = 0; CommandProcessor.ProcessCommand(s, Forge());
                    Assert.AreEqual(3, s.villagers.Length); Assert.AreEqual(1, NodeActionRules.CountPopulation(s, 0));
                    break;
                case "CombatJoinResumeAtEnemyCore":
                    s.villagers = new[] { Body(0, 0, 3, Minion), Body(1, 1, 3) };
                    s.villagers[1].hp = 1; s.villagers[1].maxHP = 1;
                    s.villagers[0].targetNodeID = 3; s.villagers[1].state = VillagerState.Fighting;
                    Ticks(s, 1); Assert.AreEqual(VillagerState.Fighting, s.villagers[0].state); Assert.AreEqual(1, s.villagers[1].hp);
                    s.villagers[1].hp = 0; Ticks(s, 1); Assert.AreEqual(VillagerState.Idle, s.villagers[0].state);
                    Assert.AreEqual(-1, s.villagers[0].targetNodeID); Assert.AreEqual(0, s.players[1].breachBar);
                    break;
                case "StationaryStorehouseCollectorAndNormalRegen":
                    s.villagers = new[] { Body(0, 0, 4, Minion) }; s.villagers[0].hp = 7;
                    s.nodes[4].bankFood = 1; Assert.IsTrue(BankRules.HasStationaryCollector(s, s.nodes[4]));
                    Ticks(s, 4); Assert.AreEqual(1, s.players[0].food); Assert.AreEqual(0, s.nodes[4].bankFood);
                    Ticks(s, balance.healIntervalTicks - 4); Assert.AreEqual(8, s.villagers[0].hp);
                    break;
                case "RemoteStorehouseCollectionUnderEnemyMinion":
                    s.villagers = new[] { Body(0, 1, 4, Minion) }; s.nodes[4].bankFood = 1;
                    Assert.IsFalse(BankRules.Locked(s, s.nodes[4], balance));
                    CommandProcessor.ProcessCommand(s, new GameCommand { type = CommandType.Collect, playerID = 0, villagerID = -1, targetNodeID = 4, value = 1 });
                    Ticks(s, 4); Assert.AreEqual(1, s.players[0].food); Assert.AreEqual(0, s.nodes[4].bankFood);
                    Assert.AreEqual(balance.claimThreshold, s.nodes[4].claimBar); Assert.AreEqual(VillagerState.Idle, s.villagers[0].state);
                    break;
                case "EnemyMinionsDoNotLockRestoreOrBlockActions":
                    s.villagers = new[] { Body(0, 1, 1, Minion) };
                    Assert.IsFalse(BankRules.Locked(s, s.nodes[1], balance)); CommandProcessor.ProcessCommand(s, Forge());
                    Assert.AreEqual(2, s.villagers.Length);
                    s.nodes[1].districtType = DistrictType.Village; Assert.IsFalse(NodeActionRules.CanRecruit(s, balance, 0, 1, out _)); // shared cooldown
                    s.nodes[1].recruitReadyTick = 0; s.players[0].food = 30;
                    Assert.IsTrue(NodeActionRules.CanRecruit(s, balance, 0, 1, out _));
                    s.nodes[1].districtType = DistrictType.Fortress;
                    Assert.IsTrue(NodeActionRules.CanUpgradeFortress(s, balance, 0, 1, 1));
                    // Isolate Restore's presence rule: the normal combat pass interrupts both bodies first.
                    s.villagers[1].state = VillagerState.Idle; s.villagers[1].suit = SuitType.None;
                    s.nodes[1].claimBar = balance.claimThreshold - 17;
                    typeof(GameSimulation).GetMethod("TryRestoreOwnedNode", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { s, 1 });
                    Assert.AreEqual(balance.claimThreshold, s.nodes[1].claimBar);
                    s.nodes[1].districtType = DistrictType.Infirmary; s.nodes[1].districtHealth = 3000;
                    s.villagers[1].suit = SuitType.Acolyte; s.villagers[1].state = VillagerState.Working;
                    Assert.AreEqual(1, GameSimulation.CountInfirmaryWorkers(s, 1, 0));
                    Assert.IsTrue((bool)typeof(GameSimulation).GetMethod("InfirmaryWorkerSlot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { s, 1 }));
                    break;
                case "EquipRefusesMinion":
                    s.villagers = new[] { Body(0, 0, 1, Minion) }; s.nodes[1].districtType = DistrictType.Barracks;
                    balance.suitStats = new[] { new SuitStats { suitType = SuitType.Warrior, moveSpeedTicks = 4, attackDamage = 2 } }; Configure();
                    s.players[0].draftedSuits = new[] { (int)SuitType.Warrior };
                    NoOp(s, new GameCommand { type = CommandType.Equip, playerID = 0, villagerID = 0, value = (int)SuitType.Warrior });
                    break;
                default: Assert.Fail(scenario); break;
            }
            return s;
        }
        private static readonly string[] Scenarios = { "ForgeCostCooldownEraAndNoAutomation", "EveryForgeRefusalIsHashNeutral",
            "PopulationIncludesDeadAndFreesConsumed", "MoveSpeedNoWorkClaimOrCoreBreach", "PierGateRetreatAndNoRoute",
            "TargetOnlyCombatDeathConsumptionAndPopulation", "CombatJoinResumeAtEnemyCore",
            "StationaryStorehouseCollectorAndNormalRegen", "EnemyMinionsDoNotLockRestoreOrBlockActions", "EquipRefusesMinion",
            "NeutralNodeAndFriendlyWorkersIgnoreMinion", "CustomMinionStatsAndForgeInvalidTuning", "RemoteStorehouseCollectionUnderEnemyMinion" };
        public static IEnumerable Cases() { foreach (string s in Scenarios) yield return new TestCaseData(s).SetName(s); }
        public static IEnumerable Twins() { foreach (string s in Scenarios) yield return new TestCaseData(s).SetName(s + "_Determinism"); }
        [TestCaseSource(nameof(Cases))] public void Correctness(string scenario) => Run(scenario);
        [TestCaseSource(nameof(Twins))] public void Correctness_Determinism(string scenario)
        {
            int first = SimulationStateHasher.ComputeHash(Run(scenario));
            Assert.AreEqual(first, SimulationStateHasher.ComputeHash(Run(scenario)));
        }
    }
}
