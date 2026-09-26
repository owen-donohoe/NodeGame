namespace NodeWar.Simulation
{
    // ===== ENUMS =====
    public enum DistrictType
    {
        None, // empty connector / crossroads
        Farm,
        Mine,
        Village,
        Barracks,
        Core,
        Forge,
        
        Camp,
        Shrine,
        Arsenal,
        Sanctuary,
        Watchtower,
        Rampart,
        Market

    }

    public enum NodeSlotType
    {
        Fixed,
        Army,
        Healing,
        Affect,
        ResourceSpecial
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
        Warrior, // renamed from Soldier
        Smelter,
        Guardian,
        Scout,
        Berserker,
        Medic,
        Merchant, // auto-assigned: Market worker
        Acolyte, // auto-assigned: Sanctuary worker
        Watcher // auto-assigned: Watchtower worker
    }

    // ===== DATA STRUCTS =====
    [System.Serializable]
    public struct NodeData
    {
        public int nodeID;
        public int gridX;
        public int gridZ;
        public Edge[] edges;
        public DistrictType districtType;
        public int claimBar;
        public int ownerID;
        public int bonusVillagersOnClaim;
        public int materialAllocation;

        public NodeSlotType slotType;
        public DistrictType baseDistrictType;

        /// <summary>
        /// Which era of its district this node plays: the era of the player who
        /// put the district here (by draft, or by upgrading it on claim). The
        /// board's own fixed placements are era 0.
        /// </summary>
        public int districtEra;
    }

    [System.Serializable]

    public struct Edge
    {
        public int toNode;
        public int travelWeight;
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
        public int attackCooldownRemaining;
        public int attackCooldownMax;
        public int combatTargetID;
        public int fightPriority;
        public bool isConsumed;
        public int productionTicksRemaining;
        public int productionTicksMax;
        public bool hasRampartBonus;

        /// <summary>
        /// The era of the Rampart whose bonus this villager holds, so leaving
        /// takes back exactly what arriving gave. Meaningless without
        /// hasRampartBonus.
        /// </summary>
        public int rampartBonusEra;
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
        public int[] draftedSuits; // (int)SuitType values this player can equip
        public int[] draftedNodes; // (int)DistrictType values for draft upgrades

        /// <summary>
        /// The era of each suit and district this player fields, indexed by
        /// (int)SuitType and (int)DistrictType. Missing or short means era 0.
        /// </summary>
        public int[] suitEras;
        public int[] districtEras;

        public int SuitEra(SuitType suit)
        {
            int i = (int)suit;
            return suitEras != null && i >= 0 && i < suitEras.Length ? suitEras[i] : 0;
        }

        public int DistrictEra(DistrictType district)
        {
            int i = (int)district;
            return districtEras != null && i >= 0 && i < districtEras.Length ? districtEras[i] : 0;
        }
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
        public int defaultEdgeWeight;

        public SimulationState()
        {
            tickCount = 0;
            gameOver = false;
            winnerID = -1;
            defaultEdgeWeight = BoardConfigData.DefaultEdgeWeight;
        }
    }
}