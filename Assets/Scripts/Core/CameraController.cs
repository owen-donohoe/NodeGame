using NodeWar.Simulation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NodeWar.Core
{
    /// <summary>
    /// Orthographic camera controller with drag-to-pan, velocity momentum,
    /// variable dampening, and scroll zoom.
    /// Middle mouse button to drag. Scroll wheel to zoom.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("Board Config")]
        [SerializeField] private BoardConfig boardConfig;

        [Header("Pan Settings")]
        [SerializeField] private float panSpeed = 1f;
        [SerializeField] private float momentumDamping = 5f;
        [SerializeField] private float maxVelocity = 20f;

        [Header("Zoom Settings")]
        [SerializeField] private float zoomSpeed = 2f;
        [SerializeField] private float minOrthoSize = 3f;
        [SerializeField] private float maxOrthoSize = 15f;
        [SerializeField] private float zoomDamping = 8f;

        [Header("Bounds")]
        [SerializeField] private bool useBounds = true;
        [SerializeField] private float boundsPushback = 10f;

        // Internal state
        private Camera cam;
        private Vector3 velocity;
        private float targetOrthoSize;
        private bool isDragging;
        private Vector3 lastMouseWorldPos;

        private void Awake()
        {
            cam = GetComponentInChildren<Camera>();
            if (cam == null) cam = Camera.main;

            targetOrthoSize = cam.orthographicSize;
            velocity = Vector3.zero;
        }

        private void Update()
        {
            HandleDragInput();
            HandleZoomInput();
            ApplyMomentum();
            ApplyZoom();
            ApplyBounds();
        }

        private void HandleDragInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // Middle mouse button for drag
            if (mouse.middleButton.wasPressedThisFrame)
            {
                isDragging = true;
                lastMouseWorldPos = GetMouseWorldPosition(mouse);
                velocity = Vector3.zero; // Kill momentum when grabbing
            }

            if (mouse.middleButton.isPressed && isDragging)
            {
                Vector3 currentMouseWorld = GetMouseWorldPosition(mouse);
                Vector3 delta = lastMouseWorldPos - currentMouseWorld;

                // Move camera by the delta
                transform.position += delta * panSpeed;

                // Track velocity from drag movement
                velocity = delta * panSpeed / Time.deltaTime;

                // Clamp velocity
                if (velocity.magnitude > maxVelocity)
                    velocity = velocity.normalized * maxVelocity;

                lastMouseWorldPos = GetMouseWorldPosition(mouse);
            }

            if (mouse.middleButton.wasReleasedThisFrame)
            {
                isDragging = false;
                // velocity persists for momentum
            }
        }

        private void HandleZoomInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                targetOrthoSize -= scroll * zoomSpeed * 0.01f;
                targetOrthoSize = Mathf.Clamp(targetOrthoSize, minOrthoSize, maxOrthoSize);
            }
        }

        private void ApplyMomentum()
        {
            if (isDragging) return;

            // Apply velocity
            if (velocity.sqrMagnitude > 0.0001f)
            {
                transform.position += velocity * Time.deltaTime;

                // Dampen
                velocity = Vector3.Lerp(velocity, Vector3.zero, momentumDamping * Time.deltaTime);

                // Kill tiny velocities
                if (velocity.sqrMagnitude < 0.001f)
                    velocity = Vector3.zero;
            }
        }

        private void ApplyZoom()
        {
            if (cam == null) return;

            float current = cam.orthographicSize;
            if (Mathf.Abs(current - targetOrthoSize) > 0.01f)
            {
                cam.orthographicSize = Mathf.Lerp(current, targetOrthoSize, zoomDamping * Time.deltaTime);
            }
        }

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

        /// <summary>
        /// Projects mouse screen position to a world-space point on the XZ ground plane.
        /// Works correctly with the isometric camera angle.
        /// </summary>
        private Vector3 GetMouseWorldPosition(Mouse mouse)
        {
            Vector2 screenPos = mouse.position.ReadValue();
            Ray ray = cam.ScreenPointToRay(screenPos);

            // Intersect with XZ plane (Y = 0)
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0) t = 0;

            return ray.origin + ray.direction * t;
        }

        // Public API for future use
        public void SetTargetZoom(float orthoSize)
        {
            targetOrthoSize = Mathf.Clamp(orthoSize, minOrthoSize, maxOrthoSize);
        }

        public void ResetToCenter()
        {
            velocity = Vector3.zero;
            // Keep Y and rotation, center X and Z
            Vector3 pos = transform.position;
            pos.x = 0f;
            pos.z = 0f;
            transform.position = pos;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!useBounds) return;

            // Try to read from boardConfig if assigned
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
            Vector3 center = new Vector3(
                (minX + maxX) * 0.5f,
                0f,
                (minZ + maxZ) * 0.5f
            );
            Vector3 size = new Vector3(
                maxX - minX,
                0.01f,
                maxZ - minZ
            );
            Gizmos.DrawCube(center, size);
        }
#endif
    }
}