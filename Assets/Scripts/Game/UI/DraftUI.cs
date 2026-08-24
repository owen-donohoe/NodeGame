using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using NodeWar.Simulation;
using NodeWar.Core;
using System.Collections.Generic;

namespace NodeWar.UI
{
    /// <summary>
    /// Controls the draft phase UI.
    /// 
    /// Responsibilities:
    /// - Draft bar (always visible, shows unconsumed slots)
    /// - Drag proxy (cursor follower, visible near bar, fades on board)
    /// - Placement preview lifecycle (created/destroyed by screen zone)
    /// - Confirm button (appears on still, independent of preview renderer)
    /// - Turn/timer display
    /// - Persistent placeholders (stay on board until game starts)
    /// 
    /// Does NOT own draft state or networking. Those live in DraftManager.
    /// </summary>
    public class DraftUI : MonoBehaviour
    {
        // ===== INSPECTOR =====

        [Header("Bar (always visible)")]
        [SerializeField] private RectTransform barPanel;
        [SerializeField] private RectTransform barContainer;
        [SerializeField] private GameObject draftSlotPrefab;

        [Header("Drag Proxy (screen-space cursor follower)")]
        [SerializeField] private RectTransform dragProxy;
        [SerializeField] private CanvasGroup dragProxyCanvasGroup;

        [Header("Confirm Button")]
        [SerializeField] private GameObject confirmButtonGO;
        [SerializeField] private Button confirmButton;

        [Header("Timer")]
        [SerializeField] private Image timerFill;
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("Turn Indicator")]
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private Image turnIndicatorBG;

        [Header("World Prefabs")]
        [SerializeField] private GameObject placementPreviewPrefab;

        [Header("Sticker Mappings")]
        [SerializeField] private StickerEntry[] stickerMappings;

        [Header("Tuning")]
        [SerializeField] private float barZone = 0.10f;
        [SerializeField] private float fadeZone = 0.20f;
        [SerializeField] private float stillThreshold = 0.3f;
        [SerializeField] private float confirmSpringDuration = 0.35f;
        [SerializeField] private float sweepDuration = 0.4f;
        [SerializeField] private float barOffscreenY = -200f;

        [Header("Colors")]
        [SerializeField] private Color p0Color = new Color(0.3f, 0.5f, 1f);
        [SerializeField] private Color p1Color = new Color(1f, 0.3f, 0.3f);
        [SerializeField] private Color validColor = new Color(0.3f, 1f, 0.3f, 0.7f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.5f);

        [System.Serializable]
        public struct StickerEntry
        {
            public DistrictType districtType;
            public Sprite sprite;
        }

        // ===== STATE =====

        private enum DragMode { Idle, Dragging, Placed, Repositioning }

        private DraftManager draftManager;
        private DraftState draftState;
        private int localPlayerID;
        private Camera mainCam;

        private DragMode dragMode = DragMode.Idle;
        private int activeSlotIndex = -1;
        private DistrictType activeDistrictType;

        // Preview
        private GameObject previewInstance;
        private Renderer previewRenderer;
        private int previewGridX = -1;
        private int previewGridZ = -1;
        private bool previewOnValidCell;

        // Still detection
        private float stillTimer;
        private Vector3 lastPreviewPos;
        private bool confirmVisible;

        // Slots
        private List<DraftSlotUI> slotDisplays = new List<DraftSlotUI>();
        private DraftSlotUI activeSlotUI;

        // Persistent placements
        private List<GameObject> persistentPlacements = new List<GameObject>();

        private Tween sweepTween;
        private Tween confirmTween;

        // ===== INITIALIZATION =====

        public void Initialize(DraftManager manager, int playerID)
        {
            draftManager = manager;
            localPlayerID = playerID;
            mainCam = Camera.main;

            // Start bar offscreen for sweep-in
            barPanel.anchoredPosition = new Vector2(barPanel.anchoredPosition.x, barOffscreenY);

            HideDragProxy();
            HideConfirm();
        }

        // ===== PUBLIC API (called by DraftManager) =====

        public void ShowInitialReveal(BoardConfig.InitialNodePlacement[] placements)
        {
            if (placements == null) return;
            for (int i = 0; i < placements.Length; i++)
            {
                Vector3 pos = draftManager.GridToWorld(placements[i].gridX, placements[i].gridZ);
                SpawnPersistentPlaceholder(pos, placements[i].districtType);
            }
        }

        public void SweepIn(DraftState state, int playerID)
        {
            draftState = state;
            localPlayerID = playerID;
            RebuildBar();
            UpdateTurnDisplay();

            sweepTween?.Kill();
            sweepTween = barPanel.DOAnchorPosY(0f, sweepDuration).SetEase(Ease.OutCubic);
        }

        public void SweepOut()
        {
            sweepTween?.Kill();
            sweepTween = barPanel.DOAnchorPosY(barOffscreenY, sweepDuration).SetEase(Ease.InCubic);
        }

        public void UpdateTimer(float remaining, float total)
        {
            if (total <= 0f) return;
            if (timerFill != null)
                timerFill.fillAmount = Mathf.Clamp01(remaining / total);
            if (timerText != null)
                timerText.text = Mathf.CeilToInt(Mathf.Max(0f, remaining)).ToString();
        }

        public void OnTurnChanged(DraftState state, int playerID)
        {
            draftState = state;
            CancelDrag();
            RebuildBar();
            UpdateTurnDisplay();
        }

        public void OnPlacementConfirmed(DraftPlacement placement)
        {
            Vector3 pos = draftManager.GridToWorld(placement.gridX, placement.gridZ);
            GameObject ph = SpawnPersistentPlaceholder(pos, placement.districtType);

            // Drop-from-above animation
            if (ph != null)
            {
                Vector3 finalPos = ph.transform.position;
                ph.transform.position = finalPos + Vector3.up * 6f;
                ph.transform.DOMove(finalPos, 0.4f).SetEase(Ease.InQuad)
                    .OnComplete(() => ph.transform.DOPunchScale(Vector3.one * 0.15f, 0.25f, 6));
            }

            CancelDrag();
            RebuildBar();
        }

        public List<GameObject> GetPersistentPlacements() => persistentPlacements;

        // ===== DRAG START (called by DraftSlotUI) =====

        public void BeginDrag(int slotIndex)
        {
            if (!draftManager.IsLocalPlayerTurn()) return;
            if (draftState == null) return;

            DraftSlot[] slots = draftState.GetPlayerSlots(localPlayerID);
            if (slotIndex < 0 || slotIndex >= slots.Length) return;
            if (slots[slotIndex].isConsumed) return;

            activeSlotIndex = slotIndex;
            activeDistrictType = slots[slotIndex].districtType;
            dragMode = DragMode.Dragging;
            confirmVisible = false;
            HideConfirm();

            // Dim the source slot
            activeSlotUI = FindSlotUI(slotIndex);
            if (activeSlotUI != null)
                activeSlotUI.SetDimmed(true);

            ShowDragProxy();
        }

        // ===== PER-FRAME UPDATE =====

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

            // --- Drag proxy visibility (fades out going up) ---
            float proxyAlpha;
            if (normalizedY < barZone)
                proxyAlpha = 1f;
            else if (normalizedY < fadeZone)
                proxyAlpha = 1f - ((normalizedY - barZone) / (fadeZone - barZone));
            else
                proxyAlpha = 0f;

            SetDragProxyAlpha(proxyAlpha);
            if (dragProxy != null)
                dragProxy.position = screenPos;

            // --- Preview lifecycle (created above 10%, destroyed below 10%) ---
            if (normalizedY < barZone)
            {
                // Below 10%: destroy preview
                DestroyPreview();
            }
            else
            {
                // Above 10%: ensure preview exists
                EnsurePreviewExists();
                UpdatePreviewPosition(screenPos);

                // Preview opacity (fades in from 10-20%)
                float previewAlpha;
                if (normalizedY < fadeZone)
                    previewAlpha = (normalizedY - barZone) / (fadeZone - barZone);
                else
                    previewAlpha = 1f;

                SetPreviewAlpha(previewAlpha);
            }

            // --- Release ---
            if (mouse.leftButton.wasReleasedThisFrame)
            {
                if (normalizedY < barZone)
                {
                    // Released in bar zone ? cancel
                    CancelDrag();
                }
                else if (previewOnValidCell)
                {
                    // Released on valid cell ? place
                    EnterPlacedState();
                }
                else
                {
                    // Released on invalid cell ? cancel
                    CancelDrag();
                }
                return;
            }

            // --- Cancel ---
            if (mouse.rightButton.wasPressedThisFrame || EscapePressed())
            {
                CancelDrag();
            }
        }

        private void UpdatePlaced()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // Still detection
            if (previewInstance != null)
            {
                if (Vector3.Distance(previewInstance.transform.position, lastPreviewPos) < 0.01f)
                    stillTimer += Time.deltaTime;
                else
                {
                    stillTimer = 0f;
                    lastPreviewPos = previewInstance.transform.position;
                }
            }
            else
            {
                stillTimer += Time.deltaTime;
            }

            // Show confirm after being still
            if (!confirmVisible && stillTimer >= stillThreshold)
            {
                ShowConfirm();
            }

            // Re-grab: pointer down but NOT on confirm button
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                {
                    // UI click — let EventSystem handle it (confirm button)
                    // Do nothing here
                }
                else if (IsPointerOnPreviewCell(mouse.position.ReadValue()))
                {
                    // Clicked on the placed node — re-grab
                    dragMode = DragMode.Repositioning;
                    HideConfirm();
                    ShowDragProxy();
                }
                return;
            }


            // Cancel
            if (mouse.rightButton.wasPressedThisFrame || EscapePressed())
            {
                CancelDrag();
            }
        }

        private void EnterPlacedState()
        {
            dragMode = DragMode.Placed;
            HideDragProxy();
            stillTimer = 0f;
            lastPreviewPos = previewInstance != null ? previewInstance.transform.position : Vector3.zero;
            confirmVisible = false;
        }

        // ===== CONFIRM =====

        private void ShowConfirm()
        {
            if (confirmButtonGO == null) return;
            confirmVisible = true;

            // Position in screen space above the placed preview
            Vector3 worldPos = draftManager.GridToWorld(previewGridX, previewGridZ);
            Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);

            confirmButtonGO.SetActive(true);
            RectTransform rect = confirmButtonGO.GetComponent<RectTransform>();
            if (rect != null)
                rect.position = screenPos + new Vector3(0f, 70f, 0f);

            // Spring animation
            confirmButtonGO.transform.localScale = Vector3.zero;
            confirmTween?.Kill();
            confirmTween = confirmButtonGO.transform
                .DOScale(Vector3.one, confirmSpringDuration)
                .SetEase(Ease.OutBack, 2.5f);
        }

        private void HideConfirm()
        {
            confirmVisible = false;
            confirmTween?.Kill();
            confirmTween = null;
            if (confirmButtonGO != null)
            {
                confirmButtonGO.SetActive(false);
                confirmButtonGO.transform.localScale = Vector3.zero;
            }
        }

        private void OnConfirmClicked()
        {
            if (dragMode != DragMode.Placed) return;
            if (activeSlotIndex < 0) return;
            if (!previewOnValidCell) return;

            draftManager.ConfirmLocalPlacement(activeSlotIndex, previewGridX, previewGridZ);

            // Clean up
            DestroyPreview();
            HideDragProxy();
            HideConfirm();
            if (activeSlotUI != null) activeSlotUI.SetDimmed(false);
            activeSlotUI = null;
            activeSlotIndex = -1;
            dragMode = DragMode.Idle;
        }

        // ===== CANCEL =====

        private void CancelDrag()
        {
            DestroyPreview();
            HideDragProxy();
            HideConfirm();
            if (activeSlotUI != null) activeSlotUI.SetDimmed(false);
            activeSlotUI = null;
            activeSlotIndex = -1;
            dragMode = DragMode.Idle;
        }

        // ===== PREVIEW MANAGEMENT =====

        private void EnsurePreviewExists()
        {
            if (previewInstance != null) return;
            if (placementPreviewPrefab == null) return;

            previewInstance = Instantiate(placementPreviewPrefab);
            previewRenderer = previewInstance.GetComponentInChildren<MeshRenderer>();
            SetPreviewSticker(activeDistrictType);
        }

        private void DestroyPreview()
        {
            if (previewInstance != null)
            {
                Destroy(previewInstance);
                previewInstance = null;
                previewRenderer = null;
            }
            previewOnValidCell = false;
        }

        private void UpdatePreviewPosition(Vector2 screenPos)
        {
            if (previewInstance == null) return;

            Ray ray = mainCam.ScreenPointToRay(screenPos);
            if (Mathf.Abs(ray.direction.y) < 0.0001f) { previewOnValidCell = false; return; }

            float t = -ray.origin.y / ray.direction.y;
            if (t < 0) { previewOnValidCell = false; return; }

            Vector3 worldHit = ray.origin + ray.direction * t;

            if (!draftManager.WorldToGrid(worldHit, out int gx, out int gz))
            {
                previewOnValidCell = false;
                previewInstance.transform.position = worldHit;
                SetPreviewTint(invalidColor);
                return;
            }

            previewOnValidCell = draftState.IsCellAvailable(gx, gz);
            previewGridX = gx;
            previewGridZ = gz;
            // In UpdatePreviewPosition, when setting position:
            previewInstance.transform.position = draftManager.GridToWorld(gx, gz) + Vector3.up * 0.1f;
            SetPreviewTint(previewOnValidCell ? validColor : invalidColor);
        }

        private void SetPreviewAlpha(float alpha)
        {
            if (previewRenderer == null) return;
            Color c = previewRenderer.material.color;
            c.a = alpha;
            previewRenderer.material.color = c;

            // Also fade sticker if present
            SpriteRenderer sr = previewInstance != null
                ? previewInstance.GetComponentInChildren<SpriteRenderer>()
                : null;
            if (sr != null)
            {
                Color sc = sr.color;
                sc.a = alpha;
                sr.color = sc;
            }
        }

        private void SetPreviewTint(Color tint)
        {
            if (previewRenderer == null) return;
            float currentAlpha = previewRenderer.material.color.a;
            tint.a = currentAlpha; // preserve alpha from zone logic
            previewRenderer.material.color = tint;
        }

        private void SetPreviewSticker(DistrictType type)
        {
            if (previewInstance == null) return;
            Sprite sprite = GetStickerSprite(type);
            if (sprite == null) return;

            Transform stickerChild = previewInstance.transform.Find("Sticker");
            SpriteRenderer sr = null;
            if (stickerChild != null) sr = stickerChild.GetComponent<SpriteRenderer>();
            if (sr == null) sr = previewInstance.GetComponentInChildren<SpriteRenderer>();
            if (sr != null) sr.sprite = sprite;
        }

        // ===== DRAG PROXY =====

        private void ShowDragProxy()
        {
            if (dragProxy == null) return;

            // Position BEFORE activating — prevents one-frame snap to old position
            Mouse mouse = Mouse.current;
            if (mouse != null)
                dragProxy.position = mouse.position.ReadValue();

            dragProxy.gameObject.SetActive(true);
            SetDragProxyAlpha(1f);
        }

        private void HideDragProxy()
        {
            if (dragProxy == null) return;
            dragProxy.gameObject.SetActive(false);
        }

        private void SetDragProxyAlpha(float alpha)
        {
            if (dragProxyCanvasGroup != null)
                dragProxyCanvasGroup.alpha = alpha;
        }

        // ===== BAR MANAGEMENT =====

        private void RebuildBar()
        {
            ClearBar();
            if (draftState == null) return;

            DraftSlot[] slots = draftState.GetPlayerSlots(localPlayerID);
            bool isMyTurn = draftManager.IsLocalPlayerTurn();

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].isConsumed) continue; // consumed = gone from bar

                GameObject go = Instantiate(draftSlotPrefab, barContainer);
                DraftSlotUI slotUI = go.GetComponent<DraftSlotUI>();
                if (slotUI != null)
                {
                    slotUI.Initialize(slots[i], i, this);
                    slotUI.SetInteractable(isMyTurn);
                }
                slotDisplays.Add(slotUI);
            }
        }

        private void ClearBar()
        {
            for (int i = 0; i < slotDisplays.Count; i++)
            {
                if (slotDisplays[i] != null && slotDisplays[i].gameObject != null)
                    Destroy(slotDisplays[i].gameObject);
            }
            slotDisplays.Clear();
        }

        private DraftSlotUI FindSlotUI(int slotIndex)
        {
            for (int i = 0; i < slotDisplays.Count; i++)
            {
                if (slotDisplays[i] != null && slotDisplays[i].SlotIndex == slotIndex)
                    return slotDisplays[i];
            }
            return null;
        }

        // ===== TURN DISPLAY =====

        private void UpdateTurnDisplay()
        {
            if (draftState == null) return;
            bool isMyTurn = (draftState.currentTurnPlayerID == localPlayerID);
            Color c = (draftState.currentTurnPlayerID == 0) ? p0Color : p1Color;

            if (turnText != null)
                turnText.text = isMyTurn ? "YOUR TURN" : "OPPONENT'S TURN";
            if (turnIndicatorBG != null)
                turnIndicatorBG.color = c;
        }

        // ===== PERSISTENT PLACEHOLDERS =====

        private GameObject SpawnPersistentPlaceholder(Vector3 pos, DistrictType type)
        {
            if (placementPreviewPrefab == null) return null;

            GameObject ph = Instantiate(placementPreviewPrefab);
            ph.transform.position = pos + Vector3.up * 0.1f;//bump up so visable over previewGrid prefab

            // Set sticker
            Sprite sprite = GetStickerSprite(type);
            if (sprite != null)
            {
                Transform stickerChild = ph.transform.Find("Sticker");
                SpriteRenderer sr = stickerChild != null
                    ? stickerChild.GetComponent<SpriteRenderer>()
                    : ph.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) sr.sprite = sprite;
            }

            // Full opacity, white tint
            MeshRenderer mr = ph.GetComponentInChildren<MeshRenderer>();
            if (mr != null) mr.material.color = new Color(1f, 1f, 1f, 0.85f);

            persistentPlacements.Add(ph);
            return ph;
        }

        // ===== HELPERS =====
        public Sprite GetStickerSprite(DistrictType type)
        {
            if (stickerMappings == null) return null;
            for (int i = 0; i < stickerMappings.Length; i++)
                if (stickerMappings[i].districtType == type)
                    return stickerMappings[i].sprite;
            return null;
        }

        private bool EscapePressed()
        {
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        }

        /// <summary>
        /// Returns true if the screen-space pointer position maps to the same
        /// grid cell as the currently placed preview. Camera-zoom-agnostic.
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

        // ===== SETUP/TEARDOWN =====

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        private void OnDestroy()
        {
            sweepTween?.Kill();
            confirmTween?.Kill();
            if (previewInstance != null) Destroy(previewInstance);
            if (confirmButton != null) confirmButton.onClick.RemoveAllListeners();
        }
    }
}