using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NodeWar.Simulation;
using System.Collections.Generic;

namespace NodeWar.UI
{
    public class BarracksPanelContent : MonoBehaviour
    {
        [Header("References (assign in prefab)")]
        [SerializeField] private RectTransform equipListContent;
        [SerializeField] private TextMeshProUGUI costReminder;
        [SerializeField] private GameObject noVillagersLabel;
        [SerializeField] private GameObject equipEntryPrefab;

        private SimulationState simState;
        private InputBuffer inputBuffer;
        private int nodeID;
        private int controlledPID;
        private bool isOwned;

        private List<EquipEntryDisplay> activeEntries = new List<EquipEntryDisplay>();
        private List<int> trackedVillagerIDs = new List<int>();

        public void Initialize(SimulationState state, InputBuffer buffer,
            int node, int pid, bool owned)
        {
            simState = state;
            inputBuffer = buffer;
            nodeID = node;
            controlledPID = pid;
            isOwned = owned;

            if (!owned)
            {
                noVillagersLabel.SetActive(true);
                if (noVillagersLabel.GetComponent<TextMeshProUGUI>() != null)
                    noVillagersLabel.GetComponent<TextMeshProUGUI>().text = "Enemy barracks";
                if (costReminder != null)
                    costReminder.gameObject.SetActive(false);
                return;
            }
        }

        private void Update()
        {
            if (simState == null || !isOwned) return;

            List<int> idleIDs = new List<int>();
            for (int i = 0; i < simState.villagers.Length; i++)
            {
                VillagerData v = simState.villagers[i];
                if (v.currentNodeID != nodeID) continue;
                if (v.ownerID != controlledPID) continue;
                if (v.state != VillagerState.Idle) continue;
                if (GameBalance.IsCombatSuit(v.suit)) continue;
                if (v.isConsumed) continue;
                idleIDs.Add(i);
            }

            noVillagersLabel.SetActive(idleIDs.Count == 0);
            SyncEntries(idleIDs);

            bool canAfford = simState.players[controlledPID].food >= 2 &&
                             simState.players[controlledPID].materials >= 1;

            for (int i = 0; i < activeEntries.Count; i++)
            {
                if (activeEntries[i] != null)
                    activeEntries[i].RefreshAffordability(canAfford);
            }
        }

        private void SyncEntries(List<int> idleIDs)
        {
            // Remove stale entries
            for (int i = activeEntries.Count - 1; i >= 0; i--)
            {
                if (!idleIDs.Contains(trackedVillagerIDs[i]))
                {
                    if (activeEntries[i] != null)
                        Destroy(activeEntries[i].gameObject);
                    activeEntries.RemoveAt(i);
                    trackedVillagerIDs.RemoveAt(i);
                }
            }

            // Add new entries
            for (int i = 0; i < idleIDs.Count; i++)
            {
                if (trackedVillagerIDs.Contains(idleIDs[i])) continue;

                GameObject entryGO = Instantiate(equipEntryPrefab, equipListContent);
                EquipEntryDisplay entry = entryGO.GetComponent<EquipEntryDisplay>();

                if (entry == null)
                {
                    Debug.LogError("[Barracks] EquipEntry prefab missing EquipEntryDisplay component!");
                    Destroy(entryGO);
                    // Still track it so we don't retry every frame
                    trackedVillagerIDs.Add(idleIDs[i]);
                    activeEntries.Add(null);
                    continue;
                }

                if (!entry.Initialize(simState, inputBuffer, idleIDs[i], controlledPID))
                {
                    Debug.LogError("[Barracks] EquipEntry Initialize failed for villager " + idleIDs[i]);
                    Destroy(entryGO);
                    trackedVillagerIDs.Add(idleIDs[i]);
                    activeEntries.Add(null);
                    continue;
                }

                activeEntries.Add(entry);
                trackedVillagerIDs.Add(idleIDs[i]);
            }
        }
    }
}