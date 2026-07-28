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
        [Header("Pan Settings")]
        [SerializeField] private float panSpeed = 1f;
        [SerializeField] private float momentumDamping = 5f;
        [SerializeField] private float maxVelocity = 20f;

        [Header("Zoom Settings")]
        [SerializeField] private float zoomSpeed = 2f;
        [SerializeField] private float minOrthoSize = 3f;
        [SerializeField] private float maxOrthoSize = 15f;
        [SerializeField] private float zoomDamping = 8f;

        [Header("Bounds (optional)")]
        [SerializeField] private bool useBounds = true;
        [SerializeField] private float boundsMinX = -12f;
        [SerializeField] private float boundsMaxX = 12f;
        [SerializeField] private float boundsMinZ = -8f;
        [SerializeField] private float boundsMaxZ = 8f;
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
            if (!useBounds) return;

            Vector3 pos = transform.position;

            // Soft pushback toward bounds
            if (pos.x < boundsMinX)
                velocity.x += boundsPushback * (boundsMinX - pos.x) * Time.deltaTime;
            if (pos.x > boundsMaxX)
                velocity.x += boundsPushback * (boundsMaxX - pos.x) * Time.deltaTime;
            if (pos.z < boundsMinZ)
                velocity.z += boundsPushback * (boundsMinZ - pos.z) * Time.deltaTime;
            if (pos.z > boundsMaxZ)
                velocity.z += boundsPushback * (boundsMaxZ - pos.z) * Time.deltaTime;
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

            Gizmos.color = new Color(1f, 1f, 0f, 0.4f);

            // Calculate corners on XZ plane at Y=0
            Vector3 bottomLeft = new Vector3(boundsMinX, 0f, boundsMinZ);
            Vector3 bottomRight = new Vector3(boundsMaxX, 0f, boundsMinZ);
            Vector3 topLeft = new Vector3(boundsMinX, 0f, boundsMaxZ);
            Vector3 topRight = new Vector3(boundsMaxX, 0f, boundsMaxZ);

            // Draw border lines
            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, bottomLeft);

            // Filled translucent quad
            Gizmos.color = new Color(1f, 1f, 0f, 0.05f);
            Vector3 center = new Vector3(
                (boundsMinX + boundsMaxX) * 0.5f,
                0f,
                (boundsMinZ + boundsMaxZ) * 0.5f
            );
            Vector3 size = new Vector3(
                boundsMaxX - boundsMinX,
                0.01f,
                boundsMaxZ - boundsMinZ
            );
            Gizmos.DrawCube(center, size);
        }
        #endif
    }
}
