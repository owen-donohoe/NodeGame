using UnityEngine;
using UnityEngine.Serialization;

namespace NodeWar.Lobby
{
    [CreateAssetMenu(fileName = "DistrictDefinition", menuName = "NodeWar/Lobby/District Definition")]
    public class DistrictDefinition : ScriptableObject
    {
        [FormerlySerializedAs("nodeID")] public string districtID;
        public string displayName;
        public Sprite icon;
        [TextArea(2, 4)]
        public string description;
        public DistrictCategory category;
    }
}