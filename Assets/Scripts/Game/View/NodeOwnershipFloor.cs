using NodeWar.Core;
using NodeWar.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace NodeWar.View
{
    /// <summary>
    /// One ownership floor for the board. Reads owners without changing state;
    /// shares geometry, material and zoom, outside every node outline group.
    /// </summary>
    public class NodeOwnershipFloor : MonoBehaviour
    {
        [Header("Zoom Fade")]
        [SerializeField][Range(0f, 1f)] private float fadeStart = 0.15f;
        [SerializeField][Range(0f, 1f)] private float fadeEnd = 0.75f;
        [SerializeField][Range(0f, 1f)] private float nearEdgeAlpha = 0.10f;
        [SerializeField][Range(0f, 1f)] private float farEdgeAlpha = 0.80f;

        [Header("Territory Edge")]
        [Tooltip("Each bracket arm's share of a side, fixed to player 1.")]
        [SerializeField][Range(0f, 0.5f)] private float bracketFraction = 0.30f;
        [SerializeField] private float minimumWidthPixels = 1.5f;
        [Tooltip("Height above the ground in board cells, to avoid z-fighting.")]
        [SerializeField] private float floorHeightCells = 0.005f;

        private SimulationState state;
        private CameraController cameraController;
        private Mesh quad;
        private Material material;
        private MeshRenderer[] renderers;
        private int[] lastOwners;
        private MaterialPropertyBlock properties;
        private bool subscribed;
        private float lastEdgeAlpha = float.NaN;

        private static readonly int OwnerColor = Shader.PropertyToID("_OwnerColor");
        private static readonly int OwnerShape = Shader.PropertyToID("_OwnerShape");
        private static readonly int EdgeAlpha = Shader.PropertyToID("_EdgeAlpha");
        private static readonly int DitherAlpha = Shader.PropertyToID("_DitherAlpha");
        private static readonly int HalfExtent = Shader.PropertyToID("_HalfExtent");
        private static readonly int EdgeWidth = Shader.PropertyToID("_EdgeWidth");
        private static readonly int CornerRadius = Shader.PropertyToID("_CornerRadius");
        private static readonly int BracketFraction = Shader.PropertyToID("_BracketFraction");
        private static readonly int MinimumWidthPixels = Shader.PropertyToID("_MinimumWidthPixels");

        /// <summary>Spawn once with board-local node centres and the already resolved camera.</summary>
        public void Initialize(SimulationState simulationState, Vector3[] nodePositions,
            float nodeScale, CameraController camera)
        {
            var footprint = NodeFloorFootprint.ForScale(nodeScale);
            if (material != null || simulationState == null || footprint.IsEmpty) return;
            Shader shader = Resources.Load<Shader>("World/NodeOwnershipFloor");
            if (shader == null)
            {
                Debug.LogError("Ownership floor shader missing: Resources/World/NodeOwnershipFloor", this);
                return;
            }

            state = simulationState;
            cameraController = camera;
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetFloat(HalfExtent, NodeFloorFootprint.TerritoryHalfExtentCells);
            material.SetFloat(EdgeWidth, NodeFloorFootprint.TerritoryWidthCells);
            material.SetFloat(CornerRadius, NodeFloorFootprint.CornerRadiusCells);
            material.SetFloat(BracketFraction, bracketFraction);
            material.SetFloat(MinimumWidthPixels, minimumWidthPixels);
            material.SetFloat(DitherAlpha, 0f);
            quad = BuildQuad();
            properties = new MaterialPropertyBlock();
            renderers = new MeshRenderer[state.nodes.Length];
            lastOwners = new int[state.nodes.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var node = new GameObject("Ownership_" + i);
                node.transform.SetParent(transform, false);
                node.transform.localPosition = nodePositions[i] + Vector3.up * (floorHeightCells * nodeScale);
                node.transform.localScale = Vector3.one * (footprint.QuadHalfExtent * 2f);
                node.AddComponent<MeshFilter>().sharedMesh = quad;
                var renderer = node.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                // District groups currently share Villagers with the units.
                // Keep this below both until their layer contract separates them.
                renderer.sortingLayerID = SortingLayer.NameToID("Ground");
                renderer.sortingOrder = 1;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.enabled = false;
                renderers[i] = renderer;
                lastOwners[i] = int.MinValue;
            }

            SyncOwners();
            SeedZoom();
            if (isActiveAndEnabled) Subscribe();
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh { name = "NodeOwnershipFloorQuad", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f)
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private void LateUpdate() => SyncOwners();

        private void SyncOwners()
        {
            if (state == null || renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                int owner = state.nodes[i].ownerID;
                if (owner == lastOwners[i]) continue;
                properties.Clear();
                properties.SetColor(OwnerColor, PlayerColors.For(owner));
                properties.SetFloat(OwnerShape, owner == 1 ? 1f : 0f);
                renderers[i].SetPropertyBlock(properties);
                renderers[i].enabled = owner >= 0;
                lastOwners[i] = owner;
            }
        }

        private void SeedZoom()
        {
            // Without a camera, keep the documented close-zoom fallback.
            ApplyZoom(cameraController != null ? cameraController.GetCurrentZoomNormalized() : 0f);
        }

        private void ApplyZoom(float zoom)
        {
            if (material == null) return;
            float alpha = OwnershipZoomMath.Alpha(zoom, fadeStart, fadeEnd, nearEdgeAlpha, farEdgeAlpha);
            if (alpha == lastEdgeAlpha) return;
            material.SetFloat(EdgeAlpha, alpha);
            lastEdgeAlpha = alpha;
        }

        private void OnEnable()
        {
            SeedZoom();
            Subscribe();
        }

        private void Subscribe()
        {
            if (cameraController == null || subscribed) return;
            cameraController.CurrentZoomChanged += ApplyZoom;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (cameraController != null && subscribed)
                cameraController.CurrentZoomChanged -= ApplyZoom;
            subscribed = false;
        }

        private void OnDisable() => Unsubscribe();

        private void OnDestroy()
        {
            Unsubscribe();
            if (material != null) Destroy(material);
            if (quad != null) Destroy(quad);
        }
    }
}
