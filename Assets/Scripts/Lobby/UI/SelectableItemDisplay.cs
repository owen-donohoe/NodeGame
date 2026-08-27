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
        private int originalSiblingIndex = -1;

        private static readonly Color normalColor = new Color(0.14f, 0.14f, 0.19f, 1f);
        private static readonly Color selectedColor = new Color(0.22f, 0.28f, 0.38f, 1f);
        private static readonly Color lockedColor = new Color(0.10f, 0.10f, 0.12f, 1f);

        private System.Action<SelectableItemDisplay> onRowSelected;

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
                                System.Action<string> useCallback,
                                System.Action<SelectableItemDisplay> rowSelectedCallback = null)
        {
            itemID = id;
            isLocked = locked;
            onUse = useCallback;
            isSelected = false;
            onRowSelected = rowSelectedCallback;

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

            RestoreSiblingIndex();
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        private void OnRowClicked()
        {
            if (isLocked) return;

            bool wasSelected = isSelected;
            isSelected = !isSelected;

            // Notify panel to deselect siblings before this item shows its Use button
            if (isSelected && !wasSelected)
            {
                onRowSelected?.Invoke(this);
                // Move to last sibling so Use button renders above items below us
                originalSiblingIndex = transform.GetSiblingIndex();
                transform.SetAsLastSibling();
            }
            else if (!isSelected && wasSelected)
            {
                RestoreSiblingIndex();
            }

            if (useButtonContainer != null)
                useButtonContainer.SetActive(isSelected);
            if (backgroundImage != null)
                backgroundImage.color = isSelected ? selectedColor : normalColor;
        }
        private void RestoreSiblingIndex()
        {
            if (originalSiblingIndex >= 0 && originalSiblingIndex < transform.parent.childCount)
            {
                transform.SetSiblingIndex(originalSiblingIndex);
                originalSiblingIndex = -1;
            }
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