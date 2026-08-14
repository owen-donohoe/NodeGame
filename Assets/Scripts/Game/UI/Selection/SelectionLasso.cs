using UnityEngine;
using UnityEngine.InputSystem;
using NodeWar.Input;

namespace NodeWar.UI
{
    /// <summary>
    /// Visual circle on the ground plane during drag-selection.
    /// Uses a LineRenderer for the outline ring and a flat SpriteRenderer for translucent fill.
    /// Positioned at Y slightly above nodes (0.02) but below villagers (0.1).
    /// 
    /// Why LineRenderer over other options:
    /// - Mesh generation: More code, same visual result, harder to tune width
    /// - Projector/Decal: Overkill, requires render feature setup, bleeds onto objects
    /// - GL.Lines: No persistent object, can't easily set material/width
    /// - LineRenderer: Built-in, loop-enabled, width curve support, one component, done
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class SelectionLasso : MonoBehaviour
    {
        [Header("Circle Settings")]
        [SerializeField] private int segments = 32;
        [SerializeField] private float lineWidth = 0.05f;
        [SerializeField] private Color outlineColor = new Color(1f, 1f, 1f, 0.8f);
        [SerializeField] private Color fillColor = new Color(1f, 1f, 1f, 0.1f);

        [Header("Positioning")]
        [SerializeField] private float groundY = 0.02f;

        [Header("References")]
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Material fillMaterial;

        // Components
        private LineRenderer lineRenderer;
        private Transform fillTransform;
        private SpriteRenderer fillSprite;
        private SelectionSystem selectionSystem;
        private Camera mainCam;

        private void Awake()
        {
            mainCam = Camera.main;

            // Line renderer setup
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.loop = true;
            lineRenderer.positionCount = segments;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.useWorldSpace = true;

            if (lineMaterial != null)
            {
                lineRenderer.material = lineMaterial;
            }
            else
            {
                // Fallback: create simple unlit material
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }

            lineRenderer.startColor = outlineColor;
            lineRenderer.endColor = outlineColor;
            lineRenderer.enabled = false;

            // Fill circle setup
            CreateFillCircle();
        }

        public void Initialize(SelectionSystem selection)
        {
            selectionSystem = selection;
        }

        private void Update()
        {
            if (selectionSystem == null) return;

            if (selectionSystem.IsDragging && selectionSystem.CurrentDragRadius > 10f)
            {
                UpdateCircle();
                lineRenderer.enabled = true;
                if (fillSprite != null) fillSprite.enabled = true;
            }
            else
            {
                lineRenderer.enabled = false;
                if (fillSprite != null) fillSprite.enabled = false;
            }
        }

        private void UpdateCircle()
        {
            // Project screen-space center to world ground plane
            Vector3 worldCenter = ScreenToGroundPoint(selectionSystem.DragStart);

            // Project a point at the edge to get world radius
            Vector2 edgeScreen = selectionSystem.DragStart + Vector2.right * selectionSystem.CurrentDragRadius;
            Vector3 worldEdge = ScreenToGroundPoint(edgeScreen);
            float worldRadius = Vector3.Distance(worldCenter, worldEdge);

            // Generate circle points
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / (float)segments * Mathf.PI * 2f;
                float x = worldCenter.x + Mathf.Cos(angle) * worldRadius;
                float z = worldCenter.z + Mathf.Sin(angle) * worldRadius;
                lineRenderer.SetPosition(i, new Vector3(x, groundY, z));
            }

            // Update fill circle
            if (fillTransform != null)
            {
                fillTransform.position = new Vector3(worldCenter.x, groundY - 0.001f, worldCenter.z);
                float diameter = worldRadius * 2f;
                fillTransform.localScale = new Vector3(diameter, diameter, 1f);
            }
        }

        private void CreateFillCircle()
        {
            GameObject fillGO = new GameObject("LassoFill");
            fillGO.transform.SetParent(transform);
            fillTransform = fillGO.transform;

            // Rotate to lie flat on XZ plane
            fillTransform.rotation = Quaternion.Euler(90f, 0f, 0f);

            fillSprite = fillGO.AddComponent<SpriteRenderer>();

            // Create a simple circle texture
            fillSprite.sprite = CreateCircleSprite(64);
            fillSprite.color = fillColor;

            if (fillMaterial != null)
            {
                fillSprite.material = fillMaterial;
            }

            // Sorting: above ground, below everything else
            fillSprite.sortingOrder = -100;
            fillSprite.enabled = false;
        }

        /// <summary>
        /// Creates a filled circle sprite at runtime (no asset dependency).
        /// </summary>
        private Sprite CreateCircleSprite(int resolution)
        {
            Texture2D tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            float center = resolution * 0.5f;
            float radiusSq = center * center;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = x - center + 0.5f;
                    float dy = y - center + 0.5f;
                    float distSq = dx * dx + dy * dy;

                    if (distSq <= radiusSq)
                        tex.SetPixel(x, y, Color.white);
                    else
                        tex.SetPixel(x, y, Color.clear);
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;

            return Sprite.Create(tex, new Rect(0, 0, resolution, resolution), new Vector2(0.5f, 0.5f), resolution);
        }

        private Vector3 ScreenToGroundPoint(Vector2 screenPos)
        {
            Ray ray = mainCam.ScreenPointToRay(screenPos);
            float t = -ray.origin.y / ray.direction.y;
            if (t < 0) t = 0;
            return ray.origin + ray.direction * t;
        }
    }
}