using UnityEngine;
using TMPro;
using DG.Tweening;

namespace NodeWar.UI
{
    /// <summary>
    /// Full-screen countdown overlay. Shows 3, 2, 1, GO then destroys itself.
    /// </summary>
    public class CountdownUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI countdownText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Timing")]
        [SerializeField] private float stepDuration = 0.7f;
        [SerializeField] private float goDuration = 0.8f;

        public System.Action OnCountdownComplete;

        public void StartCountdown()
        {
            StartCoroutine(CountdownRoutine());
        }

        private System.Collections.IEnumerator CountdownRoutine()
        {
            string[] steps = { "3", "2", "1", "GO" };

            for (int i = 0; i < steps.Length; i++)
            {
                countdownText.text = steps[i];
                countdownText.transform.localScale = Vector3.one * 1.5f;

                countdownText.transform.DOScale(1f, stepDuration * 0.3f)
                    .SetEase(Ease.OutBack);

                float wait = (i < steps.Length - 1) ? stepDuration : goDuration;
                yield return new WaitForSeconds(wait);
            }

            // Fade out
            if (canvasGroup != null)
            {
                canvasGroup.DOFade(0f, 0.3f).OnComplete(() =>
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