using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using NodeWar.Simulation;
using NodeWar.Input;
using NodeWar.Debugging;
using NodeWar.View;
using UnityEngine.UI;

namespace NodeWar.UI
{
    /// <summary>
    /// Controls the sliding node panel. Opens on left-click of owned/enemy node
    /// (no villagers selected). Populates content based on district type.
    /// </summary>
    public class NodePanelManager : MonoBehaviour
    {
        [Header("Panel References")]
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI ownerIndicatorText;
        [SerializeField] private RectTransform contentArea;
        [SerializeField] private Button closeButton;

        [Header("Content Prefabs")]
        [Tooltip("Forge only. SetAllocation is the only command it accepts.")]
        [SerializeField] private GameObject forgeContentPrefab;

        [Tooltip("Core only. Respawn plus breach pressure.")]
        [SerializeField] private GameObject coreContentPrefab;

        [Tooltip("Shared by Barracks, Camp, Arsenal and Sanctuary -- the four " +
                 "districts CanEquipSuitAtNode accepts. The suit list differs " +
                 "per district; the panel does not.")]
        [SerializeField] private GameObject equipContentPrefab;

        [Header("Animation")]
        [SerializeField] private float slideDuration = 0.22f;
        [SerializeField] private Ease slideEase = Ease.OutCubic;

        [Header("Sheet")]
        [Tooltip("Anchor the panel to the bottom edge and slide it vertically. " +
                 "Off restores the original right-edge panel, which is the " +
                 "fallback if the prefab's child layout does not survive the move.")]
        [SerializeField] private bool useBottomSheet = true;

        [Tooltip("Height of the collapsed peek state, in pixels. Enough for a " +
                 "header strip and a grab handle.")]
        [SerializeField] private float peekHeight = 72f;

        [Header("Debug")]
        [Tooltip("Logs why the camera did or did not move when a panel opens.")]
        [SerializeField] private bool verbosePanelLogging = false;

        // State
        private SimulationState simState;
        private InputBuffer inputBuffer;
        private SelectionSystem selectionSystem;
        private DebugPlayerSwitch debugPlayerSwitch;
        private NodeWar.Core.ITickProvider tickProvider;
        private GameBalanceData balance;

        private int currentNodeID = -1;
        private bool isOpen = false;
        private float panelWidth;
        private GameObject currentContent;
        private Tween slideTween;

        private Camera mainCam;
        private LayerMask nodeLayer;

        private LayerMask villagerLayer;

        public void Initialize(SimulationState state, InputBuffer buffer,
                                SelectionSystem selection, DebugPlayerSwitch debugSwitch,
                                 NodeWar.Core.ITickProvider provider,
                                 GameBalanceData balanceData)
        {
            simState = state;
            inputBuffer = buffer;
            selectionSystem = selection;
            debugPlayerSwitch = debugSwitch;
            tickProvider = provider;
            balance = balanceData;
            mainCam = Camera.main;
            nodeLayer = LayerMask.GetMask("Nodes");
            villagerLayer = LayerMask.GetMask("Villagers");  // add this

            SetupSheetGeometry();

            panelWidth = panelRect.sizeDelta.x;
            panelRect.anchoredPosition = HiddenPosition;

            if (closeButton != null)
                closeButton.onClick.AddListener(ClosePanel);
        }

        // ===== SHEET GEOMETRY =====
        //
        // Anchoring is done from code rather than authored, so moving the panel
        // to the bottom edge needs no prefab edit and can be reverted with the
        // useBottomSheet toggle. What code cannot do is re-lay-out the panel's
        // children: they were arranged for a 320-wide column on the right, and
        // a full-width sheet will want that redone by hand.

        private bool isPeeking;

        /// <summary>
        /// Deliberately does not touch anchors, pivot or size.
        ///
        /// An earlier version forced anchorMin (0,0) / anchorMax (1,0) here,
        /// which destroys an anchor-driven height: authoring the sheet as
        /// min (0,0) max (1,0.2) means its height is 20% of the screen, and
        /// rewriting anchorMax.y to 0 converts that to a sizeDelta-driven
        /// panel whose sizeDelta.y is 0. The sheet disappears at runtime and
        /// the Editor still shows it correctly, which is the worst way for
        /// this to fail.
        ///
        /// Layout is authored; this component only slides what it is given.
        /// CurrentHeight reads the resolved rect, so anchor-driven and
        /// size-driven heights both work without the code knowing which.
        /// </summary>
        private void SetupSheetGeometry()
        {
            if (panelRect == null || !useBottomSheet) return;

            if (verbosePanelLogging)
                Debug.Log("[PANEL] Sheet geometry (authored): anchors " +
                          panelRect.anchorMin + "-" + panelRect.anchorMax +
                          " pivot " + panelRect.pivot +
                          " resolved height " + CurrentHeight);
        }

        /// <summary>
        /// Height read at call time, never cached -- the sheet's height varies
        /// with its content and the old panelWidth field is the cautionary tale.
        /// </summary>
        private float CurrentHeight => panelRect != null ? panelRect.rect.height : 0f;

        private Vector2 OpenPosition =>
            useBottomSheet
                ? new Vector2(0f, 0f)
                : new Vector2(0f, panelRect.anchoredPosition.y);

        private Vector2 HiddenPosition =>
            useBottomSheet
                ? new Vector2(0f, -CurrentHeight)
                : new Vector2(panelWidth, panelRect.anchoredPosition.y);

        /// <summary>
        /// Collapsed, not dismissed. Panning means the player wants to see the
        /// board, not that they are finished with the node -- the selection and
        /// its outline persist, and the sheet is one tap from full again.
        /// </summary>
        private Vector2 PeekPosition =>
            useBottomSheet
                ? new Vector2(0f, -(CurrentHeight - peekHeight))
                : OpenPosition;

        private Tween SlideTo(Vector2 target, Ease ease)
        {
            slideTween?.Kill();
            slideTween = panelRect.DOAnchorPos(target, slideDuration).SetEase(ease);
            return slideTween;
        }

        /// <summary>
        /// Collapses to peek on a pan. Deliberately does not move the camera:
        /// the two-state rule allows only "moved to clear the panel" or "did
        /// not", and peeking un-occluding the node must not start a chase.
        /// </summary>
        private void CollapseToPeek()
        {
            if (!isOpen || isPeeking || !useBottomSheet) return;

            isPeeking = true;
            SlideTo(PeekPosition, Ease.OutCubic);
        }

        private void Update()
        {
            if (simState == null) return;
            if (simState.gameOver && isOpen) { ClosePanel(); return; }

            HandleInput();
            RefreshContent();
        }

        private void HandleInput()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            // Close conditions
            if (isOpen)
            {
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                { ClosePanel(); return; }

                // Right-click closes panel but does NOT consume the click
                // (CommandSystem will also process the right-click for move commands)
                if (mouse != null && mouse.rightButton.wasPressedThisFrame)
                { ClosePanel(); }

                // Closing on a lasso is now driven by OnLassoBegin rather than
                // polled from SelectionSystem's drag radius, which no longer
                // exists -- the lasso is a gesture, not a mouse drag.
            }

            // Open condition: left click.
            // Skipped when routed -- TapRouter owns opening, and the defensive
            // villager raycast below exists only to avoid stealing clicks from
            // SelectionSystem, which the router settles in one place instead.
            if (gestureRouted) return;

            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            // Don't open if clicking UI elements
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            // Don't open if villagers are already selected
            if (selectionSystem != null && selectionSystem.SelectedVillagerIDs.Count > 0) return;

            // Don't open if this click is hitting a villager (selection takes priority)
            Vector2 screenPos = mouse.position.ReadValue();
            Ray ray = mainCam.ScreenPointToRay(screenPos);

            RaycastHit villagerHit;
            if (Physics.Raycast(ray, out villagerHit, 100f, villagerLayer))
            {
                // Click is on a villager � let SelectionSystem handle it, don't open panel
                if (isOpen) ClosePanel();
                return;
            }

            // Raycast for node
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f, nodeLayer))
            {
                NodeView nodeView = hit.collider.GetComponentInParent<NodeView>();
                if (nodeView != null)
                {
                    int nodeID = nodeView.GetNodeID();
                    NodeData node = simState.nodes[nodeID];

                    // Unclaimed nodes don't open panel
                    if (node.ownerID == -1) return;

                    OpenPanel(nodeID);
                }
            }
            else if (isOpen)
            {
                // Clicked away from any node � close
                ClosePanel();
            }
        }

        /// <summary>
        /// When true, a TapRouter decides when the panel opens and this
        /// component stops raycasting on its own. Gated rather than deleted so
        /// the legacy path stays available while the gesture stack is tuned.
        /// </summary>
        public void SetGestureRouted(bool routed)
        {
            gestureRouted = routed;
        }

        private bool gestureRouted = false;

        /// <summary>
        /// Starting a lasso closes the panel -- the player has moved on to
        /// selecting, and the sheet would otherwise sit over the area they are
        /// drawing in.
        /// </summary>
        public void SetGestureSource(NodeWar.Input.PointerGestureSource source)
        {
            if (gestureSource != null)
            {
                gestureSource.OnLassoBegin -= HandleLassoBegin;
                gestureSource.OnPanBegin -= HandlePanBegin;
            }

            gestureSource = source;

            if (gestureSource != null)
            {
                gestureSource.OnLassoBegin += HandleLassoBegin;
                gestureSource.OnPanBegin += HandlePanBegin;
            }
        }

        /// <summary>
        /// A pan collapses the sheet rather than dismissing it. The player is
        /// looking around, not finished with the node -- so the selection and
        /// its outline survive, and the sheet returns on the next selection.
        /// </summary>
        private void HandlePanBegin(Vector2 _)
        {
            CollapseToPeek();
        }

        private NodeWar.Input.PointerGestureSource gestureSource;

        private NodeWar.Core.CameraController cameraController;
        private NodeWar.View.NodeView[] nodeViews;

        public void SetCameraController(NodeWar.Core.CameraController controller)
        {
            cameraController = controller;
        }

        /// <summary>Node views by ID, so the panel can locate the node it is covering.</summary>
        public void SetNodeViews(NodeWar.View.NodeView[] views)
        {
            nodeViews = views;
        }

        /// <summary>
        /// The panel's rect in screen pixels, measured where the panel will be
        /// once open rather than where it currently sits.
        ///
        /// This distinction is the whole thing. Clearance is requested from
        /// OpenPanel, which runs the frame the slide-in tween is *created* --
        /// the panel is still parked off-screen at that moment, so measuring it
        /// in place returns a rect that contains nothing and the camera
        /// concludes it has no work to do.
        ///
        /// So the panel is moved to its open anchor, measured, and put back,
        /// all within this call. No frame renders in between.
        ///
        /// Size is read at call time and never cached: panel height varies with
        /// content, and a size captured once at Initialize is exactly what the
        /// old panelWidth field got wrong. On a Screen Space Overlay canvas the
        /// world corners are already screen coordinates.
        /// </summary>
        private Rect GetPanelScreenRect()
        {
            if (panelRect == null) return Rect.zero;

            Vector2 resting = panelRect.anchoredPosition;

            // OpenPosition covers both anchorings, so the occlusion test does
            // not need to know which one is in use.
            panelRect.anchoredPosition = OpenPosition;
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

            Vector3[] corners = new Vector3[4];
            panelRect.GetWorldCorners(corners);

            panelRect.anchoredPosition = resting;

            float minX = Mathf.Min(corners[0].x, corners[2].x);
            float maxX = Mathf.Max(corners[0].x, corners[2].x);
            float minY = Mathf.Min(corners[0].y, corners[2].y);
            float maxY = Mathf.Max(corners[0].y, corners[2].y);

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }

        private void HandleLassoBegin(Vector2 _)
        {
            if (isOpen) ClosePanel();
        }

        /// <summary>
        /// Opens the panel for a node identified by ID, applying the same
        /// ownership rule the click path used: an unclaimed node has nothing to
        /// show, so the tap closes any open panel instead of opening a new one.
        ///
        /// The functional-versus-informational split -- farms and mines opening
        /// no panel at all -- is a later commit on the panel branch. This
        /// preserves today's behaviour so the router changes what *routes*
        /// input, not what the panel decides.
        /// </summary>
        public void OpenForNode(int nodeID)
        {
            if (simState == null) return;
            if (nodeID < 0 || nodeID >= simState.nodes.Length) return;

            NodeData node = simState.nodes[nodeID];

            // Unclaimed: nothing to show and nothing to act on.
            if (node.ownerID == -1)
            {
                if (isOpen) ClosePanel();
                return;
            }

            // Informational districts never open a sheet. This is what makes an
            // open panel mean "there is something to press" -- a farm's state
            // belongs on the farm, not behind a sheet that covers the board.
            if (!DistrictPanelPolicy.IsFunctional(node.districtType))
            {
                if (isOpen) ClosePanel();
                return;
            }

            OpenPanel(nodeID);
        }

        public void OpenPanel(int nodeID)
        {
            if (currentNodeID == nodeID && isOpen) return;

            currentNodeID = nodeID;
            NodeData node = simState.nodes[nodeID];
            int controlledPID = debugPlayerSwitch != null ? debugPlayerSwitch.GetCurrentPlayerID() : 0;

            // Header
            headerText.text = GetDistrictName(node.districtType);

            // Owner indicator
            if (node.ownerID == 0)
            {
                ownerIndicatorText.text = "Owned by: Player 0";
                ownerIndicatorText.color = new Color(0.40f, 0.60f, 1f);
            }
            else if (node.ownerID == 1)
            {
                ownerIndicatorText.text = "Owned by: Player 1";
                ownerIndicatorText.color = new Color(1f, 0.40f, 0.40f);
            }
            else
            {
                ownerIndicatorText.text = "Unclaimed";
                ownerIndicatorText.color = new Color(0.5f, 0.5f, 0.5f);
            }

            // Destroy old content
            if (currentContent != null)
                Destroy(currentContent);

            // Spawn appropriate content
            bool isOwned = (node.ownerID == controlledPID);
            GameObject prefab = GetContentPrefab(node.districtType);
            if (prefab == null)
            {
                // Nothing to show. Bail before Instantiate throws, and leave the
                // sheet closed rather than sliding up an empty one.
                Debug.LogError("[PANEL] " + node.districtType +
                               " has no content prefab assigned on NodePanelManager.");
                currentNodeID = -1;
                return;
            }

            currentContent = Instantiate(prefab, contentArea);
            RectTransform contentRect = currentContent.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            // Initialize content script
            InitializeContent(node, isOwned, controlledPID);

            // Slide in. Also re-expands a peeked sheet: selecting a new node is
            // a request to see its panel, not to keep the collapsed one.
            if (!isOpen || isPeeking)
            {
                isOpen = true;
                isPeeking = false;

                // Height must be resolved before the target is computed, or the
                // sheet slides to an offset derived from the previous content.
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
                SlideTo(OpenPosition, slideEase);
            }

            RequestCameraClearance(nodeID);
        }

        /// <summary>
        /// Asks the camera to lift the node clear of the panel, but only if the
        /// panel actually covers it. A node already visible beside or above the
        /// sheet does not move the camera at all.
        ///
        /// The layout is rebuilt synchronously first: content was instantiated
        /// moments ago and Unity would not resolve its size until end of frame,
        /// so the occlusion test would otherwise run against a stale rect.
        /// </summary>
        private void RequestCameraClearance(int nodeID)
        {
            if (cameraController == null)
            {
                if (verbosePanelLogging)
                    Debug.LogWarning("[PANEL] No CameraController -- clearance skipped. " +
                                     "SetCameraController was never called.");
                return;
            }

            if (nodeViews == null || nodeID < 0 || nodeID >= nodeViews.Length)
            {
                if (verbosePanelLogging)
                    Debug.LogWarning("[PANEL] No node views (null=" + (nodeViews == null) +
                                     ", id=" + nodeID + ") -- clearance skipped.");
                return;
            }

            NodeWar.View.NodeView view = nodeViews[nodeID];
            if (view == null)
            {
                if (verbosePanelLogging)
                    Debug.LogWarning("[PANEL] Node view " + nodeID + " is null -- clearance skipped.");
                return;
            }

            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);

            Rect panelScreen = GetPanelScreenRect();

            if (verbosePanelLogging)
            {
                Debug.Log("[PANEL] Clearance for node " + nodeID +
                          " at world " + view.transform.position +
                          " | open-panel rect " + panelScreen +
                          " | screen " + Screen.width + "x" + Screen.height);
            }

            cameraController.BeginFocusSession();
            cameraController.FocusToClearPanel(view.transform.position, panelScreen);
        }

        public void ClosePanel()
        {
            if (!isOpen) return;
            isOpen = false;
            currentNodeID = -1;

            // Returns to the pre-focus position, but only if the player never
            // panned. Manual input is never undone automatically.
            if (cameraController != null)
                cameraController.EndFocusSession();

            isPeeking = false;

            SlideTo(HiddenPosition, Ease.InCubic)
                .OnComplete(() =>
                {
                    if (currentContent != null)
                        Destroy(currentContent);
                });
        }

        private void RefreshContent()
        {
            if (!isOpen || currentNodeID < 0) return;
            // Each content script handles its own per-frame refresh in its own Update()
        }

        private void InitializeContent(NodeData node, bool isOwned, int controlledPID)
        {
            // Try each content type
            // ProductionPanelContent and GenericPanelContent are deliberately
            // absent. Farms, mines and the passive districts open no panel at
            // all now, so nothing instantiates them -- and leaving the
            // GetComponent calls here would block deleting those scripts.
            ForgePanelContent forgeContent = currentContent.GetComponent<ForgePanelContent>();
            if (forgeContent != null)
            {
                forgeContent.Initialize(simState, tickProvider, inputBuffer, currentNodeID, controlledPID, isOwned);
                return;
            }

            CorePanelContent coreContent = currentContent.GetComponent<CorePanelContent>();
            if (coreContent != null)
            {
                coreContent.Initialize(simState, tickProvider, inputBuffer,
                                       currentNodeID, controlledPID, isOwned);
                return;
            }

            BarracksPanelContent barracksContent = currentContent.GetComponent<BarracksPanelContent>();
            if (barracksContent != null)
            {
                barracksContent.Initialize(simState, inputBuffer, balance,
                                           currentNodeID, controlledPID, isOwned);
                return;
            }

            Debug.LogWarning("[PANEL] Content prefab for " + node.districtType +
                             " has no recognised content script. Expected one of " +
                             "ForgePanelContent, CorePanelContent, BarracksPanelContent.");
        }

        /// <summary>
        /// Six functional districts, three prefabs. Only functional districts
        /// reach here -- OpenForNode filters the rest -- so an unmapped type is
        /// a bug rather than a case to absorb silently.
        /// </summary>
        private GameObject GetContentPrefab(DistrictType type)
        {
            switch (type)
            {
                case DistrictType.Forge: return forgeContentPrefab;
                case DistrictType.Core: return coreContentPrefab;

                case DistrictType.Barracks:
                case DistrictType.Camp:
                case DistrictType.Arsenal:
                case DistrictType.Sanctuary:
                    return equipContentPrefab;

                default:
                    Debug.LogWarning("[PANEL] No content prefab for " + type +
                                     ". DistrictPanelPolicy calls it functional " +
                                     "but nothing maps it -- the two have drifted.");
                    return null;
            }
        }

        private string GetDistrictName(DistrictType type)
        {
            switch (type)
            {
                case DistrictType.Farm: return "Farm";
                case DistrictType.Mine: return "Mine";
                case DistrictType.Forge: return "Forge";
                case DistrictType.Core: return "Core";
                case DistrictType.Barracks: return "Barracks";
                case DistrictType.Village: return "Village";
                case DistrictType.Camp: return "Camp";
                case DistrictType.Shrine: return "Shrine";
                case DistrictType.Arsenal: return "Arsenal";
                case DistrictType.Sanctuary: return "Sanctuary";
                case DistrictType.Watchtower: return "Watchtower";
                case DistrictType.Rampart: return "Rampart";
                case DistrictType.Market: return "Market";
                default: return "Crossroads";
            }
        }

        private void OnDestroy()
        {
            slideTween?.Kill();
            if (closeButton != null)
                closeButton.onClick.RemoveAllListeners();

            if (gestureSource != null)
            {
                gestureSource.OnLassoBegin -= HandleLassoBegin;
                gestureSource.OnPanBegin -= HandlePanBegin;
            }
        }
    }
}