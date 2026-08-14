using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using NodeWar.Simulation;

namespace NodeWar.UI
{
    /// <summary>
    /// Drag up/down wheel for Forge material allocation.
    /// Value range: 0 to player's current material count.
    /// Sends SetAllocation command on value change.
    /// </summary>
    public class AllocationWheel : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        [Header("References (assign in prefab)")]
        [SerializeField] private RectTransform strip;
        [SerializeField] private TextMeshProUGUI topText;
        [SerializeField] private TextMeshProUGUI middleText;
        [SerializeField] private TextMeshProUGUI bottomText;

        [Header("Settings")]
        [SerializeField] private float cellHeight = 36f;
        [SerializeField] private float dragSensitivity = 0.5f;
        [SerializeField] private float snapDuration = 0.2f;

        private SimulationState simState;
        private InputBuffer inputBuffer;
        private int playerID;
        private int nodeID;
        private int currentValue;
        private float dragAccumulator;
        private Tween snapTween;

        public void Initialize(int startValue, SimulationState state,
            InputBuffer buffer, int pid, int node)
        {
            simState = state;
            inputBuffer = buffer;
            playerID = pid;
            nodeID = node;
            currentValue = startValue;

            RefreshDisplay();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragAccumulator = 0f;
            snapTween?.Kill();
            strip.anchoredPosition = Vector2.zero;
        }

        public void OnDrag(PointerEventData eventData)
        {
            dragAccumulator += eventData.delta.y * dragSensitivity;

            // Move strip visually during drag
            float clampedOffset = Mathf.Clamp(dragAccumulator, -cellHeight, cellHeight);
            strip.anchoredPosition = new Vector2(0f, clampedOffset);

            // Check if we've dragged far enough to change value
            if (dragAccumulator >= cellHeight)
            {
                TryChangeValue(1); // drag up = increase
                dragAccumulator -= cellHeight;
                strip.anchoredPosition = Vector2.zero;
            }
            else if (dragAccumulator <= -cellHeight)
            {
                TryChangeValue(-1); // drag down = decrease
                dragAccumulator += cellHeight;
                strip.anchoredPosition = Vector2.zero;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Snap back to center
            snapTween?.Kill();
            snapTween = strip.DOAnchorPosY(0f, snapDuration).SetEase(Ease.OutBack);
            dragAccumulator = 0f;
        }

        private void TryChangeValue(int delta)
        {
            int maxValue = simState.players[playerID].materials;
            int newValue = Mathf.Clamp(currentValue + delta, 0, maxValue);

            if (newValue == currentValue) return;

            currentValue = newValue;
            RefreshDisplay();
            SendAllocationCommand();
        }

        private void SendAllocationCommand()
        {
            GameCommand cmd = new GameCommand
            {
                type = CommandType.SetAllocation,
                playerID = playerID,
                targetNodeID = nodeID,
                value = currentValue,
                issuedOnTick = simState.tickCount
            };
            inputBuffer.EnqueueCommand(cmd);
        }

        private void RefreshDisplay()
        {
            middleText.text = currentValue.ToString();
            topText.text = (currentValue + 1).ToString();
            bottomText.text = (currentValue > 0) ? (currentValue - 1).ToString() : "";
        }

        private void OnDestroy()
        {
            snapTween?.Kill();
        }
    }
}