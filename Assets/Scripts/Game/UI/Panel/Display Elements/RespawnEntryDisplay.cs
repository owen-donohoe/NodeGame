using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// Single respawn entry: horizontal bar showing timer progress + skip button.
    /// </summary>
    public class RespawnEntryDisplay : MonoBehaviour
    {
        [Header("References (assign in prefab)")]
        [SerializeField] private Image barFill;
        [SerializeField] private TextMeshProUGUI villagerIDLabel;
        [SerializeField] private Button skipButton;
        [SerializeField] private Image skipButtonImage;

        private SimulationState simState;
        private InputBuffer inputBuffer;
        private int villagerID;
        private int controlledPID;

        private static readonly Color activeColor = new Color(0.25f, 0.40f, 0.25f, 1f);
        private static readonly Color disabledColor = new Color(0.20f, 0.20f, 0.20f, 1f);

        private const int RESPAWN_TICKS = 50;

        public void Initialize(SimulationState state, InputBuffer buffer, int vid, int pid)
        {
            simState = state;
            inputBuffer = buffer;
            villagerID = vid;
            controlledPID = pid;

            villagerIDLabel.text = "V" + vid;
            barFill.fillAmount = 0f;

            Color playerColor = (state.villagers[vid].ownerID == 0)
                ? new Color(0.40f, 0.60f, 1f)
                : new Color(1f, 0.40f, 0.40f);
            barFill.color = playerColor;

            skipButton.onClick.AddListener(OnSkipClicked);
        }

        public void Refresh(SimulationState state, NodeWar.Core.ITickProvider provider)
        {
            if (villagerID >= state.villagers.Length) return;
            VillagerData v = state.villagers[villagerID];

            if (v.state != VillagerState.Dead || v.isConsumed)
            {
                // This entry should be removed by parent — hide for now
                gameObject.SetActive(false);
                return;
            }

            float rawFill = 1f - ((float)v.respawnTicksRemaining / RESPAWN_TICKS);
            float subTick = (1f / RESPAWN_TICKS) * provider.TickAlpha;
            barFill.fillAmount = Mathf.Clamp01(rawFill + subTick);

            // Skip button: grey out if no food
            bool canAfford = state.players[controlledPID].food >= 1;
            skipButton.interactable = canAfford;
            skipButtonImage.color = canAfford ? activeColor : disabledColor;
        }

        private void OnSkipClicked()
        {
            if (simState.players[controlledPID].food < 1) return;

            GameCommand cmd = new GameCommand
            {
                type = CommandType.Respawn,
                playerID = controlledPID,
                villagerID = villagerID,
                issuedOnTick = simState.tickCount
            };
            inputBuffer.EnqueueCommand(cmd);
        }

        private void OnDestroy()
        {
            skipButton.onClick.RemoveAllListeners();
        }
    }
}