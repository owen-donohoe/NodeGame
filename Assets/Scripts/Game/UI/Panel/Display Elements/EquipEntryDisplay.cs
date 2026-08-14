using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    public class EquipEntryDisplay : MonoBehaviour
    {
        [Header("References (assign in prefab)")]
        [SerializeField] private TextMeshProUGUI villagerLabel;
        [SerializeField] private Button equipButton;
        [SerializeField] private Image buttonImage;

        private SimulationState simState;
        private InputBuffer inputBuffer;
        private int villagerID;
        private int controlledPID;
        private bool initialized = false;

        private static readonly Color affordColor = new Color(0.20f, 0.25f, 0.35f, 1f);
        private static readonly Color cantAffordColor = new Color(0.18f, 0.18f, 0.18f, 1f);

        /// <summary>
        /// Returns false if required references are missing.
        /// </summary>
        public bool Initialize(SimulationState state, InputBuffer buffer, int vid, int pid)
        {
            if (equipButton == null || villagerLabel == null || buttonImage == null)
            {
                Debug.LogError("[EquipEntry] Missing serialized references on prefab!");
                return false;
            }

            simState = state;
            inputBuffer = buffer;
            villagerID = vid;
            controlledPID = pid;

            villagerLabel.text = "Villager " + vid;
            equipButton.onClick.AddListener(OnEquipClicked);
            initialized = true;
            return true;
        }

        public void RefreshAffordability(bool canAfford)
        {
            if (!initialized) return;
            equipButton.interactable = canAfford;
            buttonImage.color = canAfford ? affordColor : cantAffordColor;
        }

        private void OnEquipClicked()
        {
            if (simState == null || inputBuffer == null) return;

            GameCommand cmd = new GameCommand
            {
                type = CommandType.Equip,
                playerID = controlledPID,
                villagerID = villagerID,
                issuedOnTick = simState.tickCount
            };
            inputBuffer.EnqueueCommand(cmd);
        }

        private void OnDestroy()
        {
            if (equipButton != null)
                equipButton.onClick.RemoveAllListeners();
        }
    }
}