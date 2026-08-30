using UnityEngine;

namespace NodeWar.Lobby
{
    [CreateAssetMenu(fileName = "NodeDefinition", menuName = "NodeWar/Lobby/Node Definition")]
    public class NodeDefinition : ScriptableObject
    {
        public string nodeID;
        public string displayName;
        public Sprite icon;
        [TextArea(2, 4)]
        public string description;
        public NodeCategory category;
    }
}