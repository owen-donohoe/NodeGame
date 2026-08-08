using UnityEngine;
using TMPro;
using NodeWar.Simulation;
using NodeWar.Debugging;

namespace NodeWar.UI
{
    /// <summary>
    /// Orchestrates the always-visible HUD elements.
    /// Holds serialized references to prefab children — does zero layout/construction.
    /// Updates resource wheels, breach bars, and villager count every frame.
    /// Detects Tab-switch and snaps resource display to new player.
    /// </summary>
    public class HUDManager : MonoBehaviour
    {
        [Header("Resource Wheels (assign WheelDisplay on each box)")]
        [SerializeField] private WheelDisplay foodWheel;
        [SerializeField] private WheelDisplay materialsWheel;
        [SerializeField] private WheelDisplay metalWheel;

        [Header("Villager Count")]
        [SerializeField] private TextMeshProUGUI villagerCountText;

        [Header("Breach Displays")]
        [SerializeField] private BreachDisplay p0BreachDisplay;
        [SerializeField] private BreachDisplay p1BreachDisplay;

        [Header("Player Colors")]
        [SerializeField] private Color p0Color = new Color(0.40f, 0.60f, 1.00f);
        [SerializeField] private Color p1Color = new Color(1.00f, 0.40f, 0.40f);

        // Dependencies — set via Initialize
        private SimulationState simState;
        private DebugPlayerSwitch debugPlayerSwitch;
        private bool initialized = false;

        // Tracking for change detection
        private int lastControlledPID = -1;
        private int lastFood = -1;
        private int lastMaterials = -1;
        private int lastMetal = -1;

        public void Initialize(SimulationState state, DebugPlayerSwitch debugSwitch)
        {
            simState = state;
            debugPlayerSwitch = debugSwitch;

            int pid = debugSwitch != null ? debugSwitch.GetCurrentPlayerID() : 0;
            lastControlledPID = pid;

            PlayerData p = state.players[pid];
            foodWheel.Initialize(p.food);
            materialsWheel.Initialize(p.materials);
            metalWheel.Initialize(p.metal);
            lastFood = p.food;
            lastMaterials = p.materials;
            lastMetal = p.metal;

            p0BreachDisplay.Initialize(0, p0Color);
            p1BreachDisplay.Initialize(1, p1Color);

            initialized = true;
        }

        private void Update()
        {
            if (!initialized || simState == null) return;

            RefreshResources();
            RefreshBreaches();
            RefreshVillagerCount();
        }

        private void RefreshResources()
        {
            int pid = debugPlayerSwitch != null ? debugPlayerSwitch.GetCurrentPlayerID() : 0;

            // Tab was pressed — snap all wheels to new player's values (no animation)
            if (pid != lastControlledPID)
            {
                lastControlledPID = pid;
                PlayerData p = simState.players[pid];
                foodWheel.Initialize(p.food);
                materialsWheel.Initialize(p.materials);
                metalWheel.Initialize(p.metal);
                lastFood = p.food;
                lastMaterials = p.materials;
                lastMetal = p.metal;
                return;
            }

            PlayerData player = simState.players[pid];

            if (player.food != lastFood)
            {
                foodWheel.SetValue(player.food);
                lastFood = player.food;
            }
            if (player.materials != lastMaterials)
            {
                materialsWheel.SetValue(player.materials);
                lastMaterials = player.materials;
            }
            if (player.metal != lastMetal)
            {
                metalWheel.SetValue(player.metal);
                lastMetal = player.metal;
            }
        }

        private void RefreshBreaches()
        {
            p0BreachDisplay.UpdateBreachCount(simState.players[0].breachCount);
            p1BreachDisplay.UpdateBreachCount(simState.players[1].breachCount);
        }

        private void RefreshVillagerCount()
        {
            int p0c = 0, p1c = 0;
            for (int i = 0; i < simState.villagers.Length; i++)
            {
                if (simState.villagers[i].isConsumed) continue;
                if (simState.villagers[i].ownerID == 0) p0c++;
                else p1c++;
            }
            villagerCountText.text = "P0: " + p0c + "/25    P1: " + p1c + "/25";
        }
    }
}