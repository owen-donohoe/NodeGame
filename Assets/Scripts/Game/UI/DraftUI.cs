using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using NodeWar.Simulation;
using NodeWar.Core;
using System.Collections.Generic;

namespace NodeWar.UI
{
    /// <summary>
    /// Facade and coordinator for the draft phase UI.
    /// 
    /// This is what DraftManager talks to. Public API matches what DraftManager expects:
    ///   ShowInitialReveal, SweepIn, SweepOut, UpdateTimer, OnTurnChanged, OnPlacementConfirmed
    /// 
    /// Owns:
    ///   - Bar panel (sweep animation, slot lifecycle)
    ///   - Timer/turn display
    ///   - Persistent placeholder tracking
    ///   - Sticker sprite registry
    ///   - Routing slot drag-starts to DraftPlacementController
    /// 
    /// Does NOT own: placement state machine, input, preview lifecycle, confirm button.
    /// Those live on DraftPlacementController and DraftConfirmPresenter.
    /// </summary>
    public class DraftUI : MonoBehaviour
    {
        [Header("Bar Panel")]
        [SerializeField] private RectTransform barPanel;
        [SerializeField] private RectTransform barContainer;
        [SerializeField] private GameObject draftSlotPrefab;

        [Header("Bar Animation")]
        [SerializeField] private float barSweepDuration = 0.4f;
        [SerializeField] private Ease barSweepInEase = Ease.OutCubic;
        [SerializeField] private Ease barSweepOutEase = Ease.InCubic;
        [SerializeField] private float barOffscreenY = -200f;

        [Header("Player Panel")]
        [Tooltip("Panel at top of screen containing turn indicator, timer, etc. Sweeps down on draft start.")]
        [SerializeField] private RectTransform playerPanel;
        [Tooltip("Offscreen Y position for player panel (positive = above screen).")]
        [SerializeField] private float playerPanelOffscreenY = 200f;

        [Header("Timer")]
        [SerializeField] private Image timerFill;
        [SerializeField] private TextMeshProUGUI timerText;

        [Header("Turn Indicator")]
        [SerializeField] private TextMeshProUGUI turnText;
        [SerializeField] private Image turnIndicatorBackground;

        [Header("Turn Colors")]
        [SerializeField] private Color player0Color = new Color(0.3f, 0.5f, 1f);
        [SerializeField] private Color player1Color = new Color(1f, 0.3f, 0.3f);

        [Header("Placement Controller")]
        [Tooltip("DraftPlacementController on this same GameObject.")]
        [SerializeField] private DraftPlacementController placementController;

        [Header("Confirmed Placement Prefab")]
        [Tooltip("World-space prefab for nodes that stay on board until game starts. Should look solid.")]
        [SerializeField] private GameObject confirmedPlacementPrefab;
        [Tooltip("Y offset for confirmed placements above grid.")]
        [SerializeField] private float confirmedPlacementYOffset = 0.1f;

        [Header("Confirmed Placement Animation")]
        [SerializeField] private float placementDropHeight = 6f;
        [SerializeField] private float placementDropDuration = 0.4f;
        [SerializeField] private Ease placementDropEase = Ease.InQuad;
        [SerializeField] private float placementBounceStrength = 0.15f;
        [SerializeField] private float placementBounceDuration = 0.25f;

        [Header("Sticker Mappings")]
        [SerializeField] private StickerEntry[] stickerMappings;

        [System.Serializable]
        public struct StickerEntry
        {
            public DistrictType districtType;
            public Sprite sprite;
        }

        // Runtime
        private DraftManager draftManager;
        private DraftState draftState;
        private int localPlayerID;

        private List<DraftSlotUI> slotDisplays = new List<DraftSlotUI>();
        private DraftSlotUI activeSlotUI;
        private List<GameObject> persistentPlacements = new List<GameObject>();
        private Tween sweepTween;
        private Tween playerPanelSweepTween;

        // ===== INITIALIZATION =====

        public void Initialize(DraftManager manager, int playerID)
        {
            draftManager = manager;
            localPlayerID = playerID;

            barPanel.anchoredPosition = new Vector2(barPanel.anchoredPosition.x, barOffscreenY);

            if (playerPanel != null)
                playerPanel.anchoredPosition = new Vector2(playerPanel.anchoredPosition.x, playerPanelOffscreenY);

            if (placementController != null)
            {
                placementController.Initialize(manager, playerID, GetStickerSprite);
                placementController.OnDragStarted += OnSlotDragStarted;
                placementController.OnDragCancelled += OnSlotDragCancelled;
            }
        }

        // ===== PUBLIC API (called by DraftManager) =====

        public void ShowInitialReveal(BoardConfig.InitialNodePlacement[] placements)
        {
            if (placements == null) return;
            for (int i = 0; i < placements.Length; i++)
            {
                Vector3 pos = draftManager.GridToWorld(placements[i].gridX, placements[i].gridZ);
                SpawnConfirmedPlaceholder(pos, placements[i].districtType);
            }
        }

        public void SweepIn(DraftState state, int playerID)
        {
            draftState = state;
            localPlayerID = playerID;

            if (placementController != null)
                placementController.SetDraftState(state);

            RebuildBar();
            UpdateTurnDisplay();

            sweepTween?.Kill();
            sweepTween = barPanel.DOAnchorPosY(0f, barSweepDuration).SetEase(barSweepInEase);

            if (playerPanel != null)
            {
                playerPanelSweepTween?.Kill();
                playerPanelSweepTween = playerPanel.DOAnchorPosY(0f, barSweepDuration).SetEase(barSweepInEase);
            }
        }

        public void SweepOut()
        {
            sweepTween?.Kill();
            sweepTween = barPanel.DOAnchorPosY(barOffscreenY, barSweepDuration).SetEase(barSweepOutEase);

            if (playerPanel != null)
            {
                playerPanelSweepTween?.Kill();
                playerPanelSweepTween = playerPanel.DOAnchorPosY(playerPanelOffscreenY, barSweepDuration).SetEase(barSweepOutEase);
            }
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

            if (placementController != null)
            {
                placementController.CancelDrag();
                placementController.SetDraftState(state);
            }

            RebuildBar();
            UpdateTurnDisplay();
        }

        public void OnPlacementConfirmed(DraftPlacement placement)
        {
            Vector3 pos = draftManager.GridToWorld(placement.gridX, placement.gridZ);
            SpawnConfirmedPlaceholder(pos, placement.districtType);

            // Cancel any active drag state and rebuild bar with consumed slot removed
            if (placementController != null)
                placementController.CancelDrag();
            RebuildBar();
        }

        public List<GameObject> GetPersistentPlacements() => persistentPlacements;

        // ===== SLOT INTERACTION (called by DraftSlotUI) =====

        /// <summary>
        /// Routes slot click to placement controller. Called by DraftSlotUI.OnPointerDown.
        /// </summary>
        public void BeginDrag(int slotIndex)
        {
            if (draftState == null) return;
            if (placementController == null) return;

            DraftSlot[] slots = draftState.GetPlayerSlots(localPlayerID);
            if (slotIndex < 0 || slotIndex >= slots.Length) return;
            if (slots[slotIndex].isConsumed) return;

            placementController.BeginDrag(slotIndex, slots[slotIndex].districtType);
        }

        private void OnSlotDragStarted(int slotIndex)
        {
            activeSlotUI = FindSlotUI(slotIndex);
            if (activeSlotUI != null)
                activeSlotUI.SetDimmed(true);
        }

        private void OnSlotDragCancelled()
        {
            if (activeSlotUI != null)
                activeSlotUI.SetDimmed(false);
            activeSlotUI = null;
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
                if (slots[i].isConsumed) continue;

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
            Color c = (draftState.currentTurnPlayerID == 0) ? player0Color : player1Color;

            if (turnText != null)
                turnText.text = isMyTurn ? "YOUR TURN" : "OPPONENT'S TURN";
            if (turnIndicatorBackground != null)
                turnIndicatorBackground.color = c;
        }

        // ===== PERSISTENT PLACEHOLDERS =====

        private void SpawnConfirmedPlaceholder(Vector3 pos, DistrictType type)
        {
            if (confirmedPlacementPrefab == null) return;

            GameObject ph = Instantiate(confirmedPlacementPrefab);
            ph.transform.position = pos + Vector3.up * confirmedPlacementYOffset;

            // Set sticker via component
            DraftPlacementPreview preview = ph.GetComponent<DraftPlacementPreview>();
            if (preview != null)
            {
                Sprite sprite = GetStickerSprite(type);
                preview.SetSticker(sprite);
                preview.SetTintWithAlpha(new Color(1f, 1f, 1f, 0.85f));
            }

            // Drop-from-above animation
            Vector3 finalPos = ph.transform.position;
            ph.transform.position = finalPos + Vector3.up * placementDropHeight;
            ph.transform.DOMove(finalPos, placementDropDuration)
                .SetEase(placementDropEase)
                .OnComplete(() => ph.transform.DOPunchScale(
                    Vector3.one * placementBounceStrength, placementBounceDuration, 6));

            persistentPlacements.Add(ph);
        }

        // ===== STICKER REGISTRY =====

        public Sprite GetStickerSprite(DistrictType type)
        {
            if (stickerMappings == null)
            {
                Debug.LogWarning("[DraftUI] stickerMappings array is null");
                return null;
            }
            for (int i = 0; i < stickerMappings.Length; i++)
            {
                if (stickerMappings[i].districtType == type)
                    return stickerMappings[i].sprite;
            }
            Debug.LogWarning("[DraftUI] No sticker mapping found for district type: " + type);
            return null;
        }

        // ===== CLEANUP =====

        private void OnDestroy()
        {
            sweepTween?.Kill();
            playerPanelSweepTween?.Kill();
            if (placementController != null)
            {
                placementController.OnDragStarted -= OnSlotDragStarted;
                placementController.OnDragCancelled -= OnSlotDragCancelled;
            }
        }
    }
}