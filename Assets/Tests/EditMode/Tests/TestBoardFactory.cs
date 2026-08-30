using NodeWar.Simulation;

namespace NodeWar.Tests
{
    /// <summary>
    /// Shared minimal 3-node board used by MovementCorrectnessTests and
    /// DeterminismBaselineTests: player 0's Core (node 0) and player 1's Core
    /// (node 2) connected through one neutral connector (node 1), with one Idle
    /// villager per player parked on their own Core.
    /// </summary>
    internal static class TestBoardFactory
    {
        /// <summary>
        /// Returns a fresh SimulationState every call so independent test runs
        /// never share array references.
        /// </summary>
        public static SimulationState BuildThreeNodeBoard(GameBalanceData balance)
        {
            SimulationState state = new SimulationState();

            state.nodes = new NodeData[]
            {
                new NodeData
                {
                    nodeID = 0,
                    gridX = 0, // view-only; excluded from SimulationStateHasher
                    gridZ = 0,
                    edges = new Edge[]
                    {
                        new Edge { toNode = 1, travelWeight = 1 } // minimal weight for a tight test
                    },
                    districtType = DistrictType.Core,
                    baseDistrictType = DistrictType.Core,
                    slotType = NodeSlotType.Fixed,
                    claimBar = 10000, // fully owned by player 0 (GameManager's +/-10000 core convention)
                    ownerID = 0,
                    bonusVillagersOnClaim = 0,
                    materialAllocation = 0
                },
                new NodeData
                {
                    nodeID = 1,
                    gridX = 1,
                    gridZ = 0,
                    edges = new Edge[]
                    {
                        new Edge { toNode = 0, travelWeight = 1 },
                        new Edge { toNode = 2, travelWeight = 1 }
                    },
                    districtType = DistrictType.None, // neutral connector node
                    baseDistrictType = DistrictType.None,
                    slotType = NodeSlotType.Fixed,
                    claimBar = 0,
                    ownerID = -1, // unowned
                    bonusVillagersOnClaim = 0,
                    materialAllocation = 0
                },
                new NodeData
                {
                    nodeID = 2,
                    gridX = 2,
                    gridZ = 0,
                    edges = new Edge[]
                    {
                        new Edge { toNode = 1, travelWeight = 1 }
                    },
                    districtType = DistrictType.Core,
                    baseDistrictType = DistrictType.Core,
                    slotType = NodeSlotType.Fixed,
                    claimBar = -10000, // fully owned by player 1
                    ownerID = 1,
                    bonusVillagersOnClaim = 0,
                    materialAllocation = 0
                }
            };

            state.players = new PlayerData[]
            {
                // Starting resources match BoardConfigData.Default() (0/0/0) --
                // GameBalanceData itself defines no starting-resource fields.
                new PlayerData { playerID = 0, coreNodeID = 0, food = 0, materials = 0, metal = 0, breachCount = 0 },
                new PlayerData { playerID = 1, coreNodeID = 2, food = 0, materials = 0, metal = 0, breachCount = 0 }
            };

            state.villagers = new VillagerData[]
            {
                MakeIdleVillager(villagerID: 0, ownerID: 0, currentNodeID: 0, balance),
                MakeIdleVillager(villagerID: 1, ownerID: 1, currentNodeID: 2, balance)
            };

            return state;
        }

        public static VillagerData MakeIdleVillager(int villagerID, int ownerID, int currentNodeID, GameBalanceData balance)
        {
            return new VillagerData
            {
                villagerID = villagerID,
                ownerID = ownerID,
                currentNodeID = currentNodeID,
                targetNodeID = -1,
                movePath = new int[0],
                movePathIndex = 0,
                moveProgress = 0,
                previousNodeID = currentNodeID,
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
            };
        }
    }
}
