namespace NodeWar.Simulation
{
    // ===== ENUMS =====
    public enum DistrictType
    {
        None = 0, // empty connector / crossroads
        Farm = 1,
        Mine = 2,
        Village = 3,
        Barracks = 4,
        Core = 5,
        Forge = 6,
        
        Camp = 7,
        Shrine = 8,
        Arsenal = 9,
        Sanctuary = 10,
        Watchtower = 11,
        Rampart = 12,
        Market = 13,

        /// <summary>
        /// Built on a Lake cell that the board marks as a district slot. Appended
        /// after Market so every earlier value keeps its number. In stage B it is
        /// an inert connector: it grants nothing and blocks nobody.
        /// </summary>
        Pier = 14,
        Town = 15,
        Infirmary = 16,
        Fortress = 17,
        Storehouse = 18
    }

    /// <summary>
    /// What a board cell is made of. Land cells always carry a node; a Lake cell
    /// carries one only when a Pier is built on it; Ocean never does. A Pier slot
    /// is a Lake cell with <see cref="BoardConfigData.districtSlots"/> set, not a
    /// fourth terrain value. Values are persisted in match logs: never renumber.
    /// </summary>
    public enum TerrainType
    {
        Land = 0,
        Lake = 1,
        Ocean = 2
    }

    public enum DistrictUpgradeCategory
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
        Dead,
        Breaching,
        AttackingStructure
    }

    public enum StructureKind
    {
        None = 0,
        Minion = 1,
        Fortification = 2
    }

    public enum SuitType
    {
        None = 0,
        Farmer,
        Miner,
        Warrior, // renamed from Soldier
        Smelter,
        Guardian,
        Scout,
        Berserker,
        Medic,
        Merchant, // Historical Market suit; never assigned by active production
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
        public Link[] links;
        public DistrictType districtType;
        public int claimBar;
        public int ownerID;
        public int townPaidMask;
        public int fortressLevel;
        public StructureKind structureKind;
        public int structureHP;
        public int bankFood;
        public int bankMaterials;
        public int bankMetal;
        public int minionProductionRemaining;
        public int storehouseNextResource;
        public bool storehouseInitialised;
        public int materialAllocation;
        public int recruitReadyTick;
        public bool autoRecruit;

        public DistrictUpgradeCategory upgradeCategory;
        public DistrictType baseDistrictType;

        /// <summary>The terrain of this node's cell: Land, or Lake under a Pier.</summary>
        public TerrainType terrain;

        /// <summary>
        /// Which era of its district this node plays: the era of the player who
        /// put the district here (by draft, or by upgrading it on claim). The
        /// board's own fixed placements are era 0.
        /// </summary>
        public int districtEra;
    }

    [System.Serializable]

    public struct Link
    {
        public int toNodeID;
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
        /// <summary>Successful paid respawns by this player during this match.</summary>
        public int paidRespawns;
        /// <summary>Successful recruits during this match; persists through ownership loss.</summary>
        public int recruitCount;
        /// <summary>Progress against this player's core.</summary>
        public int breachBar;
        /// <summary>Derived candidate cache, refreshed after all tick mutations; -1 for none.</summary>
        public int nextBreacherID;
        public int[] draftedSuits; // (int)SuitType values this player can equip
        public int[] draftedDistricts; // (int)DistrictType values for draft upgrades

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
        public int defaultLinkWeight;

        /// <summary>
        /// <see cref="BoardHasher"/> fingerprint of the board this state was built
        /// from, set once by <see cref="MatchFactory"/>. Hashed so two peers on
        /// different maps diverge at the first checkpoint, not silently.
        /// </summary>
        public int boardHash;

        public SimulationState()
        {
            tickCount = 0;
            gameOver = false;
            winnerID = -1;
            defaultLinkWeight = BoardConfigData.DefaultLinkWeight;
        }

        /// <summary>
        /// Makes this state an independent copy of <paramref name="source"/>, in
        /// place: views, selection and the HUD hold a reference to this object,
        /// so a rollback (8.2e) must change what it contains, not which object
        /// it is. Every mutable array is copied fresh so the two never share a
        /// write. Node links are shared: they are fixed once the board is built
        /// and no tick writes them.
        ///
        /// Like SimulationStateHasher, this must name every field. A field added
        /// to any of these types and not copied here is caught by
        /// SimulationStateCopyTests, which sets every field by reflection.
        /// </summary>
        public void CopyFrom(SimulationState source)
        {
            if (source == null) throw new System.ArgumentNullException(nameof(source));
            if (ReferenceEquals(source, this)) return;

            // Value-type clone includes banks, minion timers/construction and every node scalar.
            nodes = source.nodes == null ? null : (NodeData[])source.nodes.Clone();

            if (source.villagers == null) villagers = null;
            else
            {
                villagers = (VillagerData[])source.villagers.Clone();
                for (int i = 0; i < villagers.Length; i++)
                    villagers[i].movePath = CopyInts(villagers[i].movePath);
            }

            if (source.players == null) players = null;
            else
            {
                players = (PlayerData[])source.players.Clone();
                for (int i = 0; i < players.Length; i++)
                {
                    players[i].draftedSuits = CopyInts(players[i].draftedSuits);
                    players[i].draftedDistricts = CopyInts(players[i].draftedDistricts);
                    players[i].suitEras = CopyInts(players[i].suitEras);
                    players[i].districtEras = CopyInts(players[i].districtEras);
                }
            }

            tickCount = source.tickCount;
            gameOver = source.gameOver;
            winnerID = source.winnerID;
            defaultLinkWeight = source.defaultLinkWeight;
            boardHash = source.boardHash;
        }

        private static int[] CopyInts(int[] values) => values == null ? null : (int[])values.Clone();
    }
}
