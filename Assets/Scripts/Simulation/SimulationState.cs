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
        Core
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
        Soldier
    }

    // ===== DATA STRUCTS =====

    [System.Serializable]
    public struct NodeData
    {
        public int nodeID;
        public Vector3 worldPosition;       // Used by View layer only
        public int[] connectedNodes;
        public DistrictType districtType;
        public int claimBar;                // -10000 to +10000
        public int ownerID;                 // -1 neutral, 0 player0, 1 player1
        public int bonusVillagersOnClaim;
    }

    [System.Serializable]
    public struct VillagerData
    {
        public int villagerID;
        public int ownerID;                 // 0 or 1
        public int currentNodeID;
        public int targetNodeID;            // -1 if stationary
        public int[] movePath;              // BFS path (node IDs in order)
        public int movePathIndex;           // Current index in movePath
        public int moveProgress;            // 0 to moveSpeedTicks
        public int previousNodeID;          // Last node left (for interpolation)
        public VillagerState state;
        public SuitType suit;
        public int hp;
        public int maxHP;
        public int attackDamage;
        public int moveSpeedTicks;          // Ticks to traverse one edge
        public int respawnTicksRemaining;
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
        public PlayerData[] players;        // Always length 2
        public int tickCount;
        public bool gameOver;
        public int winnerID;                // -1 if no winner yet

        public SimulationState()
        {
            tickCount = 0;
            gameOver = false;
            winnerID = -1;
        }
    }
}