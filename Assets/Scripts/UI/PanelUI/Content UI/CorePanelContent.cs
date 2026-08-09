using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NodeWar.Simulation;
using System.Collections.Generic;

namespace NodeWar.UI
{
    /// <summary>
    /// Core panel content: breach status + scrollable list of respawning villagers.
    /// Each dead villager gets a horizontal progress bar with a skip button.
    /// </summary>
    public class CorePanelContent : MonoBehaviour
    {
        [Header("References (assign in prefab)")]
        [SerializeField] private TextMeshProUGUI breachStatusLabel;
        [SerializeField] private RectTransform respawnListContent;
        [SerializeField] private GameObject allAliveLabel;
        [SerializeField] private GameObject respawnEntryPrefab;

        private SimulationState simState;
        private NodeWar.Core.ITickProvider tickProvider;
        private InputBuffer inputBuffer;
        private int nodeID;
        private int controlledPID;

        // Tracked entries
        private List<RespawnEntryDisplay> activeEntries = new List<RespawnEntryDisplay>();
        private List<int> trackedVillagerIDs = new List<int>();

        public void Initialize(SimulationState state, NodeWar.Core.ITickProvider provider,
                                InputBuffer buffer, int node, int pid)
        {
            simState = state;
            tickProvider = provider;
            inputBuffer = buffer;
            nodeID = node;
            controlledPID = pid;

            // Determine which player's core this is
            int coreOwner = state.nodes[nodeID].ownerID;

            Color playerColor = (coreOwner == 0)
                ? new Color(0.40f, 0.60f, 1f)
                : new Color(1f, 0.40f, 0.40f);

            breachStatusLabel.color = playerColor;
        }

        private void Update()
        {
            if (simState == null) return;

            int coreOwner = simState.nodes[nodeID].ownerID;
            int breaches = simState.players[coreOwner].breachCount;
            breachStatusLabel.text = "Breaches: " + breaches + " / 3";

            // Gather dead non-consumed villagers for this core's owner
            List<int> deadIDs = new List<int>();
            for (int i = 0; i < simState.villagers.Length; i++)
            {
                VillagerData v = simState.villagers[i];
                if (v.ownerID != coreOwner) continue;
                if (v.state != VillagerState.Dead) continue;
                if (v.isConsumed) continue;
                deadIDs.Add(i);
            }

            // Show/hide "all alive" label
            allAliveLabel.SetActive(deadIDs.Count == 0);

            // Sync entry list with dead villagers
            SyncEntries(deadIDs);

            // Update each entry's progress
            for (int i = 0; i < activeEntries.Count; i++)
            {
                activeEntries[i].Refresh(simState, tickProvider);
            }
        }

        private void SyncEntries(List<int> deadIDs)
        {
            // Remove entries for villagers no longer dead
            for (int i = activeEntries.Count - 1; i >= 0; i--)
            {
                if (!deadIDs.Contains(trackedVillagerIDs[i]))
                {
                    Destroy(activeEntries[i].gameObject);
                    activeEntries.RemoveAt(i);
                    trackedVillagerIDs.RemoveAt(i);
                }
            }

            // Add entries for newly dead villagers
            for (int i = 0; i < deadIDs.Count; i++)
            {
                if (!trackedVillagerIDs.Contains(deadIDs[i]))
                {
                    GameObject entryGO = Instantiate(respawnEntryPrefab, respawnListContent);
                    RespawnEntryDisplay entry = entryGO.GetComponent<RespawnEntryDisplay>();
                    entry.Initialize(simState, inputBuffer, deadIDs[i], controlledPID);
                    activeEntries.Add(entry);
                    trackedVillagerIDs.Add(deadIDs[i]);
                }
            }
        }
    }
}