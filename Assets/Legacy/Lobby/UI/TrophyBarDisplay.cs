using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NodeWar.Lobby
{
    public class TrophyBarDisplay : MonoBehaviour
    {
        [Header("Scroll")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;

        [Header("Bar Images (inside content, stretch fill, Filled vertical from bottom)")]
        [SerializeField] private Image backgroundBar;
        [SerializeField] private Image maxFillBar;
        [SerializeField] private Image currentFillBar;

        [Header("Current Value Marker (anchored bottom-left of content)")]
        [SerializeField] private RectTransform currentMarker;
        [SerializeField] private TextMeshProUGUI currentValueText;

        [Header("Tick Prefab (anchored bottom-right)")]
        [SerializeField] private GameObject tickPrefab;
        [SerializeField] private RectTransform tickContainer;

        [Header("Settings")]
        [SerializeField] private float pixelsPerTrophy = 0.5f;
        [SerializeField] private int tickInterval = 100;
        [SerializeField] private int maxDisplayRange = 2000;
        [SerializeField] private float bottomPadding = 20f;
        [SerializeField] private Color currentFillColor = new Color(0.96f, 0.78f, 0.26f, 1f);
        [SerializeField] private Color maxFillColor = new Color(0.23f, 0.21f, 0.13f, 1f);

        private int displayedMax;

        public void Setup(int currentTrophies)
        {
            displayedMax = maxDisplayRange; // always use full range

            float contentHeight = (displayedMax * pixelsPerTrophy) + bottomPadding;
            content.sizeDelta = new Vector2(content.sizeDelta.x, contentHeight);

            if (currentFillBar != null) currentFillBar.color = currentFillColor;
            if (maxFillBar != null) maxFillBar.color = maxFillColor;

            GenerateTicks(contentHeight);
            Refresh(currentTrophies);
        }

        public void Refresh(int currentTrophies)
        {
            if (displayedMax <= 0) return;

            float contentHeight = content.sizeDelta.y;
            float usableHeight = contentHeight - bottomPadding;

            float currentRatio = (float)currentTrophies / displayedMax;

            if (currentFillBar != null)
                currentFillBar.fillAmount = Mathf.Clamp01((bottomPadding + currentRatio * usableHeight) / contentHeight);

            if (maxFillBar != null)
            {
                if (currentTrophies > 0)
                {
                    int nextMilestone = ((currentTrophies / 100) + 1) * 100;
                    float maxRatio = (float)nextMilestone / displayedMax;
                    maxFillBar.fillAmount = Mathf.Clamp01((bottomPadding + maxRatio * usableHeight) / contentHeight);
                }
                else
                {
                    maxFillBar.fillAmount = Mathf.Clamp01(bottomPadding / contentHeight);
                }
            }

            if (currentMarker != null)
            {
                float markerY = bottomPadding + currentRatio * usableHeight;
                currentMarker.anchoredPosition = new Vector2(
                    currentMarker.anchoredPosition.x, markerY);
            }

            if (currentValueText != null)
                currentValueText.text = currentTrophies.ToString();

            ScrollToValue(currentTrophies, contentHeight, usableHeight);
        }

        private void ScrollToValue(int trophies, float contentHeight, float usableHeight)
        {
            if (scrollRect == null) return;

            float viewportHeight = scrollRect.viewport.rect.height;

            if (contentHeight <= viewportHeight)
            {
                scrollRect.verticalNormalizedPosition = 0f;
                return;
            }

            float ratio = (float)trophies / displayedMax;
            float targetY = bottomPadding + ratio * usableHeight;
            float scrollable = contentHeight - viewportHeight;
            float targetOffset = targetY - (viewportHeight * 0.5f);
            targetOffset = Mathf.Clamp(targetOffset, 0f, scrollable);
            float normalized = targetOffset / scrollable;

            scrollRect.verticalNormalizedPosition = normalized;
        }

        private void GenerateTicks(float contentHeight)
        {
            if (tickContainer != null)
            {
                for (int i = tickContainer.childCount - 1; i >= 0; i--)
                    Destroy(tickContainer.GetChild(i).gameObject);
            }

            if (tickPrefab == null || tickContainer == null) return;

            int tickCount = displayedMax / tickInterval;

            for (int i = 0; i <= tickCount; i++)
            {
                int trophyValue = i * tickInterval;
                float yPos = bottomPadding + ((float)trophyValue / displayedMax) * (contentHeight - bottomPadding);

                GameObject tick = Instantiate(tickPrefab, tickContainer);
                RectTransform tickRect = tick.GetComponent<RectTransform>();
                tickRect.anchoredPosition = new Vector2(0f, yPos);

                TextMeshProUGUI tickLabel = tick.GetComponentInChildren<TextMeshProUGUI>();
                if (tickLabel != null)
                    tickLabel.text = trophyValue.ToString();
            }
        }

        private void ScrollToValue(int trophies, float contentHeight)
        {
            if (scrollRect == null) return;

            float viewportHeight = scrollRect.viewport.rect.height;

            if (contentHeight <= viewportHeight)
            {
                scrollRect.verticalNormalizedPosition = 0f;
                return;
            }

            float ratio = (float)trophies / displayedMax;
            float scrollable = contentHeight - viewportHeight;
            float targetOffset = (ratio * contentHeight) - (viewportHeight * 0.5f);
            targetOffset = Mathf.Clamp(targetOffset, 0f, scrollable);
            float normalized = targetOffset / scrollable;

            scrollRect.verticalNormalizedPosition = normalized;
        }
    }
}