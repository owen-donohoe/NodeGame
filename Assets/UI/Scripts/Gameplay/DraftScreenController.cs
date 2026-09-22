using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using DG.Tweening;
using NodeWar.Simulation;
using NodeWar.Core;

// SafeAreaBinder lives in NodeWar.Lobby because that is where it was first
// needed. Same borrow GameplayHUDController makes, for the same reason: it is
// a general utility and moving it belongs after the migration, not during it.
using NodeWar.Lobby;

namespace NodeWar.UI
{
    /// <summary>
    /// The UI Toolkit draft screen, after ingame-prototype.html. Counterpart to
    /// the uGUI DraftUI + DraftPlacementController pair, and live only when
    /// GameManager.useUIToolkitDraft is on - the same one-checkbox switch the
    /// HUD migration used, so the old draft stays one tick away.
    ///
    /// Namespace NodeWar.UI rather than NodeWar.Lobby, like GameplayHUDController
    /// and for the same reason: the folder says which stack, the namespace says
    /// which layer.
    ///
    /// ================= WHAT THIS OWNS =================
    ///
    /// The chrome (banner, timer, card bar, the proxy under the finger, the
    /// Confirm pair) AND the placement interaction, because on this stack they
    /// cannot be separated: the drag starts on a UI Toolkit element and ends on
    /// the 3D board, and one object has to hold both halves of it.
    ///
    /// It does NOT own the turn machine, the clock, the network or the rules.
    /// Those are DraftManager's, and this class only ever asks it two things:
    /// is it my turn, and please confirm this placement.
    ///
    /// ================= THE TWO INPUT ROUTES =================
    ///
    /// TAP a card to arm it, then tap a cell. DRAG a card onto a cell. Both end
    /// in the same parked state, which is why there is one `handSlot` and not a
    /// mode flag - a piece dragged out can then be moved by tapping a different
    /// cell, and neither route has a step the other lacks.
    ///
    /// A press on a card resolves late. Under the slop it was a tap; past it, a
    /// drag. Nothing visible happens until it is decided, because a card that
    /// armed itself on touch-down would flicker under every drag.
    ///
    /// ================= WHY POINTER AND NOT MOUSE =================
    ///
    /// The uGUI draft reads Mouse.current and cancels on right-click or Escape,
    /// neither of which a phone has. This reads Pointer.current, which is the
    /// mouse or the touchscreen, and the cancel is a ✕ next to Confirm and a
    /// drag back into the bar. That is the single biggest thing the prototype
    /// was asked to fix.
    ///
    /// The card press comes through UI Toolkit (so it knows which card) and the
    /// rest of the drag is read from Pointer.current in Update. The card
    /// captures the pointer so nothing else in the panel can claim the same
    /// finger part-way through a drag.
    ///
    /// ================= READ-ONLY =================
    ///
    /// This surface never touches SimulationState - the draft runs before there
    /// is one. It reads DraftState, which DraftManager hands it, and writes
    /// nothing anywhere. See .claude/rules/view-ui.md.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class DraftScreenController : MonoBehaviour, IDraftPresenter
    {
        [Tooltip("The draft layout. Assign DraftScreen.uxml.")]
        [SerializeField] private VisualTreeAsset draftLayout;

        [Header("World-space pieces")]
        [Tooltip("The translucent ghost shown on the cell under the finger. " +
                 "Same prefab the uGUI draft uses.")]
        [SerializeField] private GameObject ghostPreviewPrefab;

        [Tooltip("The solid piece that stays on the board once a placement is " +
                 "confirmed, until the real nodes replace it.")]
        [SerializeField] private GameObject confirmedPlacementPrefab;

        [Tooltip("Height above the grid plane for ghosts and placed pieces.")]
        [SerializeField] private float pieceYOffset = 0.1f;

        // Every piece wears its owner's colour, the ghost included, so the board
        // says whose piece is whose at a glance. That is why a blocked cell is
        // grey rather than the old red: red is player 2's colour, and a red
        // ghost over a taken cell would read as "this is theirs".
        [Header("Piece tints")]
        [Tooltip("Opacity of the ghost over a cell it can take. Its colour is the local player's.")]
        [SerializeField] private float ghostAlpha = 0.75f;

        [Tooltip("The ghost over a cell it cannot take, or off the grid.")]
        [SerializeField] private Color blockedCellTint = new Color(0.55f, 0.55f, 0.58f, 0.45f);

        [Tooltip("Opacity of a placed piece. Its colour is its owner's; an unowned " +
                 "initial node is left white.")]
        [SerializeField] private float placedAlpha = 0.9f;

        [Header("Placement drop")]
        [SerializeField] private float placementDropHeight = 6f;
        [SerializeField] private float placementDropDuration = 0.4f;
        [SerializeField] private Ease placementDropEase = Ease.InQuad;
        [SerializeField] private float placementBounceStrength = 0.15f;
        [SerializeField] private float placementBounceDuration = 0.25f;

        [Header("District art")]
        [Tooltip("Sticker sprite per district. Copied from the uGUI DraftUI prefab " +
                 "by Tools > Node War > Set Up UI Toolkit Draft.")]
        [SerializeField] private StickerEntry[] stickerMappings;

        [Tooltip("The lobby's NodeDefinitions, for display names. A district with " +
                 "no definition falls back to its enum name.")]
        [SerializeField] private NodeDefinition[] nodeDefinitions;

        [System.Serializable]
        public struct StickerEntry
        {
            public DistrictType districtType;
            public Sprite sprite;
        }

        /// <summary>
        /// How far above the parked cell the Confirm pair sits, in panel units.
        /// The prototype's 70px: far enough that a thumb on Confirm is not over
        /// the piece it is confirming.
        /// </summary>
        private const float ConfirmLift = 70f;

        /// <summary>
        /// Travel, in panel units, that turns a press into a drag. Below it the
        /// press is still a tap. Eight is the prototype's, and about a
        /// millimetre on a phone - under the jitter of a resting thumb.
        /// </summary>
        private const float DragSlop = 8f;

        /// <summary>Seconds left at which the clock starts reading as a threat.</summary>
        private const float LowClockSeconds = 5f;

        /// <summary>
        /// How long the chrome takes to sweep on or off. Must match the
        /// transition-duration on .draft__banner and .draft__bar in Draft.uss -
        /// it is only read to know when the exit has finished playing.
        /// </summary>
        private const float SweepSeconds = 0.4f;

        // ===== UI =====

        private UIDocument document;
        private VisualElement root;
        private SafeAreaBinder bannerSafeArea;
        private SafeAreaBinder barSafeArea;

        private VisualElement bannerInner;
        private VisualElement turnMark;
        private Label turnMarkLabel;
        private Label whoLabel;
        private Label secondsLabel;
        private VisualElement trackFill;

        private VisualElement bar;
        private Label barLabel;
        private VisualElement cards;
        private Label barEmpty;

        private VisualElement proxy;
        private VisualElement proxyTile;
        private Label proxyMonogram;
        private Label proxyName;

        private VisualElement confirmRow;
        private Button confirmOk;
        private Button confirmCancel;

        // ===== Runtime =====

        private DraftManager draftManager;
        private DraftState draftState;
        private int localPlayerID;
        private Camera cam;
        private bool initialized;

        /// <summary>
        /// The piece in hand, by slot index, however it got there - armed by a
        /// tap or parked by a drag. -1 is an empty hand. One field for both
        /// routes is what keeps them interchangeable.
        /// </summary>
        private int handSlot = -1;
        private DistrictType handDistrict;

        /// <summary>
        /// A parked piece is on a cell and waiting for Confirm. It is a real
        /// answer to "where" - DraftManager takes it at timeout rather than
        /// placing something random.
        /// </summary>
        private bool parked;
        private int parkedX = -1;
        private int parkedZ = -1;

        // The live drag, whether it started on a card or on a parked ghost.
        private bool dragging;
        private bool dragMoved;
        private bool dragFromBoard;
        private int dragSlot = -1;
        private DistrictType dragDistrict;
        private Vector2 dragStartScreen;
        private VisualElement dragCard;
        private int dragPointerId = -1;

        // Where the ghost currently is, and whether that cell could take it.
        private GameObject ghost;
        private DraftPlacementPreview ghostPreview;
        private int ghostX = -1;
        private int ghostZ = -1;
        private bool ghostOnValidCell;

        // A board press that has begun but not yet resolved into a tap.
        private bool boardPressActive;
        private bool boardPressOverUI;
        private Vector2 boardPressScreen;

        private readonly List<GameObject> persistentPlacements = new List<GameObject>();
        private readonly List<VisualElement> cardElements = new List<VisualElement>();

        private int lastSecondsShown = -1;
        private bool lastLowShown;
        private float lastTrackPercent = -1f;

        // ===== LIFECYCLE =====

        private void OnEnable()
        {
            BuildUI();
        }

        private void OnDisable()
        {
            // Not CancelHand: that touches the UI, which is being torn down.
            ReleaseDragCapture();
            DestroyGhost();
            initialized = false;
        }

        private void OnDestroy()
        {
            DestroyGhost();
        }

        private void BuildUI()
        {
            document = GetComponent<UIDocument>();

            if (document.panelSettings == null)
            {
                Debug.LogError("[Draft] UIDocument has no PanelSettings; nothing will render. " +
                               "Run Tools > Node War > Set Up UI Toolkit Draft.");
                return;
            }

            root = document.rootVisualElement;
            if (root == null) return;

            root.Clear();

            VisualTreeAsset layout = draftLayout != null ? draftLayout : document.visualTreeAsset;
            if (layout == null)
            {
                Debug.LogError("[Draft] No DraftScreen.uxml assigned, on either this component " +
                               "or the UIDocument.");
                return;
            }

            layout.CloneTree(root);

            VisualElement draftRoot = root.Q<VisualElement>("draft-root");
            if (draftRoot != null)
            {
                draftRoot.style.position = Position.Absolute;
                draftRoot.style.left = 0;
                draftRoot.style.right = 0;
                draftRoot.style.top = 0;
                draftRoot.style.bottom = 0;
            }

            bannerInner = root.Q<VisualElement>("draft-banner-inner");
            turnMark = root.Q<VisualElement>("draft-mark");
            turnMarkLabel = root.Q<Label>("draft-mark-label");
            whoLabel = root.Q<Label>("draft-who");
            secondsLabel = root.Q<Label>("draft-seconds");
            trackFill = root.Q<VisualElement>("draft-track-fill");

            bar = root.Q<VisualElement>("draft-bar");
            barLabel = root.Q<Label>("draft-bar-label");
            cards = root.Q<VisualElement>("draft-cards");
            barEmpty = root.Q<Label>("draft-bar-empty");

            proxy = root.Q<VisualElement>("draft-proxy");
            proxyTile = root.Q<VisualElement>("draft-proxy-tile");
            proxyMonogram = root.Q<Label>("draft-proxy-monogram");
            proxyName = root.Q<Label>("draft-proxy-name");

            confirmRow = root.Q<VisualElement>("draft-confirm");
            confirmOk = root.Q<Button>("draft-confirm-ok");
            confirmCancel = root.Q<Button>("draft-confirm-cancel");

            // The banner's fill reaches the top edge; only its contents inset,
            // so the notch sits on glass rather than on a seam. The bar is the
            // mirror of that at the bottom.
            if (bannerInner != null)
                bannerSafeArea = new SafeAreaBinder(bannerInner, SafeAreaBinder.Edges.Top);
            if (bar != null)
                barSafeArea = new SafeAreaBinder(bar, SafeAreaBinder.Edges.Bottom);

            if (confirmOk != null) confirmOk.clicked += HandleConfirm;
            if (confirmCancel != null) confirmCancel.clicked += HandleCancelPressed;

            HideConfirm();
            HideProxy();

            cam = Camera.main;
            initialized = true;
        }

        // ===== IDraftPresenter =====

        public void Initialize(DraftManager manager, int playerID)
        {
            draftManager = manager;
            localPlayerID = playerID;

            if (!initialized) BuildUI();
            if (cam == null) cam = Camera.main;

            // Off screen until ActiveDraft. Nothing is decided during
            // WaitingForReady or the reveal, so there is nothing to show.
            if (root != null) root.RemoveFromClassList("draft--on");
        }

        public void ShowInitialReveal(BoardConfigData.InitialNodePlacement[] placements)
        {
            if (placements == null || draftManager == null) return;

            for (int i = 0; i < placements.Length; i++)
            {
                Vector3 pos = draftManager.GridToWorld(placements[i].gridX, placements[i].gridZ);
                SpawnConfirmedPlaceholder(pos, placements[i].districtType, placements[i].ownerID);
            }
        }

        public void SweepIn(DraftState state, int playerID)
        {
            draftState = state;
            localPlayerID = playerID;

            RebuildCards();
            UpdateTurnDisplay();

            if (root != null) root.AddToClassList("draft--on");
        }

        public void SweepOut()
        {
            CancelHand();
            if (root != null) root.RemoveFromClassList("draft--on");

            // Off, but not before the sweep has finished playing. Deactivating
            // on the same frame would cut the animation the call exists to
            // start. The world pieces it spawned are not children of this
            // object, so they are untouched - MatchTransitionController owns
            // them from here.
            if (isActiveAndEnabled) StartCoroutine(HideAfterSweep());
        }

        private System.Collections.IEnumerator HideAfterSweep()
        {
            yield return new WaitForSeconds(SweepSeconds);
            gameObject.SetActive(false);
        }

        public void UpdateTimer(float remaining, float total)
        {
            if (total <= 0f) return;

            float clamped = Mathf.Max(0f, remaining);
            int seconds = Mathf.CeilToInt(clamped);
            bool low = clamped <= LowClockSeconds;

            // Every frame of ActiveDraft lands here. Writing a Label's text
            // allocates a string comparison and dirties layout, so the second
            // is only written when the second changes.
            if (seconds != lastSecondsShown)
            {
                lastSecondsShown = seconds;
                if (secondsLabel != null) secondsLabel.text = seconds.ToString();
            }

            if (low != lastLowShown)
            {
                lastLowShown = low;
                if (secondsLabel != null)
                    secondsLabel.EnableInClassList("draft__seconds--low", low);
                if (trackFill != null)
                    trackFill.EnableInClassList("draft__track-fill--low", low);
            }

            // Quantised to a tenth of a percent. The bar still drains smoothly
            // at any sane turn length, and the frames where the answer would be
            // identical no longer dirty the panel's layout.
            float percent = Mathf.Round(Mathf.Clamp01(clamped / total) * 1000f) / 10f;

            if (trackFill != null && !Mathf.Approximately(percent, lastTrackPercent))
            {
                lastTrackPercent = percent;
                trackFill.style.width = Length.Percent(percent);
            }
        }

        public void OnTurnChanged(DraftState state, int playerID)
        {
            draftState = state;

            // The turn passing ends everything in flight. A Confirm left
            // hanging into the opponent's turn would commit a placement the
            // rules no longer allow, and DraftManager would silently drop it -
            // which looks exactly like a broken button.
            CancelHand();

            RebuildCards();
            UpdateTurnDisplay();
        }

        public void OnPlacementConfirmed(DraftPlacement placement)
        {
            if (draftManager == null) return;

            Vector3 pos = draftManager.GridToWorld(placement.gridX, placement.gridZ);
            SpawnConfirmedPlaceholder(pos, placement.districtType, placement.playerID);

            CancelHand();
            RebuildCards();
        }

        public List<GameObject> GetPersistentPlacements()
        {
            return persistentPlacements;
        }

        public bool TryGetPendingPlacement(out int slotIndex, out int gridX, out int gridZ)
        {
            slotIndex = handSlot;
            gridX = parkedX;
            gridZ = parkedZ;

            // Only a parked piece counts. Mid-drag the finger is still moving
            // and the player has not chosen anything yet.
            return parked && handSlot >= 0 && parkedX >= 0 && parkedZ >= 0;
        }

        // ===== FRAME =====

        private void Update()
        {
            if (!initialized) return;

            if (bannerSafeArea != null) bannerSafeArea.Update();
            if (barSafeArea != null) barSafeArea.Update();

            if (draftManager == null) return;

            if (dragging) UpdateDrag();
            else ReadBoardPress();

            // The draft's own pinch zoom stays live, so a parked piece's cell
            // moves on screen under a camera that is still being flown. The
            // Confirm pair has to follow it or it points at the wrong cell.
            if (parked) PositionConfirm();
        }

        // ===== CARDS =====

        private void RebuildCards()
        {
            if (cards == null) return;

            cards.Clear();
            cardElements.Clear();

            if (draftState == null) return;

            DraftSlot[] slots = draftState.GetPlayerSlots(localPlayerID);
            int remaining = 0;

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].isConsumed) continue;

                remaining++;
                cards.Add(BuildCard(i, slots[i].districtType));
            }

            bool myTurn = draftManager != null && draftManager.IsLocalPlayerTurn();

            if (barLabel != null)
            {
                barLabel.text = remaining > 0
                    ? "YOUR PIECES · " + remaining + " LEFT"
                    : "ALL PLACED";
            }

            if (barEmpty != null)
                barEmpty.EnableInClassList("draft__bar-empty--on", remaining == 0);

            if (cards != null)
                cards.style.display = remaining == 0 ? DisplayStyle.None : DisplayStyle.Flex;

            // D4. The dim is the visible half; the controller refusing the
            // press is the half that matters, and both are driven from here so
            // they cannot disagree.
            if (bar != null)
                bar.EnableInClassList("draft__bar--inert", !myTurn);
        }

        /// <summary>
        /// One card. The element carries its SLOT index, not its position in
        /// the row: consumed slots are skipped when building, so the two part
        /// company the moment anything is placed, and DraftManager indexes by
        /// slot.
        /// </summary>
        private VisualElement BuildCard(int slotIndex, DistrictType district)
        {
            string name = DraftPieceInfo.DisplayName(district, nodeDefinitions);

            VisualElement card = new VisualElement();
            card.AddToClassList("draft__card");
            card.userData = slotIndex;

            VisualElement tile = new VisualElement();
            tile.AddToClassList("draft__card-tile");
            tile.AddToClassList(DraftPieceInfo.TintClass(district));
            tile.pickingMode = PickingMode.Ignore;

            Label monogram = new Label(DraftPieceInfo.Monogram(name));
            monogram.AddToClassList("draft__card-monogram");
            monogram.AddToClassList("ui-w600");
            monogram.pickingMode = PickingMode.Ignore;
            tile.Add(monogram);

            Label label = new Label(name);
            label.AddToClassList("draft__card-name");
            label.AddToClassList("ui-w600");
            label.pickingMode = PickingMode.Ignore;

            card.Add(tile);
            card.Add(label);

            card.RegisterCallback<PointerDownEvent>(OnCardPointerDown);

            if (handSlot == slotIndex && !parked)
                card.AddToClassList("draft__card--armed");

            cardElements.Add(card);
            return card;
        }

        private VisualElement FindCard(int slotIndex)
        {
            for (int i = 0; i < cardElements.Count; i++)
            {
                if (cardElements[i] != null && (int)cardElements[i].userData == slotIndex)
                    return cardElements[i];
            }
            return null;
        }

        private void SetArmedCard(int slotIndex)
        {
            for (int i = 0; i < cardElements.Count; i++)
            {
                if (cardElements[i] == null) continue;
                cardElements[i].EnableInClassList("draft__card--armed",
                    (int)cardElements[i].userData == slotIndex);
            }
        }

        private void SetCardDragging(int slotIndex, bool on)
        {
            VisualElement card = FindCard(slotIndex);
            if (card != null) card.EnableInClassList("draft__card--dragging", on);
        }

        // ===== INPUT: the card half =====

        private void OnCardPointerDown(PointerDownEvent evt)
        {
            if (draftManager == null || !draftManager.IsLocalPlayerTurn()) return;
            if (dragging) return;

            VisualElement card = evt.currentTarget as VisualElement;
            if (card == null) return;

            int slotIndex = (int)card.userData;
            if (draftState == null) return;

            DraftSlot[] slots = draftState.GetPlayerSlots(localPlayerID);
            if (slotIndex < 0 || slotIndex >= slots.Length) return;
            if (slots[slotIndex].isConsumed) return;

            // The capture is not how the drag is read - that is Pointer.current
            // in Update. It is so no other element in the panel can claim this
            // finger once it leaves the card and goes out over the board.
            card.CapturePointer(evt.pointerId);
            evt.StopPropagation();

            BeginDrag(slotIndex, slots[slotIndex].districtType, card, evt.pointerId, false);
        }

        private void BeginDrag(int slotIndex, DistrictType district, VisualElement card,
                               int pointerId, bool fromBoard)
        {
            dragging = true;
            dragMoved = false;
            dragFromBoard = fromBoard;
            dragSlot = slotIndex;
            dragDistrict = district;
            dragCard = card;
            dragPointerId = pointerId;
            dragStartScreen = CurrentPointerScreen();
        }

        // ===== INPUT: the board half =====

        /// <summary>
        /// A press on the board, read raw because the board is not a UI Toolkit
        /// element. Two things can come of it: picking a parked piece back up,
        /// or dropping the piece in hand onto a free cell.
        ///
        /// A press that lands on the bar or on Confirm is not a board press at
        /// all. panel.Pick skips every element whose picking-mode is Ignore, and
        /// this surface sets Ignore on all of them but the controls - so a
        /// non-null hit means a real control and nothing else does.
        /// </summary>
        private void ReadBoardPress()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null) return;
            if (draftState == null) return;
            if (!draftManager.IsLocalPlayerTurn()) { boardPressActive = false; return; }

            if (pointer.press.wasPressedThisFrame)
            {
                boardPressActive = true;
                boardPressScreen = pointer.position.ReadValue();
                boardPressOverUI = IsOverUI(boardPressScreen);

                if (!boardPressOverUI && parked && PointerOnCell(boardPressScreen, parkedX, parkedZ))
                {
                    // Picking the parked piece back up. It stays in hand, so
                    // releasing without travelling leaves it exactly where it
                    // was rather than cancelling it.
                    HideConfirm();
                    BeginDrag(handSlot, handDistrict, FindCard(handSlot), -1, true);
                    boardPressActive = false;
                }

                return;
            }

            if (!boardPressActive) return;

            if (pointer.press.wasReleasedThisFrame)
            {
                boardPressActive = false;

                if (boardPressOverUI) return;

                Vector2 screen = pointer.position.ReadValue();

                // A press that travelled was a camera gesture, not a tap. The
                // draft leaves pinch-zoom live, and a zoom that also placed a
                // piece would be unusable.
                if (Vector2.Distance(screen, boardPressScreen) > DragSlop) return;

                if (handSlot < 0) return;
                if (!ScreenToCell(screen, out int gx, out int gz)) return;
                if (!draftState.IsCellAvailable(gx, gz)) return;

                ParkAt(gx, gz);
            }
        }

        // ===== THE DRAG =====

        private void UpdateDrag()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null) { EndDrag(false); return; }

            Vector2 screen = pointer.position.ReadValue();

            if (!dragMoved)
            {
                if (Vector2.Distance(screen, dragStartScreen) < DragSlop)
                {
                    // Still undecided. Only a release can settle it.
                    if (pointer.press.wasReleasedThisFrame) EndDrag(true);
                    return;
                }

                dragMoved = true;

                // A new drag drops whatever was parked. The piece is in the
                // air again and the old cell is no longer an answer.
                parked = false;
                HideConfirm();

                if (!dragFromBoard)
                {
                    SetCardDragging(dragSlot, true);
                    ShowProxy(dragDistrict);
                }
            }

            bool overBar = IsOverBar(screen);

            if (!dragFromBoard)
            {
                MoveProxy(screen);
                // D5: the proxy is the piece while it is in the bar, the ghost
                // is the piece once it is over the board. Never both.
                proxy.EnableInClassList("draft__proxy--faded", !overBar);
            }

            if (overBar)
            {
                DestroyGhost();
            }
            else
            {
                EnsureGhost();
                MoveGhost(screen);
            }

            if (pointer.press.wasReleasedThisFrame)
                EndDrag(false);
        }

        /// <summary>
        /// The end of a press on a card or on a parked piece. A press that never
        /// travelled is a tap; one that did is a drop.
        /// </summary>
        private void EndDrag(bool asTap)
        {
            int slot = dragSlot;
            DistrictType district = dragDistrict;
            bool moved = dragMoved;
            bool fromBoard = dragFromBoard;
            bool landedValid = ghostOnValidCell;
            int landX = ghostX;
            int landZ = ghostZ;

            ReleaseDragCapture();

            if (!fromBoard) SetCardDragging(slot, false);

            dragging = false;
            dragMoved = false;
            dragFromBoard = false;
            dragSlot = -1;
            dragCard = null;

            // A press consumed as a drag is not also a board tap. Without this
            // a half-open board press could survive the drag and be settled by
            // the next release, against a start point from a gesture ago.
            boardPressActive = false;

            HideProxy();

            if (asTap || !moved)
            {
                DestroyGhost();

                if (fromBoard)
                {
                    // A tap on the parked piece changes nothing. Put the
                    // Confirm back and leave the piece where it is.
                    if (parked) { ShowConfirm(); return; }
                }

                // Tapping the armed card again disarms it; tapping a different
                // one moves the hand.
                if (handSlot == slot && !parked) ClearHand();
                else ArmSlot(slot, district);

                return;
            }

            if (!landedValid)
            {
                // Released in the bar, off the grid, or on a taken cell. The
                // piece goes back; nothing is parked.
                DestroyGhost();
                ClearHand();
                return;
            }

            handSlot = slot;
            handDistrict = district;
            ParkAt(landX, landZ);
        }

        private void ReleaseDragCapture()
        {
            if (dragCard != null && dragPointerId >= 0 && dragCard.HasPointerCapture(dragPointerId))
                dragCard.ReleasePointer(dragPointerId);

            dragPointerId = -1;
        }

        // ===== HAND =====

        private void ArmSlot(int slotIndex, DistrictType district)
        {
            handSlot = slotIndex;
            handDistrict = district;
            parked = false;
            parkedX = -1;
            parkedZ = -1;

            DestroyGhost();
            HideConfirm();
            SetArmedCard(slotIndex);
        }

        private void ClearHand()
        {
            handSlot = -1;
            parked = false;
            parkedX = -1;
            parkedZ = -1;

            DestroyGhost();
            HideConfirm();
            SetArmedCard(-1);
        }

        /// <summary>
        /// Everything in flight, stopped. Used when the turn passes, when a
        /// placement lands and when the draft ends - all three of which can
        /// happen while a finger is down.
        /// </summary>
        private void CancelHand()
        {
            ReleaseDragCapture();

            if (dragging && !dragFromBoard) SetCardDragging(dragSlot, false);

            dragging = false;
            dragMoved = false;
            dragFromBoard = false;
            dragSlot = -1;
            dragCard = null;
            boardPressActive = false;

            HideProxy();
            ClearHand();
        }

        /// <summary>
        /// D6. The one place a placement becomes pending, whichever route put
        /// it there - so a drag and a tap cannot drift apart.
        /// </summary>
        private void ParkAt(int gridX, int gridZ)
        {
            if (draftState == null || !draftState.IsCellAvailable(gridX, gridZ)) return;

            parked = true;
            parkedX = gridX;
            parkedZ = gridZ;

            EnsureGhost();
            PlaceGhostOnCell(gridX, gridZ, true);

            // Stays armed: the piece is still in hand, so tapping a different
            // cell moves it rather than starting over.
            SetArmedCard(handSlot);
            ShowConfirm();
        }

        // ===== CONFIRM =====

        private void HandleConfirm()
        {
            if (!parked) return;
            if (handSlot < 0) return;
            if (draftManager == null) return;

            int slot = handSlot;
            int gx = parkedX;
            int gz = parkedZ;

            // Cleared first. ConfirmLocalPlacement can refuse - the cell may
            // have been taken, or the turn may have passed in the same frame -
            // and it says so by doing nothing. Clearing first means the second
            // of two fast presses finds an empty hand and stops, and a refused
            // placement leaves its card back in the bar on the next rebuild
            // rather than a ghost stranded on the board.
            ClearHand();

            draftManager.ConfirmLocalPlacement(slot, gx, gz);
        }

        private void HandleCancelPressed()
        {
            // ✕ puts the piece back in the bar entirely, rather than leaving it
            // armed. A player who pressed cancel wants out of the placement,
            // not a half-step back into it.
            ClearHand();
        }

        private void ShowConfirm()
        {
            if (confirmRow == null) return;
            PositionConfirm();
            confirmRow.AddToClassList("draft__confirm--on");
        }

        private void HideConfirm()
        {
            if (confirmRow == null) return;
            confirmRow.RemoveFromClassList("draft__confirm--on");
        }

        /// <summary>
        /// Puts the Confirm pair above the parked cell, in panel coordinates
        /// derived from the cell's world position - so it tracks a camera that
        /// is still moving.
        ///
        /// The row is centred on the cell by translating itself half its own
        /// width, which is only known after a layout pass; until then its width
        /// is NaN and the translate would be dropped. Reading it every frame
        /// costs nothing and is correct from the first frame it can be.
        /// </summary>
        private void PositionConfirm()
        {
            if (confirmRow == null || draftManager == null) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Vector3 world = draftManager.GridToWorld(parkedX, parkedZ) + Vector3.up * pieceYOffset;

            // Behind the camera, where WorldToPanel mirrors the point to the
            // wrong side of the screen. Hidden rather than un-parked, and with
            // visibility rather than the --on class: the piece is still parked
            // and the button must come back by itself when the camera swings
            // round, which dropping the class would not do.
            bool behind = cam.WorldToViewportPoint(world).z <= 0f;
            confirmRow.style.visibility = behind ? Visibility.Hidden : Visibility.Visible;
            if (behind) return;

            Vector2 panelPos = RuntimePanelUtils.CameraTransformWorldToPanel(
                confirmRow.panel, world, cam);

            float width = confirmRow.resolvedStyle.width;
            float height = confirmRow.resolvedStyle.height;
            if (float.IsNaN(width)) width = 0f;
            if (float.IsNaN(height)) height = 0f;

            float left = panelPos.x - width * 0.5f;
            float top = panelPos.y - ConfirmLift - height;

            // A cell near the top of the screen would push the pair off it.
            // Clamped into the panel rather than hidden: the player needs the
            // button more than they need it in its authored place.
            float panelWidth = root.resolvedStyle.width;
            float panelHeight = root.resolvedStyle.height;

            if (!float.IsNaN(panelWidth) && panelWidth > 0f)
                left = Mathf.Clamp(left, 8f, Mathf.Max(8f, panelWidth - width - 8f));
            if (!float.IsNaN(panelHeight) && panelHeight > 0f)
                top = Mathf.Clamp(top, 8f, Mathf.Max(8f, panelHeight - height - 8f));

            confirmRow.style.left = left;
            confirmRow.style.top = top;
        }

        // ===== PROXY =====

        private void ShowProxy(DistrictType district)
        {
            if (proxy == null) return;

            string name = DraftPieceInfo.DisplayName(district, nodeDefinitions);

            if (proxyName != null) proxyName.text = name;
            if (proxyMonogram != null) proxyMonogram.text = DraftPieceInfo.Monogram(name);

            if (proxyTile != null)
            {
                for (int i = 0; i < ItemTint.Count; i++)
                    proxyTile.RemoveFromClassList("ui-tile-tint--" + i);
                proxyTile.AddToClassList(DraftPieceInfo.TintClass(district));
            }

            proxy.AddToClassList("draft__proxy--on");
            proxy.RemoveFromClassList("draft__proxy--faded");
        }

        private void HideProxy()
        {
            if (proxy == null) return;
            proxy.RemoveFromClassList("draft__proxy--on");
            proxy.RemoveFromClassList("draft__proxy--faded");
        }

        private void MoveProxy(Vector2 screenPos)
        {
            if (proxy == null || root == null) return;

            Vector2 panelPos = ScreenToPanel(screenPos);

            float width = proxy.resolvedStyle.width;
            float height = proxy.resolvedStyle.height;
            if (float.IsNaN(width)) width = 68f;
            if (float.IsNaN(height)) height = 64f;

            // Held above the finger, not under it. A card centred on the touch
            // point is a card you cannot see while you drag it.
            proxy.style.left = panelPos.x - width * 0.5f;
            proxy.style.top = panelPos.y - height - 12f;
        }

        // ===== GHOST =====

        private void EnsureGhost()
        {
            if (ghost != null) return;
            if (ghostPreviewPrefab == null) return;

            ghost = Instantiate(ghostPreviewPrefab);
            ghostPreview = ghost.GetComponent<DraftPlacementPreview>();

            if (ghostPreview != null)
            {
                ghostPreview.SetSticker(GetStickerSprite(dragging ? dragDistrict : handDistrict));
                ghostPreview.SetTintWithAlpha(GhostTint(true));
            }
        }

        // With alpha, not SetTint: SetTint keeps whatever alpha the material
        // already has, so the ghost never became see-through at all.
        private Color GhostTint(bool valid)
        {
            if (!valid) return blockedCellTint;

            Color tint = NodeWar.View.PlayerColors.For(localPlayerID);
            tint.a = ghostAlpha;
            return tint;
        }

        private void DestroyGhost()
        {
            if (ghost != null) Destroy(ghost);

            ghost = null;
            ghostPreview = null;
            ghostX = -1;
            ghostZ = -1;
            ghostOnValidCell = false;
        }

        private void MoveGhost(Vector2 screenPos)
        {
            if (ghost == null) return;

            if (!ScreenToWorldOnGround(screenPos, out Vector3 world))
            {
                ghostOnValidCell = false;
                return;
            }

            if (!draftManager.WorldToGrid(world, out int gx, out int gz))
            {
                // Off the grid entirely. The ghost follows the finger anyway,
                // marked bad, rather than sticking to the last legal cell - a
                // ghost that will not move reads as a frozen game.
                ghostOnValidCell = false;
                ghost.transform.position = world + Vector3.up * pieceYOffset;
                if (ghostPreview != null) ghostPreview.SetTintWithAlpha(GhostTint(false));
                return;
            }

            PlaceGhostOnCell(gx, gz, draftState != null && draftState.IsCellAvailable(gx, gz));
        }

        private void PlaceGhostOnCell(int gx, int gz, bool valid)
        {
            if (ghost == null) return;

            ghostX = gx;
            ghostZ = gz;
            ghostOnValidCell = valid;

            ghost.transform.position = draftManager.GridToWorld(gx, gz) + Vector3.up * pieceYOffset;

            if (ghostPreview != null)
                ghostPreview.SetTintWithAlpha(GhostTint(valid));
        }

        // ===== CONFIRMED PIECES =====

        private void SpawnConfirmedPlaceholder(Vector3 pos, DistrictType type, int ownerID)
        {
            if (confirmedPlacementPrefab == null) return;

            GameObject piece = Instantiate(confirmedPlacementPrefab);
            piece.transform.position = pos + Vector3.up * pieceYOffset;

            DraftPlacementPreview preview = piece.GetComponent<DraftPlacementPreview>();
            if (preview != null)
            {
                preview.SetSticker(GetStickerSprite(type));

                Color tint = ownerID >= 0 ? NodeWar.View.PlayerColors.For(ownerID) : Color.white;
                tint.a = placedAlpha;
                preview.SetTintWithAlpha(tint);
            }

            // D7: it drops onto the cell. Both players' pieces do, which is how
            // the board fills as the draft runs.
            Vector3 final = piece.transform.position;
            piece.transform.position = final + Vector3.up * placementDropHeight;
            piece.transform.DOMove(final, placementDropDuration)
                .SetEase(placementDropEase)
                .OnComplete(() => piece.transform.DOPunchScale(
                    Vector3.one * placementBounceStrength, placementBounceDuration, 6));

            persistentPlacements.Add(piece);
        }

        private Sprite GetStickerSprite(DistrictType type)
        {
            if (stickerMappings == null) return null;

            for (int i = 0; i < stickerMappings.Length; i++)
            {
                if (stickerMappings[i].districtType == type)
                    return stickerMappings[i].sprite;
            }

            return null;
        }

        // ===== TURN DISPLAY =====

        private void UpdateTurnDisplay()
        {
            if (draftState == null) return;

            int turnPlayer = draftState.currentTurnPlayerID;
            bool mine = turnPlayer == localPlayerID;

            if (whoLabel != null)
                whoLabel.text = mine ? "YOUR TURN" : "OPPONENT'S TURN";

            // D1 and §2.2: the turn owner's colour never travels alone. The
            // square-with-1 / circle-with-2 mark is the channel that survives
            // colour blindness and a washed-out phone screen in sunlight.
            if (turnMark != null)
            {
                turnMark.EnableInClassList("ui-mark--p0", turnPlayer == 0);
                turnMark.EnableInClassList("ui-mark--p1", turnPlayer == 1);
            }

            if (turnMarkLabel != null)
                turnMarkLabel.text = (turnPlayer + 1).ToString();
        }

        // ===== SCREEN / PANEL / WORLD =====

        private Vector2 CurrentPointerScreen()
        {
            Pointer pointer = Pointer.current;
            return pointer != null ? pointer.position.ReadValue() : Vector2.zero;
        }

        /// <summary>
        /// Screen pixels to panel units. The Y flip is the whole subtlety: a
        /// screen point has its origin bottom-left and a panel point top-left.
        /// </summary>
        private Vector2 ScreenToPanel(Vector2 screenPos)
        {
            if (root == null || root.panel == null) return Vector2.zero;

            return RuntimePanelUtils.ScreenToPanel(
                root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
        }

        /// <summary>
        /// Whether a real control is under this screen point. Pick walks past
        /// every element whose picking-mode is Ignore, and this surface marks
        /// all of its containers Ignore, so a non-null result is the bar, a card
        /// or one of the two confirm buttons and can be nothing else.
        /// </summary>
        private bool IsOverUI(Vector2 screenPos)
        {
            if (root == null || root.panel == null) return false;
            return root.panel.Pick(ScreenToPanel(screenPos)) != null;
        }

        /// <summary>
        /// The bar's own rectangle, asked directly rather than through Pick.
        /// During a drag the card has the pointer captured, so Pick would still
        /// answer for the card wherever the finger went - and "is the finger
        /// back over the tray" is a question about geometry, not about capture.
        /// </summary>
        private bool IsOverBar(Vector2 screenPos)
        {
            if (bar == null) return false;

            Rect rect = bar.worldBound;
            if (float.IsNaN(rect.width) || rect.width <= 0f) return false;

            return rect.Contains(ScreenToPanel(screenPos));
        }

        private bool ScreenToWorldOnGround(Vector2 screenPos, out Vector3 world)
        {
            if (cam == null) cam = Camera.main;
            return CameraController.TryScreenToGroundPoint(cam, screenPos, out world);
        }

        private bool ScreenToCell(Vector2 screenPos, out int gridX, out int gridZ)
        {
            gridX = -1;
            gridZ = -1;

            if (!ScreenToWorldOnGround(screenPos, out Vector3 world)) return false;
            return draftManager.WorldToGrid(world, out gridX, out gridZ);
        }

        private bool PointerOnCell(Vector2 screenPos, int gridX, int gridZ)
        {
            if (!ScreenToCell(screenPos, out int gx, out int gz)) return false;
            return gx == gridX && gz == gridZ;
        }
    }
}
