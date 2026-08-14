using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Single item row in the scrollable list.
    /// Shows icon + name. Click to select (expands USE button).
    /// USE button equips the item.
    /// Locked items are non-interactable.
    /// </summary>
    public class SelectableItemDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button rowButton;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private GameObject useButtonContainer;
        [SerializeField] private Button useButton;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private GameObject lockIcon;

        private string itemID;
        private bool isLocked;
        private bool isSelected;
        private System.Action<string> onUse;

        private static readonly Color normalColor = new Color(0.14f, 0.14f, 0.19f, 1f);
        private static readonly Color selectedColor = new Color(0.22f, 0.28f, 0.38f, 1f);
        private static readonly Color lockedColor = new Color(0.10f, 0.10f, 0.12f, 1f);

        private void Awake()
        {
            if (rowButton != null)
                rowButton.onClick.AddListener(OnRowClicked);
            if (useButton != null)
                useButton.onClick.AddListener(OnUseClicked);

            if (useButtonContainer != null)
                useButtonContainer.SetActive(false);
        }

        public void Initialize(string id, string displayName, Sprite icon, bool locked,
            System.Action<string> useCallback)
        {
            itemID = id;
            isLocked = locked;
            onUse = useCallback;
            isSelected = false;

            if (nameText != null) nameText.text = displayName;
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = (icon != null);
            }

            if (lockIcon != null) lockIcon.SetActive(locked);
            if (rowButton != null) rowButton.interactable = !locked;
            if (backgroundImage != null)
                backgroundImage.color = locked ? lockedColor : normalColor;

            if (useButtonContainer != null)
                useButtonContainer.SetActive(false);
        }

        public string ItemID => itemID;

        public void Deselect()
        {
            isSelected = false;
            if (useButtonContainer != null)
                useButtonContainer.SetActive(false);
            if (backgroundImage != null && !isLocked)
                backgroundImage.color = normalColor;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void OnRowClicked()
        {
            if (isLocked) return;

            isSelected = !isSelected;

            if (useButtonContainer != null)
                useButtonContainer.SetActive(isSelected);
            if (backgroundImage != null)
                backgroundImage.color = isSelected ? selectedColor : normalColor;
        }

        private void OnUseClicked()
        {
            onUse?.Invoke(itemID);
        }

        private void OnDestroy()
        {
            if (rowButton != null) rowButton.onClick.RemoveAllListeners();
            if (useButton != null) useButton.onClick.RemoveAllListeners();
        }
    }
}