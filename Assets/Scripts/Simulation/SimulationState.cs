using UnityEngine;

namespace NodeWar.Simulation
{
    // ===== ENUMS =====

    public enum DistrictType
    {
        None,
        Farm,
        Mine,
        Village,
        Barracks,
        Core,
        Forge
    }

    public enum VillagerState
    {
        Idle,
        Moving,
        Working,
        Claiming,
        Fighting,
        Dead
    }

    public enum SuitType
    {
        None,
        Farmer,
        Miner,
        Soldier,
        Smelter
    }

    // ===== DATA STRUCTS =====

    [System.Serializable]
    public struct NodeData
    {
        public int nodeID;
        public Vector3 worldPosition;
        public int[] connectedNodes;
        public DistrictType districtType;
        public int claimBar;
        public int ownerID;
        public int bonusVillagersOnClaim;
        public int materialAllocation;
    }

    [System.Serializable]
    public struct VillagerData
    {
        public int villagerID;
        public int ownerID;
        public int currentNodeID;
        public int targetNodeID;
        public int[] movePath;
        public int movePathIndex;
        public int moveProgress;
        public int previousNodeID;
        public VillagerState state;
        public SuitType suit;
        public int hp;
        public int maxHP;
        public int attackDamage;
        public int moveSpeedTicks;
        public int respawnTicksRemaining;

        // Phase 5: Combat fields
        public int attackCooldownRemaining;
        public int attackCooldownMax;
        public int combatTargetID;
        public int fightPriority;

        // Phase 6: Breach fields
        public bool isConsumed;

        // Phase 7: Production fields
        public int productionTicksRemaining;
        public int productionTicksMax;
    }

    [System.Serializable]
    public struct PlayerData
    {
        public int playerID;
        public int coreNodeID;
        public int food;
        public int materials;
        public int metal;
        public int breachCount;
    }

    // ===== SIMULATION STATE =====

    [System.Serializable]
    public class SimulationState
    {
        public NodeData[] nodes;
        public VillagerData[] villagers;
        public PlayerData[] players;
        public int tickCount;
        public bool gameOver;
        public int winnerID;

        public SimulationState()
        {
            tickCount = 0;
            gameOver = false;
            winnerID = -1;
        }
    }
}