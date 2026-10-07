using UnityEngine;
using UnityEngine.InputSystem;
using NodeWar.Simulation;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace NodeWar.Input
{
    public class SelectionSystem : MonoBehaviour
    {
        [Tooltip("Per-click selection logging. Off by default -- it fires on every " +
                 "mouse release and drowns out networking/draft logs.")]
        [SerializeField] private bool verboseSelectionLogging = false;

        private SimulationState simState;
        private int localPlayerID = 0;

        private List<int> selectedVillagerIDs = new List<int>();
        public IReadOnlyList<int> SelectedVillagerIDs => selectedVillagerIDs;

        private bool isDragging = false;
        private Vector2 dragStartScreenPos;
        private const float DRAG_THRESHOLD = 10f;

        private Camera mainCam;

        private LayerMask villagerLayer;
        private Transform[] villagerTransforms;
        private NodeWar.View.VillagerPositioner[] villagerPositioners;

        public void Initialize(SimulationState state, int playerID)
        {
            simState = state;
            localPlayerID = playerID;
            mainCam = Camera.main;
            villagerLayer = LayerMask.GetMask("Villagers");
        }

        public void SetPlayerID(int id)
        {
            localPlayerID = id;
            ClearSelection();
        }

        public void SetVillagerTransforms(Transform[] transforms)
        {
            villagerTransforms = transforms;
        }

        public void SetVillagerPositioners(NodeWar.View.VillagerPositioner[] managers)
        {
            villagerPositioners = managers;
        }

        /// <summary>
        /// When true, a TapRouter drives selection and this component stops
        /// reading the mouse. The legacy path is gated rather than deleted so
        /// the two can be compared during tuning; GameManager sets this on when
        /// it builds the gesture stack.
        /// </summary>
        public void SetGestureRouted(bool routed)
        {
            gestureRouted = routed;
        }

        private bool gestureRouted = false;

        /// <summary>
        /// Whether this villager can be selected at all: ours, alive, not
        /// consumed.
        ///
        /// Public because the gesture source uses it to decide whether a
        /// villager is even a tap target. An opponent's villager is not one --
        /// the raycast falls through it to the node underneath, so an enemy
        /// standing on your node does not block the node.
        /// </summary>
        public bool IsSelectable(int villagerID)
        {
            if (simState == null) return false;
            if (villagerID < 0 || villagerID >= simState.villagers.Length) return false;

            VillagerData v = simState.villagers[villagerID];
            if (v.ownerID != localPlayerID) return false;
            if (v.state == VillagerState.Dead) return false;
            if (v.isConsumed) return false;

            return true;
        }

        /// <summary>
        /// Replaces the selection with a single villager. Returns false if the
        /// villager is not selectable -- not ours, dead, or consumed -- and
        /// leaves the selection untouched so the caller can decide what a tap
        /// on an unselectable target should mean.
        ///
        /// Validity lives here rather than in the router because selection
        /// ownership is this component's concern.
        /// </summary>
        public bool SelectSingle(int villagerID)
        {
            if (!IsSelectable(villagerID)) return false;

            selectedVillagerIDs.Clear();
            selectedVillagerIDs.Add(villagerID);
            return true;
        }

        public bool TapVillager(int villagerID, NodeWar.Lobby.InputAction action)
        {
            if (!IsSelectable(villagerID)) return false;
            SelectionRules.TapVillager(selectedVillagerIDs, villagerID, action);
            return true;
        }

        /// <summary>The node a villager is standing on, or -1 if it does not exist.</summary>
        public int NodeOfVillager(int villagerID)
        {
            if (simState == null || villagerID < 0 || villagerID >= simState.villagers.Length) return -1;
            return simState.villagers[villagerID].currentNodeID;
        }

        /// <summary>Selects every villager of ours that is standing idle.</summary>
        public void SelectAllIdle()
        {
            if (simState == null) return;
            selectedVillagerIDs.Clear();
            for (int i = 0; i < simState.villagers.Length; i++)
                if (IsSelectable(i) && simState.villagers[i].state == VillagerState.Idle)
                    selectedVillagerIDs.Add(i);
        }

        /// <summary>Selects every villager of ours on the node this villager is standing on.</summary>
        public void SelectAllOnNodeOf(int villagerID)
        {
            if (simState == null || villagerID < 0 || villagerID >= simState.villagers.Length) return;
            int node = simState.villagers[villagerID].currentNodeID;
            selectedVillagerIDs.Clear();
            for (int i = 0; i < simState.villagers.Length; i++)
                if (IsSelectable(i) && simState.villagers[i].currentNodeID == node)
                    selectedVillagerIDs.Add(i);
        }

        private void Update()
        {
            if (simState == null) return;
            if (gestureRouted) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (UnityEngine.EventSystems.EventSystem.current != null &&
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return;

                isDragging = true;
                dragStartScreenPos = mouse.position.ReadValue();
            }

            if (mouse.leftButton.wasReleasedThisFrame && isDragging)
            {
                isDragging = false;
                Vector2 releasePos = mouse.position.ReadValue();
                float dragDistance = Vector2.Distance(dragStartScreenPos, releasePos);

                if (verboseSelectionLogging)
                    Debug.Log("[SEL] Left released. Drag distance: " + dragDistance);

                // Legacy mouse path only. A drag no longer selects anything --
                // the lasso is a long-press gesture now, and arrives through
                // the gesture source rather than from a mouse read here.
                if (dragDistance < DRAG_THRESHOLD)
                {
                    TryClickSelect(releasePos);
                }

                if (verboseSelectionLogging)
                {
                    Debug.Log("[SEL] After select action. Selected count: " + selectedVillagerIDs.Count);
                    for (int i = 0; i < selectedVillagerIDs.Count; i++)
                        Debug.Log("[SEL]   Selected villager ID: " + selectedVillagerIDs[i]);
                }
            }
        }

        private void TryClickSelect(Vector2 screenPos)
        {
            bool shiftHeld = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

            Ray ray = mainCam.ScreenPointToRay(screenPos);

            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f, villagerLayer))
            {
                NodeWar.View.VillagerView villagerView = hit.collider.GetComponentInParent<NodeWar.View.VillagerView>();
                if (villagerView != null)
                {
                    int id = villagerView.GetVillagerID();

                    if (simState.villagers[id].ownerID == localPlayerID &&
                        simState.villagers[id].state != VillagerState.Dead &&
                        !simState.villagers[id].isConsumed)
                    {
                        if (shiftHeld)
                        {
                            // Toggle: remove if already selected, add if not
                            if (selectedVillagerIDs.Contains(id))
                                selectedVillagerIDs.Remove(id);
                            else
                                selectedVillagerIDs.Add(id);
                        }
                        else
                        {
                            // Normal: clear and select single
                            selectedVillagerIDs.Clear();
                            selectedVillagerIDs.Add(id);
                        }
                        return;
                    }
                }
            }

            // Clicked nothing valid
            if (!shiftHeld)
                ClearSelection();
        }

        /// <summary>
        /// Selects every owned villager whose screen position falls inside the
        /// stroke. Replaces the radius circle: the component was named lasso
        /// and drew one, but selected by distance from a centre point.
        ///
        /// The lasso always replaces. There is no additive path -- draw a
        /// bigger lasso if you want a bigger selection -- which is what makes
        /// LassoGeometry's nonzero winding rule load-bearing: a stroke that
        /// loops back inside itself must add, never subtract.
        ///
        /// A stroke too small to be a shape, or capturing nobody, leaves the
        /// selection untouched. A long press that goes nowhere is a no-op.
        /// </summary>
        public void ApplyLasso(IReadOnlyList<Vector2> rawPoints)
        {
            if (simState == null || mainCam == null) return;

            // Smoothed before testing, with the same iteration count the
            // renderer uses, so the shape the player saw is the shape that
            // selects. Chaikin cuts corners inward -- testing the raw polyline
            // against a smoothed drawing would select villagers sitting
            // visibly outside the line.
            LassoGeometry.Smooth(rawPoints, smoothedLasso,
                thresholds.lassoSmoothingIterations, thresholds.MaxSmoothedPoints);

            IReadOnlyList<Vector2> points = smoothedLasso;

            if (!LassoGeometry.IsValid(points, thresholds.MinLassoAreaSqPx)) return;

            // Cheap screen-space reject before the per-edge containment test.
            Rect bounds = LassoGeometry.Bounds(points);

            lassoSelection.Clear();

            for (int i = 0; i < simState.villagers.Length; i++)
            {
                VillagerData v = simState.villagers[i];
                if (v.ownerID != localPlayerID) continue;
                if (v.state == VillagerState.Dead) continue;
                if (v.isConsumed) continue;

                Vector3 worldPos;
                if (villagerTransforms != null && i < villagerTransforms.Length && villagerTransforms[i] != null)
                {
                    worldPos = villagerTransforms[i].position;
                }
                else if (villagerPositioners != null && v.currentNodeID < villagerPositioners.Length &&
                    villagerPositioners[v.currentNodeID] != null)
                {
                    worldPos = villagerPositioners[v.currentNodeID].transform.position;
                }
                else
                {
                    continue;
                }

                Vector3 screenPos = mainCam.WorldToScreenPoint(worldPos);

                // Behind the camera projects with negative z and would otherwise
                // land at a mirrored screen point, selecting villagers the
                // player cannot see.
                if (screenPos.z < 0) continue;

                Vector2 flat = new Vector2(screenPos.x, screenPos.y);
                if (!bounds.Contains(flat)) continue;
                if (!LassoGeometry.Contains(points, flat)) continue;

                lassoSelection.Add(i);
            }

            SelectionRules.ReplaceLassoIfAny(selectedVillagerIDs, lassoSelection);

            if (verboseSelectionLogging)
                Debug.Log("[SEL] Lasso selected " + selectedVillagerIDs.Count + " villagers");
        }

        public void ClearSelection()
        {
            selectedVillagerIDs.Clear();
        }

        /// <summary>
        /// A rollback (8.2e) put the villager list back to <paramref name="count"/>
        /// entries. Selected IDs past it name villagers that no longer exist.
        /// </summary>
        public void DropVillagersFrom(int count)
        {
            selectedVillagerIDs.RemoveAll(id => id >= count);
        }

        public bool IsSelected(int villagerID)
        {
            for (int i = 0; i < selectedVillagerIDs.Count; i++)
            {
                if (selectedVillagerIDs[i] == villagerID) return true;
            }
            return false;
        }

        /// <summary>
        /// Subscribes to lasso completion. SelectionSystem listens directly
        /// rather than going through TapRouter because a lasso has exactly one
        /// meaning -- unlike a click, where three components each inferring
        /// intent was the problem the router exists to solve.
        /// </summary>
        public void SetGestureSource(PointerGestureSource source)
        {
            if (gestureSource != null)
                gestureSource.OnLassoComplete -= ApplyLasso;

            gestureSource = source;

            if (gestureSource != null)
                gestureSource.OnLassoComplete += ApplyLasso;
        }

        private PointerGestureSource gestureSource;

        /// <summary>Thresholds come from the gesture source so the area gate cannot disagree with the stroke that produced it.</summary>
        private GestureThresholds thresholds =>
            gestureSource != null ? gestureSource.Thresholds : fallbackThresholds;

        private readonly GestureThresholds fallbackThresholds = new GestureThresholds();
        private readonly List<Vector2> smoothedLasso = new List<Vector2>();
        private readonly List<int> lassoSelection = new List<int>();

        private void OnDestroy()
        {
            if (gestureSource != null)
                gestureSource.OnLassoComplete -= ApplyLasso;
        }
    }
}
