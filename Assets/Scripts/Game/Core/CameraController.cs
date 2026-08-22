using NodeWar.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NodeWar.Core
{
    /// <summary>
    /// Perspective camera controller with drag-to-pan, velocity momentum,
    /// variable dampening, dolly zoom, bounds, and camera shake.
    /// 
    /// Hierarchy (set up in Editor):
    ///   CameraRig [this script] — world X/Z position, pan target
    ///     ??? CameraPivot — rotation only (viewing angle, e.g. X=50)
    ///           ??? Camera — local Z = -zoomDistance (dolly)
    ///
    /// Middle mouse button to drag. Scroll wheel to zoom.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Board Config")]
        [SerializeField] private BoardConfig boardConfig;

        [Header("References")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Camera cam;

        [Header("Pan Settings")]
        [SerializeField] private float panSpeed = 1f;
        [SerializeField] private float momentumDamping = 5f;
        [SerializeField] private float maxVelocity = 20f;

        [Header("Zoom Settings (Dolly)")]
        [SerializeField] private float zoomSpeed = 2f;
        [SerializeField] private float minZoomDistance = 5f;
        [SerializeField] private float maxZoomDistance = 30f;
        [SerializeField] private float zoomDamping = 8f;

        [Header("Bounds")]
        [SerializeField] private bool useBounds = true;
        [SerializeField] private float boundsPushback = 10f;

        [Header("Shake Settings")]
        [SerializeField] private float defaultShakeIntensity = 0.15f;
        [SerializeField] private float defaultShakeRotationalIntensity = 0.5f;
        [SerializeField] private float defaultShakeDuration = 0.4f;
        [SerializeField] private float defaultShakeFrequency = 25f;
        [SerializeField] private float maxShakeOffset = 0.5f;
        [SerializeField] private float maxShakeRotation = 3f;
        [SerializeField] private AnimationCurve shakeFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        // Per-side camera memory
        private struct SideState
        {
            public Vector3 position;
            public float zoomDistance;
            public bool initialized;
        }

        private SideState[] sideStates = new SideState[2];
        private int currentSide = 0;
        private bool sideHasBeenSet = false;

        // Pan state
        private Vector3 velocity;
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
        private float shakeSeed; // Unique per shake instance for varied Perlin sampling

        private void Awake()
        {
            if (cam == null)
                cam = GetComponentInChildren<Camera>();
            if (cam == null)
                cam = Camera.main;

            if (cameraPivot == null)
            {
                // Fallback: find first child as pivot
                if (transform.childCount > 0)
                    cameraPivot = transform.GetChild(0);
            }

            // Initialize zoom from current camera position
            currentZoomDistance = Mathf.Abs(cam.transform.localPosition.z);
            targetZoomDistance = currentZoomDistance;
            velocity = Vector3.zero;
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
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.middleButton.wasPressedThisFrame)
            {
                isDragging = true;
                lastMouseWorldPos = GetMouseWorldPosition(mouse);
                velocity = Vector3.zero;
            }

            if (mouse.middleButton.isPressed && isDragging)
            {
                Vector3 currentMouseWorld = GetMouseWorldPosition(mouse);
                Vector3 delta = lastMouseWorldPos - currentMouseWorld;

                transform.position += delta * panSpeed;

                velocity = delta * panSpeed / Time.deltaTime;

                if (velocity.magnitude > maxVelocity)
                    velocity = velocity.normalized * maxVelocity;

                // Recalculate after move to prevent drift with perspective
                lastMouseWorldPos = GetMouseWorldPosition(mouse);
            }

            if (mouse.middleButton.wasReleasedThisFrame)
            {
                isDragging = false;
            }
        }

        private void ApplyMomentum()
        {
            if (isDragging) return;

            if (velocity.sqrMagnitude > 0.0001f)
            {
                transform.position += velocity * Time.deltaTime;

                velocity = Vector3.Lerp(velocity, Vector3.zero, momentumDamping * Time.deltaTime);

                if (velocity.sqrMagnitude < 0.001f)
                    velocity = Vector3.zero;
            }
        }

        // ===== ZOOM (DOLLY) =====

        private void HandleZoomInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // Zoom toward board (scroll up = closer = smaller distance)
                targetZoomDistance -= scroll * zoomSpeed * 0.01f * targetZoomDistance;
                targetZoomDistance = Mathf.Clamp(targetZoomDistance, minZoomDistance, maxZoomDistance);
            }
        }

        private void ApplyZoom()
        {
            if (cam == null) return;

            if (Mathf.Abs(currentZoomDistance - targetZoomDistance) > 0.01f)
            {
                currentZoomDistance = Mathf.Lerp(currentZoomDistance, targetZoomDistance, zoomDamping * Time.deltaTime);
            }
            else
            {
                currentZoomDistance = targetZoomDistance;
            }

            // Camera sits along pivot's -Z at zoom distance (no shake applied here)
            cam.transform.localPosition = new Vector3(0f, 0f, -currentZoomDistance);
        }

        // ===== BOUNDS =====

        private void ApplyBounds()
        {
            if (!useBounds || boardConfig == null) return;

            Vector3 pos = transform.position;

            float minX = boardConfig.boundsMinX;
            float maxX = boardConfig.boundsMaxX;
            float minZ = boardConfig.boundsMinZ;
            float maxZ = boardConfig.boundsMaxZ;

            if (pos.x < minX)
                velocity.x += boundsPushback * (minX - pos.x) * Time.deltaTime;
            if (pos.x > maxX)
                velocity.x += boundsPushback * (maxX - pos.x) * Time.deltaTime;
            if (pos.z < minZ)
                velocity.z += boundsPushback * (minZ - pos.z) * Time.deltaTime;
            if (pos.z > maxZ)
                velocity.z += boundsPushback * (maxZ - pos.z) * Time.deltaTime;
        }

        // ===== CAMERA SHAKE =====

        /// <summary>
        /// Trigger shake with default parameters.
        /// </summary>
        public void Shake()
        {
            Shake(defaultShakeIntensity, defaultShakeRotationalIntensity,
                  defaultShakeDuration, defaultShakeFrequency);
        }

        /// <summary>
        /// Trigger shake with custom parameters. Stacks by taking the stronger value
        /// if a shake is already active.
        /// </summary>
        public void Shake(float intensity, float rotationalIntensity, float duration, float frequency)
        {
            // If new shake is stronger or current is nearly done, override
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
                // No shake — ensure clean local rotation on camera
                cam.transform.localRotation = Quaternion.identity;
                return;
            }

            shakeTimeRemaining -= Time.deltaTime;
            if (shakeTimeRemaining < 0f) shakeTimeRemaining = 0f;

            // Normalized time (1 at start, 0 at end)
            float normalizedTime = 1f - (shakeTimeRemaining / shakeDuration);
            float envelope = shakeFalloff.Evaluate(normalizedTime);

            // Perlin-based smooth noise (two different seeds for X and Y)
            float time = (shakeDuration - shakeTimeRemaining) * shakeFrequency;

            float noiseX = (Mathf.PerlinNoise(shakeSeed + time, 0f) - 0.5f) * 2f;
            float noiseY = (Mathf.PerlinNoise(0f, shakeSeed + time) - 0.5f) * 2f;
            float noiseZ = (Mathf.PerlinNoise(shakeSeed + time, shakeSeed + time) - 0.5f) * 2f;

            // Positional offset (applied on top of zoom position)
            Vector3 posOffset = new Vector3(noiseX, noiseY, 0f) * shakeIntensity * envelope;
            posOffset.x = Mathf.Clamp(posOffset.x, -maxShakeOffset, maxShakeOffset);
            posOffset.y = Mathf.Clamp(posOffset.y, -maxShakeOffset, maxShakeOffset);

            cam.transform.localPosition = new Vector3(posOffset.x, posOffset.y, -currentZoomDistance + posOffset.z * 0.5f);

            // Rotational offset
            Vector3 rotOffset = new Vector3(noiseY, noiseX, noiseZ) * shakeRotationalIntensity * envelope;
            rotOffset.x = Mathf.Clamp(rotOffset.x, -maxShakeRotation, maxShakeRotation);
            rotOffset.y = Mathf.Clamp(rotOffset.y, -maxShakeRotation, maxShakeRotation);
            rotOffset.z = Mathf.Clamp(rotOffset.z, -maxShakeRotation, maxShakeRotation);

            cam.transform.localRotation = Quaternion.Euler(rotOffset);
        }

        // ===== MOUSE PROJECTION =====

        /// <summary>
        /// Projects mouse screen position to a world-space point on the XZ ground plane (Y=0).
        /// Works correctly with perspective camera at any angle/distance.
        /// </summary>
        private Vector3 GetMouseWorldPosition(Mouse mouse)
        {
            Vector2 screenPos = mouse.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(screenPos);

            // Intersect with XZ plane (Y = 0)
            if (Mathf.Abs(ray.direction.y) < 0.0001f)
                return transform.position; // Ray parallel to ground — fallback

            float t = -ray.origin.y / ray.direction.y;
            if (t < 0) t = 0;

            return ray.origin + ray.direction * t;
        }

        // ===== PUBLIC API =====

        /// <summary>
        /// Call once after boardConfig is available. Computes default centered positions
        /// for both sides based on grid dimensions.
        /// </summary>
        public void InitializeSides(BoardConfig config)
        {
            float centerX = (config.gridCols - 1) * config.nodeScale * 0.5f;
            float maxZ = (config.gridRows - 1) * config.nodeScale;
            float offset = config.nodeScale * 0.35f;
            float defaultZoom = Mathf.Lerp(minZoomDistance, maxZoomDistance, 0.65f);

            // P0: behind their core (high Z), looking toward -Z
            sideStates[0] = new SideState
            {
                position = new Vector3(centerX + offset * 0.5f, 0f, maxZ + offset),
                zoomDistance = defaultZoom,
                initialized = true
            };

            // P1: behind their core (low Z), looking toward +Z
            sideStates[1] = new SideState
            {
                position = new Vector3(centerX, 0f, offset * 0.6f),
                zoomDistance = defaultZoom,
                initialized = true
            };
        }

        /// <summary>
        /// Flip camera to view from the specified player's side.
        /// Stores current position/zoom for the old side, restores for the new side.
        /// 0 = behind P0's core (high Z), looking toward -Z.
        /// 1 = behind P1's core (low Z), looking toward +Z.
        /// </summary>
        public void SetPlayerSide(int playerID)
        {
            if (cameraPivot == null) return;

            // Store current side's state (skip first call — scene position is meaningless)
            if (sideHasBeenSet && sideStates[currentSide].initialized)
            {
                sideStates[currentSide].position = transform.position;
                sideStates[currentSide].zoomDistance = targetZoomDistance;
            }

            currentSide = playerID;
            sideHasBeenSet = true;

            // Restore target side's state
            if (sideStates[currentSide].initialized)
            {
                transform.position = sideStates[currentSide].position;
                targetZoomDistance = sideStates[currentSide].zoomDistance;
                currentZoomDistance = targetZoomDistance;
            }

            // Kill momentum on switch
            velocity = Vector3.zero;

            // Flip pivot Y rotation
            float yRotation = (playerID == 0) ? 180f : 0f;
            cameraPivot.localRotation = Quaternion.Euler(
                cameraPivot.localRotation.eulerAngles.x, yRotation, 0f);
        }

        /// <summary>
        /// Returns the euler rotation sprites should use to face the current camera.
        /// </summary>
        public Vector3 GetSpriteRotation()
        {
            if (cameraPivot == null) return new Vector3(50f, 0f, 0f);
            return cameraPivot.localRotation.eulerAngles;
        }

        public void SetTargetZoom(float distance)
        {
            targetZoomDistance = Mathf.Clamp(distance, minZoomDistance, maxZoomDistance);
        }

        public float GetCurrentZoomDistance()
        {
            return currentZoomDistance;
        }

        /// <summary>
        /// Normalized zoom: 0 = fully zoomed in, 1 = fully zoomed out.
        /// Useful for UI or other systems that need to know relative zoom level.
        /// </summary>
        public float GetZoomNormalized()
        {
            if (maxZoomDistance <= minZoomDistance) return 0f;
            return (currentZoomDistance - minZoomDistance) / (maxZoomDistance - minZoomDistance);
        }

        public void ResetToCenter()
        {
            velocity = Vector3.zero;

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

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!useBounds) return;

            float minX = boardConfig != null ? boardConfig.boundsMinX : -12f;
            float maxX = boardConfig != null ? boardConfig.boundsMaxX : 12f;
            float minZ = boardConfig != null ? boardConfig.boundsMinZ : -8f;
            float maxZ = boardConfig != null ? boardConfig.boundsMaxZ : 8f;

            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);

            Vector3 bottomLeft = new Vector3(minX, 0f, minZ);
            Vector3 bottomRight = new Vector3(maxX, 0f, minZ);
            Vector3 topLeft = new Vector3(minX, 0f, maxZ);
            Vector3 topRight = new Vector3(maxX, 0f, maxZ);

            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, bottomLeft);

            Gizmos.color = new Color(1f, 1f, 0f, 0.05f);
            Vector3 center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            Vector3 size = new Vector3(maxX - minX, 0.01f, maxZ - minZ);
            Gizmos.DrawCube(center, size);
        }
#endif
    }
}