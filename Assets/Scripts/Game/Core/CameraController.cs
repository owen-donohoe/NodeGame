using NodeWar.Simulation;
using NodeWar.Config;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NodeWar.Core
{
    /// <summary>
    /// Perspective camera controller with drag-to-pan, momentum, dolly zoom, 
    /// bounds, shake, draft-mode framing, and per-player-side memory.
    /// 
    /// Hierarchy (set up in Editor):
    ///   CameraRig [this script] — world X/Z position
    ///     ? CameraPivot — rotation only (viewing angle)
    ///           ? Camera — local Z = -zoomDistance (dolly)
    ///
    /// Middle mouse to drag. Scroll to zoom.
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

        [Header("Shake Defaults")]
        [SerializeField] private float defaultShakeIntensity = 0.15f;
        [SerializeField] private float defaultShakeRotationalIntensity = 0.5f;
        [SerializeField] private float defaultShakeDuration = 0.4f;
        [SerializeField] private float defaultShakeFrequency = 25f;
        [SerializeField] private float shakeMaxPositionalOffset = 0.5f;
        [SerializeField] private float shakeMaxRotationalOffset = 3f;
        [SerializeField] private AnimationCurve shakeFalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        [Header("Draft Mode Framing")]
        [Tooltip("Multiplier on largest grid dimension to determine zoom distance during draft.")]
        [SerializeField][Range(1.0f, 3.0f)] private float draftZoomBoardMultiplier = 1.3f;
        [Tooltip("Pivot X angle during draft. Higher = more top-down.")]
        [SerializeField][Range(30f, 90f)] private float draftPivotAngle = 70f;
        [Tooltip("If true, draft zoom never goes below zoomMaxDistance.")]
        [SerializeField] private bool draftZoomNeverBelowMax = true;
        [Tooltip("Scroll sensitivity for vertical panning during draft mode. Allows seeing full board.")]
        [SerializeField] private float draftScrollPanSensitivity = 2f;

        [Header("Per-Side Defaults")]
        [Tooltip("Normalized position between min/max zoom for gameplay start. 0=closest, 1=farthest.")]
        [SerializeField][Range(0f, 1f)] private float sideDefaultZoomNormalized = 0.65f;
        [Tooltip("Fraction of nodeScale used as Z offset from board edge.")]
        [SerializeField][Range(0f, 1f)] private float sideZOffsetFactor = 0.35f;
        [SerializeField][Range(0f, 1f)] private float sideP0LateralNudgeFactor = 0.5f;
        [SerializeField][Range(0f, 1f)] private float sideP1ZPositionFactor = 0.6f;

        [Header("Sprite Rotation Fallback")]
        [Tooltip("Returned by GetSpriteRotation() if cameraPivot is null.")]
        [SerializeField] private Vector3 spriteRotationFallback = new Vector3(50f, 0f, 0f);

        // Pan state
        private Vector3 panVelocity;
        private bool isDragging;
        private Vector3 lastMouseWorldPos;

        // Zoom state
        private float targetZoomDistance;
        private float currentZoomDistance;

        // Shake state
        private float shakeTimeRemaining;
        private float shakeDuration;
        private float shakeIntensity;
        private float shakeRotationalIntensity;
        private float shakeFrequency;
        private float shakeSeed;

        // Side memory
        private struct SideState
        {
            public Vector3 position;
            public float zoomDistance;
            public bool initialized;
        }

        private SideState[] sideStates = new SideState[2];
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

            currentZoomDistance = Mathf.Abs(cam.transform.localPosition.z);
            targetZoomDistance = currentZoomDistance;
            panVelocity = Vector3.zero;
        }

        private void Update()
        {
            HandleDragInput();
            HandleZoomInput();
            HandleDraftScroll();
            ApplyFocus();
            ApplyMomentum();
            ApplyZoom();
            ApplyBounds();
            ApplyShake();
        }

        // ===== PAN =====

        private void HandleDragInput()
        {
            if (isDraftMode) return;

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
            {
                Vector3 currentMouseWorld = GetMouseWorldPosition(mouse);
                Vector3 delta = lastMouseWorldPos - currentMouseWorld;

                transform.position += delta * panSpeed;
                panVelocity = delta * panSpeed / Time.deltaTime;

                if (panVelocity.magnitude > panMaxVelocity)
                    panVelocity = panVelocity.normalized * panMaxVelocity;

                // Recalculate after move to prevent perspective drift
                lastMouseWorldPos = GetMouseWorldPosition(mouse);
            }

            if (mouse.middleButton.wasReleasedThisFrame)
                isDragging = false;
        }

        private void HandleDraftScroll()
        {
            if (!isDraftMode) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.01f) return;

            // Scroll moves camera along Z axis to view different parts of the board
            Vector3 pos = transform.position;
            pos.z += scroll * draftScrollPanSensitivity * 0.1f;
            transform.position = pos;
        }

        private void ApplyMomentum()
        {
            // The focus tween owns transform.position while it runs. Two
            // writers in one frame is how a focus move ends with a visible
            // slide, so momentum yields rather than blending.
            if (isFocusing) return;
            if (isDragging) return;
            if (panVelocity.sqrMagnitude < 0.0001f) return;

            transform.position += panVelocity * Time.deltaTime;
            panVelocity = Vector3.Lerp(panVelocity, Vector3.zero, panMomentumDamping * Time.deltaTime);

            if (panVelocity.sqrMagnitude < 0.001f)
                panVelocity = Vector3.zero;
        }

        // ===== ZOOM =====

        private void HandleZoomInput()
        {
            if (isDraftMode) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // Proportional: zoom feels consistent at any distance
                targetZoomDistance -= scroll * zoomScrollSensitivity * 0.01f * targetZoomDistance;
                targetZoomDistance = Mathf.Clamp(targetZoomDistance, zoomMinDistance, zoomMaxDistance);
            }
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

            // Soft spring pushback rather than hard clamp — feels natural
            if (pos.x < boardConfig.boundsMinX)
                panVelocity.x += boundsPushbackForce * (boardConfig.boundsMinX - pos.x) * Time.deltaTime;
            if (pos.x > boardConfig.boundsMaxX)
                panVelocity.x += boundsPushbackForce * (boardConfig.boundsMaxX - pos.x) * Time.deltaTime;
            if (pos.z < boardConfig.boundsMinZ)
                panVelocity.z += boundsPushbackForce * (boardConfig.boundsMinZ - pos.z) * Time.deltaTime;
            if (pos.z > boardConfig.boundsMaxZ)
                panVelocity.z += boundsPushbackForce * (boardConfig.boundsMaxZ - pos.z) * Time.deltaTime;
        }

        // ===== SHAKE =====

        public void Shake()
        {
            Shake(defaultShakeIntensity, defaultShakeRotationalIntensity,
                  defaultShakeDuration, defaultShakeFrequency);
        }

        /// <summary>
        /// Stronger shake wins if one is already active.
        /// </summary>
        public void Shake(float intensity, float rotationalIntensity, float duration, float frequency)
        {
            if (intensity >= shakeIntensity || shakeTimeRemaining < 0.05f)
            {
                shakeIntensity = intensity;
                shakeRotationalIntensity = rotationalIntensity;
                shakeDuration = duration;
                shakeTimeRemaining = duration;
                shakeFrequency = frequency;
                shakeSeed = Random.Range(0f, 1000f);
            }
        }

        private void ApplyShake()
        {
            if (cam == null) return;

            if (shakeTimeRemaining <= 0f)
            {
                cam.transform.localRotation = Quaternion.identity;
                return;
            }

            shakeTimeRemaining -= Time.deltaTime;
            if (shakeTimeRemaining < 0f) shakeTimeRemaining = 0f;

            float normalizedTime = 1f - (shakeTimeRemaining / shakeDuration);
            float envelope = shakeFalloffCurve.Evaluate(normalizedTime);
            float time = (shakeDuration - shakeTimeRemaining) * shakeFrequency;

            // Perlin noise per axis with offset seeds for variety
            float noiseX = (Mathf.PerlinNoise(shakeSeed + time, 0f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0f, shakeSeed + time) - 0.5f) * 2f;
            float noiseZ = (Mathf.PerlinNoise(shakeSeed + time, shakeSeed + time) - 0.5f) * 2f;

            Vector3 posOffset = new Vector3(noiseX, noiseY, 0f) * shakeIntensity * envelope;
            posOffset.x = Mathf.Clamp(posOffset.x, -shakeMaxPositionalOffset, shakeMaxPositionalOffset);
            posOffset.y = Mathf.Clamp(posOffset.y, -shakeMaxPositionalOffset, shakeMaxPositionalOffset);

            cam.transform.localPosition = new Vector3(
                posOffset.x, posOffset.y, -currentZoomDistance + posOffset.z * 0.5f);

            Vector3 rotOffset = new Vector3(noiseY, noiseX, noiseZ) * shakeRotationalIntensity * envelope;
            rotOffset.x = Mathf.Clamp(rotOffset.x, -shakeMaxRotationalOffset, shakeMaxRotationalOffset);
            rotOffset.y = Mathf.Clamp(rotOffset.y, -shakeMaxRotationalOffset, shakeMaxRotationalOffset);
            rotOffset.z = Mathf.Clamp(rotOffset.z, -shakeMaxRotationalOffset, shakeMaxRotationalOffset);

            cam.transform.localRotation = Quaternion.Euler(rotOffset);
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
        /// True once the player has panned during the current session. Manual
        /// input is never undone, so this decides whether dismissal restores
        /// the previous position or leaves the camera where they put it.
        /// </summary>
        public bool SessionPanDirty => sessionPanDirty;

        /// <summary>
        /// Marks the current session as manually panned. Called by any
        /// player-initiated camera movement -- including a notification tap,
        /// which is an instruction, not an automatic move.
        /// </summary>
        public void NotifyManualPan()
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

        /// <summary>
        /// True if a world point projects inside the viewport, inset by a
        /// normalised margin. Used by off-screen notification indicators.
        /// </summary>
        public bool IsPointOnScreen(Vector3 worldPos, float viewportMargin)
        {
            if (cam == null) return false;

            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            if (vp.z < 0f) return false;

            return vp.x >= viewportMargin && vp.x <= 1f - viewportMargin
                && vp.y >= viewportMargin && vp.y <= 1f - viewportMargin;
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

            // P0: high-Z side, looking toward -Z
            sideStates[0] = new SideState
            {
                position = new Vector3(centerX + zOffset * sideP0LateralNudgeFactor, 0f, maxZ + zOffset),
                zoomDistance = defaultZoom,
                initialized = true
            };

            // P1: low-Z side, looking toward +Z
            sideStates[1] = new SideState
            {
                position = new Vector3(centerX, 0f, zOffset * sideP1ZPositionFactor),
                zoomDistance = defaultZoom,
                initialized = true
            };
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

        public Vector3 GetSpriteRotation()
        {
            if (cameraPivot == null) return spriteRotationFallback;
            return cameraPivot.localRotation.eulerAngles;
        }

        public void SetTargetZoom(float distance)
        {
            targetZoomDistance = Mathf.Clamp(distance, zoomMinDistance, zoomMaxDistance);
        }

        public float GetCurrentZoomDistance() => currentZoomDistance;

        /// <summary>
        /// 0 = fully zoomed in, 1 = fully zoomed out.
        /// </summary>
        public float GetZoomNormalized()
        {
            if (zoomMaxDistance <= zoomMinDistance) return 0f;
            return (currentZoomDistance - zoomMinDistance) / (zoomMaxDistance - zoomMinDistance);
        }

        /// <summary>
        /// Locks camera to centered bird's-eye for draft phase. Disables pan/zoom input.
        /// </summary>
        public void SetDraftMode(bool enabled)
        {
            isDraftMode = enabled;

            if (!enabled) return;

            panVelocity = Vector3.zero;
            ResetToCenter();

            // Fit board with configured padding
            float gridWidth = 0f;
            float gridHeight = 0f;
            if (boardConfig != null)
            {
                gridWidth = (boardConfig.Data.gridCols - 1) * boardConfig.nodeScale;
                gridHeight = (boardConfig.Data.gridRows - 1) * boardConfig.nodeScale;
            }

            float neededZoom = Mathf.Max(gridWidth, gridHeight) * draftZoomBoardMultiplier;
            if (draftZoomNeverBelowMax)
                neededZoom = Mathf.Max(neededZoom, zoomMaxDistance);

            targetZoomDistance = neededZoom;
            currentZoomDistance = neededZoom;

            if (cameraPivot != null)
                cameraPivot.localRotation = Quaternion.Euler(draftPivotAngle, 0f, 0f);
        }

        public void ResetToCenter()
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
        ///
        /// Generalised from the mouse-only version because the focus rule needs
        /// it for two arbitrary screen points. Every projection in this file
        /// goes through here, so the degenerate cases -- a ray parallel to the
        /// plane, and a plane behind the camera -- are handled once.
        /// </summary>
        public Vector3 ScreenToGroundPoint(Vector2 screenPos)
        {
            if (cam == null) return transform.position;

            Ray ray = cam.ScreenPointToRay(screenPos);

            if (Mathf.Abs(ray.direction.y) < 0.0001f)
                return transform.position;

            float t = -ray.origin.y / ray.direction.y;
            if (t < 0) t = 0;

            return ray.origin + ray.direction * t;
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