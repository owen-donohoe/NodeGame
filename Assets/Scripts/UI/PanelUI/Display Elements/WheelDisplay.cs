using UnityEngine;
using TMPro;
using DG.Tweening;

namespace NodeWar.UI
{
    /// <summary>
    /// Odometer-style scroll animation for a single integer value.
    /// 3 TMP texts stacked inside a clipping mask. Strip slides up/down on value change.
    /// Increasing values scroll upward (new value enters from below).
    /// Decreasing values scroll downward (new value enters from above).
    /// </summary>
    public class WheelDisplay : MonoBehaviour
    {
        [Header("References (assign in prefab)")]
        [SerializeField] private RectTransform strip;
        [SerializeField] private TextMeshProUGUI topText;
        [SerializeField] private TextMeshProUGUI middleText;
        [SerializeField] private TextMeshProUGUI bottomText;

        [Header("Settings")]
        [SerializeField] private float cellHeight = 30f;
        [SerializeField] private float animDuration = 0.5f;
        [SerializeField] private Ease animEase = Ease.OutCubic;

        private int currentDisplayValue = 0;
        private int targetValue = 0;
        private bool isAnimating = false;
        private Tween activeTween;

        /// <summary>
        /// Sets the display immediately with no animation.
        /// Call on first frame or when switching controlled player.
        /// </summary>
        public void Initialize(int startValue)
        {
            activeTween?.Kill();
            currentDisplayValue = startValue;
            targetValue = startValue;
            middleText.text = startValue.ToString();
            topText.text = "";
            bottomText.text = "";
            strip.anchoredPosition = Vector2.zero;
            isAnimating = false;
        }

        /// <summary>
        /// Animate to a new value. If already animating, snaps current and starts fresh.
        /// </summary>
        public void SetValue(int newValue)
        {
            if (newValue == targetValue) return;
            targetValue = newValue;

            if (isAnimating)
            {
                activeTween?.Kill();
                strip.anchoredPosition = Vector2.zero;
                middleText.text = currentDisplayValue.ToString();
                topText.text = "";
                bottomText.text = "";
                isAnimating = false;
            }

            AnimateTo(newValue);
        }

        private void AnimateTo(int newValue)
        {
            isAnimating = true;

            if (newValue > currentDisplayValue)
            {
                // Value increasing — strip moves UP, new value enters from below
                bottomText.text = newValue.ToString();
                topText.text = "";
                middleText.text = currentDisplayValue.ToString();

                activeTween = strip.DOAnchorPosY(cellHeight, animDuration)
                    .SetEase(animEase)
                    .OnComplete(() => FinishAnimation(newValue));
            }
            else
            {
                // Value decreasing — strip moves DOWN, new value enters from above
                topText.text = newValue.ToString();
                bottomText.text = "";
                middleText.text = currentDisplayValue.ToString();

                activeTween = strip.DOAnchorPosY(-cellHeight, animDuration)
                    .SetEase(animEase)
                    .OnComplete(() => FinishAnimation(newValue));
            }
        }

        private void FinishAnimation(int finalValue)
        {
            currentDisplayValue = finalValue;
            middleText.text = finalValue.ToString();
            topText.text = "";
            bottomText.text = "";
            strip.anchoredPosition = Vector2.zero;
            isAnimating = false;
        }

        private void OnDestroy()
        {
            activeTween?.Kill();
        }
    }
}