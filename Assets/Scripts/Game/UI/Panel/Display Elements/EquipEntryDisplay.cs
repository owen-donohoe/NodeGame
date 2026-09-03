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
        private SuitType suit;
        private SuitStats suitStats;
        private bool initialized = false;

        private static readonly Color affordColor = new Color(0.20f, 0.25f, 0.35f, 1f);
        private static readonly Color cantAffordColor = new Color(0.18f, 0.18f, 0.18f, 1f);

        /// <summary>
        /// Returns false if required references are missing.
        ///
        /// The suit is passed in rather than assumed. It used to be hardcoded
        /// to Warrior, which a Sanctuary never accepts -- CanEquipSuitAtNode
        /// allows only Medic there -- so that panel offered a button the
        /// simulation silently threw away.
        /// </summary>
        public bool Initialize(SimulationState state, InputBuffer buffer, int vid, int pid,
                               SuitType equipSuit, SuitStats stats)
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
            suit = equipSuit;
            suitStats = stats;

            villagerLabel.text = "Villager " + vid + "  -  " + suit +
                                 "  (" + stats.foodCost + "f " + stats.materialCost + "m)";

            equipButton.onClick.AddListener(OnEquipClicked);
            initialized = true;
            return true;
        }

        /// <summary>
        /// Recomputes whether this entry's equip would actually be accepted.
        ///
        /// Each entry answers for its own suit. The previous version was handed
        /// a single shared bool built from a hardcoded "food >= 2 && materials
        /// >= 1", which matched no suit in particular and could present a button
        /// as affordable that CommandProcessor then rejected on cost.
        ///
        /// Mirrors the gates in ProcessEquipCommand that depend on player
        /// resources and draft, so the button reflects the same answer the
        /// simulation will give.
        /// </summary>
        public void Refresh()
        {
            if (!initialized || simState == null) return;

            bool canAfford =
                simState.players[controlledPID].food >= suitStats.foodCost &&
                simState.players[controlledPID].materials >= suitStats.materialCost;

            bool enabled = canAfford && HasSuitDrafted();

            equipButton.interactable = enabled;
            buttonImage.color = enabled ? affordColor : cantAffordColor;
        }

        /// <summary>
        /// Replicates CommandProcessor.PlayerHasSuitDrafted, which is private.
        /// It reads only public state, so this is a View-side read rather than
        /// a duplicated rule -- but it will drift if that gate ever changes.
        /// </summary>
        private bool HasSuitDrafted()
        {
            int[] drafted = simState.players[controlledPID].draftedSuits;
            if (drafted == null) return false;

            for (int i = 0; i < drafted.Length; i++)
            {
                if (drafted[i] == (int)suit) return true;
            }
            return false;
        }

        private void OnEquipClicked()
        {
            if (simState == null || inputBuffer == null) return;

            GameCommand cmd = new GameCommand
            {
                type = CommandType.Equip,
                playerID = controlledPID,
                villagerID = villagerID,
                value = (int)suit,
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