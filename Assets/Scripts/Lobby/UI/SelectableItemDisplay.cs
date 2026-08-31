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

        // While selected, the Use button is re-parented into a shared overlay
        // object that sits last under the same layout content as the rows.
        //
        // A nested Canvas with overrideSorting was tried first and cannot work
        // here: both Mask and RectMask2D cancel clipping for content under an
        // overrideSorting canvas (Mask via FindRootSortOverrideCanvas cutting
        // the stencil-depth walk short, RectMask2D via GetRectMaskForClippable
        // nulling the mask when it finds an overrideSorting canvas the mask is
        // not a descendant of). The button escaped the scroll viewport entirely.
        //
        // The overlay is a plain RectTransform -- no canvas, no sorting
        // override -- so clipping behaves normally, and being the last sibling
        // is what puts it above the other rows. LayoutElement.ignoreLayout
        // keeps the layout group from positioning it and keeps ContentSizeFitter
        // from counting it.
        private const string UseButtonOverlayName = "__UseButtonOverlay";

        private RectTransform useButtonRect;
        private Transform useButtonHome;
        private int useButtonHomeSiblingIndex;
        private Vector2 useButtonHomeAnchorMin;
        private Vector2 useButtonHomeAnchorMax;
        private Vector2 useButtonHomePivot;
        private Vector2 useButtonHomeAnchoredPos;
        private Vector2 useButtonHomeSizeDelta;
        private Vector3 useButtonHomeLocalScale;
        private Quaternion useButtonHomeLocalRot;

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

            CacheUseButtonHome();
        }

        // Records where the Use button lives inside this row, before it is ever
        // moved, so RestoreUseButton can put it back exactly.
        private void CacheUseButtonHome()
        {
            if (useButtonContainer == null) return;

            useButtonRect = useButtonContainer.transform as RectTransform;
            if (useButtonRect == null) return;

            useButtonHome = useButtonRect.parent;
            useButtonHomeSiblingIndex = useButtonRect.GetSiblingIndex();
            useButtonHomeAnchorMin = useButtonRect.anchorMin;
            useButtonHomeAnchorMax = useButtonRect.anchorMax;
            useButtonHomePivot = useButtonRect.pivot;
            useButtonHomeAnchoredPos = useButtonRect.anchoredPosition;
            useButtonHomeSizeDelta = useButtonRect.sizeDelta;
            useButtonHomeLocalScale = useButtonRect.localScale;
            useButtonHomeLocalRot = useButtonRect.localRotation;
        }

        // Finds (or creates) the overlay shared by every row under this row's
        // layout parent. Not owned by any single row, so it survives rows being
        // rebuilt and is simply re-found by whichever row needs it next.
        private RectTransform GetOrCreateUseButtonOverlay()
        {
            Transform content = transform.parent;
            if (content == null) return null;

            RectTransform overlay = content.Find(UseButtonOverlayName) as RectTransform;
            if (overlay != null) return overlay;

            GameObject go = new GameObject(UseButtonOverlayName, typeof(RectTransform));
            overlay = (RectTransform)go.transform;
            overlay.SetParent(content, false);
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.offsetMin = Vector2.zero;
            overlay.offsetMax = Vector2.zero;
            overlay.localScale = Vector3.one;

            LayoutElement ignore = go.AddComponent<LayoutElement>();
            ignore.ignoreLayout = true;

            return overlay;
        }

        private void RaiseUseButton()
        {
            if (useButtonRect == null) return;

            RectTransform overlay = GetOrCreateUseButtonOverlay();
            if (overlay == null) return;

            // Re-assert last-sibling every time: rows can be rebuilt after the
            // overlay was created. ignoreLayout means this moves no row.
            overlay.SetAsLastSibling();

            // worldPositionStays keeps the button visually where the row put it.
            useButtonRect.SetParent(overlay, true);
        }

        private void RestoreUseButton()
        {
            if (useButtonRect == null || useButtonHome == null) return;
            if (useButtonRect.parent == useButtonHome) return;

            useButtonRect.SetParent(useButtonHome, false);
            useButtonRect.SetSiblingIndex(useButtonHomeSiblingIndex);
            useButtonRect.anchorMin = useButtonHomeAnchorMin;
            useButtonRect.anchorMax = useButtonHomeAnchorMax;
            useButtonRect.pivot = useButtonHomePivot;
            useButtonRect.anchoredPosition = useButtonHomeAnchoredPos;
            useButtonRect.sizeDelta = useButtonHomeSizeDelta;
            useButtonRect.localScale = useButtonHomeLocalScale;
            useButtonRect.localRotation = useButtonHomeLocalRot;
        }

        // Covers row hidden via SetVisible, row destroyed, and component
        // disabled -- without this the button would stay parented to the
        // overlay and remain visible after its row went away.
        private void OnDisable()
        {
            RestoreUseButton();
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

            // Guards against a recycled row being re-initialized while its Use
            // button is still parented into the overlay.
            RestoreUseButton();
            if (useButtonContainer != null)
                useButtonContainer.SetActive(false);
        }

        public string ItemID => itemID;

        public void Deselect()
        {
            isSelected = false;
            RestoreUseButton();
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

            bool wasSelected = isSelected;
            isSelected = !isSelected;

            if (isSelected && !wasSelected)
            {
                // Notify panel to deselect siblings before this row shows its
                // Use button.
                onRowSelected?.Invoke(this);

                // Activate first so the button resolves its position inside the
                // row, then move it into the overlay preserving that position.
                if (useButtonContainer != null)
                    useButtonContainer.SetActive(true);
                RaiseUseButton();
            }
            else if (!isSelected && wasSelected)
            {
                RestoreUseButton();
                if (useButtonContainer != null)
                    useButtonContainer.SetActive(false);
            }

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