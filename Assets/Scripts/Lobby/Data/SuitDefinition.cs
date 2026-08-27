using UnityEngine;

namespace NodeWar.Lobby
{
    [CreateAssetMenu(fileName = "SuitDefinition", menuName = "NodeWar/Lobby/Suit Definition")]
    public class SuitDefinition : ScriptableObject
    {
        public string suitID;
        public string displayName;
        public Sprite icon;
        [TextArea(2, 4)]
        public string description;
        public bool isGlobal;
    }
}