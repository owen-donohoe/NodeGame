using NodeWar.Simulation;
using NodeWar.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NodeWar.Core
{
    /// <summary>
    /// Perspective camera controller with drag-to-pan, momentum, dolly zoom,
    /// bounds, draft-mode framing, and per-player-side memory.
    ///
    /// Hierarchy (set up in Editor):
    ///   CameraRig [this script] — world X/Z position
    ///     ? CameraPivot — rotation only (viewing angle)
    ///           ? Camera — local Z = -zoomDistance (dolly)
    ///
    /// Middle mouse to drag and scroll to zoom on desktop. The one-finger pan
    /// and pinch zoom published by PointerGestureSource drive the same two
    /// operations on touch, and are the primary interface the project targets.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Board Config")]
        [SerializeField] private BoardConfig boardConfig;

        [Header("References")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Camera cam;

        [Header("Pan")]
        [SerializeField] private float panSpeed = 1f;
        [SerializeField] private float panMomentumDamping = 5f;
        [SerializeField] private float panMaxVelocity = 20f;

        [Header("Zoom (Dolly)")]
        [SerializeField] private float zoomScrollSensitivity = 2f;
        [SerializeField] private float zoomMinDistance = 5f;
        [SerializeField] private float zoomMaxDistance = 30f;
        [SerializeField] private float zoomSmoothingSpeed = 8f;

        [Header("Bounds")]
        [SerializeField] private bool useBounds = true;
        [Tooltip("Spring force pushing camera back inside BoardConfig bounds.")]
        [SerializeField] private float boundsPushbackForce = 10f;

        [Header("Draft Mode Framing")]
        //
        // The draft opens on an authored framing rather than a derived one.
        // Fitting the board by multiplying its largest dimension produced a
        // near-top-down plan view that was technically complete and read as
        // flat: the whole point of the phase is choosing WHERE, and where needs
        // depth to be legible. These three values are that framing, measured
        // off the rig in the Editor. Gameplay.unity now serializes draftPitch
        // and draftZoomDistance directly, so the defaults below are only what
        // a scene that has not set them falls back to.
        //
        // The board is 4x7 at nodeScale 6, so it spans x 0..18 and z 0..36 and
        // its centre is (9, 0, 18). ResetToCenter puts the rig there and
        // draftRigOffset pulls it back to z 12, which is the pivot the framing
        // below was measured against. Changing nodeScale or the grid moves the
        // pivot with it; these two numbers are the shot, not the position.
        [Tooltip("Pivot X angle during the draft. Higher = more top-down.")]
        [SerializeField][Range(30f, 90f)] private float draftPitch = 60f;

        [Tooltip("Camera dolly distance during the draft. The camera sits this " +
                 "far back along the pivot's -Z.")]
        [SerializeField][Range(10f, 120f)] private float draftZoomDistance = 50f;

        [Tooltip("Rig offset from board centre during the draft, in world units. " +
                 "Pulling back on Z re-centres the board in frame once the " +
                 "pitch is shallow enough to see along it.")]
        [SerializeField] private Vector3 draftRigOffset = new Vector3(0f, 0f, -6f);

        [Tooltip("Multiplier on largest grid dimension. No longer sets the " +
                 "opening framing - it sets how far out the pinch may go.")]
        // NOTE: Gameplay.unity serializes 1.853, which overrides this default.
        [SerializeField][Range(1.0f, 3.0f)] private float draftZoomBoardMultiplier = 1.3f;
        [Tooltip("If true, the draft zoom-out limit never falls below zoomMaxDistance.")]
        [SerializeField] private bool draftZoomNeverBelowMax = true;

        [Header("Per-Side Defaults")]
        // NOTE: Gameplay.unity serializes 0.65, which overrides this default.
        // Raising how far out the match starts is an Inspector change on the
        // CameraRig, not a code one.
        [Tooltip("Normalized position between min/max zoom for gameplay start. 0=closest, 1=farthest.")]
        [SerializeField][Range(0f, 1f)] private float sideDefaultZoomNormalized = 0.65f;

        [Tooltip("How far the starting camera is pushed from your own edge " +
                 "toward the opponent's, as a fraction of the board's depth. " +
                 "0 sits on your edge as before; higher shows more of their half.")]
        [SerializeField][Range(0f, 0.5f)] private float sideOpponentBiasFactor = 0.18f;
        [Tooltip("Fraction of nodeScale used as Z offset from board edge.")]
        [SerializeField][Range(0f, 1f)] private float sideZOffsetFactor = 0.35f;
        [SerializeField][Range(0f, 1f)] private float sideP0LateralNudgeFactor = 0.5f;
        [SerializeField][Range(0f, 1f)] private float sideP1ZPositionFactor = 0.6f;

        // Pan state
        private Vector3 panVelocity;
        private bool isDragging;
        private Vector3 lastMouseWorldPos;

        // Zoom state
        private float targetZoomDistance;
        private float currentZoomDistance;

        // Side memory
        private struct SideState
        {
            public Vector3 position;
            public float zoomDistance;
            public bool initialized;
        }

        private SideState[] sideStates = new SideState[2];

        // The framing each side was *given*, captured once and never written
        // again. sideStates is overwritten on every side switch so a player
        // returning to their own side finds the camera where they left it --
        // useful, but it means it is not a fixed point, and recentre needs one.
        // Without this pair, panning and then switching sides twice would make
        // "home" wherever the player last happened to stop.
        private SideState[] sideHomeStates = new SideState[2];

        // World units each side's framing is pushed toward the opponent,
        // computed once from the board's depth in InitializeSides.
        private float opponentBias;

        private int currentSide = 0;
        private bool sideHasBeenSet = false;

        private bool isDraftMode = false;

        private void Awake()
        {
            if (cam == null)
                cam = GetComponentInChildren<Camera>();
            if (cam == null)
                cam = Camera.main;

            if (cam != null)
            {
                cam.transparencySortMode = UnityEngine.TransparencySortMode.CustomAxis;
                cam.transparencySortAxis = new Vector3(0f, 0f, -1f); // P1 default before SetPlayerSide is called
            }

            if (cameraPivot == null && transform.childCount > 0)
                cameraPivot = transform.GetChild(0);

            // The authored transform is the opening zoom - but only while it is
            // still the truth. GameManager builds the DraftManager from its own
            // Awake, and Awake order between two scene objects is arbitrary, so
            // SetDraftMode can and does run before this one. Seeding
            // unconditionally then overwrote a framing the draft had already
            // applied, and the draft opened at the match's zoom instead of the
            // whole-board one - with everything else about the draft camera,
            // the rig position and the pitch, correctly in place, which is what
            // made it read as a framing problem rather than an ordering one.
            if (!isDraftMode && cam != null)
            {
                currentZoomDistance = Mathf.Abs(cam.transform.localPosition.z);
                targetZoomDistance = currentZoomDistance;
            }

            panVelocity = Vector3.zero;
        }

        private void Update()
        {
            HandleDragInput();
            HandleZoomInput();
            HandleDraftScroll();
            ApplyFocus();

            // Consume the spring correction in this frame's momentum step.
            ApplyBounds();
            ApplyMomentum();
            ApplyZoom();
        }

        // ===== PAN =====

        private void HandleDragInput()
        {
            if (isDraftMode) return;
            if (gestureSource != null && gestureSource.PanSuppressed)
            {
                isDragging = false;
                panVelocity = Vector3.zero;
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.middleButton.wasPressedThisFrame)
            {
                isDragging = true;
                lastMouseWorldPos = GetMouseWorldPosition(mouse);
                panVelocity = Vector3.zero;

                // Any manual pan cancels the automatic return on dismissal.
                // A focus tween in flight is abandoned rather than fought.
                isFocusing = false;
                NotifyManualPan();
            }

            if (mouse.middleButton.isPressed && isDragging)
                ApplyPanDelta(mouse.position.ReadValue(), ref lastMouseWorldPos);

            if (mouse.middleButton.wasReleasedThisFrame)
                isDragging = false;
        }

        /// <summary>
        /// Samples the ground, moves the rig, and derives capped momentum for
        /// mouse and gesture pan. Each input keeps its own anchor, and both
        /// paths yield to an armed lasso.
        /// </summary>
        private void ApplyPanDelta(Vector2 screenPos, ref Vector3 lastWorld)
        {
            if (gestureSource != null && gestureSource.PanSuppressed) return;

            Vector3 delta = lastWorld - ScreenToGroundPoint(screenPos);
            transform.position += delta * panSpeed;

            if (Time.deltaTime > 0f)
            {
                panVelocity = delta * panSpeed / Time.deltaTime;
                if (panVelocity.magnitude > panMaxVelocity)
                    panVelocity = panVelocity.normalized * panMaxVelocity;
            }

            // Re-sample for the next delta. The pan follows the pointer
            // exactly at panSpeed == 1 (Gameplay.unity's value); bounds
            // pushback may then move the rig and rebase this anchor.
            lastWorld = ScreenToGroundPoint(screenPos);
        }

        /// <summary>
        /// Draft zoom on desktop. Scroll used to nudge the rig along Z, which
        /// panned a camera the phase had otherwise locked and gave the draft a
        /// control the match did not have. It zooms now, matching gameplay and
        /// matching the pinch.
        /// </summary>
        private void HandleDraftScroll()
        {
            if (!isDraftMode) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            float fit = draftFitZoomDistance > 0f ? draftFitZoomDistance : zoomMaxDistance;

            SetTargetZoomClamped(
                targetZoomDistance - scroll * zoomScrollSensitivity * 0.01f * targetZoomDistance,
                fit, manual: false);
        }

        private void ApplyMomentum()
        {
            // The focus tween owns transform.position while it runs. Two
            // writers in one frame is how a focus move ends with a visible
            // slide, so momentum yields rather than blending.
            if (isFocusing) return;
            if (isDragging) return;

            // Momentum applies after the finger lifts, not while it is still
            // driving the position directly.
            if (gesturePanActive) return;

            transform.position += panVelocity * Time.deltaTime;
            panVelocity = Vector3.Lerp(panVelocity, Vector3.zero, panMomentumDamping * Time.deltaTime);

            // Keep small spring corrections alive outside the bounds. Otherwise
            // damping's stop threshold can strand the rig just beyond an edge.
            if (panVelocity.sqrMagnitude < 0.001f &&
                transform.position.Equals(ClampToBounds(transform.position)))
                panVelocity = Vector3.zero;
        }

        // ===== ZOOM =====

        private void HandleZoomInput()
        {
            // The draft has its own clamp, so its scroll is handled by
            // HandleDraftScroll. Reading it here too would apply every notch twice.
            if (isDraftMode) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // Proportional: zoom feels consistent at any distance
                SetTargetZoom(targetZoomDistance - scroll * zoomScrollSensitivity * 0.01f * targetZoomDistance);
            }
        }

        /// <summary>
        /// Every zoom in the game lands here. Pinch, the HUD zoom handle, the
        /// scroll wheel and the draft all write targetZoomDistance through this
        /// one setter (or SetTargetZoomClamped, for the draft's own ceiling)
        /// and let ApplyZoom smooth it.
        ///
        /// Nothing may set cam.transform.localPosition to zoom instead -- a
        /// second writer is not an alternative route to the same place, it is
        /// a fight, resolved differently depending on which ran last, and it
        /// reads as jitter.
        /// </summary>
        public void SetTargetZoom(float distance)
        {
            SetTargetZoomClamped(distance, zoomMaxDistance, manual: true);
        }

        /// <summary>
        /// Shared zoom input writer. The draft supplies its own ceiling and
        /// does not mark a gameplay focus session dirty.
        /// </summary>
        private void SetTargetZoomClamped(float distance, float ceiling, bool manual)
        {
            float clamped = Mathf.Clamp(distance, zoomMinDistance, ceiling);
            if (Mathf.Approximately(clamped, targetZoomDistance)) return;

            targetZoomDistance = clamped;

            // A manual zoom is the player moving the camera, so it counts
            // against the focus session exactly as a pan does. Without this,
            // dismissing the node sheet would tween the camera back to where
            // it sat before the sheet opened and silently undo the zoom. The
            // draft has no focus session to disturb, so its two writers pass
            // manual: false.
            if (manual) NotifyManualPan();

            RaiseZoomChanged();
        }

        private void ApplyZoom()
        {
            if (cam == null) return;

            if (Mathf.Abs(currentZoomDistance - targetZoomDistance) > 0.01f)
                currentZoomDistance = Mathf.Lerp(currentZoomDistance, targetZoomDistance, zoomSmoothingSpeed * Time.deltaTime);
            else
                currentZoomDistance = targetZoomDistance;

            cam.transform.localPosition = new Vector3(0f, 0f, -currentZoomDistance);
        }

        // ===== BOUNDS =====

        private void ApplyBounds()
        {
            if (!useBounds || boardConfig == null) return;

            // The spring would fight the focus tween and drag the camera back
            // after it lands, turning "one short motion" into a motion plus a
            // slide. Focus targets are clamped into bounds before the tween
            // starts (see ClampToBounds), so suspending it here is safe.
            if (isFocusing) return;

            Vector3 pos = transform.position;
            Vector3 push = Vector3.zero;

            // Soft spring pushback rather than hard clamp — feels natural
            if (pos.x < boardConfig.boundsMinX)
                push.x = boundsPushbackForce * (boardConfig.boundsMinX - pos.x) * Time.deltaTime;
            if (pos.x > boardConfig.boundsMaxX)
                push.x = boundsPushbackForce * (boardConfig.boundsMaxX - pos.x) * Time.deltaTime;
            if (pos.z < boardConfig.boundsMinZ)
                push.z = boundsPushbackForce * (boardConfig.boundsMinZ - pos.z) * Time.deltaTime;
            if (pos.z > boardConfig.boundsMaxZ)
                push.z = boundsPushbackForce * (boardConfig.boundsMaxZ - pos.z) * Time.deltaTime;

            if (push.Equals(Vector3.zero)) return;

            // A live drag writes position directly and suspends momentum, so
            // apply the soft return to position while held. Once released, the
            // same spring accelerates momentum before it is integrated.
            if (isDragging || gesturePanActive)
            {
                transform.position += push;

                // Ground projections move with the rig. Rebase the held
                // anchors too, or the next pan sample undoes this correction
                // even when the pointer has not moved.
                if (isDragging) lastMouseWorldPos += push;
                if (gesturePanActive) gesturePanLastWorld += push;
            }
            else
                panVelocity += push;
        }

        // ===== GESTURE PAN =====
        //
        // One finger dragging the board. The middle-mouse path below is kept
        // for desktop habit, but this is the one that exists on a phone.

        private NodeWar.Input.PointerGestureSource gestureSource;
        private bool gesturePanActive;
        private Vector3 gesturePanLastWorld;

        public void SetGestureSource(NodeWar.Input.PointerGestureSource source)
        {
            if (gestureSource != null)
            {
                gestureSource.OnPanBegin -= HandlePanBegin;
                gestureSource.OnPanUpdate -= HandlePanUpdate;
                gestureSource.OnPanEnd -= HandlePanEnd;
                gestureSource.OnZoomBegin -= HandleZoomBegin;
                gestureSource.OnZoomUpdate -= HandleZoomUpdate;
                gestureSource.OnZoomEnd -= HandleZoomEnd;
            }

            gestureSource = source;

            if (gestureSource != null)
            {
                gestureSource.OnPanBegin += HandlePanBegin;
                gestureSource.OnPanUpdate += HandlePanUpdate;
                gestureSource.OnPanEnd += HandlePanEnd;
                gestureSource.OnZoomBegin += HandleZoomBegin;
                gestureSource.OnZoomUpdate += HandleZoomUpdate;
                gestureSource.OnZoomEnd += HandleZoomEnd;
            }
        }

        // ===== GESTURE ZOOM =====
        //
        // Pinch and the HUD zoom handle arrive through the same three events
        // and are not told apart here. Both report a scale measured from where
        // the gesture began, so the distance is recomputed from a captured
        // origin every frame rather than accumulated -- returning the fingers
        // to where they started returns the camera with them.

        private float zoomGestureStartDistance;

        /// <summary>
        /// Raised whenever the target distance changes, with 0 = fully zoomed
        /// in and 1 = fully out. The HUD's transient readout listens; nothing
        /// polls, so a camera at rest costs nothing.
        /// </summary>
        public event System.Action<float> ZoomChanged;

        /// <summary>
        /// Raised when a zoom gesture starts and ends. The readout shows itself
        /// on the first and schedules its own fade on the second, so it answers
        /// "am I in zoom mode" and then leaves.
        /// </summary>
        public event System.Action<bool> ZoomGestureActiveChanged;

        private void HandleZoomBegin()
        {
            BeginZoomGesture();
        }

        /// <summary>
        /// Opens a zoom gesture from outside the gesture source. The HUD's zoom
        /// handle uses this: its press begins over UI, so PointerGestureSource
        /// latches the stroke Blocked and publishes nothing, and the handle
        /// drives the camera directly instead.
        /// </summary>
        public void BeginZoomGesture()
        {
            zoomGestureStartDistance = targetZoomDistance;
            isFocusing = false;
            ZoomGestureActiveChanged?.Invoke(true);
        }

        public void EndZoomGesture()
        {
            ZoomGestureActiveChanged?.Invoke(false);
        }

        /// <summary>
        /// 0 = fully zoomed in, 1 = fully out. The zoom handle works in this
        /// space rather than in scale, because a handle has a fixed throw and
        /// wants a fixed fraction of the range per millimetre of travel -- a
        /// multiplicative scale would move much further at one end than the other.
        /// </summary>
        public void SetZoomNormalized(float normalized)
        {
            SetTargetZoom(Mathf.Lerp(zoomMinDistance, zoomMaxDistance, Mathf.Clamp01(normalized)));
        }

        /// <summary>The target the smoothing is heading toward, not where it is now.</summary>
        public float GetTargetZoomNormalized()
        {
            float range = zoomMaxDistance - zoomMinDistance;
            if (range <= 0f) return 0f;

            return Mathf.Clamp01((targetZoomDistance - zoomMinDistance) / range);
        }

        private void HandleZoomUpdate(float scaleFromStart)
        {
            if (scaleFromStart <= 0.01f) return;

            if (isDraftMode)
            {
                ApplyDraftZoom(scaleFromStart);
                return;
            }

            // Scale above 1 means the player asked to come closer, and closer
            // is a *smaller* dolly distance -- hence divide rather than
            // multiply. Getting this backwards is the classic inverted pinch.
            SetTargetZoom(zoomGestureStartDistance / scaleFromStart);
        }

        private void HandleZoomEnd()
        {
            EndZoomGesture();
        }

        private void RaiseZoomChanged()
        {
            if (ZoomChanged == null) return;

            // The draft's fit distance sits beyond zoomMaxDistance on purpose,
            // so normalizing against the gameplay ceiling during the draft
            // pegged this at exactly 1.0 for every draft zoom level -- the
            // readout's own ceiling is used instead whenever one is in play.
            float ceiling = isDraftMode
                ? (draftFitZoomDistance > 0f ? draftFitZoomDistance : zoomMaxDistance)
                : zoomMaxDistance;

            float range = ceiling - zoomMinDistance;
            float normalized = range <= 0f
                ? 0f
                : Mathf.Clamp01((targetZoomDistance - zoomMinDistance) / range);

            ZoomChanged.Invoke(normalized);
        }

        private void HandlePanBegin(Vector2 screenPos)
        {
            if (isDraftMode) return;

            // A lasso latches PanSuppressed for the rest of its stroke. Long
            // press is reserved for multi-select and the camera may never
            // steal it.
            if (gestureSource != null && gestureSource.PanSuppressed) return;

            gesturePanActive = true;
            gesturePanLastWorld = ScreenToGroundPoint(screenPos);

            panVelocity = Vector3.zero;

            // Abandon any focus move in flight and mark the session, so
            // dismissal will not undo where the player just put the camera.
            isFocusing = false;
            NotifyManualPan();
        }

        private void HandlePanUpdate(Vector2 screenPos)
        {
            if (!gesturePanActive || isDraftMode) return;

            if (gestureSource != null && gestureSource.PanSuppressed)
            {
                gesturePanActive = false;
                return;
            }

            ApplyPanDelta(screenPos, ref gesturePanLastWorld);
        }

        private void HandlePanEnd()
        {
            gesturePanActive = false;
        }

        private void OnDestroy()
        {
            if (gestureSource != null)
            {
                gestureSource.OnPanBegin -= HandlePanBegin;
                gestureSource.OnPanUpdate -= HandlePanUpdate;
                gestureSource.OnPanEnd -= HandlePanEnd;
                gestureSource.OnZoomBegin -= HandleZoomBegin;
                gestureSource.OnZoomUpdate -= HandleZoomUpdate;
                gestureSource.OnZoomEnd -= HandleZoomEnd;
            }
        }

        // ===== FOCUS =====
        //
        // The camera moves for exactly one reason: the selected node would sit
        // behind the panel. Two states, one rule -- either it moved to clear the
        // sheet or it did not. There is no averaged position between them.

        [Header("Focus")]
        [Tooltip("Duration of the move that clears the panel. Short enough to " +
                 "read as one motion rather than a journey.")]
        [SerializeField] private float focusDuration = 0.28f;

        [Tooltip("Extra gap above the panel edge, in screen pixels, so the node " +
                 "clears it rather than touching it.")]
        [SerializeField] private float focusMarginPx = 48f;

        [Tooltip("Logs every focus decision: whether the node was occluded, " +
                 "the delta computed, and whether bounds clamped it away.")]
        [SerializeField] private bool verboseFocusLogging = false;

        private bool isFocusing;
        private Vector3 focusFrom;
        private Vector3 focusTo;
        private float focusElapsed;

        // Session state. A session spans one selection: it opens when a panel
        // opens and closes when it dismisses.
        private bool sessionActive;
        private Vector3 sessionReturnPosition;
        private bool sessionPanDirty;

        /// <summary>
        /// Marks the focus session dirty after a manual pan, zoom or recentre.
        /// </summary>
        private void NotifyManualPan()
        {
            if (sessionActive) sessionPanDirty = true;
        }

        /// <summary>
        /// Captures the position to return to. Taken before any focus move and
        /// only once per session, so a second selection inside the same session
        /// cannot overwrite the origin with an already-focused position.
        /// </summary>
        public void BeginFocusSession()
        {
            if (sessionActive) return;

            sessionActive = true;
            sessionPanDirty = false;

            // Momentum is zeroed first so the captured origin is where the
            // camera actually rests, not a point it is still drifting through.
            panVelocity = Vector3.zero;
            sessionReturnPosition = transform.position;
        }

        /// <summary>
        /// Ends the session. Returns to the captured position only if the
        /// player never panned; if they did, the camera stays where they left
        /// it.
        /// </summary>
        public void EndFocusSession()
        {
            if (!sessionActive) return;

            bool shouldReturn = !sessionPanDirty;
            sessionActive = false;
            sessionPanDirty = false;

            if (verboseFocusLogging)
                Debug.Log("[FOCUS] session end -- " + (shouldReturn
                    ? "returning to " + sessionReturnPosition
                    : "player panned, staying put"));

            if (shouldReturn) StartFocusTween(sessionReturnPosition);
        }

        /// <summary>
        /// Moves the camera only far enough to lift a world point clear of a
        /// panel occupying the bottom <paramref name="panelHeightPx"/> pixels.
        /// A point already above that band does not move the camera at all.
        ///
        /// The delta needs no basis maths. Project the point to the screen, ask
        /// where the ground sits under it and under the position it should end
        /// up at, and take the difference: moving the rig by (A - B) puts the
        /// world point at A onto the screen position that currently shows B.
        /// The pivot's yaw and pitch are already baked into both rays, so there
        /// is no rotation to decompose and no chance of getting that wrong.
        /// </summary>
        public void FocusToClearPanel(Vector3 worldPos, Rect panelScreenRect)
        {
            if (cam == null || isDraftMode) return;

            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

            // Behind the camera: the projection is mirrored and any delta from
            // it would move the wrong way. Centre instead.
            if (screenPos.z < 0f)
            {
                StartFocusTween(worldPos);
                return;
            }

            // Rect rather than a bottom band, so the test is correct whatever
            // edge the panel is anchored to. A node beside the panel is not
            // occluded by it and must not move the camera.
            bool occluded = panelScreenRect.Contains(new Vector2(screenPos.x, screenPos.y));

            if (verboseFocusLogging)
                Debug.Log("[FOCUS] node screen " + (Vector2)screenPos +
                          " vs panel " + panelScreenRect +
                          " -> " + (occluded ? "OCCLUDED" : "clear, no move"));

            if (!occluded) return;

            float clearY = panelScreenRect.yMax + focusMarginPx;
            float needed = clearY - screenPos.y;

            // Already clear. Requirement is explicit that this does nothing.
            if (needed <= 0f)
            {
                if (verboseFocusLogging)
                    Debug.Log("[FOCUS] already above panel top, no move");
                return;
            }

            Vector2 from = new Vector2(screenPos.x, screenPos.y);
            Vector2 to = new Vector2(screenPos.x, screenPos.y + needed);

            Vector3 groundFrom = ScreenToGroundPoint(from);
            Vector3 groundTo = ScreenToGroundPoint(to);

            Vector3 delta = groundFrom - groundTo;
            delta.y = 0f;

            if (verboseFocusLogging)
                Debug.Log("[FOCUS] need +" + needed.ToString("0") + "px -> world delta " + delta);

            StartFocusTween(transform.position + delta);
        }

        private void StartFocusTween(Vector3 target)
        {
            focusFrom = transform.position;
            focusTo = ClampToBounds(target);
            focusTo.y = transform.position.y;
            focusElapsed = 0f;
            isFocusing = true;

            // Momentum would otherwise resume the instant the tween ends.
            panVelocity = Vector3.zero;

            if (verboseFocusLogging)
            {
                Vector3 moved = focusTo - focusFrom;
                Debug.Log("[FOCUS] tween " + focusFrom + " -> " + focusTo +
                          "  (moved " + moved.magnitude.ToString("0.00") + "u" +
                          (moved.magnitude < 0.001f ? ", CLAMPED TO NOTHING -- target outside board bounds" : "") + ")");
            }
        }

        private void ApplyFocus()
        {
            if (!isFocusing) return;

            focusElapsed += Time.deltaTime;

            float t = focusDuration <= 0f ? 1f : Mathf.Clamp01(focusElapsed / focusDuration);

            // Smoothstep: eased at both ends so it reads as one motion rather
            // than a snap that decelerates.
            float eased = t * t * (3f - 2f * t);

            transform.position = Vector3.Lerp(focusFrom, focusTo, eased);

            if (t >= 1f)
            {
                transform.position = focusTo;
                isFocusing = false;
            }
        }

        /// <summary>
        /// Clamps a focus target inside the board bounds so the spring has
        /// nothing to correct when the tween lands.
        /// </summary>
        private Vector3 ClampToBounds(Vector3 position)
        {
            if (!useBounds || boardConfig == null) return position;

            position.x = Mathf.Clamp(position.x, boardConfig.boundsMinX, boardConfig.boundsMaxX);
            position.z = Mathf.Clamp(position.z, boardConfig.boundsMinZ, boardConfig.boundsMaxZ);
            return position;
        }

        // ===== PUBLIC API =====

        /// <summary>
        /// Computes default per-side camera positions from grid dimensions.
        /// Called once by GameManager after BoardConfig is available.
        /// </summary>
        public void InitializeSides(BoardConfig config)
        {
            float centerX = (config.Data.gridCols - 1) * config.nodeScale * 0.5f;
            float maxZ = (config.Data.gridRows - 1) * config.nodeScale;
            float zOffset = config.nodeScale * sideZOffsetFactor;
            float defaultZoom = Mathf.Lerp(zoomMinDistance, zoomMaxDistance, sideDefaultZoomNormalized);

            // Pushed off your own edge toward the middle. The old framing sat
            // the rig on the player's back row, which spent most of the screen
            // on empty space behind them and cut the opponent's half off at the
            // top. Biasing down-board costs nothing at your end -- your core is
            // still comfortably in frame -- and buys the half of the board where
            // the threat comes from.
            float bias = maxZ * sideOpponentBiasFactor;

            // P0: high-Z side, looking toward -Z. Their opponent is at low Z,
            // so the bias subtracts.
            sideStates[0] = new SideState
            {
                position = new Vector3(centerX + zOffset * sideP0LateralNudgeFactor, 0f, maxZ + zOffset - bias),
                zoomDistance = defaultZoom,
                initialized = true
            };

            // P1: low-Z side, looking toward +Z. Their opponent is at high Z,
            // so the same bias adds.
            sideStates[1] = new SideState
            {
                position = new Vector3(centerX, 0f, zOffset * sideP1ZPositionFactor + bias),
                zoomDistance = defaultZoom,
                initialized = true
            };

            sideHomeStates[0] = sideStates[0];
            sideHomeStates[1] = sideStates[1];

            // Kept so SetHomeAnchor can apply the same push when the real core
            // positions arrive, without recomputing the board's depth.
            opponentBias = bias;
        }

        /// <summary>
        /// Replaces a side's guessed framing with one built on where that
        /// player's Core actually is, once the board exists.
        ///
        /// The rig position is what the camera looks at, so this is the point
        /// that ends up in the middle of the screen. Your core, pushed toward
        /// the opponent by the same bias the defaults use -- centred exactly on
        /// the core would spend half the screen on the empty ground behind you.
        ///
        /// Called during setup, before the player has touched anything, so a
        /// side that has not been visited is moved outright rather than tweened.
        /// </summary>
        public void SetHomeAnchor(int playerID, Vector3 coreWorldPosition)
        {
            if (playerID < 0 || playerID > 1) return;

            // P0 sits at high Z and faces -Z, so its opponent is the way the
            // bias subtracts; P1 is the mirror.
            float bias = playerID == 0 ? -opponentBias : opponentBias;

            Vector3 home = new Vector3(
                coreWorldPosition.x,
                0f,
                coreWorldPosition.z + bias);

            sideHomeStates[playerID].position = home;
            sideHomeStates[playerID].initialized = true;

            // InitializeSides normally set this already. If it did not, a zero
            // would recentre the player to the closest clamp every time.
            if (sideHomeStates[playerID].zoomDistance <= 0f)
            {
                sideHomeStates[playerID].zoomDistance =
                    Mathf.Lerp(zoomMinDistance, zoomMaxDistance, sideDefaultZoomNormalized);
            }

            // The stored framing is only overwritten while it is still the
            // guess. Once a side has been played, that is the player's camera
            // and setup has no business moving it.
            if (!sideHasBeenSet || playerID != currentSide)
            {
                sideStates[playerID].position = home;
                sideStates[playerID].initialized = true;
            }

            // Never during the draft: that phase has deliberately framed the
            // whole board from its centre, and this would drag it to one end.
            if (playerID == currentSide && !sessionActive && !isDraftMode)
            {
                transform.position = new Vector3(home.x, transform.position.y, home.z);
                panVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Stores departing side's state, restores arriving side's state, flips pivot rotation.
        /// </summary>
        public void SetPlayerSide(int playerID)
        {
            if (cameraPivot == null) return;

            // Store current (skip first call — scene start position is meaningless)
            if (sideHasBeenSet && sideStates[currentSide].initialized)
            {
                sideStates[currentSide].position = transform.position;
                sideStates[currentSide].zoomDistance = targetZoomDistance;
            }

            currentSide = playerID;
            sideHasBeenSet = true;

            if (sideStates[currentSide].initialized)
            {
                transform.position = sideStates[currentSide].position;
                targetZoomDistance = sideStates[currentSide].zoomDistance;
                currentZoomDistance = targetZoomDistance;
            }

            if (cam != null)
            {
                // P0 faces -Z (pivot Y=180): higher Z is further back, axis points +Z
                // P1 faces +Z (pivot Y=0): lower Z is further back, axis points -Z
                cam.transparencySortAxis = (playerID == 0)
                    ? new Vector3(0f, 0f, 1f)
                    : new Vector3(0f, 0f, -1f);
            }

            panVelocity = Vector3.zero;

            // P0 faces -Z (Y=180), P1 faces +Z (Y=0)
            float yRotation = (playerID == 0) ? 180f : 0f;
            cameraPivot.localRotation = Quaternion.Euler(
                cameraPivot.localRotation.eulerAngles.x, yRotation, 0f);
        }

        public float GetCurrentZoomDistance() => currentZoomDistance;

        /// <summary>
        /// Where the zoom is heading, before smoothing. Anything reporting the
        /// zoom to the player wants this: ZoomChanged fires the moment the
        /// target moves, and the current distance at that moment is still the
        /// old one.
        /// </summary>
        public float GetTargetZoomDistance() => targetZoomDistance;

        /// <summary>
        /// The distance the match starts at for the current side. The readout
        /// divides by this to show a magnification, so "1.0x" means the framing
        /// the player was given rather than an arbitrary point in the range.
        /// </summary>
        public float DefaultZoomDistance
        {
            get
            {
                if (sideHomeStates[currentSide].initialized)
                    return sideHomeStates[currentSide].zoomDistance;

                return Mathf.Lerp(zoomMinDistance, zoomMaxDistance, sideDefaultZoomNormalized);
            }
        }

        // ===== RECENTRE =====
        //
        // The click half of the HUD's zoom handle. The drag half zooms; this
        // puts the camera back where the match started it.

        /// <summary>
        /// Returns to this side's default framing and zoom. Routed through the
        /// focus tween so it is one eased motion rather than a snap, and marked
        /// as manual so dismissing a sheet afterwards does not undo it.
        /// </summary>
        public void RecentreOnHome()
        {
            if (isDraftMode || !sideHomeStates[currentSide].initialized) return;

            NotifyManualPan();
            SetTargetZoom(sideHomeStates[currentSide].zoomDistance);
            StartFocusTween(sideHomeStates[currentSide].position);
        }

        /// <summary>
        /// Frames the board for the draft. Locks pan; scroll and pinch still zoom.
        /// </summary>
        public void SetDraftMode(bool enabled)
        {
            if (enabled == isDraftMode) return;

            isDraftMode = enabled;

            if (!enabled)
            {
                // The draft borrows the pivot's pitch and must give it back.
                // Without this the near-top-down draft angle survived into
                // play: SetPlayerSide rebuilds the rotation as
                // Euler(eulerAngles.x, yaw, 0), preserving whatever X it finds,
                // so every match after a draft ran at the draft's angle instead
                // of the one authored on the pivot.
                if (cameraPivot != null && hasStashedPitch)
                {
                    Vector3 euler = cameraPivot.localRotation.eulerAngles;
                    cameraPivot.localRotation = Quaternion.Euler(stashedPitch, euler.y, 0f);
                    hasStashedPitch = false;
                }

                // The draft fits the whole board, which is far outside the
                // gameplay clamp. Leaving it there would strand the first zoom
                // input at the maximum with no visible response.
                targetZoomDistance = Mathf.Clamp(targetZoomDistance, zoomMinDistance, zoomMaxDistance);
                currentZoomDistance = targetZoomDistance;
                return;
            }

            panVelocity = Vector3.zero;

            // Centre first, then the authored offset. Expressed as an offset
            // rather than an absolute position so it survives a board of a
            // different size: X stays on the board's midline and Z pulls back
            // toward the near edge, which is what a 60-degree pitch needs to
            // put the far row in frame.
            ResetToCenter();
            transform.position += draftRigOffset;

            // The zoom-out ceiling, not the opening distance. Seeing the whole
            // board is the point of the phase, so the pinch may go past the
            // gameplay clamp - but never so short that the authored opening
            // framing would itself be clamped away.
            float gridWidth = 0f;
            float gridHeight = 0f;
            if (boardConfig != null)
            {
                gridWidth = (boardConfig.Data.gridCols - 1) * boardConfig.nodeScale;
                gridHeight = (boardConfig.Data.gridRows - 1) * boardConfig.nodeScale;
            }

            float ceiling = Mathf.Max(gridWidth, gridHeight) * draftZoomBoardMultiplier;
            if (draftZoomNeverBelowMax)
                ceiling = Mathf.Max(ceiling, zoomMaxDistance);

            draftFitZoomDistance = Mathf.Max(ceiling, draftZoomDistance);

            targetZoomDistance = Mathf.Clamp(draftZoomDistance, zoomMinDistance, draftFitZoomDistance);
            currentZoomDistance = targetZoomDistance;

            if (cameraPivot != null)
            {
                stashedPitch = cameraPivot.localRotation.eulerAngles.x;
                hasStashedPitch = true;
                cameraPivot.localRotation = Quaternion.Euler(draftPitch, 0f, 0f);
            }
        }

        // The pivot pitch the draft displaced, held so it can be restored.
        private float stashedPitch;
        private bool hasStashedPitch;

        // The whole-board fit computed when the draft opened. Zooming out during
        // the draft stops here rather than at the gameplay clamp -- seeing the
        // board is the point of the phase, and further out is only void.
        private float draftFitZoomDistance;

        /// <summary>
        /// Draft zoom. The gameplay clamp does not apply: the draft's fit
        /// distance is deliberately beyond zoomMaxDistance, so clamping to the
        /// gameplay range here would snap the board out of frame the moment the
        /// player touched the pinch.
        /// </summary>
        private void ApplyDraftZoom(float scaleFromStart)
        {
            if (scaleFromStart <= 0.01f) return;

            float fit = draftFitZoomDistance > 0f ? draftFitZoomDistance : zoomMaxDistance;

            SetTargetZoomClamped(zoomGestureStartDistance / scaleFromStart, fit, manual: false);
        }

        private void ResetToCenter()
        {
            panVelocity = Vector3.zero;

            if (boardConfig != null)
            {
                float centerX = (boardConfig.Data.gridCols - 1) * boardConfig.nodeScale * 0.5f;
                float centerZ = (boardConfig.Data.gridRows - 1) * boardConfig.nodeScale * 0.5f;
                transform.position = new Vector3(centerX, 0f, centerZ);
            }
            else
            {
                Vector3 pos = transform.position;
                pos.x = 0f;
                pos.z = 0f;
                transform.position = pos;
            }
        }

        // ===== HELPERS =====

        /// <summary>
        /// Raycast from screen position to XZ ground plane (Y=0).
        /// </summary>
        private Vector3 GetMouseWorldPosition(Mouse mouse)
        {
            return ScreenToGroundPoint(mouse.position.ReadValue());
        }

        /// <summary>
        /// Raycast from any screen position to the XZ ground plane (Y=0).
        /// Returns the rig position for a missing camera or parallel ray, and
        /// the ray origin when the plane is behind the ray.
        /// </summary>
        public Vector3 ScreenToGroundPoint(Vector2 screenPos)
        {
            return TryScreenToGroundPoint(cam, screenPos, out Vector3 point, clampBehindCamera: true)
                ? point
                : transform.position;
        }

        /// <summary>
        /// Shared ground projection for camera controls and UI. The supplied
        /// camera preserves each caller's view without requiring rig wiring.
        /// Missing cameras and parallel rays return false. By default a plane
        /// behind the ray also returns false; clamping instead returns the ray
        /// origin and true. The output is zero on failure.
        /// </summary>
        public static bool TryScreenToGroundPoint(Camera camera, Vector2 screenPos,
            out Vector3 point, bool clampBehindCamera = false)
        {
            point = Vector3.zero;
            if (camera == null) return false;

            Ray ray = camera.ScreenPointToRay(screenPos);

            if (Mathf.Abs(ray.direction.y) < 0.0001f)
                return false;

            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f)
            {
                if (!clampBehindCamera) return false;
                t = 0f;
            }

            point = ray.origin + ray.direction * t;
            return true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!useBounds) return;

            float minX = boardConfig != null ? boardConfig.boundsMinX : -12f;
            float maxX = boardConfig != null ? boardConfig.boundsMaxX : 12f;
            float minZ = boardConfig != null ? boardConfig.boundsMinZ : -8f;
            float maxZ = boardConfig != null ? boardConfig.boundsMaxZ : 8f;

            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
            Gizmos.DrawLine(new Vector3(minX, 0f, minZ), new Vector3(maxX, 0f, minZ));
            Gizmos.DrawLine(new Vector3(maxX, 0f, minZ), new Vector3(maxX, 0f, maxZ));
            Gizmos.DrawLine(new Vector3(maxX, 0f, maxZ), new Vector3(minX, 0f, maxZ));
            Gizmos.DrawLine(new Vector3(minX, 0f, maxZ), new Vector3(minX, 0f, minZ));

            Gizmos.color = new Color(1f, 1f, 0f, 0.05f);
            Vector3 center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            Vector3 size = new Vector3(maxX - minX, 0.01f, maxZ - minZ);
            Gizmos.DrawCube(center, size);
        }
#endif
    }
}
