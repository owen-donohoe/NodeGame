using NodeWar.Simulation;
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

        private void ApplyMomentum()
        {
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

        // ===== PUBLIC API =====

        /// <summary>
        /// Computes default per-side camera positions from grid dimensions.
        /// Called once by GameManager after BoardConfig is available.
        /// </summary>
        public void InitializeSides(BoardConfig config)
        {
            float centerX = (config.gridCols - 1) * config.nodeScale * 0.5f;
            float maxZ = (config.gridRows - 1) * config.nodeScale;
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
                gridWidth = (boardConfig.gridCols - 1) * boardConfig.nodeScale;
                gridHeight = (boardConfig.gridRows - 1) * boardConfig.nodeScale;
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
                float centerX = (boardConfig.gridCols - 1) * boardConfig.nodeScale * 0.5f;
                float centerZ = (boardConfig.gridRows - 1) * boardConfig.nodeScale * 0.5f;
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
            Vector2 screenPos = mouse.position.ReadValue();
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