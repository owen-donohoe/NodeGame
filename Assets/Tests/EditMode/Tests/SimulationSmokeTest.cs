using NUnit.Framework;
using NodeWar.Simulation;

namespace NodeWar.Tests
{
    public class SimulationSmokeTest
    {
        [Test]
        public void SimulateTick_RunsOnceWithoutThrowing()
        {
            GameBalanceData balance = GameBalanceData.Default();
            GameSimulation.SetBalance(balance);

            SimulationState state = new SimulationState();

            state.nodes = new NodeData[]
            {
                new NodeData
                {
                    nodeID = 0,
                    gridX = 0,
                    gridZ = 0,
                    edges = new Edge[0],
                    districtType = DistrictType.None,
                    baseDistrictType = DistrictType.None,
                    slotType = NodeSlotType.Fixed,
                    claimBar = 0,
                    ownerID = 0,
                    bonusVillagersOnClaim = 0,
                    materialAllocation = 0
                }
            };

            state.players = new PlayerData[]
            {
                new PlayerData { playerID = 0, coreNodeID = 0 },
                new PlayerData { playerID = 1, coreNodeID = 0 }
            };

            state.villagers = new VillagerData[]
            {
                new VillagerData
                {
                    villagerID = 0,
                    ownerID = 0,
                    currentNodeID = 0,
                    targetNodeID = -1,
                    movePath = new int[0],
                    movePathIndex = 0,
                    moveProgress = 0,
                    previousNodeID = 0,
                    state = VillagerState.Idle,
                    suit = SuitType.None,
                    hp = balance.baseHP,
                    maxHP = balance.baseHP,
                    attackDamage = balance.baseAttackDamage,
                    moveSpeedTicks = balance.baseMoveSpeedTicks,
                    respawnTicksRemaining = 0,
                    attackCooldownRemaining = balance.baseAttackCooldownMax,
                    attackCooldownMax = balance.baseAttackCooldownMax,
                    combatTargetID = -1,
                    fightPriority = 0,
                    isConsumed = false,
                    productionTicksRemaining = 0,
                    productionTicksMax = 0,
                    hasRampartBonus = false
                }
            };

            Assert.DoesNotThrow(() => GameSimulation.SimulateTick(state));
            Assert.IsNotNull(state);
        }
    }
}
