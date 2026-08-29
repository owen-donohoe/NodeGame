using UnityEngine;
using NodeWar.Simulation;

namespace NodeWar.Config
{
    [CreateAssetMenu(fileName = "BoardConfig", menuName = "NodeWar/Board Config")]
    public class BoardConfig : ScriptableObject
    {
        [SerializeField] private BoardConfigData data = BoardConfigData.Default();

        public BoardConfigData Data => data;

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
        public DraftNodeEntry[] baseDraftNodesP0;
        public DraftNodeEntry[] baseDraftNodesP1;

        [Header("Bot Draft Loadout")]
        [Tooltip("Additional nodes added to the bot player's draft pool beyond the base draft nodes.")]
        public DraftNodeEntry[] botLoadoutNodes;

        [System.Serializable]
        public struct DraftNodeEntry
        {
            public DistrictType districtType;
        }
    }
}
