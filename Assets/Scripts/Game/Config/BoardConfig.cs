using UnityEngine;
using UnityEngine.Serialization;
using NodeWar.Simulation;

namespace NodeWar.Config
{
    [CreateAssetMenu(fileName = "BoardConfig", menuName = "NodeWar/Board Config")]
    public class BoardConfig : ScriptableObject
    {
        [SerializeField] private string mapId = PremadeMaps.Hourglass01Id;

        /// <summary>
        /// The pre-terrain board the asset was saved with. Kept so the asset still loads and
        /// the Editor migration can read it; the running game builds the map named by
        /// <see cref="mapId"/>, never this.
        /// </summary>
        [SerializeField] private SerializedBoard data;

        public string MapId => mapId;

        /// <summary>The shipped map this config names. An unknown ID is an error, not a default board.</summary>
        public BoardConfigData Data
        {
            get
            {
                if (!PremadeMaps.TryGet(mapId, out BoardConfigData board))
                    throw new System.InvalidOperationException("Unknown map ID \"" + mapId + "\" in BoardConfig.");
                return board;
            }
        }

        [Header("Spacing")]
        [Tooltip("World-space distance between adjacent nodes")]
        public float nodeScale = 6f;

        [Header("Camera Bounds")]
        public float boundsMinX = -12f;
        public float boundsMaxX = 12f;
        public float boundsMinZ = -8f;
        public float boundsMaxZ = 8f;

        [Header("Draft Configuration")]
        public float draftTurnDuration = 15f;
        public int maxConsecutiveTimeouts = 2;
        [FormerlySerializedAs("baseDraftNodesP0")] public DraftDistrictEntry[] baseDraftDistrictsP0;
        [FormerlySerializedAs("baseDraftNodesP1")] public DraftDistrictEntry[] baseDraftDistrictsP1;

        [Header("Bot Draft Loadout")]
        [Tooltip("Additional nodes added to the bot player's draft pool beyond the base draft nodes.")]
        [FormerlySerializedAs("botLoadoutNodes")] public DraftDistrictEntry[] botLoadoutDistricts;

        /// <summary>
        /// What Unity stores under <c>data:</c> in the asset. Simulation owns
        /// <see cref="BoardConfigData"/> and cannot carry a Unity attribute, so
        /// the serialized names live here: a field renamed on the simulation
        /// side keeps reading the key the asset was saved with, and the asset
        /// is only rewritten when the Editor next saves it.
        /// </summary>
        [System.Serializable]
        public struct SerializedBoard
        {
            public int gridCols;
            public int gridRows;
            [FormerlySerializedAs("defaultEdgeWeight")] public int defaultLinkWeight;
            public int startingVillagersPerPlayer;
            public int startingFood;
            public int startingMaterials;
            public int startingMetal;
            public int ownedMultiplier;
            public int partiallyOwnedMultiplier;
            public int unownedMultiplier;
            public int enemyPartiallyOwnedMultiplier;
            public int enemyOwnedMultiplier;
            public BoardConfigData.InitialDistrictPlacement[] initialPlacements;

            public static SerializedBoard From(BoardConfigData board)
            {
                return new SerializedBoard
                {
                    gridCols = board.gridCols,
                    gridRows = board.gridRows,
                    defaultLinkWeight = board.defaultLinkWeight,
                    startingVillagersPerPlayer = board.startingVillagersPerPlayer,
                    startingFood = board.startingFood,
                    startingMaterials = board.startingMaterials,
                    startingMetal = board.startingMetal,
                    ownedMultiplier = board.ownedMultiplier,
                    partiallyOwnedMultiplier = board.partiallyOwnedMultiplier,
                    unownedMultiplier = board.unownedMultiplier,
                    enemyPartiallyOwnedMultiplier = board.enemyPartiallyOwnedMultiplier,
                    enemyOwnedMultiplier = board.enemyOwnedMultiplier,
                    initialPlacements = board.initialPlacements
                };
            }

            public BoardConfigData ToData()
            {
                return new BoardConfigData
                {
                    gridCols = gridCols,
                    gridRows = gridRows,
                    defaultLinkWeight = defaultLinkWeight,
                    startingVillagersPerPlayer = startingVillagersPerPlayer,
                    startingFood = startingFood,
                    startingMaterials = startingMaterials,
                    startingMetal = startingMetal,
                    ownedMultiplier = ownedMultiplier,
                    partiallyOwnedMultiplier = partiallyOwnedMultiplier,
                    unownedMultiplier = unownedMultiplier,
                    enemyPartiallyOwnedMultiplier = enemyPartiallyOwnedMultiplier,
                    enemyOwnedMultiplier = enemyOwnedMultiplier,
                    initialPlacements = initialPlacements
                };
            }
        }

        [System.Serializable]
        public struct DraftDistrictEntry
        {
            public DistrictType districtType;
        }
    }
}
