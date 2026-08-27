using UnityEngine;
using NodeWar.Simulation;

namespace NodeWar.View
{
    /// <summary>
    /// Reads simulation node state each frame and updates mesh color accordingly.
    /// Color reflects ownership and claim progress via lerp between neutral and player colors.
    /// </summary>
    public class NodeView : MonoBehaviour
    {
        [Header("Ownership Colors")]
        [SerializeField] private Color neutralColor = new Color(0.45f, 0.45f, 0.4f);
        [SerializeField] private Color player0ClaimedColor = new Color(0.2f, 0.3f, 0.5f);
        [SerializeField] private Color player1ClaimedColor = new Color(0.5f, 0.2f, 0.2f);
        [SerializeField] private Color coreColorPlayer0 = new Color(0.15f, 0.2f, 0.6f);
        [SerializeField] private Color coreColorPlayer1 = new Color(0.6f, 0.15f, 0.15f);

        private SimulationState simState;
        private int nodeID;
        private bool initialized = false;

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propBlock;

        private NodeHighlight highlight;
        private NodeWar.UI.NodeClaimBar claimBar;

        public void Initialize(SimulationState state, int id)
        {
            simState = state;
            nodeID = id;
            initialized = true;

            meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            propBlock = new MaterialPropertyBlock();

            highlight = gameObject.AddComponent<NodeHighlight>();

            UpdateVisuals();
        }

        public void SetClaimBar(NodeWar.UI.NodeClaimBar bar)
        {
            claimBar = bar;
        }

        public void TriggerHighlight(Color color)
        {
            if (highlight != null)
                highlight.Pulse(color);
        }

        private void Update()
        {
            if (!initialized) return;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (meshRenderer == null) return;

            Color color = CalculateNodeColor(simState.nodes[nodeID]);

            meshRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_Color", color);
            propBlock.SetColor("_BaseColor", color);
            meshRenderer.SetPropertyBlock(propBlock);
        }

        /// <summary>
        /// Cores use flat player colors. Other nodes lerp between neutral and player color
        /// based on normalized claim bar progress. Contesting progress uses opponent color direction.
        /// </summary>
        private Color CalculateNodeColor(NodeData node)
        {
            if (node.districtType == DistrictType.Core)
                return (node.ownerID == 0) ? coreColorPlayer0 : coreColorPlayer1;

            if (node.ownerID == 0)
                return Color.Lerp(neutralColor, player0ClaimedColor, (float)node.claimBar / 10000f);

            if (node.ownerID == 1)
                return Color.Lerp(neutralColor, player1ClaimedColor, (float)(-node.claimBar) / 10000f);

            // Unowned but contested — show partial progress toward whichever player is claiming
            if (node.claimBar > 0)
                return Color.Lerp(neutralColor, player0ClaimedColor, (float)node.claimBar / 10000f);
            if (node.claimBar < 0)
                return Color.Lerp(neutralColor, player1ClaimedColor, (float)(-node.claimBar) / 10000f);

            return neutralColor;
        }

        public int GetNodeID() => nodeID;
    }
}