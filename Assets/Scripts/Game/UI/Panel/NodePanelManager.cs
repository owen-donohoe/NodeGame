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
        [SerializeField] private GameObject farmContentPrefab;
        [SerializeField] private GameObject mineContentPrefab;
        [SerializeField] private GameObject forgeContentPrefab;
        [SerializeField] private GameObject coreContentPrefab;
        [SerializeField] private GameObject barracksContentPrefab;
        [SerializeField] private GameObject genericContentPrefab;

        [Header("Animation")]
        [SerializeField] private float slideDuration = 0.22f;
        [SerializeField] private Ease slideEase = Ease.OutCubic;

        // State
        private SimulationState simState;
        private InputBuffer inputBuffer;
        private SelectionSystem selectionSystem;
        private DebugPlayerSwitch debugPlayerSwitch;
        private NodeWar.Core.ITickProvider tickProvider;

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
                                 NodeWar.Core.ITickProvider provider)
        {
            simState = state;
            inputBuffer = buffer;
            selectionSystem = selection;
            debugPlayerSwitch = debugSwitch;
            tickProvider = provider;
            mainCam = Camera.main;
            nodeLayer = LayerMask.GetMask("Nodes");
            villagerLayer = LayerMask.GetMask("Villagers");  // add this

            panelWidth = panelRect.sizeDelta.x;
            panelRect.anchoredPosition = new Vector2(panelWidth, panelRect.anchoredPosition.y);

            if (closeButton != null)
                closeButton.onClick.AddListener(ClosePanel);
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
                gestureSource.OnLassoBegin -= HandleLassoBegin;

            gestureSource = source;

            if (gestureSource != null)
                gestureSource.OnLassoBegin += HandleLassoBegin;
        }

        private NodeWar.Input.PointerGestureSource gestureSource;

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

            if (simState.nodes[nodeID].ownerID == -1)
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
            currentContent = Instantiate(prefab, contentArea);
            RectTransform contentRect = currentContent.GetComponent<RectTransform>();
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = Vector2.one;
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;

            // Initialize content script
            InitializeContent(node, isOwned, controlledPID);

            // Slide in
            if (!isOpen)
            {
                isOpen = true;
                slideTween?.Kill();
                slideTween = panelRect.DOAnchorPosX(0f, slideDuration).SetEase(slideEase);
            }
        }

        public void ClosePanel()
        {
            if (!isOpen) return;
            isOpen = false;
            currentNodeID = -1;

            slideTween?.Kill();
            slideTween = panelRect.DOAnchorPosX(panelWidth, slideDuration)
                .SetEase(Ease.InCubic)
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
            ProductionPanelContent prodContent = currentContent.GetComponent<ProductionPanelContent>();
            if (prodContent != null)
            {
                prodContent.Initialize(simState, tickProvider, currentNodeID, controlledPID, isOwned);
                return;
            }

            ForgePanelContent forgeContent = currentContent.GetComponent<ForgePanelContent>();
            if (forgeContent != null)
            {
                forgeContent.Initialize(simState, tickProvider, inputBuffer, currentNodeID, controlledPID, isOwned);
                return;
            }

            CorePanelContent coreContent = currentContent.GetComponent<CorePanelContent>();
            if (coreContent != null)
            {
                coreContent.Initialize(simState, tickProvider, inputBuffer, currentNodeID, controlledPID);
                return;
            }

            BarracksPanelContent barracksContent = currentContent.GetComponent<BarracksPanelContent>();
            if (barracksContent != null)
            {
                barracksContent.Initialize(simState, inputBuffer, currentNodeID, controlledPID, isOwned);
                return;
            }

            GenericPanelContent genericContent = currentContent.GetComponent<GenericPanelContent>();
            if (genericContent != null)
            {
                genericContent.Initialize(simState, currentNodeID, controlledPID);
            }
        }

        private GameObject GetContentPrefab(DistrictType type)
        {
            switch (type)
            {


                case DistrictType.Farm: return farmContentPrefab;
                case DistrictType.Mine: return mineContentPrefab;
                case DistrictType.Forge: return forgeContentPrefab;
                case DistrictType.Core: return coreContentPrefab;
                case DistrictType.Barracks:
                case DistrictType.Camp:
                case DistrictType.Arsenal:
                case DistrictType.Sanctuary:
                    return barracksContentPrefab;
                case DistrictType.Market:
                    return farmContentPrefab;
                case DistrictType.Watchtower:
                case DistrictType.Rampart:
                case DistrictType.Shrine:
                    return genericContentPrefab;
                default: return genericContentPrefab;
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
                gestureSource.OnLassoBegin -= HandleLassoBegin;
        }
    }
}