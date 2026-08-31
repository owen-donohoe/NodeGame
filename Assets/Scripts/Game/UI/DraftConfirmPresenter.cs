using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace NodeWar.UI
{
    /// <summary>
    /// Self-contained confirm button for draft placement.
    /// Handles its own show/hide animation. Fires OnConfirmed when clicked.
    /// Lives on the confirm button GameObject within the DraftUI prefab hierarchy.
    ///
    /// Can be swapped for a different prefab/child as long as this component
    /// is present and wired to DraftPlacementController.
    /// </summary>
    public class DraftConfirmPresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button button;
        [SerializeField] private RectTransform rectTransform;

        [Header("Animation")]
        [SerializeField] private float springInDuration = 0.35f;
        [SerializeField] private Ease springInEase = Ease.OutBack;
        [SerializeField][Range(1f, 5f)] private float springOvershoot = 2.5f;

        [Header("Positioning")]
        [Tooltip("Pixels above the world-to-screen target position.")]
        [SerializeField] private float screenOffsetY = 70f;

        public System.Action OnConfirmed;

        private Tween activeTween;

        private void Awake()
        {
            if (button != null)
                button.onClick.AddListener(HandleClick);

            // Deliberately no SetActive(false) here. This object is inactive in the prefab, so
            // Unity defers Awake until the first SetActive(true) -- which happens inside Show().
            // Self-deactivating here ran during that Show() and silently undid it, leaving the
            // button invisible for the first placement of every match. The initial hidden state
            // is owned by DraftPlacementController.Initialize(), which calls Hide() during setup.
        }

        /// <summary>
        /// Animates in at screen position above the given world point.
        /// </summary>
        public void Show(Vector3 screenPosition)
        {
            // Position while inactive so layout system sees the correct position on first activation.
            // Never call Canvas.ForceUpdateCanvases() here � it triggers synchronous layout callbacks
            // that can call Hide() before the method returns.
            if (rectTransform != null)
                rectTransform.position = screenPosition + new Vector3(0f, screenOffsetY, 0f);

            gameObject.SetActive(true);
            transform.localScale = Vector3.zero;
            activeTween?.Kill();
            activeTween = transform.DOScale(Vector3.one, springInDuration)
                .SetEase(springInEase, springOvershoot);
        }

        public void Hide()
        {
            activeTween?.Kill();
            activeTween = null;
            transform.localScale = Vector3.zero;
            gameObject.SetActive(false);
        }

        private void HandleClick()
        {
            OnConfirmed?.Invoke();
        }

        private void OnDestroy()
        {
            activeTween?.Kill();
            if (button != null)
                button.onClick.RemoveAllListeners();
        }
    }
}
