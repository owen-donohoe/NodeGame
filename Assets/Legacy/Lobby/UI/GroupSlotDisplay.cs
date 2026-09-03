using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NodeWar.Lobby
{
    /// <summary>
    /// Single equipped slot in the loadout. Shows icon + name when filled,
    /// "empty" placeholder when not. Click to unequip.
    /// </summary>
    public class GroupSlotDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Button slotButton;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private GameObject emptyState;
        [SerializeField] private GameObject filledState;

        private string equippedID;
        private System.Action<string> onUnequip;

        private static readonly Color emptyColor = new Color(0.12f, 0.12f, 0.16f, 1f);
        private static readonly Color filledColor = new Color(0.20f, 0.25f, 0.35f, 1f);

        private void Awake()
        {
            if (slotButton != null)
                slotButton.onClick.AddListener(OnSlotClicked);
        }

        public void Initialize(System.Action<string> unequipCallback)
        {
            onUnequip = unequipCallback;
            SetEmpty();
        }

        public void SetItem(string id, string displayName, Sprite icon)
        {
            equippedID = id;

            if (emptyState != null) emptyState.SetActive(false);
            if (filledState != null) filledState.SetActive(true);

            if (nameText != null) nameText.text = displayName;
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = (icon != null);
            }

            if (slotButton != null)
                slotButton.image.color = filledColor;
        }

        public void SetEmpty()
        {
            equippedID = null;

            if (emptyState != null) emptyState.SetActive(true);
            if (filledState != null) filledState.SetActive(false);

            if (slotButton != null)
                slotButton.image.color = emptyColor;
        }

        public bool IsEmpty => string.IsNullOrEmpty(equippedID);
        public string EquippedID => equippedID;

        private void OnSlotClicked()
        {
            if (IsEmpty) return;
            string id = equippedID;
            SetEmpty();
            onUnequip?.Invoke(id);
        }

        private void OnDestroy()
        {
            if (slotButton != null) slotButton.onClick.RemoveAllListeners();
        }
    }
}