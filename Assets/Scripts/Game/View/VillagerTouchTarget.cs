using UnityEngine;
using NodeWar.Input;

namespace NodeWar.View
{
    /// <summary>
    /// Keeps a villager's tap target a constant physical size on screen,
    /// independent of how large its sprite happens to be drawn.
    ///
    /// The prefab's authored collider is a fixed 0.4 x 0.1 x 0.4 box in world
    /// units, so its screen footprint shrinks with every zoom-out. At the far
    /// end of the 6x dolly range a villager becomes a target a few pixels
    /// across -- fine for a mouse cursor, unhittable with a fingertip.
    ///
    /// This adds a sphere sized each frame so its projected radius stays at
    /// touchTargetRadiusMm. It sits on the Villagers layer and is allowed to
    /// overlap the node beneath it: the gesture source raycasts villagers
    /// before nodes, so a villager standing on a node still wins the tap, which
    /// is the intended priority.
    ///
    /// Built at runtime rather than authored, so no prefab change is needed.
    /// </summary>
    public class VillagerTouchTarget : MonoBehaviour
    {
        [Tooltip("How often to resize, in frames. The target only needs to " +
                 "track camera distance, which changes slowly.")]
        [SerializeField] private int resizeIntervalFrames = 6;

        private Camera cam;
        private GestureThresholds thresholds;
        private SphereCollider sphere;
        private Transform sphereTransform;
        private int frameCounter;

        public void Initialize(Camera camera, GestureThresholds gestureThresholds)
        {
            cam = camera != null ? camera : Camera.main;
            thresholds = gestureThresholds;

            if (sphere == null)
            {
                // Own GameObject so the villager's authored collider and
                // visuals are left exactly as they are.
                GameObject targetGO = new GameObject("TouchTarget");
                targetGO.layer = gameObject.layer;
                sphereTransform = targetGO.transform;
                sphereTransform.SetParent(transform, false);
                sphereTransform.localPosition = Vector3.zero;

                sphere = targetGO.AddComponent<SphereCollider>();

                // A trigger so it never participates in physics, only queries.
                // This relies on Physics.queriesHitTriggers, which is on in
                // DynamicsManager (m_QueriesHitTriggers: 1). If that is ever
                // turned off project-wide, villagers silently stop being
                // tappable -- make this a solid collider rather than chasing it.
                sphere.isTrigger = true;
            }

            Resize();
        }

        private void LateUpdate()
        {
            if (sphere == null || cam == null) return;

            frameCounter++;
            if (frameCounter < resizeIntervalFrames) return;
            frameCounter = 0;

            Resize();
        }

        /// <summary>
        /// Converts the desired screen radius into a world radius at this
        /// villager's distance from the camera.
        ///
        /// At distance d the visible half-height of the frustum is
        /// d * tan(fov/2), covering Screen.height pixels. One pixel is
        /// therefore 2 * d * tan(fov/2) / Screen.height world units.
        /// </summary>
        private void Resize()
        {
            if (sphere == null || cam == null || thresholds == null) return;
            if (Screen.height <= 0) return;

            float radiusPx = ScreenMetrics.MmToPixels(thresholds.touchTargetRadiusMm);

            float distance = Vector3.Distance(cam.transform.position, transform.position);

            float worldPerPixel;
            if (cam.orthographic)
            {
                worldPerPixel = (cam.orthographicSize * 2f) / Screen.height;
            }
            else
            {
                float halfHeight = distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                worldPerPixel = (halfHeight * 2f) / Screen.height;
            }

            float worldRadius = radiusPx * worldPerPixel;

            // The collider inherits the villager's lossy scale; divide it out so
            // the target is the size asked for rather than that times the sprite
            // scale.
            Vector3 scale = transform.lossyScale;
            float maxScale = Mathf.Max(0.0001f, Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)));

            sphere.radius = worldRadius / maxScale;
        }
    }
}
