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
        Breaching
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
        public Link[] links;
        public DistrictType districtType;
        public int claimBar;
        public int ownerID;
        public int bonusVillagersOnClaim;
        public int materialAllocation;

        public DistrictUpgradeCategory upgradeCategory;
        public DistrictType baseDistrictType;

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
        /// <summary>Successful paid respawns by this player during this match.</summary>
        public int paidRespawns;
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
        }

        private static int[] CopyInts(int[] values) => values == null ? null : (int[])values.Clone();
    }
}
