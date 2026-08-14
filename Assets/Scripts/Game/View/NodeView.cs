using UnityEngine;
using NodeWar.Simulation;

namespace NodeWar.View
{
    public class NodeView : MonoBehaviour
    {
        private SimulationState simState;
        private int nodeID;
        private bool initialized = false;

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propBlock;

        // Sub-components
        private NodeHighlight highlight;
        private NodeWar.UI.NodeClaimBar claimBar;

        // Muted node colors
        private static readonly Color neutralColor = new Color(0.45f, 0.45f, 0.4f);
        private static readonly Color player0Color = new Color(0.2f, 0.3f, 0.5f);
        private static readonly Color player1Color = new Color(0.5f, 0.2f, 0.2f);
        private static readonly Color coreColor0 = new Color(0.15f, 0.2f, 0.6f);
        private static readonly Color coreColor1 = new Color(0.6f, 0.15f, 0.15f);

        public void Initialize(SimulationState state, int id)
        {
            simState = state;
            nodeID = id;
            initialized = true;

            meshRenderer = GetComponentInChildren<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = GetComponent<MeshRenderer>();

            propBlock = new MaterialPropertyBlock();

            // Add NodeHighlight component
            highlight = gameObject.AddComponent<NodeHighlight>();

            UpdateVisuals();
        }

        /// <summary>
        /// Set the claim bar reference (created externally by GameManager or prefab).
        /// </summary>
        public void SetClaimBar(NodeWar.UI.NodeClaimBar bar)
        {
            claimBar = bar;
        }

        /// <summary>
        /// Trigger a brief pulse highlight on this node.
        /// Called by CommandSystem when this node is a move target.
        /// </summary>
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
            NodeData node = simState.nodes[nodeID];

            if (meshRenderer != null)
            {
                Color color = CalculateNodeColor(node);

                meshRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor("_Color", color);
                propBlock.SetColor("_BaseColor", color);
                meshRenderer.SetPropertyBlock(propBlock);
            }

            // Scale is now driven by GameManager's nodeScale (set during initialization via transform)
            // Don't override scale here — let the prefab/spawn handle it
        }

        private Color CalculateNodeColor(NodeData node)
        {
            if (node.districtType == DistrictType.Core)
            {
                return (node.ownerID == 0) ? coreColor0 : coreColor1;
            }

            if (node.ownerID == 0)
            {
                float t = (float)node.claimBar / 10000f;
                return Color.Lerp(neutralColor, player0Color, t);
            }
            if (node.ownerID == 1)
            {
                float t = (float)(-node.claimBar) / 10000f;
                return Color.Lerp(neutralColor, player1Color, t);
            }

            if (node.claimBar > 0)
            {
                float t = (float)node.claimBar / 10000f;
                return Color.Lerp(neutralColor, player0Color, t);
            }
            if (node.claimBar < 0)
            {
                float t = (float)(-node.claimBar) / 10000f;
                return Color.Lerp(neutralColor, player1Color, t);
            }

            return neutralColor;
        }

        public int GetNodeID()
        {
            return nodeID;
        }
    }
}