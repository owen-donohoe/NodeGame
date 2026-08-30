using UnityEngine;
using TMPro;
using DG.Tweening;
using System.Collections;

namespace NodeWar.UI
{
    /// <summary>
    /// Screen-space countdown overlay shown between draft completion and match start.
    /// Spawned by MatchTransitionController. Destroys itself after completion.
    /// Fires OnCountdownComplete when the sequence finishes — controller waits on this.
    /// </summary>
    public class CountdownUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Timing")]
        [Tooltip("How long each number (3, 2, 1) is displayed.")]
        [SerializeField] private float stepDisplayDuration = 0.7f;
        [Tooltip("How long GO is displayed before fading out.")]
        [SerializeField] private float goDisplayDuration = 0.8f;
        [Tooltip("Duration of the canvas fade after GO.")]
        [SerializeField] private float fadeOutDuration = 0.3f;

        [Header("Animation")]
        [Tooltip("Starting scale of each step before punching down to 1.")]
        [SerializeField] private float stepStartScale = 1.5f;
        [Tooltip("Duration of the scale punch-in per step.")]
        [SerializeField] private float stepPunchDuration = 0.21f;
        [SerializeField] private Ease stepPunchEase = Ease.OutBack;

        public System.Action OnCountdownComplete;

        public void StartCountdown()
        {
            StartCoroutine(CountdownRoutine());
        }

        private IEnumerator CountdownRoutine()
        {
            string[] steps = { "3", "2", "1", "GO" };

            for (int i = 0; i < steps.Length; i++)
            {
                countdownText.text = steps[i];
                countdownText.transform.localScale = Vector3.one * stepStartScale;

                countdownText.transform
                    .DOScale(1f, stepPunchDuration)
                    .SetEase(stepPunchEase);

                float wait = (i < steps.Length - 1) ? stepDisplayDuration : goDisplayDuration;
                yield return new WaitForSeconds(wait);
            }

            if (canvasGroup != null)
            {
                canvasGroup.DOFade(0f, fadeOutDuration).OnComplete(() =>
                {
                    OnCountdownComplete?.Invoke();
                    Destroy(gameObject);
                });
            }
            else
            {
                OnCountdownComplete?.Invoke();
                Destroy(gameObject);
            }
        }
    }
}