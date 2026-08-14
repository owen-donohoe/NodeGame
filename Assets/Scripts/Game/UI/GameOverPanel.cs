using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NodeWar.UI
{
    /// <summary>
    /// Full-screen overlay for game over and disconnect states.
    /// Starts hidden. Activated by GameManager when match ends.
    /// Contains title, info text, and a return-to-lobby button.
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