using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// Full-screen overlay for game over and disconnect states.
    /// Starts hidden. Activated by GameManager when match ends.
    /// Contains title, info text, and a return-to-lobby button.
    ///
    /// It words the result itself. GameManager used to build "PLAYER 0 WINS!"
    /// and hand it over, which put player-facing text in the layer that runs
    /// the match, and read as zero-indexed next to the 1/2 marks used
    /// everywhere else. The result is Victory or Defeat, from the viewer's
    /// side, and the tally is the scoreboard.
    /// </summary>
    public class GameOverPanel : MonoBehaviour
    {
        [Header("References (assign in prefab)")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI infoText;
        [SerializeField] private Button returnButton;

        public System.Action OnReturnToLobby;

        private void Awake()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);

            if (returnButton != null)
                returnButton.onClick.AddListener(HandleReturnClicked);
        }

        /// <summary>
        /// The end of the match as the viewing player saw it.
        /// </summary>
        public void ShowResult(SimulationState state, int viewerPID, int breachThreshold)
        {
            bool won = state.winnerID == viewerPID;

            // Green for a win, plain white for a loss. Not red: red is
            // player 1's colour on every other surface in the match.
            Color titleColor = won ? new Color(0.30f, 0.63f, 0.25f) : Color.white;

            Show(won ? "VICTORY" : "DEFEAT", titleColor, Tally(state, viewerPID, breachThreshold));
        }

        /// <summary>
        /// The opponent left. Can happen mid-match, so the tally still stands.
        /// </summary>
        public void ShowDisconnected(SimulationState state, int viewerPID, int breachThreshold)
        {
            Show("DISCONNECTED", new Color(1f, 0.8f, 0.2f),
                 "Opponent has disconnected." + NewLine + Tally(state, viewerPID, breachThreshold));
        }

        private const string NewLine = "\n";

        /// <summary>Both breach counts and the match length, as time not ticks.</summary>
        private static string Tally(SimulationState state, int viewerPID, int breachThreshold)
        {
            int other = viewerPID == 0 ? 1 : 0;
            int seconds = state.tickCount / 10;

            return "You  " + state.players[viewerPID].breachCount + " / " + breachThreshold + NewLine +
                   "Opponent  " + state.players[other].breachCount + " / " + breachThreshold + NewLine +
                   "Match length " + (seconds / 60) + ":" + (seconds % 60).ToString("00");
        }

        /// <summary>
        /// Show the game over overlay with specified content.
        /// </summary>
        public void Show(string title, Color titleColor, string info)
        {
            if (panelRoot != null)
                panelRoot.SetActive(true);

            if (titleText != null)
            {
                titleText.text = title;
                titleText.color = titleColor;
            }

            if (infoText != null)
                infoText.text = info;
        }

        public void Hide()
        {
            if (panelRoot != null)
                panelRoot.SetActive(false);
        }

        private void HandleReturnClicked()
        {
            OnReturnToLobby?.Invoke();
        }

        private void OnDestroy()
        {
            if (returnButton != null)
                returnButton.onClick.RemoveAllListeners();
        }
    }
}