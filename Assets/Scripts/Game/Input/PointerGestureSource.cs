using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NodeWar.Input
{
    /// <summary>
    /// The single pointer reader for gameplay selection and move orders.
    /// CameraController separately handles desktop middle-drag and scroll.
    ///
    /// Previously SelectionSystem, CommandSystem and NodePanelManager each
    /// raycast the same press independently and guessed at what the others would
    /// do with it -- NodePanelManager already carried a defensive villager
    /// raycast purely to avoid stealing clicks from SelectionSystem. That race is
    /// what this replaces: a press is resolved once, here, and the result is
    /// published. Consumers act on intent and never read a device.
    ///
    /// Mouse and touch share one path. Touchscreen derives from Pointer, so the
    /// same press/position controls serve both and Editor testing exercises the
    /// same code the phone runs.
    /// </summary>
    public class PointerGestureSource : MonoBehaviour
    {
        [Header("Thresholds")]
        [SerializeField] private GestureThresholds thresholds = new GestureThresholds();

        [Header("Raycasting")]
        [Tooltip("Layers searched for a villager under the press. Villager wins over node.")]
        [SerializeField] private string villagerLayerName = "Villagers";
        [Tooltip("Layers searched for a node when no villager was hit.")]
        [SerializeField] private string nodeLayerName = "Nodes";
        [SerializeField] private float raycastDistance = 100f;

        [Header("Debug")]
        [SerializeField] private bool verboseLogging = false;

        // ===== EVENTS =====

        /// <summary>
        /// Fired the instant a press begins, before the gesture has resolved.
        /// This is what drives the touch-down flash: feedback must start before
        /// we know whether this becomes a tap, a pan or a lasso.
        /// </summary>
        public event Action<GestureTarget> OnPointerDown;

        /// <summary>A pending selection was abandoned -- the press became a pan or was cancelled.</summary>
        public event Action OnGestureCancelled;

        /// <summary>Short press and release within the slop. The only thing that changes selection by touch.</summary>
        public event Action<GestureTarget> OnTap;

        /// <summary>Desktop right-click destination, resolved against nodes only after the UI guard.</summary>
        public event Action<GestureTarget> OnSecondaryClick;

        public event Action<Vector2> OnPanBegin;
        public event Action<Vector2> OnPanUpdate;
        public event Action OnPanEnd;

        public event Action<Vector2> OnLassoBegin;
        public event Action<Vector2> OnLassoPoint;
        public event Action<IReadOnlyList<Vector2>> OnLassoComplete;

        /// <summary>
        /// A pinch began. Consumers capture whatever they are about to scale here.
        ///
        /// The pair below publish a scale measured from that captured start,
        /// never a per-frame delta. An accumulating delta drifts: every frame's
        /// rounding is kept, and a pinch that returns the fingers to exactly
        /// where they started would not return the camera with them.
        /// </summary>
        public event Action OnZoomBegin;

        /// <summary>
        /// Scale relative to the pinch start. Above 1 means zoom in (fingers
        /// spreading), below 1 means out.
        /// </summary>
        public event Action<float> OnZoomUpdate;

        public event Action OnZoomEnd;

        // ===== STATE =====

        private GestureClassifier classifier;
        private GestureTarget downTarget;
        private readonly List<Vector2> strokePoints = new List<Vector2>();
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private readonly List<PointerSample> samples = new List<PointerSample>();
        private Camera cam;
        private int villagerMask;
        private int nodeMask;
        private bool initialized;

        public GestureState State => classifier != null ? classifier.State : GestureState.Idle;
        public GestureThresholds Thresholds => thresholds;
        public IReadOnlyList<Vector2> CurrentStroke => strokePoints;
        public bool PanSuppressed => classifier != null && classifier.PanSuppressed;
        private System.Func<int, bool> villagerFilter;

        /// <summary>
        /// Decides whether a villager is a tap target at all. Supplied rather
        /// than implemented here so the input layer does not need to know what
        /// makes a villager selectable -- SelectionSystem owns that rule and
        /// this only asks it.
        /// </summary>
        public void SetVillagerFilter(System.Func<int, bool> filter)
        {
            villagerFilter = filter;
        }

        public void Initialize(Camera camera)
        {
            cam = camera != null ? camera : Camera.main;
            villagerMask = LayerMask.GetMask(villagerLayerName);
            nodeMask = LayerMask.GetMask(nodeLayerName);
            if (classifier == null)
            {
                classifier = new GestureClassifier(NodeWar.Lobby.InputBindings.CreateDefault(),
                    thresholds.tapSlopMm, thresholds.longPressTime, thresholds.lassoDecimationMm,
                    thresholds.maxLassoPoints, thresholds.pinchDeadZoneMm, 1f / ScreenMetrics.PixelsPerMm);
                classifier.Published += Publish;
            }
            initialized = true;
        }

        private void Awake()
        {
            if (!initialized) Initialize(Camera.main);
        }

        private void Update()
        {
            if (!initialized) return;
            samples.Clear();
            float now = Time.unscaledTime;
            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                Vector2 pos = mouse.position.ReadValue();
                samples.Add(new PointerSample(now, ToMm(pos), -1, PointerButton.Secondary,
                    PointerPhase.Began, IsMouseOverUI(pos)));
            }

            Pointer pointer = Pointer.current;
            if (pointer != null)
            {
                PointerPhase phase = pointer.press.wasPressedThisFrame ? PointerPhase.Began
                    : pointer.press.isPressed ? PointerPhase.Held
                    : pointer.press.wasReleasedThisFrame ? PointerPhase.Ended : PointerPhase.None;
                Touchscreen touch = Touchscreen.current;
                int id = touch != null && touch.primaryTouch.press.isPressed
                    ? touch.primaryTouch.touchId.ReadValue() : -1;
                samples.Add(new PointerSample(now, ToMm(pointer.position.ReadValue()), id,
                    PointerButton.Primary, phase, phase == PointerPhase.Began && IsPointerOverUI()));
                if (touch != null)
                {
                    foreach (var finger in touch.touches)
                        if (finger.press.isPressed)
                            samples.Add(new PointerSample(now, ToMm(finger.position.ReadValue()),
                                finger.touchId.ReadValue(), PointerButton.Touch, PointerPhase.Held));
                }
            }
            classifier.ProcessFrame(samples);
        }

        private static GesturePoint ToMm(Vector2 position)
        {
            float scale = ScreenMetrics.PixelsPerMm;
            return new GesturePoint(position.x / scale, position.y / scale);
        }

        private static Vector2 ToPixels(GesturePoint position)
        {
            float scale = ScreenMetrics.PixelsPerMm;
            return new Vector2(position.X * scale, position.Y * scale);
        }

        private void Publish(GestureEvent gesture)
        {
            Vector2 pos = ToPixels(gesture.Position);
            switch (gesture.Kind)
            {
                case GestureEventKind.PointerDown:
                    strokePoints.Clear();
                    downTarget = ResolveTarget(pos);
                    OnPointerDown?.Invoke(downTarget);
                    break;
                case GestureEventKind.Cancelled: OnGestureCancelled?.Invoke(); break;
                case GestureEventKind.Tap: OnTap?.Invoke(downTarget); break;
                case GestureEventKind.SecondaryClick:
                    OnSecondaryClick?.Invoke(ResolveTarget(pos, nodesOnly: true));
                    break;
                case GestureEventKind.PanBegin: OnPanBegin?.Invoke(pos); break;
                case GestureEventKind.PanUpdate: OnPanUpdate?.Invoke(pos); break;
                case GestureEventKind.PanEnd: OnPanEnd?.Invoke(); break;
                case GestureEventKind.LassoBegin:
                    strokePoints.Clear();
                    strokePoints.Add(pos);
                    OnLassoBegin?.Invoke(pos);
                    break;
                case GestureEventKind.LassoPoint:
                    strokePoints.Add(pos);
                    OnLassoPoint?.Invoke(pos);
                    break;
                case GestureEventKind.LassoComplete:
                    strokePoints.Clear();
                    foreach (GesturePoint point in gesture.Points) strokePoints.Add(ToPixels(point));
                    OnLassoComplete?.Invoke(strokePoints);
                    break;
                case GestureEventKind.ZoomBegin: strokePoints.Clear(); OnZoomBegin?.Invoke(); break;
                case GestureEventKind.ZoomUpdate: OnZoomUpdate?.Invoke(gesture.Scale); break;
                case GestureEventKind.ZoomEnd: OnZoomEnd?.Invoke(); break;
            }
            Log(gesture.Kind.ToString());
        }
        // ===== RESOLUTION =====

        /// <summary>
        /// Resolves the world target at press time. Primary presses prefer a
        /// selectable villager; right-click destinations search only nodes.
        /// </summary>
        private GestureTarget ResolveTarget(Vector2 screenPos, bool nodesOnly = false)
        {
            if (cam == null) return GestureTarget.None();

            Ray ray = cam.ScreenPointToRay(screenPos);
            RaycastHit hit;

            if (!nodesOnly && Physics.Raycast(ray, out hit, raycastDistance, villagerMask))
            {
                var villager = hit.collider.GetComponentInParent<NodeWar.View.VillagerView>();
                if (villager != null)
                {
                    int id = villager.GetVillagerID();

                    // An opponent's villager is not a tap target, so the press
                    // falls through to the node beneath rather than being
                    // swallowed. Otherwise an enemy standing on your node would
                    // block you from opening it -- and their touch targets are
                    // finger-sized, so they cover a lot of board.
                    if (villagerFilter == null || villagerFilter(id))
                        return new GestureTarget(GestureTargetKind.Villager, id);
                }
            }

            if (Physics.Raycast(ray, out hit, raycastDistance, nodeMask))
            {
                var node = hit.collider.GetComponentInParent<NodeWar.View.NodeView>();
                if (node != null)
                    return new GestureTarget(GestureTargetKind.Node, node.GetNodeID());
            }

            return GestureTarget.None();
        }

        private bool IsMouseOverUI(Vector2 screenPos)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            // Query this click's position, not the input module's cached hover
            // from a potentially earlier Update. PanelRaycaster covers the
            // UI Toolkit HUD and sheet; GraphicRaycaster covers legacy uGUI.
            var pointerData = new PointerEventData(eventSystem)
            {
                position = screenPos,
                pointerId = -1,
                button = PointerEventData.InputButton.Right
            };
            uiHits.Clear();
            eventSystem.RaycastAll(pointerData, uiHits);
            for (int i = 0; i < uiHits.Count; i++)
            {
                if (uiHits[i].module is UnityEngine.UI.GraphicRaycaster ||
                    uiHits[i].module is UnityEngine.UIElements.PanelRaycaster)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// EventSystem's pointer-over test needs the touch id on a touchscreen;
        /// the no-argument overload silently reports the mouse and would let
        /// every touch fall through UI on device.
        /// </summary>
        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;

            Touchscreen touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
                return EventSystem.current.IsPointerOverGameObject(touch.primaryTouch.touchId.ReadValue());

            return EventSystem.current.IsPointerOverGameObject();
        }

        private void Log(string message)
        {
            if (verboseLogging) Debug.Log("[GESTURE] " + message);
        }
    }
}
