using UnityEngine;
using TMPro;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// Generic panel for Village and None district types.
    /// Shows basic info only — no interactive elements.
    /// </summary>
    public class GenericPanelContent : MonoBehaviour
    {
        [Header("References (assign in prefab)")]
        [SerializeField] private TextMeshProUGUI infoLabel;

        private SimulationState simState;
        private int nodeID;
        private int controlledPID;

        public void Initialize(SimulationState state, int node, int pid)
        {
            simState = state;
            nodeID = node;
            controlledPID = pid;

            NodeData n = state.nodes[nodeID];
            string typeName = (n.districtType == DistrictType.Village) ? "Village" : "Crossroads";

            int villagerCount = 0;
            for (int i = 0; i < state.villagers.Length; i++)
            {
                if (state.villagers[i].currentNodeID == nodeID &&
                    state.villagers[i].state != VillagerState.Dead &&
                    !state.villagers[i].isConsumed)
                    villagerCount++;
            }

            infoLabel.text = typeName + "\nVillagers present: " + villagerCount;
        }

        private void Update()
        {
            if (simState == null) return;

            int villagerCount = 0;
            for (int i = 0; i < simState.villagers.Length; i++)
            {
                if (simState.villagers[i].currentNodeID == nodeID &&
                    simState.villagers[i].state != VillagerState.Dead &&
                    !simState.villagers[i].isConsumed)
                    villagerCount++;
            }

            NodeData n = simState.nodes[nodeID];
            string typeName = (n.districtType == DistrictType.Village) ? "Village" : "Crossroads";
            infoLabel.text = typeName + "\nVillagers present: " + villagerCount;
        }
    }
}