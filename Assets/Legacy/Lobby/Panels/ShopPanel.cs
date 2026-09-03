using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NodeWar.Lobby
{
    public class ShopPanel : LobbyPanel
    {
        [Header("Parallax")]
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform shopkeeperRect;
        [SerializeField] private float parallaxFactor = 0.4f;

        [Header("Daily Offers (inside parallax layer)")]
        [SerializeField] private Button dailyOffer0;
        [SerializeField] private Button dailyOffer1;
        [SerializeField] private Button dailyOffer2;
        [SerializeField] private TextMeshProUGUI dailyOffer0Text;
        [SerializeField] private TextMeshProUGUI dailyOffer1Text;
        [SerializeField] private TextMeshProUGUI dailyOffer2Text;

        [Header("Navigation")]
        [SerializeField] private Button backButton;

        private float shopkeeperStartY;

        private void Awake()
        {
            if (backButton != null)
                backButton.onClick.AddListener(OnBackClicked);

            if (dailyOffer0 != null)
                dailyOffer0.onClick.AddListener(() => OnOfferClicked(0));
            if (dailyOffer1 != null)
                dailyOffer1.onClick.AddListener(() => OnOfferClicked(1));
            if (dailyOffer2 != null)
                dailyOffer2.onClick.AddListener(() => OnOfferClicked(2));

            if (scrollRect != null)
                scrollRect.onValueChanged.AddListener(OnScroll);
        }

        private void OnEnable()
        {
            if (shopkeeperRect != null)
                shopkeeperStartY = shopkeeperRect.anchoredPosition.y;
        }

        public override void OnShow()
        {
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;

            if (shopkeeperRect != null)
                shopkeeperRect.anchoredPosition = new Vector2(
                    shopkeeperRect.anchoredPosition.x, shopkeeperStartY);

            RefreshDailyOffers();
        }

        private void OnScroll(Vector2 scrollPos)
        {
            if (shopkeeperRect == null || scrollRect == null) return;

            float scrolledAmount = (1f - scrollPos.y);

            float contentHeight = scrollRect.content.sizeDelta.y;
            float viewportHeight = scrollRect.viewport.rect.height;
            float scrollableDistance = contentHeight - viewportHeight;

            if (scrollableDistance <= 0f) return;

            float offset = scrolledAmount * scrollableDistance * parallaxFactor;
            shopkeeperRect.anchoredPosition = new Vector2(
                shopkeeperRect.anchoredPosition.x,
                shopkeeperStartY - offset);
        }

        private void RefreshDailyOffers()
        {
            if (dailyOffer0Text != null) dailyOffer0Text.text = "Daily Deal 1\n50 Gems";
            if (dailyOffer1Text != null) dailyOffer1Text.text = "Daily Deal 2\n100 Gems";
            if (dailyOffer2Text != null) dailyOffer2Text.text = "Daily Deal 3\n200 Gems";
        }

        private void OnOfferClicked(int index)
        {
            Debug.Log("[Shop] Offer " + index + " clicked (placeholder)");
        }

        private void OnBackClicked()
        {
            lobbyManager.ShowPanel(PanelType.Homepage);
        }

        private void OnDestroy()
        {
            if (backButton != null) backButton.onClick.RemoveAllListeners();
            if (dailyOffer0 != null) dailyOffer0.onClick.RemoveAllListeners();
            if (dailyOffer1 != null) dailyOffer1.onClick.RemoveAllListeners();
            if (dailyOffer2 != null) dailyOffer2.onClick.RemoveAllListeners();
            if (scrollRect != null) scrollRect.onValueChanged.RemoveAllListeners();
        }
    }
}