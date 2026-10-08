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
        private GameBalanceData balance;
        private int nodeID;
        private int controlledPID;
        private bool isOwned;
        private static readonly SuitType[] CombatSuits = { SuitType.Warrior, SuitType.Guardian, SuitType.Scout, SuitType.Berserker, SuitType.Medic };

        private List<EquipEntryDisplay> activeEntries = new List<EquipEntryDisplay>();
        private List<int> trackedVillagerIDs = new List<int>();
        private List<SuitType> trackedSuits = new List<SuitType>();

        public void Initialize(SimulationState state, InputBuffer buffer, GameBalanceData balanceData,
            int node, int pid, bool owned)
        {
            simState = state;
            inputBuffer = buffer;
            balance = balanceData;
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
            if (simState.nodes[nodeID].districtType != DistrictType.Barracks || simState.nodes[nodeID].ownerID != controlledPID)
            { SyncEntries(new List<int>()); return; }

            List<int> idleIDs = new List<int>();
            for (int i = 0; i < simState.villagers.Length; i++)
            {
                VillagerData v = simState.villagers[i];
                if (v.currentNodeID != nodeID) continue;
                if (v.ownerID != controlledPID) continue;
                if (v.state != VillagerState.Idle) continue;
                if (GameBalanceData.IsCombatSuit(v.suit)) continue;
                if (v.isConsumed) continue;
                idleIDs.Add(i);
            }

            noVillagersLabel.SetActive(idleIDs.Count == 0);
            SyncEntries(idleIDs);

            // Each entry answers for its own suit. The single shared bool this
            // replaced was built from a hardcoded food>=2 && materials>=1,
            // which matched no suit in particular.
            for (int i = 0; i < activeEntries.Count; i++)
            {
                if (activeEntries[i] != null)
                    activeEntries[i].Refresh();
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
                    trackedSuits.RemoveAt(i);
                }
            }

            // Add new entries
            for (int i = 0; i < idleIDs.Count; i++)
            {
                for (int suitIndex = 0; suitIndex < CombatSuits.Length; suitIndex++)
                {
                    SuitType districtSuit = CombatSuits[suitIndex];
                    bool exists = false;
                    for (int entryIndex = 0; entryIndex < trackedVillagerIDs.Count; entryIndex++)
                        if (trackedVillagerIDs[entryIndex] == idleIDs[i] && trackedSuits[entryIndex] == districtSuit) exists = true;
                    if (exists) continue;

                    GameObject entryGO = Instantiate(equipEntryPrefab, equipListContent);
                    EquipEntryDisplay entry = entryGO.GetComponent<EquipEntryDisplay>();

                    if (entry == null)
                    {
                        Debug.LogError("[Barracks] EquipEntry prefab missing EquipEntryDisplay component!");
                        Destroy(entryGO);
                        // Still track it so we don't retry every frame
                        trackedVillagerIDs.Add(idleIDs[i]); trackedSuits.Add(districtSuit);
                        activeEntries.Add(null);
                        continue;
                    }

                    if (!entry.Initialize(simState, inputBuffer, idleIDs[i], controlledPID,
                                          districtSuit, balance.GetSuitStats(districtSuit,
                                              simState.players[controlledPID].SuitEra(districtSuit))))
                    {
                        Debug.LogError("[Barracks] EquipEntry Initialize failed for villager " + idleIDs[i]);
                        Destroy(entryGO);
                        trackedVillagerIDs.Add(idleIDs[i]); trackedSuits.Add(districtSuit);
                        activeEntries.Add(null);
                        continue;
                    }

                    activeEntries.Add(entry);
                    trackedVillagerIDs.Add(idleIDs[i]); trackedSuits.Add(districtSuit);
                }
            }
        }
    }
}
