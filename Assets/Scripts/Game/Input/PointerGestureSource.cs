using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace NodeWar.Input
{
    public enum GestureState
    {
        /// <summary>Nothing pressed.</summary>
        Idle,
        /// <summary>Pressed, but not yet resolved into tap, pan or long press.</summary>
        Pending,
        /// <summary>Moved past the slop before the long-press timer elapsed.</summary>
        Panning,
        /// <summary>Held past the long-press timer without moving. Lasso is armed.</summary>
        LassoArmed,
        /// <summary>Armed and now drawing.</summary>
        Lassoing,
        /// <summary>Press began over UI. Consumes the stroke and publishes nothing.</summary>
        Blocked,
        /// <summary>Two fingers down and spreading. Drives zoom, never pan.</summary>
        Pinching,
        /// <summary>A second finger arrived. The stroke is abandoned.</summary>
        Cancelled
    }

    /// <summary>
    /// The single reader of pointer devices during gameplay.
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

        private GestureState state = GestureState.Idle;
        private Vector2 downPos;
        private float downTime;
        private GestureTarget downTarget;

        private readonly List<Vector2> strokePoints = new List<Vector2>();

        private float pinchStartSpan;

        private Camera cam;
        private int villagerMask;
        private int nodeMask;
        private bool initialized;

        public GestureState State => state;
        public GestureThresholds Thresholds => thresholds;
        public IReadOnlyList<Vector2> CurrentStroke => strokePoints;

        /// <summary>
        /// True while a lasso is armed or drawing. The camera must not pan for
        /// the rest of the stroke once this latches -- long press is reserved
        /// permanently for multi-select and nothing may steal it.
        /// </summary>
        public bool PanSuppressed => state == GestureState.LassoArmed || state == GestureState.Lassoing;

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
            initialized = true;
        }

        private void Awake()
        {
            if (!initialized) Initialize(Camera.main);
        }

        private void Update()
        {
            if (!initialized) return;

            Pointer pointer = Pointer.current;
            if (pointer == null) return;

            // Two fingers are a pinch, and the check runs before anything else
            // so a pan never half-resolves into one. A lasso is the exception:
            // PanSuppressed latches long press for multi-select and zoom loses
            // that contest exactly as pan does, so the stroke is abandoned
            // rather than turned into a camera move.
            if (ActiveTouchCount() >= 2)
            {
                if (state == GestureState.Pinching)
                {
                    UpdatePinch();
                    return;
                }

                if (state == GestureState.Blocked) return;

                if (PanSuppressed)
                {
                    Cancel();

                    // Consume the rest of the stroke. Cancel leaves the state
                    // Idle, and Idle plus two fingers still down would begin a
                    // pinch on the very next frame -- so the lasso the player
                    // abandoned would silently become a zoom without them ever
                    // lifting a finger. Blocked already means "publish nothing
                    // until this stroke ends", and EndPress clears it.
                    state = GestureState.Blocked;
                    return;
                }

                BeginPinch();
                return;
            }

            // Fewer than two fingers left. The survivor does not inherit the
            // stroke -- resuming a pan from whichever finger happened to lift
            // last makes the board lurch at the end of every pinch.
            if (state == GestureState.Pinching)
            {
                EndZoom();
                return;
            }

            Vector2 pos = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                BeginPress(pos);
                return;
            }

            if (pointer.press.isPressed)
            {
                ContinuePress(pos);
                return;
            }

            if (pointer.press.wasReleasedThisFrame)
            {
                EndPress(pos);
            }
        }

        // ===== TRANSITIONS =====

        private void BeginPress(Vector2 pos)
        {
            downPos = pos;
            downTime = Time.unscaledTime;
            strokePoints.Clear();

            // Latched at press time, not polled per frame: once a stroke starts
            // on UI it stays a UI stroke even if the finger slides off the
            // button, which is what every other touch surface does.
            if (IsPointerOverUI())
            {
                state = GestureState.Blocked;
                downTarget = GestureTarget.None(pos);
                Log("down over UI -> Blocked");
                return;
            }

            downTarget = ResolveTarget(pos);
            state = GestureState.Pending;

            // Before the gesture resolves. This is the flash.
            OnPointerDown?.Invoke(downTarget);
            Log("down -> Pending on " + downTarget);
        }

        private void ContinuePress(Vector2 pos)
        {
            switch (state)
            {
                case GestureState.Pending:
                {
                    float moved = Vector2.Distance(pos, downPos);
                    float held = Time.unscaledTime - downTime;

                    if (moved > thresholds.TapSlopPx && held < thresholds.longPressTime)
                    {
                        // Movement first: this is a pan, and any pending
                        // selection intent is dropped.
                        state = GestureState.Panning;
                        OnGestureCancelled?.Invoke();
                        OnPanBegin?.Invoke(downPos);
                        OnPanUpdate?.Invoke(pos);
                        Log("slop exceeded -> Panning");
                    }
                    else if (held >= thresholds.longPressTime && moved <= thresholds.TapSlopPx)
                    {
                        // Held still: arm the lasso. PanSuppressed is now true
                        // for the remainder of the stroke.
                        state = GestureState.LassoArmed;
                        strokePoints.Clear();
                        LassoGeometry.TryAppend(strokePoints, downPos,
                            thresholds.LassoDecimationPx, thresholds.maxLassoPoints);
                        OnGestureCancelled?.Invoke();
                        OnLassoBegin?.Invoke(downPos);
                        Log("long press -> LassoArmed");
                    }
                    break;
                }

                case GestureState.Panning:
                    OnPanUpdate?.Invoke(pos);
                    break;

                case GestureState.LassoArmed:
                case GestureState.Lassoing:
                {
                    if (LassoGeometry.TryAppend(strokePoints, pos,
                            thresholds.LassoDecimationPx, thresholds.maxLassoPoints))
                    {
                        state = GestureState.Lassoing;
                        OnLassoPoint?.Invoke(pos);
                    }
                    break;
                }
            }
        }

        private void EndPress(Vector2 pos)
        {
            switch (state)
            {
                case GestureState.Pending:
                {
                    float moved = Vector2.Distance(pos, downPos);
                    float held = Time.unscaledTime - downTime;

                    if (moved <= thresholds.TapSlopPx && held < thresholds.longPressTime)
                    {
                        OnTap?.Invoke(downTarget);
                        Log("release -> Tap on " + downTarget);
                    }
                    else
                    {
                        // Released past the long-press time without moving and
                        // without the timer having fired mid-frame. Nothing to do.
                        OnGestureCancelled?.Invoke();
                        Log("release -> no gesture");
                    }
                    break;
                }

                case GestureState.Panning:
                    OnPanEnd?.Invoke();
                    Log("release -> pan end");
                    break;

                case GestureState.LassoArmed:
                case GestureState.Lassoing:
                    // A stroke too small to be a shape is published anyway;
                    // consumers gate on LassoGeometry.IsValid and leave the
                    // selection untouched when it fails. A long press on
                    // nothing is a no-op, never a deselect.
                    OnLassoComplete?.Invoke(strokePoints);
                    Log("release -> lasso complete, " + strokePoints.Count + " pts, area " +
                        LassoGeometry.Area(strokePoints).ToString("0"));
                    break;
            }

            state = GestureState.Idle;
        }

        private void Cancel()
        {
            if (state == GestureState.Idle) return;

            bool wasDrawing = state == GestureState.LassoArmed || state == GestureState.Lassoing;

            state = GestureState.Cancelled;
            strokePoints.Clear();
            OnGestureCancelled?.Invoke();

            if (wasDrawing) OnLassoComplete?.Invoke(strokePoints);

            state = GestureState.Idle;
            Log("cancelled (second finger)");
        }

        // ===== ZOOM =====

        private void BeginPinch()
        {
            float span;
            if (!TryReadPinchSpan(out span)) return;

            // A pan interrupted by a second finger has, in fact, ended -- fire
            // the real end event so every existing OnPanEnd subscriber gets
            // the same cleanup a normal release would have given it.
            // CameraController.gesturePanActive in particular is only ever
            // cleared by OnPanEnd; without this it stayed true through the
            // pinch and for the rest of the match.
            if (state == GestureState.Panning) OnPanEnd?.Invoke();

            // Whatever the first finger was doing is abandoned before the
            // pinch starts, so a half-formed pan does not leave a consumer
            // believing a drag is still live.
            if (state != GestureState.Idle) OnGestureCancelled?.Invoke();

            state = GestureState.Pinching;
            pinchStartSpan = span;
            strokePoints.Clear();

            OnZoomBegin?.Invoke();
            Log("two fingers -> Pinching, span " + span.ToString("0"));
        }

        private void UpdatePinch()
        {
            float span;
            if (!TryReadPinchSpan(out span)) return;

            // Below the dead zone the fingers are resting rather than pinching,
            // and publishing that noise would make the board creep.
            if (Mathf.Abs(span - pinchStartSpan) < thresholds.PinchDeadZonePx) return;

            OnZoomUpdate?.Invoke(span / pinchStartSpan);
        }

        private void EndZoom()
        {
            state = GestureState.Idle;
            OnZoomEnd?.Invoke();
            Log("zoom end");
        }

        /// <summary>
        /// Distance between the first two pressed touches. Guarded against a
        /// zero span: two fingers reported at the same point would make the
        /// ratio infinite and throw the camera to a clamp instantly.
        /// </summary>
        private bool TryReadPinchSpan(out float span)
        {
            span = 0f;

            Touchscreen touch = Touchscreen.current;
            if (touch == null) return false;

            Vector2 a = Vector2.zero;
            int found = 0;

            var touches = touch.touches;
            for (int i = 0; i < touches.Count && found < 2; i++)
            {
                if (!touches[i].press.isPressed) continue;

                Vector2 p = touches[i].position.ReadValue();
                if (found == 0) a = p;
                else span = Vector2.Distance(a, p);
                found++;
            }

            if (found < 2) return false;
            return span > 1f;
        }

        // ===== RESOLUTION =====

        /// <summary>
        /// One raycast pair, at press time. Villager wins over node when both
        /// are under the finger -- decided here so no consumer re-decides it.
        /// </summary>
        private GestureTarget ResolveTarget(Vector2 screenPos)
        {
            if (cam == null) return GestureTarget.None(screenPos);

            Ray ray = cam.ScreenPointToRay(screenPos);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, raycastDistance, villagerMask))
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
                        return new GestureTarget(GestureTargetKind.Villager, id, screenPos);
                }
            }

            if (Physics.Raycast(ray, out hit, raycastDistance, nodeMask))
            {
                var node = hit.collider.GetComponentInParent<NodeWar.View.NodeView>();
                if (node != null)
                    return new GestureTarget(GestureTargetKind.Node, node.GetNodeID(), screenPos);
            }

            return GestureTarget.None(screenPos);
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

        private int ActiveTouchCount()
        {
            Touchscreen touch = Touchscreen.current;
            if (touch == null) return 0;

            int count = 0;
            var touches = touch.touches;
            for (int i = 0; i < touches.Count; i++)
            {
                if (touches[i].press.isPressed) count++;
            }
            return count;
        }

        private void Log(string message)
        {
            if (verboseLogging) Debug.Log("[GESTURE] " + message);
        }
    }
}
