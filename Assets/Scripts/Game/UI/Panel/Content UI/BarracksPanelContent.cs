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
        private SuitType districtSuit = SuitType.None;

        private List<EquipEntryDisplay> activeEntries = new List<EquipEntryDisplay>();
        private List<int> trackedVillagerIDs = new List<int>();

        public void Initialize(SimulationState state, InputBuffer buffer, GameBalanceData balanceData,
            int node, int pid, bool owned)
        {
            simState = state;
            inputBuffer = buffer;
            balance = balanceData;
            nodeID = node;
            controlledPID = pid;
            isOwned = owned;

            districtSuit = ResolveDistrictSuit(state.nodes[node].districtType);

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

        /// <summary>
        /// The suit this district actually equips.
        ///
        /// One prefab serves Camp, Barracks, Arsenal and Sanctuary, and those
        /// accept different suits -- Sanctuary takes only Medic. Picking the
        /// first suit CanEquipSuitAtNode allows keeps the panel honest without
        /// duplicating the eligibility table here.
        ///
        /// A single suit per district is a placeholder for the suit picker the
        /// entry still lacks; Barracks accepts four and this offers the first.
        /// </summary>
        private SuitType ResolveDistrictSuit(DistrictType district)
        {
            // GameBalanceData is a struct, so there is no null to guard against.
            // CanEquipSuitAtNode switches on the district and answers correctly
            // even for a default-constructed value.
            foreach (SuitType candidate in System.Enum.GetValues(typeof(SuitType)))
            {
                if (candidate == SuitType.None) continue;
                if (!GameBalanceData.IsCombatSuit(candidate)) continue;
                if (balance.CanEquipSuitAtNode(candidate, district)) return candidate;
            }

            return SuitType.None;
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

                if (!entry.Initialize(simState, inputBuffer, idleIDs[i], controlledPID,
                                      districtSuit, balance.GetSuitStats(districtSuit)))
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