using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    public class DraftPickUI : MonoBehaviour, IPointerDownHandler
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

        public void Initialize(DraftPick slot, int index, DraftUI ui)
        {
            slotIndex = index;
            parentUI = ui;

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (labelText != null)
                labelText.text = GetDistrictName(slot.districtType) + " (" +
                    NodeWar.View.DistrictFallback.Describe(slot.districtType).Monogram + ")";

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
            return NodeWar.View.DistrictFallback.Describe(type).Name ?? type.ToString();
        }
    }
}
