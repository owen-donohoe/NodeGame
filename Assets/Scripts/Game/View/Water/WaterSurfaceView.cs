using NodeWar.Lobby;
using NodeWar.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace NodeWar.View
{
    /// <summary>Owns the runtime water resources; terrain is copied, never modified.</summary>
    public sealed class WaterSurfaceView : MonoBehaviour
    {
        private int Padding => WaterFeatures.PaddingFor(WaterQuality.Current);
        private int meshPadding;
        private static readonly List<WaterSurfaceView> active = new List<WaterSurfaceView>();
        private int cols, rows, subdivisions, skirtCells;
        private float scale, surfaceY, blend = 1f;
        private TerrainType[] terrain;
        private MaterialPropertyBlock properties;
        private Vector4 shoreST;
        private bool generatedSurfaceFoam, generatedSwell;
        private ReflectionProbe reflectionProbe;
        private BoardTerrainView boardView;
        private bool probeDirty, probeAfterSettled;
        private Mesh floorMesh;
        private Material floorMaterial;
        private MeshRenderer floorRenderer;
        private Mesh mesh;
        private MeshFilter filter;
        private MeshRenderer surfaceRenderer;
        private Texture2D current, previous;

        private static bool ReducedMotion => PlayerProfile.Instance != null && PlayerProfile.Instance.Settings.reducedMotion;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => active.Clear();

        private void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
            WaterQuality.Apply();
        }

        private void OnDisable() => active.Remove(this);

        public static void RefreshAll() => WaterQuality.Apply();

        internal static void RefreshActive()
        {
            for (int i = 0; i < active.Count; i++) active[i].RefreshQuality();
        }

        private void RefreshQuality()
        {
            if (surfaceRenderer == null) return; // AddComponent enables before Initialize.
            surfaceRenderer.sharedMaterial = WaterMaterialFactory.Runtime;
            ApplyProperties();
            int next = WaterFeatures.SubdivisionsFor(WaterQuality.Current, WaterQuality.Waves, WaterMaterialFactory.Runtime.GetFloat("_WaveScale"), scale);
            RefreshProbe();
            RefreshFloor();
            surfaceRenderer.receiveShadows = WaterQuality.Has(GraphicsFeature.Shadows);
            if (next == subdivisions && meshPadding == Padding) return;
            subdivisions = next;
            meshPadding = Padding;
            if (terrain != null) RebuildMesh(terrain);
        }

        public void Initialize(int columns, int rowCount, float cellScale, float y, int skirtCells = 40)
        {
            int waterLayer = LayerMask.NameToLayer("Water");
            if (waterLayer >= 0) gameObject.layer = waterLayer;
            boardView = GetComponentInParent<BoardTerrainView>();
            cols = columns; rows = rowCount; scale = cellScale; surfaceY = y;
            this.skirtCells = skirtCells;
            subdivisions = WaterFeatures.SubdivisionsFor(WaterQuality.Current, WaterQuality.Waves, WaterMaterialFactory.Runtime.GetFloat("_WaveScale"), scale);
            WaterQuality.Apply();
            properties = new MaterialPropertyBlock();
            filter = gameObject.AddComponent<MeshFilter>();
            surfaceRenderer = gameObject.AddComponent<MeshRenderer>();
            surfaceRenderer.sharedMaterial = WaterMaterialFactory.Runtime;
            ApplyProperties();
            surfaceRenderer.sortingOrder = TerrainPresentation.WaterSortingOrder;
            surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off;
            surfaceRenderer.receiveShadows = WaterQuality.Has(GraphicsFeature.Shadows);
            RefreshProbe();
            RefreshFloor();
        }

        public void SetTerrain(TerrainType[] resolved)
        {
            if (SameTerrain(resolved)) return;
            ShoreFieldData field = ShoreField.Bake(cols, rows, resolved);
            var texture = new Texture2D(field.width, field.height, TextureFormat.RGBA32, false, true)
            {
                name = "Water shore field", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.LoadRawTextureData(field.rgba);
            texture.Apply(false, false);

            // Preserve the currently visible field when another edit interrupts a blend.
            if (previous != null && previous != current)
            {
                if (blend < 1f)
                {
                    Color32[] from = previous.GetPixels32(), to = current.GetPixels32();
                    for (int i = 0; i < to.Length; i++) to[i] = Color32.Lerp(from[i], to[i], blend);
                    current.SetPixels32(to);
                    current.Apply(false, false);
                }
                Release(previous);
            }
            previous = current != null ? current : texture;
            current = texture;
            blend = previous == current || ReducedMotion ? 1f : 0f;
            Vector3 origin = transform.TransformPoint(new Vector3(field.originX * scale, surfaceY, field.originZ * scale));
            shoreST = new Vector4(origin.x, origin.z,
                1f / (field.width * field.worldPerTexel * scale), 1f / (field.height * field.worldPerTexel * scale));
            ApplyProperties();

            RebuildMesh(resolved);
            terrain = (TerrainType[])resolved.Clone();
            probeDirty = true;
            probeAfterSettled = true;
            if (blend == 1f) FinishBlend();
        }

        private void RebuildMesh(TerrainType[] resolved)
        {
            var data = WaterMeshBuilder.Build(cols, rows, resolved, subdivisions, Padding, skirtCells);
            var vertices = new Vector3[data.positions.Length / 3];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = new Vector3(data.positions[i * 3] * scale, surfaceY, data.positions[i * 3 + 2] * scale);
            var nextMesh = new Mesh { name = "Water surface", indexFormat = IndexFormat.UInt32 };
            nextMesh.vertices = vertices;
            nextMesh.triangles = data.indices;
            nextMesh.RecalculateBounds();
            // Recalculated bounds include the whole skirt; expand further for GPU Gerstner displacement.
            Bounds bounds = nextMesh.bounds;
            bounds.Expand(40f); // Supports the full wave/group/steepness slider range.
            nextMesh.bounds = bounds;
            filter.sharedMesh = nextMesh;
            Release(mesh);
            mesh = nextMesh;
        }

        private bool SameTerrain(TerrainType[] resolved)
        {
            if (terrain == null || resolved == null || terrain.Length != resolved.Length) return false;
            for (int i = 0; i < terrain.Length; i++) if (terrain[i] != resolved[i]) return false;
            return true;
        }

        private void Update()
        {
            if (surfaceRenderer == null) return;
            Material material = surfaceRenderer.sharedMaterial;
            if (WaterMaterialFactory.NeedsFoam(material, "_FoamTex") != generatedSurfaceFoam
                || WaterMaterialFactory.NeedsSwell(material) != generatedSwell) ApplyProperties();
            if (floorRenderer != null && floorRenderer.gameObject.activeSelf)
                floorRenderer.transform.localPosition = new Vector3(0, surfaceY - material.GetFloat("_FloorDepth"), 0);
            if (blend >= 1f) return;
            blend = ReducedMotion ? 1f : Mathf.Min(1f, blend + Time.unscaledDeltaTime / 0.4f);
            ApplyProperties();
            if (blend == 1f) FinishBlend();
        }

        private void FinishBlend()
        {
            if (previous != current) Release(previous);
            previous = current;
            ApplyProperties();
        }

        private void ApplyProperties()
        {
            properties.Clear();
            if (current != null) properties.SetTexture("_ShoreTex", current);
            if (previous != null) properties.SetTexture("_ShorePrevTex", previous);
            properties.SetFloat("_ShoreBlend", blend);
            properties.SetVector("_ShoreST", shoreST);
            Vector3 min = transform.TransformPoint(new Vector3(-0.5f * scale, surfaceY, -0.5f * scale));
            Vector3 max = transform.TransformPoint(new Vector3((cols - 0.5f) * scale, surfaceY, (rows - 0.5f) * scale));
            properties.SetVector("_BoardRect", new Vector4(min.x, min.z, max.x, max.z));
            properties.SetFloat("_CellSize", scale);
            properties.SetFloat("_WaveFadeEnd", Mathf.Min(Padding, WaterMaterialFactory.Runtime.GetFloat("_WaveFadeEnd")));
            properties.SetFloat("_DetailFadeEnd", Mathf.Min(Padding + 6, WaterMaterialFactory.Runtime.GetFloat("_DetailFadeEnd")));
            Material material = surfaceRenderer.sharedMaterial;
            generatedSurfaceFoam = WaterMaterialFactory.NeedsFoam(material, "_FoamTex");
            generatedSwell = WaterMaterialFactory.NeedsNormals(material);
            if (generatedSurfaceFoam) properties.SetTexture("_FoamTex", WaterMaterialFactory.SurfaceFoam());
            if (generatedSwell) properties.SetTexture("_SwellTex", WaterMaterialFactory.Swell());
            surfaceRenderer.SetPropertyBlock(properties);
            if (floorRenderer != null)
            {
                properties.SetFloat("_BodyStepA", material.GetFloat("_BodyStepA"));
                properties.SetFloat("_BodyStepB", material.GetFloat("_BodyStepB"));
                properties.SetColor("_OceanDeepColor", material.GetColor("_OceanDeepColor"));
                properties.SetColor("_LakeDeepColor", material.GetColor("_LakeDeepColor"));
                properties.SetColor("_DeepLakeDeepColor", material.GetColor("_DeepLakeDeepColor"));
                floorRenderer.SetPropertyBlock(properties);
            }
        }

        private void RefreshFloor()
        {
            bool enabled = WaterQuality.Has(GraphicsFeature.Transparency);
            if (enabled && floorRenderer == null)
            {
                var child = new GameObject("Sea floor");
                child.layer = gameObject.layer;
                child.transform.SetParent(transform, false);
                float left = (-Padding - 0.5f) * scale, right = (cols + Padding - 0.5f) * scale;
                float bottom = (-Padding - 0.5f) * scale, top = (rows + Padding - 0.5f) * scale;
                floorMesh = new Mesh { name = "Sea floor", vertices = new[] { new Vector3(left, 0, bottom), new Vector3(left, 0, top), new Vector3(right, 0, top), new Vector3(right, 0, bottom) }, triangles = new[] { 0, 1, 2, 0, 2, 3 } };
                child.AddComponent<MeshFilter>().sharedMesh = floorMesh;
                floorRenderer = child.AddComponent<MeshRenderer>();
                // Runtime floor material owns its render state; no material asset is changed.
                floorMaterial = new Material(Resources.Load<Shader>("Water/WaterFloor"));
                floorRenderer.sharedMaterial = floorMaterial;
                floorRenderer.sortingOrder = TerrainPresentation.WaterSortingOrder;
                floorRenderer.shadowCastingMode = ShadowCastingMode.Off;
                floorRenderer.receiveShadows = false;
            }
            if (floorRenderer != null)
            {
                floorRenderer.gameObject.SetActive(enabled);
                float left = (-Padding - 0.5f) * scale, right = (cols + Padding - 0.5f) * scale;
                float bottom = (-Padding - 0.5f) * scale, top = (rows + Padding - 0.5f) * scale;
                floorMesh.vertices = new[] { new Vector3(left, 0, bottom), new Vector3(left, 0, top), new Vector3(right, 0, top), new Vector3(right, 0, bottom) };
                floorMesh.RecalculateBounds();
                floorRenderer.transform.localPosition = new Vector3(0, surfaceY - surfaceRenderer.sharedMaterial.GetFloat("_FloorDepth"), 0);
                ApplyProperties();
            }
        }

        private void RefreshProbe()
        {
            bool enabled = WaterQuality.Has(GraphicsFeature.Reflections);
            if (!enabled)
            {
                if (reflectionProbe != null) reflectionProbe.gameObject.SetActive(false);
                surfaceRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                return;
            }
            if (reflectionProbe == null)
            {
                var child = new GameObject("Water reflection probe");
                child.transform.SetParent(transform, false);
                child.transform.localPosition = new Vector3((cols - 1) * scale * 0.5f, surfaceY + scale, (rows - 1) * scale * 0.5f);
                reflectionProbe = child.AddComponent<ReflectionProbe>();
                reflectionProbe.mode = ReflectionProbeMode.Realtime;
                reflectionProbe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
                reflectionProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
                reflectionProbe.boxProjection = true;
                reflectionProbe.size = new Vector3((cols + Padding * 2) * scale, 4 * scale, (rows + Padding * 2) * scale);
                reflectionProbe.cullingMask = ~(1 << gameObject.layer);
                reflectionProbe.nearClipPlane = 0.1f;
                reflectionProbe.farClipPlane = Mathf.Max(cols, rows) * scale * 4;
                reflectionProbe.importance = 10;
                surfaceRenderer.probeAnchor = child.transform;
                probeDirty = true;
            }
            if (!reflectionProbe.gameObject.activeSelf) probeDirty = true;
            reflectionProbe.gameObject.SetActive(true);
            int resolution = WaterQuality.Current == 3 ? 256 : 128;
            if (reflectionProbe.resolution != resolution) { reflectionProbe.resolution = resolution; probeDirty = true; }
            surfaceRenderer.reflectionProbeUsage = ReflectionProbeUsage.Simple;
        }

        private void LateUpdate()
        {
            if (reflectionProbe == null || !reflectionProbe.isActiveAndEnabled || terrain == null) return;
            if (probeDirty)
            {
                reflectionProbe.RenderProbe();
                probeDirty = false;
            }
            else if (probeAfterSettled && blend >= 1f && (boardView == null || boardView.PlinthsSettled))
            {
                reflectionProbe.RenderProbe();
                probeAfterSettled = false;
            }
        }

        private void OnDestroy()
        {
            if (previous != current) Release(previous);
            Release(current); Release(mesh); Release(floorMesh); Release(floorMaterial);
        }

        private static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
