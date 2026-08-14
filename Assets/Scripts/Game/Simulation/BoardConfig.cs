using UnityEngine;

namespace NodeWar.Simulation
{
    [CreateAssetMenu(fileName = "BoardConfig", menuName = "NodeWar/Board Config")]
    public class BoardConfig : ScriptableObject
    {
        [Header("Grid Dimensions")]
        public int gridCols = 4;
        public int gridRows = 7;

        [Header("Spacing")]
        [Tooltip("World-space distance between adjacent nodes")]
        public float nodeScale = 5f;

        [Header("Edge Weights")]
        [Tooltip("Base travel cost per edge. Higher = slower traversal")]
        public int defaultEdgeWeight = 3;

        [Header("Starting Resources")]
        public int startingVillagersPerPlayer = 3;
        public int startingFood = 5;
        public int startingMaterials = 3;
        public int startingMetal = 0;

        [Header("Pathfinding Preference (integer percentages)")]
        [Tooltip("50 = 0.5x cost (preferred). 200 = 2.0x cost (avoided)")]
        public int ownedMultiplier = 50;
        public int partiallyOwnedMultiplier = 75;
        public int unownedMultiplier = 100;
        public int enemyPartiallyOwnedMultiplier = 150;
        public int enemyOwnedMultiplier = 200;

        [Header("Camera Bounds")]
        public float boundsMinX = -12f;
        public float boundsMaxX = 12f;
        public float boundsMinZ = -8f;
        public float boundsMaxZ = 8f;
    }
}