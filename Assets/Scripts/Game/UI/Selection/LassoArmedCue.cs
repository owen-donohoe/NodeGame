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

        private PointerGestureSource source;
        private Camera cam;
        private NodeHighlight ring;

        public void Initialize(PointerGestureSource gestureSource, Camera camera)
        {
            Unsubscribe();

            source = gestureSource;
            cam = camera != null ? camera : Camera.main;

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
