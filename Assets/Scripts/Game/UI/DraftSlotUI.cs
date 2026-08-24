using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    public class DraftSlotUI : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI labelText;

        private int slotIndex;
        private bool isInteractable = true;
        private DraftUI parentUI;
        private CanvasGroup canvasGroup;

        private static readonly Color normalColor = new Color(0.18f, 0.22f, 0.30f, 1f);
        private static readonly Color disabledColor = new Color(0.12f, 0.12f, 0.15f, 0.6f);

        public int SlotIndex => slotIndex;

        public void Initialize(DraftSlot slot, int index, DraftUI ui)
        {
            slotIndex = index;
            parentUI = ui;

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (labelText != null)
                labelText.text = GetDistrictName(slot.districtType);

            if (iconImage != null && parentUI != null)
            {
                Sprite icon = parentUI.GetStickerSprite(slot.districtType);
                if (icon != null)
                {
                    iconImage.sprite = icon;
                    iconImage.enabled = true;
                }
                else
                {
                    iconImage.enabled = false;
                }
            }

            if (backgroundImage != null)
                backgroundImage.color = normalColor;
        }

        public void SetInteractable(bool interactable)
        {
            isInteractable = interactable;
            if (backgroundImage != null)
                backgroundImage.color = interactable ? normalColor : disabledColor;
        }

        public void SetDimmed(bool dimmed)
        {
            if (canvasGroup != null)
                canvasGroup.alpha = dimmed ? 0.4f : 1f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!isInteractable) return;
            if (parentUI != null)
                parentUI.BeginDrag(slotIndex);
        }

        private string GetDistrictName(DistrictType type)
        {
            switch (type)
            {
                case DistrictType.Farm: return "Farm";
                case DistrictType.Mine: return "Mine";
                case DistrictType.Village: return "Village";
                case DistrictType.Barracks: return "Barracks";
                case DistrictType.Forge: return "Forge";
                case DistrictType.Camp: return "Camp";
                case DistrictType.Shrine: return "Shrine";
                case DistrictType.Arsenal: return "Arsenal";
                case DistrictType.Sanctuary: return "Sanctuary";
                case DistrictType.Watchtower: return "Watchtower";
                case DistrictType.Rampart: return "Rampart";
                case DistrictType.Market: return "Market";
                default: return "Node";
            }
        }
    }
}