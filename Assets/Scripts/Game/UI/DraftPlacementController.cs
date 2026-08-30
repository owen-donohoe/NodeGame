using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using NodeWar.Simulation;
using NodeWar.Core;

namespace NodeWar.UI
{
    /// <summary>
    /// Draft placement state machine and input handler.
    /// Manages the lifecycle of drag --> preview --> confirm --> place.
    /// 
    /// State flow: Idle --> Dragging --> Placed --> (Repositioning --> Dragging) or Confirmed
    /// 
    /// Responsibilities:
    ///   - Reading mouse input and screen-zone detection
    ///   - Creating/destroying ghost preview instances
    ///   - Controlling drag proxy visibility and position
    ///   - Showing/hiding confirm button at correct timing
    ///   - Calling DraftManager.ConfirmLocalPlacement on confirm
    ///   
    /// Does NOT own: bar, timer, turn display, persistent placeholders, sticker data.
    /// Those live on DraftUI.
    /// 
    /// Lives on the DraftUI root GameObject. Initialized by DraftUI.
    /// </summary>
    public class DraftPlacementController : MonoBehaviour
    {
        [Header("Screen Zones (fraction of screen height)")]
        [Tooltip("Below this = cursor in bar zone. Preview destroyed, proxy visible.")]
        [SerializeField][Range(0.05f, 0.25f)] private float barZoneThreshold = 0.10f;
        [Tooltip("Above this = placement eligible on release. Between bar and this, preview visible but release cancels.")]
        [SerializeField][Range(0.08f, 0.35f)] private float placementEligibleThreshold = 0.15f;
        [Tooltip("Above this = proxy fully invisible, preview fully opaque.")]
        [SerializeField][Range(0.10f, 0.40f)] private float fadeZoneThreshold = 0.20f;

        [Header("Confirm Timing")]
        [Tooltip("Seconds the preview must be still before confirm button appears.")]
        [SerializeField] private float confirmStillThreshold = 0.3f;

        [Header("Preview Prefab")]
        [Tooltip("World-space prefab shown during drag. Should look ghostly/translucent.")]
        [SerializeField] private GameObject ghostPreviewPrefab;

        [Header("Preview Positioning")]
        [Tooltip("Y offset applied to preview above the grid plane.")]
        [SerializeField] private float previewYOffset = 0.1f;

        [Header("Tint Colors")]
        [SerializeField] private Color validCellTint = new Color(0.3f, 1f, 0.3f, 0.7f);
        [SerializeField] private Color invalidCellTint = new Color(1f, 0.3f, 0.3f, 0.5f);

        [Header("References (wire in prefab)")]
        [SerializeField] private RectTransform dragProxy;
        [SerializeField] private CanvasGroup dragProxyCanvasGroup;
        [SerializeField] private DraftConfirmPresenter confirmPresenter;

        // Injected at runtime
        private DraftManager draftManager;
        private DraftState draftState;
        private Camera mainCam;
        private int localPlayerID;

        // Sticker lookup — provided by DraftUI
        private System.Func<DistrictType, Sprite> stickerLookup;

        // State machine
        private enum DragMode { Idle, Dragging, Placed, Repositioning }
        private DragMode dragMode = DragMode.Idle;

        // Active drag
        private int activeSlotIndex = -1;
        private DistrictType activeDistrictType;

        // Preview instance
        private GameObject previewInstance;
        private DraftPlacementPreview previewComponent;
        private int previewGridX = -1;
        private int previewGridZ = -1;
        private bool previewOnValidCell;

        // Still detection for confirm
        private float stillTimer;
        private Vector3 lastPreviewPosition;
        private bool confirmVisible;

        // Callback to DraftUI for slot dimming
        public System.Action<int> OnDragStarted;
        public System.Action OnDragCancelled;

        // ===== INITIALIZATION =====

        public void Initialize(DraftManager manager, int playerID,
            System.Func<DistrictType, Sprite> stickerFunc)
        {
            draftManager = manager;
            localPlayerID = playerID;
            mainCam = Camera.main;
            stickerLookup = stickerFunc;

            HideDragProxy();
            if (confirmPresenter != null)
                confirmPresenter.Hide();
        }

        /// <summary>
        /// Called each time turn changes — controller needs fresh DraftState reference.
        /// </summary>
        public void SetDraftState(DraftState state)
        {
            draftState = state;
        }

        // ===== PUBLIC API =====

        /// <summary>
        /// Called by DraftUI when a slot is clicked. Enters Dragging state.
        /// </summary>
        public void BeginDrag(int slotIndex, DistrictType districtType)
        {
            if (!draftManager.IsLocalPlayerTurn()) return;

            // Cancel any existing drag before starting a new one.
            // This ensures the previous slot gets undimmed and state is clean.
            if (dragMode != DragMode.Idle)
            {
                DestroyPreview();
                HideDragProxy();
                if (confirmPresenter != null) confirmPresenter.Hide();
                confirmVisible = false;
                OnDragCancelled?.Invoke();
            }

            activeSlotIndex = slotIndex;
            activeDistrictType = districtType;
            dragMode = DragMode.Dragging;
            confirmVisible = false;

            if (confirmPresenter != null)
                confirmPresenter.Hide();

            OnDragStarted?.Invoke(slotIndex);
            ShowDragProxy();
        }

        public void CancelDrag()
        {
            DestroyPreview();
            HideDragProxy();
            if (confirmPresenter != null)
                confirmPresenter.Hide();
            confirmVisible = false;

            int cancelledSlot = activeSlotIndex;
            activeSlotIndex = -1;
            dragMode = DragMode.Idle;

            OnDragCancelled?.Invoke();
        }

        public bool IsActive => dragMode != DragMode.Idle;

        // ===== UPDATE =====

        private void Update()
        {
            switch (dragMode)
            {
                case DragMode.Idle:
                    break;
                case DragMode.Dragging:
                case DragMode.Repositioning:
                    UpdateDragging();
                    break;
                case DragMode.Placed:
                    UpdatePlaced();
                    break;
            }
        }

        private void UpdateDragging()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 screenPos = mouse.position.ReadValue();
            float normalizedY = screenPos.y / Screen.height;

            // Drag proxy fades out going up, preview fades in
            UpdateDragProxyAlpha(normalizedY);
            if (dragProxy != null)
                dragProxy.position = screenPos;

            // Preview lifecycle driven by screen zone
            if (normalizedY < barZoneThreshold)
            {
                DestroyPreview();
            }
            else
            {
                EnsurePreviewExists();
                UpdatePreviewPosition(screenPos);
                UpdatePreviewAlpha(normalizedY);
            }

            // Release
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (normalizedY < placementEligibleThreshold)
                    CancelDrag();
                else if (previewOnValidCell)
                    EnterPlacedState();
                else
                    CancelDrag();
                return;
            }

            // Cancel via right-click or escape
            if (mouse.rightButton.wasPressedThisFrame || EscapePressed())
                CancelDrag();
        }

        private void UpdatePlaced()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // Re-grab: click on the placed cell to reposition
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    // UI click (probably confirm button) — let EventSystem handle it
                }
                else if (IsPointerOnPreviewCell(mouse.position.ReadValue()))
                {
                    dragMode = DragMode.Repositioning;
                    if (confirmPresenter != null) confirmPresenter.Hide();
                    confirmVisible = false;
                    ShowDragProxy();
                }
                return;
            }

            if (mouse.rightButton.wasPressedThisFrame || EscapePressed())
                CancelDrag();

            // Re-grab: click on the placed cell to reposition
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    // UI click (probably confirm button) — let EventSystem handle it
                }
                else if (IsPointerOnPreviewCell(mouse.position.ReadValue()))
                {
                    dragMode = DragMode.Repositioning;
                    if (confirmPresenter != null) confirmPresenter.Hide();
                    confirmVisible = false;
                    ShowDragProxy();
                }
                return;
            }

            if (mouse.rightButton.wasPressedThisFrame || EscapePressed())
                CancelDrag();
        }

        private void EnterPlacedState()
        {
            dragMode = DragMode.Placed;
            HideDragProxy();

            if (previewOnValidCell)
            {
                // Show confirm immediately — the spring animation provides visual delay
                ShowConfirm();
            }
            else
            {
                stillTimer = 0f;
                lastPreviewPosition = previewInstance != null ? previewInstance.transform.position : Vector3.zero;
                confirmVisible = false;
            }
        }

        // ===== CONFIRM =====

        private void ShowConfirm()
        {
            if (confirmPresenter == null) return;
            confirmVisible = true;

            Vector3 worldPos = draftManager.GridToWorld(previewGridX, previewGridZ);
            Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);

            confirmPresenter.Show(screenPos);
            confirmPresenter.OnConfirmed = HandleConfirmClicked;
        }

        private void HandleConfirmClicked()
        {
            if (dragMode != DragMode.Placed) return;
            if (activeSlotIndex < 0) return;
            if (!previewOnValidCell) return;

            draftManager.ConfirmLocalPlacement(activeSlotIndex, previewGridX, previewGridZ);

            // Clean up — DraftUI.OnPlacementConfirmed will handle visual feedback
            DestroyPreview();
            HideDragProxy();
            if (confirmPresenter != null) confirmPresenter.Hide();
            confirmVisible = false;
            activeSlotIndex = -1;
            dragMode = DragMode.Idle;

            OnDragCancelled?.Invoke(); // signals slot undim
        }

        // ===== PREVIEW =====

        private void EnsurePreviewExists()
        {
            if (previewInstance != null) return;
            if (ghostPreviewPrefab == null) return;

            previewInstance = Instantiate(ghostPreviewPrefab);
            previewComponent = previewInstance.GetComponent<DraftPlacementPreview>();

            if (previewComponent != null && stickerLookup != null)
                previewComponent.SetSticker(stickerLookup(activeDistrictType));
        }

        private void DestroyPreview()
        {
            if (previewInstance != null)
            {
                Destroy(previewInstance);
                previewInstance = null;
                previewComponent = null;
            }
            previewOnValidCell = false;
        }

        private void UpdatePreviewPosition(Vector2 screenPos)
        {
            if (previewInstance == null) return;

            // Raycast to ground plane
            Ray ray = mainCam.ScreenPointToRay(screenPos);
            if (Mathf.Abs(ray.direction.y) < 0.0001f) { previewOnValidCell = false; return; }

            float t = -ray.origin.y / ray.direction.y;
            if (t < 0) { previewOnValidCell = false; return; }

            Vector3 worldHit = ray.origin + ray.direction * t;

            if (!draftManager.WorldToGrid(worldHit, out int gx, out int gz))
            {
                previewOnValidCell = false;
                previewInstance.transform.position = worldHit;
                if (previewComponent != null) previewComponent.SetTint(invalidCellTint);
                return;
            }

            previewOnValidCell = draftState.IsCellAvailable(gx, gz);
            previewGridX = gx;
            previewGridZ = gz;
            previewInstance.transform.position = draftManager.GridToWorld(gx, gz) + Vector3.up * previewYOffset;

            if (previewComponent != null)
                previewComponent.SetTint(previewOnValidCell ? validCellTint : invalidCellTint);
        }

        private void UpdatePreviewAlpha(float normalizedY)
        {
            if (previewComponent == null) return;

            float alpha;
            if (normalizedY < fadeZoneThreshold)
                alpha = (normalizedY - barZoneThreshold) / (fadeZoneThreshold - barZoneThreshold);
            else
                alpha = 1f;

            previewComponent.SetAlpha(alpha);
        }

        // ===== DRAG PROXY =====

        private void ShowDragProxy()
        {
            if (dragProxy == null) return;

            // Position before activating to prevent one-frame snap
            Mouse mouse = Mouse.current;
            if (mouse != null)
                dragProxy.position = mouse.position.ReadValue();

            dragProxy.gameObject.SetActive(true);
            if (dragProxyCanvasGroup != null)
                dragProxyCanvasGroup.alpha = 1f;
        }

        private void HideDragProxy()
        {
            if (dragProxy == null) return;
            dragProxy.gameObject.SetActive(false);
        }

        private void UpdateDragProxyAlpha(float normalizedY)
        {
            if (dragProxyCanvasGroup == null) return;

            float alpha;
            if (normalizedY < barZoneThreshold)
                alpha = 1f;
            else if (normalizedY < fadeZoneThreshold)
                alpha = 1f - ((normalizedY - barZoneThreshold) / (fadeZoneThreshold - barZoneThreshold));
            else
                alpha = 0f;

            dragProxyCanvasGroup.alpha = alpha;
        }

        // ===== HELPERS =====

        /// <summary>
        /// World-space grid-cell check — zoom-agnostic.
        /// </summary>
        private bool IsPointerOnPreviewCell(Vector2 screenPos)
        {
            if (previewGridX < 0 || previewGridZ < 0) return false;

            Ray ray = mainCam.ScreenPointToRay(screenPos);
            if (Mathf.Abs(ray.direction.y) < 0.0001f) return false;

            float t = -ray.origin.y / ray.direction.y;
            if (t < 0) return false;

            Vector3 worldHit = ray.origin + ray.direction * t;
            if (!draftManager.WorldToGrid(worldHit, out int gx, out int gz)) return false;

            return gx == previewGridX && gz == previewGridZ;
        }

        private bool EscapePressed()
        {
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        }

        private void OnDestroy()
        {
            if (previewInstance != null)
                Destroy(previewInstance);
        }
    }
}