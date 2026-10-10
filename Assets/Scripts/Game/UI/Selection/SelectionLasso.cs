using System.Collections.Generic;
using UnityEngine;
using NodeWar.Input;

namespace NodeWar.UI
{
    /// <summary>
    /// Screen-space paint coverage from the source's recorded points and frozen radius.
    /// Opaque capsule stamps form a union mask; tint is composited once, so retracing
    /// never darkens the fill. The existing component/event names retain scene wiring.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class SelectionLasso : MonoBehaviour
    {
        // Retain serialized legacy fields without reinterpreting scene values.
        [SerializeField] private float lineWidth = 0.004f;
        [SerializeField] private Color validColor = new Color(1f, 0.85f, 0.4f, 0.95f);
        [SerializeField] private Color tooSmallColor = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Material lineMaterial;

        private PointerGestureSource source;
        private Material coverageMaterial;
        private RenderTexture coverageMask;
        private Rect coverageRect;
        private bool drawing;
        private bool dirty;
        private bool subscribed;
        private int screenWidth, screenHeight;
        private float radius;
        private readonly List<Vector2> screenPoints = new List<Vector2>();
        private static readonly int Segment = Shader.PropertyToID("_Segment");
        private static readonly int Radius = Shader.PropertyToID("_Radius");
        private static readonly int CoverageRect = Shader.PropertyToID("_CoverageRect");
        private static readonly int Tint = Shader.PropertyToID("_Tint");
        private static readonly int OutlineStep = Shader.PropertyToID("_OutlineStep");

        private void Awake()
        {
            // The old closed polygon must never remain visible alongside the brush.
            var line = GetComponent<LineRenderer>();
            line.positionCount = 0;
            line.enabled = false;
        }

        public void Initialize(PointerGestureSource gestureSource, Camera camera)
        {
            Unsubscribe();
            StopDrawing();
            source = gestureSource;
            if (isActiveAndEnabled) Subscribe();
        }

        private void OnEnable() => Subscribe();
        private void Subscribe()
        {
            if (source == null || subscribed) return;
            source.OnLassoBegin += HandleBegin;
            source.OnLassoPoint += HandlePoint;
            source.OnLassoComplete += HandleComplete;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (source == null || !subscribed) return;
            source.OnLassoBegin -= HandleBegin;
            source.OnLassoPoint -= HandlePoint;
            source.OnLassoComplete -= HandleComplete;
            subscribed = false;
        }

        private void OnDisable()
        {
            Unsubscribe();
            StopDrawing();
            ReleaseResources();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            ReleaseResources();
        }

        private void HandleBegin(Vector2 start)
        {
            screenPoints.Clear();
            screenPoints.Add(start);
            radius = source.CurrentPaintRadiusPx;
            drawing = true;
            dirty = true;
        }

        private void HandlePoint(Vector2 point)
        {
            if (!drawing) return;
            screenPoints.Add(point);
            dirty = true;
        }

        private void HandleComplete(IReadOnlyList<Vector2> points) => StopDrawing();
        private void StopDrawing()
        {
            drawing = false;
            screenPoints.Clear();
            ReleaseMask();
        }

        private void LateUpdate()
        {
            if (!drawing) return;
            if (screenWidth != Screen.width || screenHeight != Screen.height) dirty = true;
            if (dirty) RebuildMask();
        }

        private bool EnsureMaterial()
        {
            if (coverageMaterial != null) return true;
            // A Resources reference keeps the shader available in player builds.
            Shader shader = Resources.Load<Shader>("UI/PaintCoverage");
            if (shader == null)
            {
                Debug.LogError("Paint coverage shader missing: Resources/UI/PaintCoverage", this);
                drawing = false;
                return false;
            }
            coverageMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return true;
        }

        private void RebuildMask()
        {
            dirty = false;
            if (screenWidth != Screen.width || screenHeight != Screen.height) ReleaseMask();
            screenWidth = Screen.width;
            screenHeight = Screen.height;
            if (!LassoGeometry.IsValid(screenPoints, radius) || !EnsureMaterial()) return;
            PaintBounds bounds = LassoGeometry.Bounds(screenPoints, radius);
            // Screen-clipped bounds plus two pixels for the outer outline. No world
            // projection or zoom scaling; storage never exceeds the visible screen.
            int x = Mathf.FloorToInt(Mathf.Clamp((float)bounds.MinX - 2f, 0f, screenWidth));
            int y = Mathf.FloorToInt(Mathf.Clamp((float)bounds.MinY - 2f, 0f, screenHeight));
            int right = Mathf.CeilToInt(Mathf.Clamp((float)bounds.MaxX + 2f, 0f, screenWidth));
            int top = Mathf.CeilToInt(Mathf.Clamp((float)bounds.MaxY + 2f, 0f, screenHeight));
            int width = right - x, height = top - y;
            if (width <= 0 || height <= 0) { ReleaseMask(); return; }
            // Grow geometrically within screen dimensions instead of allocating on
            // every new sample. A changed origin simply rebuilds the same mask.
            if (coverageMask == null || coverageMask.width < width || coverageMask.height < height)
            {
                int capacityWidth = Mathf.Min(screenWidth, Mathf.NextPowerOfTwo(width));
                int capacityHeight = Mathf.Min(screenHeight, Mathf.NextPowerOfTwo(height));
                if (coverageMask != null)
                {
                    capacityWidth = Mathf.Max(capacityWidth, coverageMask.width);
                    capacityHeight = Mathf.Max(capacityHeight, coverageMask.height);
                }
                ReleaseMask();
                coverageMask = new RenderTexture(capacityWidth, capacityHeight, 0, RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.Linear) {
                    hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp, useMipMap = false, antiAliasing = 1
                };
                coverageMask.Create();
            }
            width = coverageMask.width;
            height = coverageMask.height;
            coverageRect = new Rect(x, y, width, height);
            coverageMaterial.SetVector(CoverageRect, new Vector4(x, y, width, height));
            coverageMaterial.SetFloat(Radius, radius);
            var previous = RenderTexture.active;
            GL.PushMatrix();
            try
            {
                RenderTexture.active = coverageMask;
                GL.Clear(false, true, Color.clear);
                GL.LoadOrtho();
                // Each pass overwrites covered pixels with 1 and discards elsewhere.
                // Repeated segments therefore add coverage without adding opacity.
                for (int i = 0; i < screenPoints.Count; i++)
                {
                    Vector2 a = screenPoints[i == 0 ? 0 : i - 1], b = screenPoints[i];
                    coverageMaterial.SetVector(Segment, new Vector4(a.x, a.y, b.x, b.y));
                    coverageMaterial.SetPass(0);
                    // Rasterize only this capsule's expanded bounds, not the full mask.
                    float u0 = Mathf.Clamp01((Mathf.Min(a.x, b.x) - radius - x) / width);
                    float v0 = Mathf.Clamp01((Mathf.Min(a.y, b.y) - radius - y) / height);
                    float u1 = Mathf.Clamp01((Mathf.Max(a.x, b.x) + radius - x) / width);
                    float v1 = Mathf.Clamp01((Mathf.Max(a.y, b.y) + radius - y) / height);
                    GL.Begin(GL.QUADS);
                    GL.TexCoord2(u0, v0); GL.Vertex3(u0, v0, 0f);
                    GL.TexCoord2(u1, v0); GL.Vertex3(u1, v0, 0f);
                    GL.TexCoord2(u1, v1); GL.Vertex3(u1, v1, 0f);
                    GL.TexCoord2(u0, v1); GL.Vertex3(u0, v1, 0f);
                    GL.End();
                }
            }
            finally { GL.PopMatrix(); RenderTexture.active = previous; }
        }

        private void OnGUI()
        {
            if (!drawing || Event.current.type != EventType.Repaint) return;
            if (dirty || screenWidth != Screen.width || screenHeight != Screen.height) RebuildMask();
            if (coverageMask == null || coverageMaterial == null) return;
            coverageMaterial.SetColor(Tint, validColor);
            coverageMaterial.SetVector(OutlineStep, new Vector4(1f / coverageMask.width,
                1f / coverageMask.height, 0f, 0f));
            // GUI is Y-down; captured points and mask UVs are Y-up.
            Rect guiRect = new Rect(coverageRect.x, Screen.height - coverageRect.yMax,
                coverageRect.width, coverageRect.height);
            Graphics.DrawTexture(guiRect, coverageMask, new Rect(0f, 0f, 1f, 1f),
                0, 0, 0, 0, Color.white, coverageMaterial, 1);
        }

        private void ReleaseMask()
        {
            if (coverageMask == null) return;
            coverageMask.Release();
            Destroy(coverageMask);
            coverageMask = null;
        }

        private void ReleaseResources()
        {
            ReleaseMask();
            if (coverageMaterial != null) Destroy(coverageMaterial);
            coverageMaterial = null;
        }
    }
}
