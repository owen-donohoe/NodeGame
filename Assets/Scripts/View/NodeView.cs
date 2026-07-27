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

            UpdateVisuals();
        }

        private void Update()
        {
            if (!initialized) return;
            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            NodeData node = simState.nodes[nodeID];

            transform.position = node.worldPosition;

            if (meshRenderer != null)
            {
                Color color = CalculateNodeColor(node);

                meshRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor("_Color", color);
                propBlock.SetColor("_BaseColor", color);
                meshRenderer.SetPropertyBlock(propBlock);
            }

            // Scale by district type
            float scale = 1f;
            switch (node.districtType)
            {
                case DistrictType.Core: scale = 1.5f; break;
                case DistrictType.Village: scale = 1.2f; break;
                case DistrictType.Barracks: scale = 1.3f; break;
                case DistrictType.None: scale = 0.7f; break;
                default: scale = 1f; break;
            }
            transform.localScale = Vector3.one * scale;
        }

        private Color CalculateNodeColor(NodeData node)
        {
            // Cores: always full owner color
            if (node.districtType == DistrictType.Core)
            {
                return (node.ownerID == 0) ? coreColor0 : coreColor1;
            }

            // Owned nodes: blend from full color toward neutral based on bar erosion
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

            // Neutral: show claim progress as color shift
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