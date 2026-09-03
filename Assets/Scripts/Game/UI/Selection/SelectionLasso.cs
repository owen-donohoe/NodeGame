using System.Collections.Generic;
using UnityEngine;
using NodeWar.Input;

namespace NodeWar.UI
{
    /// <summary>
    /// Draws the freeform lasso stroke while the player is dragging one.
    ///
    /// This replaces a circle. The previous version was named lasso and drew
    /// one on the ground plane, but its shape was a radius swept from a centre
    /// point -- the player could not enclose anything, only reach outward.
    ///
    /// Screen space, not the ground plane. A stroke drawn under a tilted
    /// perspective camera does not project to a well-formed ground polygon:
    /// near the horizon a few pixels of stroke cover unbounded world distance,
    /// and the projected shape can self-intersect where the stroke did not.
    /// Villagers are billboarded, so testing and drawing where things *appear*
    /// is also what the player means. The points are pushed just past the near
    /// clip plane so the line reads as an overlay.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class SelectionLasso : MonoBehaviour
    {
        [Header("Line")]
        [SerializeField] private float lineWidth = 0.004f;
        [Tooltip("Stroke colour once the enclosed area is large enough to select.")]
        [SerializeField] private Color validColor = new Color(1f, 0.85f, 0.4f, 0.95f);
        [Tooltip("Stroke colour while the enclosed area is still below the minimum.")]
        [SerializeField] private Color tooSmallColor = new Color(1f, 1f, 1f, 0.35f);

        [Header("References")]
        [SerializeField] private Material lineMaterial;

        private LineRenderer lineRenderer;
        private Camera cam;
        private PointerGestureSource source;

        private readonly List<Vector2> screenPoints = new List<Vector2>();
        private bool drawing;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.loop = true;              // the stroke is a closed shape
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            // Rounds the joints the smoothing pass leaves behind. Cheap: these
            // are extra verts on an already tiny mesh, not extra draw calls.
            lineRenderer.numCornerVertices = 4;
            lineRenderer.numCapVertices = 4;
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;

            lineRenderer.material = lineMaterial != null
                ? lineMaterial
                : new Material(Shader.Find("Sprites/Default"));
        }

        public void Initialize(PointerGestureSource gestureSource, Camera camera)
        {
            Unsubscribe();

            source = gestureSource;
            cam = camera != null ? camera : Camera.main;

            Subscribe();
        }

        private void Subscribe()
        {
            if (source == null) return;
            source.OnLassoBegin += HandleBegin;
            source.OnLassoPoint += HandlePoint;
            source.OnLassoComplete += HandleComplete;
        }

        private void Unsubscribe()
        {
            if (source == null) return;
            source.OnLassoBegin -= HandleBegin;
            source.OnLassoPoint -= HandlePoint;
            source.OnLassoComplete -= HandleComplete;
        }

        private void OnDestroy() => Unsubscribe();

        private void HandleBegin(Vector2 start)
        {
            screenPoints.Clear();
            screenPoints.Add(start);
            drawing = true;
            lineRenderer.enabled = true;
            Redraw();
        }

        private void HandlePoint(Vector2 point)
        {
            if (!drawing) return;
            screenPoints.Add(point);
            Redraw();
        }

        private void HandleComplete(IReadOnlyList<Vector2> points)
        {
            drawing = false;
            screenPoints.Clear();
            lineRenderer.positionCount = 0;
            lineRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            // The camera cannot pan mid-lasso (PanSuppressed latches for the
            // stroke), but zoom and shake still move it, so the projection is
            // refreshed rather than baked once.
            if (drawing) Redraw();
        }

        private void Redraw()
        {
            if (cam == null || screenPoints.Count == 0)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            // Smoothed with the same iteration count SelectionSystem applies
            // before testing containment, so the line the player sees is the
            // boundary that actually selects.
            GestureThresholds t = source != null ? source.Thresholds : null;
            int iterations = t != null ? t.lassoSmoothingIterations : 0;
            int cap = t != null ? t.MaxSmoothedPoints : 2048;

            LassoGeometry.Smooth(screenPoints, drawPoints, iterations, cap);

            Color c = t != null && LassoGeometry.IsValid(drawPoints, t.MinLassoAreaSqPx)
                ? validColor
                : tooSmallColor;

            lineRenderer.startColor = c;
            lineRenderer.endColor = c;

            float depth = cam.nearClipPlane + 0.01f;

            lineRenderer.positionCount = drawPoints.Count;
            for (int i = 0; i < drawPoints.Count; i++)
            {
                Vector2 p = drawPoints[i];
                lineRenderer.SetPosition(i, cam.ScreenToWorldPoint(new Vector3(p.x, p.y, depth)));
            }
        }

        private readonly List<Vector2> drawPoints = new List<Vector2>();
    }
}
