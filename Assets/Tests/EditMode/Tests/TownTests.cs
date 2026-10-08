using System;
using System.Reflection;
using NodeWar.Simulation;
using NUnit.Framework;

namespace NodeWar.Tests
{
    public class TownTests
    {
        private GameBalanceData balance;
        private SimulationState Fixture(int population = 1)
        {
            balance = GameBalanceData.Default();
            balance.respawnTicks = int.MaxValue;
            MatchFactory.Configure(balance, new BoardConfigData());
            var s = new SimulationState {
                players = new[] { new PlayerData { playerID = 0, coreNodeID = 0, nextBreacherID = -1 },
                    new PlayerData { playerID = 1, coreNodeID = 3, nextBreacherID = -1 } },
                nodes = new NodeData[4], villagers = new VillagerData[population] };
            for (int i = 0; i < 4; i++) s.nodes[i] = new NodeData { nodeID = i,
                districtType = i == 0 || i == 3 ? DistrictType.Core : (DistrictType)15,
                baseDistrictType = i == 0 || i == 3 ? DistrictType.Core : (DistrictType)15,
                ownerID = i == 0 ? 0 : i == 3 ? 1 : -1, links = Array.Empty<Link>() };
            for (int i = 0; i < population; i++) s.villagers[i] = Body(i, 0, i == 0 ? 1 : 0);
            return s;
        }
        private VillagerData Body(int id, int owner, int node) => new VillagerData {
            villagerID = id, ownerID = owner, currentNodeID = node, previousNodeID = node,
            targetNodeID = -1, combatTargetID = -1, movePath = Array.Empty<int>(),
            state = VillagerState.Idle, hp = balance.baseHP, maxHP = balance.baseHP,
            attackDamage = balance.baseAttackDamage, moveSpeedTicks = balance.baseMoveSpeedTicks,
            attackCooldownMax = balance.baseAttackCooldownMax };
        private static int Mask(SimulationState s, int node = 1)
        {
            var f = typeof(NodeData).GetField("townPaidMask"); Assert.IsNotNull(f, "townPaidMask");
            return (int)f.GetValue(s.nodes[node]);
        }
        private void Claim(SimulationState s, int player, int node = 1)
        {
            for (int i = 0; i < s.villagers.Length; i++)
                if (!s.villagers[i].isConsumed && s.villagers[i].state != VillagerState.Dead)
                    s.villagers[i].currentNodeID = s.players[s.villagers[i].ownerID].coreNodeID;
            s.villagers[0] = Body(0, player, node);
            s.nodes[node].ownerID = 1 - player;
            s.nodes[node].claimBar = player == 0 ? balance.claimThreshold - 1 : -balance.claimThreshold + 1;
            GameSimulation.SimulateTick(s);
            Assert.AreEqual(player, s.nodes[node].ownerID);
        }
        private SimulationState Run(string scenario)
        {
            var s = Fixture(scenario == "TownCap_ConsumesEntitlementWithoutDeferredCredit" ? 24 : 1);
            switch (scenario)
            {
                case "EachPlayerGetsTwoOncePerTown":
                    Claim(s, 0); Assert.AreEqual(3, s.villagers.Length); Assert.AreEqual(1, Mask(s));
                    Claim(s, 1); Assert.AreEqual(5, s.villagers.Length); Assert.AreEqual(3, Mask(s));
                    Claim(s, 0); Assert.AreEqual(5, s.villagers.Length); Assert.AreEqual(3, Mask(s));
                    Assert.AreEqual(0, Mask(s, 2)); Claim(s, 0, 2);
                    Assert.AreEqual(7, s.villagers.Length); Assert.AreEqual(1, Mask(s, 2));
                    // Ownership neutralisation and district replacement cannot restore credit.
                    s.nodes[1].claimBar = 1;
                    s.villagers[0] = Body(0, 1, 1); GameSimulation.SimulateTick(s);
                    Assert.AreEqual(-1, s.nodes[1].ownerID); Assert.AreEqual(3, Mask(s));
                    s.nodes[1].districtType = DistrictType.None; Claim(s, 0);
                    s.nodes[1].districtType = (DistrictType)15; Claim(s, 0);
                    Assert.AreEqual(7, s.villagers.Length); Assert.AreEqual(3, Mask(s));
                    break;
                case "TownCap_ConsumesEntitlementWithoutDeferredCredit":
                    for (int i = 1; i < s.villagers.Length; i++) {
                        s.villagers[i].state = VillagerState.Dead; s.villagers[i].hp = 0;
                        s.villagers[i].respawnTicksRemaining = int.MaxValue; }
                    Claim(s, 0); Assert.AreEqual(25, s.villagers.Length); Assert.AreEqual(1, Mask(s));
                    Claim(s, 0, 2); Assert.AreEqual(25, s.villagers.Length); Assert.AreEqual(1, Mask(s, 2));
                    s.villagers[1].isConsumed = true; Claim(s, 0); Claim(s, 0, 2);
                    Assert.AreEqual(25, s.villagers.Length); Assert.AreEqual(24, NodeActionRules.CountPopulation(s, 0));
                    break;
                case "PartialClaimAndRestore_DoNotPay":
                    GameSimulation.SimulateTick(s); Assert.AreEqual(1, s.villagers.Length); Assert.AreEqual(0, Mask(s));
                    s.nodes[1].ownerID = 0; s.nodes[1].claimBar = balance.claimThreshold - 1;
                    GameSimulation.SimulateTick(s); Assert.AreEqual(balance.claimThreshold, s.nodes[1].claimBar);
                    Assert.AreEqual(1, s.villagers.Length); Assert.AreEqual(0, Mask(s));
                    Claim(s, 0); Assert.AreEqual(3, s.villagers.Length); Assert.AreEqual(1, Mask(s));
                    break;
                case "TownIsDeadAfterPayment":
                    Claim(s, 0); s.nodes[1].claimBar = balance.claimThreshold - 100;
                    for (int i = 0; i < s.villagers.Length; i++) {
                        s.villagers[i].currentNodeID = 1; s.villagers[i].hp = s.villagers[i].maxHP; }
                    CommandProcessor.ProcessCommand(s, new GameCommand { type = CommandType.Recruit, playerID = 0,
                        targetNodeID = 1, villagerID = -1 });
                    for (int t = 0; t < 240; t++) GameSimulation.SimulateTick(s);
                    Assert.AreEqual(3, s.villagers.Length); Assert.AreEqual(1, Mask(s));
                    Assert.AreEqual(balance.claimThreshold, s.nodes[1].claimBar);
                    Assert.AreEqual(0, s.players[0].food); Assert.AreEqual(0, s.players[0].materials);
                    Assert.AreEqual(0, s.players[0].metal); Assert.AreEqual(0, s.players[0].recruitCount);
                    foreach (var v in s.villagers) { Assert.AreEqual(VillagerState.Idle, v.state);
                        Assert.AreEqual(SuitType.None, v.suit); Assert.AreEqual(balance.baseHP, v.hp); }
                    break;
                case "TownRaidReward_SpawnsAtCapturedTown":
                    Claim(s, 0); Assert.AreEqual(3, s.villagers.Length); Assert.AreEqual(0, s.villagers[0].villagerID);
                    for (int i = 1; i < 3; i++) { var v = s.villagers[i];
                        Assert.AreEqual(i, v.villagerID); Assert.AreEqual(1, v.currentNodeID);
                        Assert.AreEqual(0, v.ownerID); Assert.AreEqual(SuitType.None, v.suit);
                        Assert.AreEqual(VillagerState.Idle, v.state); Assert.AreEqual(balance.baseHP, v.hp);
                        Assert.AreEqual(balance.baseHP, v.maxHP); Assert.AreEqual(balance.baseAttackDamage, v.attackDamage);
                        Assert.AreEqual(balance.baseMoveSpeedTicks, v.moveSpeedTicks);
                        Assert.AreEqual(balance.baseAttackCooldownMax, v.attackCooldownMax);
                        Assert.AreEqual(-1, v.targetNodeID); Assert.AreEqual(-1, v.combatTargetID); Assert.IsEmpty(v.movePath); }
                    break;
            }
            return s;
        }
        [TestCase("EachPlayerGetsTwoOncePerTown")]
        [TestCase("TownCap_ConsumesEntitlementWithoutDeferredCredit")]
        [TestCase("PartialClaimAndRestore_DoNotPay")]
        [TestCase("TownIsDeadAfterPayment")]
        [TestCase("TownRaidReward_SpawnsAtCapturedTown")]
        public void Correctness(string scenario) => Run(scenario);
        [TestCase("EachPlayerGetsTwoOncePerTown")]
        [TestCase("TownCap_ConsumesEntitlementWithoutDeferredCredit")]
        [TestCase("PartialClaimAndRestore_DoNotPay")]
        [TestCase("TownIsDeadAfterPayment")]
        [TestCase("TownRaidReward_SpawnsAtCapturedTown")]
        public void Scenario_Determinism(string scenario)
        {
            int first = SimulationStateHasher.ComputeHash(Run(scenario));
            Assert.AreEqual(first, SimulationStateHasher.ComputeHash(Run(scenario)));
        }
    }
}
