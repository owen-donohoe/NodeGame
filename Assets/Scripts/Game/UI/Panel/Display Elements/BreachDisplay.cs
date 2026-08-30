using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace NodeWar.UI
{
    /// <summary>
    /// Horizontal health bar representing one player's breach status.
    /// Full bar = 0 breaches (safe). Empty bar = breach threshold reached (lost).
    /// Animates fill depletion and punches scale on breach increment.
    /// </summary>
    public class BreachDisplay : MonoBehaviour
    {
        [Header("References (assign in prefab)")]
        [SerializeField] private Image fillImage;
        [SerializeField] private Image backgroundImage;
        //[SerializeField] private TextMeshProUGUI labelText;
        [SerializeField] private RectTransform barRect;

        [Header("Animation")]
        [SerializeField] private float fillAnimDuration = 0.35f;
        [SerializeField] private float punchDuration = 0.5f;
        [SerializeField] private float punchStrength = 0.18f;

        private int lastBreachCount = 0;
        private int breachThreshold = 3;
        private Color baseColor;
        private Tween fillTween;
        private Tween punchTween;

        public void Initialize(int playerID, Color color, int breachThreshold)
        {
            baseColor = color;
            fillImage.color = color;
            fillImage.fillAmount = 1f;
            //labelText.text = "P" + playerID;
            lastBreachCount = 0;
            this.breachThreshold = breachThreshold;
        }

        public void UpdateBreachCount(int breachCount)
        {
            if (breachCount == lastBreachCount) return;
            lastBreachCount = breachCount;

            float targetFill = breachThreshold > 0
                ? Mathf.Clamp01((breachThreshold - breachCount) / (float)breachThreshold)
                : 0f;

            // Animate fill depletion
            fillTween?.Kill();
            fillTween = fillImage.DOFillAmount(targetFill, fillAnimDuration)
                .SetEase(Ease.InOutQuad);

            // Punch scale for impact
            punchTween?.Kill();
            barRect.localScale = Vector3.one;
            punchTween = barRect.DOPunchScale(Vector3.one * punchStrength, punchDuration, 8, 0.5f);

            // Brief color flash to white and back
            fillImage.DOColor(Color.white, 0.06f)
                .OnComplete(() => fillImage.DOColor(baseColor, 0.3f));
        }

        private void OnDestroy()
        {
            fillTween?.Kill();
            punchTween?.Kill();
        }
    }
}