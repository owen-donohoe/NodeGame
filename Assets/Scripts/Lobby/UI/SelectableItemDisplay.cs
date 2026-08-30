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

        // Nested Canvas used to raise this row's render order above the rows
        // below it while selected, without touching sibling index (which
        // would also move the row within the VerticalLayoutGroup).
        private Canvas sortingCanvas;
        private GraphicRaycaster sortingRaycaster;
        private const int RaisedSortingOrder = 1;

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

            EnsureSortingComponents();
        }

        // Adds (or finds) the Canvas/GraphicRaycaster pair once and keeps them
        // cached for the lifetime of the row. A nested Canvas with
        // overrideSorting breaks raycasting for its children unless a
        // GraphicRaycaster is also present on the same object, so both are
        // ensured together. overrideSorting only has a visible effect when a
        // parent Canvas exists in the hierarchy; if the lookups ever fail to
        // produce components (e.g. AddComponent blocked for some reason) the
        // raise/lower calls below simply no-op instead of throwing.
        private void EnsureSortingComponents()
        {
            if (sortingCanvas == null)
                sortingCanvas = GetComponent<Canvas>();
            if (sortingCanvas == null)
                sortingCanvas = gameObject.AddComponent<Canvas>();
            if (sortingCanvas != null)
                sortingCanvas.overrideSorting = false;

            if (sortingRaycaster == null)
                sortingRaycaster = GetComponent<GraphicRaycaster>();
            if (sortingRaycaster == null)
                sortingRaycaster = gameObject.AddComponent<GraphicRaycaster>();
        }

        private void RaiseSortingOrder()
        {
            if (sortingCanvas == null) return;
            sortingCanvas.overrideSorting = true;
            sortingCanvas.sortingOrder = RaisedSortingOrder;
        }

        private void LowerSortingOrder()
        {
            if (sortingCanvas == null) return;
            sortingCanvas.overrideSorting = false;
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

            LowerSortingOrder();
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
                // Raise render order (via nested Canvas) so the Use button renders
                // above items below us, without touching layout position.
                RaiseSortingOrder();
            }
            else if (!isSelected && wasSelected)
            {
                LowerSortingOrder();
            }

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