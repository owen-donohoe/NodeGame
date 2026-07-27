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

        // Node colors — muted, desaturated (distinct from bright villager colors)
        private static readonly Color neutralColor = new Color(0.45f, 0.45f, 0.4f);
        private static readonly Color player0Color = new Color(0.2f, 0.3f, 0.5f);   // Dark blue-grey
        private static readonly Color player1Color = new Color(0.5f, 0.2f, 0.2f);   // Dark red-brown
        private static readonly Color coreColor0 = new Color(0.15f, 0.2f, 0.6f);    // Deep blue
        private static readonly Color coreColor1 = new Color(0.6f, 0.15f, 0.15f);   // Deep red

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
                Color color;

                if (node.districtType == DistrictType.Core)
                {
                    color = (node.ownerID == 0) ? coreColor0 : coreColor1;
                }
                else
                {
                    switch (node.ownerID)
                    {
                        case 0: color = player0Color; break;
                        case 1: color = player1Color; break;
                        default: color = neutralColor; break;
                    }
                }

                meshRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor("_Color", color);
                propBlock.SetColor("_BaseColor", color);
                meshRenderer.SetPropertyBlock(propBlock);
            }

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

        public int GetNodeID()
        {
            return nodeID;
        }
    }
}