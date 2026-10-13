using UnityEngine;
using UnityEngine.UI;
using NodeWar.Simulation;
using NodeWar.View;

namespace NodeWar.UI
{
    // Bounds must reflect this frame's sprite facing and movement before layout.
    [DefaultExecutionOrder(100)]
    public class VillagerHealthBar : MonoBehaviour
    {
        [SerializeField] private Image trackImage;
        [SerializeField] private Image fillImage;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private bool alwaysVisible;

        [Header("Colors")]
        [SerializeField] private Color fullHealthColor = new Color(0.3f, 1f, 0.4f);
        [SerializeField] private Color midHealthColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] private Color lowHealthColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private Color trackColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);

        private SimulationState simState;
        private int villagerID;
        private VillagerView villagerView;
        private RectTransform rect;

        public void Initialize(SimulationState state, int id, VillagerView view)
        {
            simState = state;
            villagerID = id;
            villagerView = view;
            rect = transform as RectTransform;
            if (trackImage != null)
            {
                trackImage.raycastTarget = false;
                trackImage.color = trackColor;
            }
            if (fillImage != null)
            {
                fillImage.raycastTarget = false;
                fillImage.type = Image.Type.Filled;
                fillImage.fillMethod = Image.FillMethod.Horizontal;
                fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            }
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.alpha = 0f;
            }
        }

        private void LateUpdate()
        {
            bool valid = simState != null && villagerID >= 0 && villagerID < simState.villagers.Length;
            if (!valid || villagerView == null || rect == null || !villagerView.TryGetSpriteBounds(out Bounds bounds))
            {
                if (canvasGroup != null) canvasGroup.alpha = 0f;
                return;
            }

            VillagerData villager = simState.villagers[villagerID];
            bool visible = VillagerHealthMath.Visible(villager.hp, villager.maxHP,
                villager.state != VillagerState.Dead, villager.isConsumed, alwaysVisible);
            if (canvasGroup != null) canvasGroup.alpha = visible ? 1f : 0f;
            float fraction = VillagerHealthMath.Fraction(villager.hp, villager.maxHP);
            if (fillImage != null)
            {
                fillImage.fillAmount = fraction;
                fillImage.color = fraction <= 0.3f ? lowHealthColor
                    : fraction <= 0.6f ? midHealthColor : fullHealthColor;
            }

            // Project the world AABB onto the canvas right axis for all four POVs.
            Vector3 right = transform.right;
            float width = Mathf.Abs(right.x) * bounds.size.x + Mathf.Abs(right.y) * bounds.size.y
                + Mathf.Abs(right.z) * bounds.size.z;
            var layout = VillagerHealthMath.Layout(width, bounds.size.y, bounds.max.y);
            Vector3 scale = transform.lossyScale;
            if (Mathf.Abs(scale.x) < 0.00001f || Mathf.Abs(scale.y) < 0.00001f) return;
            rect.sizeDelta = new Vector2(layout.Width / Mathf.Abs(scale.x), layout.Height / Mathf.Abs(scale.y));
            // Bottom edge clears the world top; the bar itself keeps camera-facing.
            transform.position = new Vector3(bounds.center.x, layout.BottomY, bounds.center.z)
                + transform.up * (layout.Height * 0.5f);
        }
    }
}
