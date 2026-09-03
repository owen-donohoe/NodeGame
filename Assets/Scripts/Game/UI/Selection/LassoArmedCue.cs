using UnityEngine;
using NodeWar.Input;
using NodeWar.View;

namespace NodeWar.UI
{
    /// <summary>
    /// Pulses a ring under the finger the instant the long press arms the
    /// lasso.
    ///
    /// Without it the long press is silent: the player holds, nothing happens,
    /// and the only confirmation is the stroke appearing once they start
    /// moving. That makes a 0.3s hold feel like a delay rather than a
    /// threshold, and gives no way to learn the timing.
    ///
    /// Reuses NodeHighlight -- the same expanding ring a move order plays on
    /// its destination node -- rather than inventing a second confirmation
    /// visual. Same grammar, so the ring already reads as "registered".
    /// </summary>
    public class LassoArmedCue : MonoBehaviour
    {
        [Tooltip("Ring colour. Distinct from the move-order highlight so the " +
                 "two reads stay separable.")]
        [SerializeField] private Color cueColor = new Color(1f, 0.85f, 0.4f, 0.9f);

        [Tooltip("Height above the ground plane. Matches the lasso's own " +
                 "layering so the cue does not z-fight with the board.")]
        [SerializeField] private float groundY = 0.04f;

        [Header("Ring")]
        [Tooltip("Start radius at the reference camera distance. A third of " +
                 "NodeHighlight's node-sized default, which reads too large " +
                 "under a finger.")]
        [SerializeField] private float startScale = 0.167f;

        [Tooltip("End radius at the reference camera distance. A third of the " +
                 "node-sized default.")]
        [SerializeField] private float endScale = 0.6f;

        [Tooltip("Half the move-order highlight's duration. The cue confirms a " +
                 "threshold that already took 0.3s to reach, so it needs to be " +
                 "gone before the stroke starts.")]
        [SerializeField] private float pulseDuration = 0.25f;

        [Tooltip("Camera distance the scales above were tuned at: the P0 " +
                 "gameplay default, Lerp(zoomMin 5, zoomMax 30, 0.65). The ring " +
                 "scales linearly against this so it holds a constant apparent " +
                 "size across the 6x dolly range.")]
        [SerializeField] private float referenceZoomDistance = 21.25f;

        private PointerGestureSource source;
        private Camera cam;
        private NodeWar.Core.CameraController cameraController;
        private NodeHighlight ring;

        public void Initialize(PointerGestureSource gestureSource, Camera camera,
                               NodeWar.Core.CameraController controller)
        {
            Unsubscribe();

            source = gestureSource;
            cam = camera != null ? camera : Camera.main;
            cameraController = controller;

            if (ring == null)
                ring = gameObject.AddComponent<NodeHighlight>();

            Subscribe();
        }

        private void Subscribe()
        {
            if (source == null) return;
            source.OnLassoBegin += HandleLassoBegin;
        }

        private void Unsubscribe()
        {
            if (source == null) return;
            source.OnLassoBegin -= HandleLassoBegin;
        }

        private void OnDestroy() => Unsubscribe();

        private void HandleLassoBegin(Vector2 screenPos)
        {
            if (cam == null || ring == null) return;

            transform.position = ScreenToGround(screenPos);

            // Ratio of distances, not GetZoomNormalized. Normalized zoom is a
            // 0-1 position within the dolly range, so using it as a multiplier
            // would shrink the ring to nothing at full zoom-in -- exactly where
            // the finger is most precise and the cue is still wanted. Apparent
            // size is constant when world size is linear in camera distance.
            float multiplier = 1f;
            if (cameraController != null && referenceZoomDistance > 0.01f)
                multiplier = cameraController.GetCurrentZoomDistance() / referenceZoomDistance;

            ring.Configure(startScale * multiplier, endScale * multiplier, pulseDuration);
            ring.Pulse(cueColor);
        }

        /// <summary>
        /// Projects a screen point onto the Y=0 ground plane. Guards the
        /// degenerate cases the same way the camera's own helper does: a ray
        /// parallel to the plane never intersects, and a negative parameter
        /// means the plane is behind the camera.
        /// </summary>
        private Vector3 ScreenToGround(Vector2 screenPos)
        {
            Ray ray = cam.ScreenPointToRay(screenPos);

            if (Mathf.Abs(ray.direction.y) < 0.0001f)
                return transform.position;

            float t = -ray.origin.y / ray.direction.y;
            if (t < 0f) t = 0f;

            Vector3 point = ray.origin + ray.direction * t;
            point.y = groundY;
            return point;
        }
    }
}
