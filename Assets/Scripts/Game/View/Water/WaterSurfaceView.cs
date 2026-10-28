using NodeWar.Lobby;
using NodeWar.Simulation;
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Profiling;
using System.Threading.Tasks;

namespace NodeWar.View
{
    /// <summary>Owns the runtime water resources; terrain is copied, never modified.</summary>
    public sealed class WaterSurfaceView : MonoBehaviour
    {
        private static readonly ProfilerMarker BakeMarker = new ProfilerMarker("Water.ShoreBake");
        private static readonly ProfilerMarker UploadMarker = new ProfilerMarker("Water.ShoreUpload");
        private static readonly ProfilerMarker MeshMarker = new ProfilerMarker("Water.MeshRebuild");
        private static readonly ProfilerMarker ProbeMarker = new ProfilerMarker("Water.ProbeSubmit");
        public static double LastBakeMs { get; private set; }
        public static double LastUploadMs { get; private set; }
        public static double LastMeshMs { get; private set; }
        public static double LastProbeMs { get; private set; }
        public static string PlacementTimings => $"Water ms ({LastBakePath}): bake {LastBakeMs:F2}, upload {LastUploadMs:F2}, mesh {LastMeshMs:F2}, probe submit {LastProbeMs:F2}";
        private static double Milliseconds(long start) => (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
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
        private bool probeDirty;
        private float probeNotBefore, lastProbeAt = float.NegativeInfinity;
        private int probeRenderId = -1;
        private float floorY = float.NaN;
        private Mesh floorMesh;
        private Material floorMaterial;
        private MeshRenderer floorRenderer;
        private Mesh mesh;
        private MeshFilter filter;
        private MeshRenderer surfaceRenderer;
        private Texture2D current, previous;
        private ShoreField.Workspace shoreWorkspace;
        private TerrainType[] bakingTerrain;
        private byte[] currentBytes, previousBytes;
        private Task<double> bakeTask;
        private int requestVersion, bakingVersion, appliedVersion;
        public static string LastBakePath { get; private set; } = "pending";

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
            surfaceRenderer.sharedMaterials = new[] { WaterMaterialFactory.Runtime, WaterMaterialFactory.Far() };
            ApplyProperties();
            int next = WaterFeatures.SubdivisionsFor(WaterQuality.Current, WaterQuality.Waves, WaterMaterialFactory.Runtime.GetFloat("_WaveScale"), scale);
            RefreshProbe();
            RefreshFloor();
            surfaceRenderer.receiveShadows = WaterQuality.Has(GraphicsFeature.Shadows);
            WaterMaterialFactory.WarmCurrentTier();
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
            shoreWorkspace = new ShoreField.Workspace(cols, rows);
            terrain = new TerrainType[cols * rows];
            bakingTerrain = new TerrainType[terrain.Length];
            currentBytes = new byte[shoreWorkspace.Field.rgba.Length];
            previousBytes = new byte[currentBytes.Length];
            subdivisions = WaterFeatures.SubdivisionsFor(WaterQuality.Current, WaterQuality.Waves, WaterMaterialFactory.Runtime.GetFloat("_WaveScale"), scale);
            WaterQuality.Apply();
            properties = new MaterialPropertyBlock();
            filter = gameObject.AddComponent<MeshFilter>();
            surfaceRenderer = gameObject.AddComponent<MeshRenderer>();
            surfaceRenderer.sharedMaterials = new[] { WaterMaterialFactory.Runtime, WaterMaterialFactory.Far() };
            ApplyProperties();
            surfaceRenderer.sortingOrder = TerrainPresentation.WaterSortingOrder;
            surfaceRenderer.shadowCastingMode = ShadowCastingMode.Off;
            surfaceRenderer.receiveShadows = WaterQuality.Has(GraphicsFeature.Shadows);
            WaterMaterialFactory.WarmCurrentTier();
            RefreshProbe();
            RefreshFloor();
        }

        public void SetTerrain(TerrainType[] resolved)
        {
            if (requestVersion > 0 && SameTerrain(resolved)) return;
            ShoreField.Validate(cols, rows, resolved, 16, 4);
            LastBakeMs = LastUploadMs = LastMeshMs = LastProbeMs = 0;
            System.Array.Copy(resolved, terrain, terrain.Length);
            requestVersion++;
            if (mesh == null)
            {
                meshPadding = Padding;
                RebuildMesh(resolved);
            }
            probeDirty = true;
            probeNotBefore = Time.unscaledTime + 1.5f;
            StartBake();
        }

        private void StartBake()
        {
            if (bakeTask != null || requestVersion == appliedVersion) return;
            System.Array.Copy(terrain, bakingTerrain, terrain.Length);
            bakingVersion = requestVersion;
            bakeTask = Task.Run(BakeRequestedTerrain);
        }

        private double BakeRequestedTerrain()
        {
            long started = Stopwatch.GetTimestamp();
            using (BakeMarker.Auto())
            {
                bool incremental = shoreWorkspace.TryAddLand(bakingTerrain);
                if (!incremental) shoreWorkspace.Bake(bakingTerrain);
                // Task completion publishes this result along with its byte buffer.
                workerIncremental = incremental;
            }
            return Milliseconds(started);
        }

        private bool workerIncremental;

        private void PollBake()
        {
            if (bakeTask == null || !bakeTask.IsCompleted) return; // Never wait on a frame.
            Task<double> finished = bakeTask;
            bakeTask = null;
            if (finished.IsFaulted)
            {
                UnityEngine.Debug.LogException(finished.Exception, this);
                appliedVersion = bakingVersion; // Retry only when new terrain arrives.
            }
            else if (bakingVersion == requestVersion)
            {
                LastBakeMs = finished.Result;
                LastBakePath = workerIncremental ? "incremental worker" : "full EDT worker";
                UploadField(shoreWorkspace.Field);
                appliedVersion = bakingVersion;
                UnityEngine.Debug.Log(PlacementTimings, this);
            }
            StartBake(); // Newer terrain replaces stale results; at most one worker owns the buffers.
        }

        private void UploadField(ShoreFieldData field)
        {
            long started = Stopwatch.GetTimestamp();
            using (UploadMarker.Auto())
            {
                bool first = current == null;
                if (first)
                {
                    current = CreateShoreTexture(field);
                    previous = CreateShoreTexture(field);
                }
                // Flatten the visible blend into the old source before swapping both reusable textures.
                // No GetPixels32 arrays and no texture/buffer allocation during subsequent placements.
                if (!first)
                    for (int i = 0; i < currentBytes.Length; i++)
                        previousBytes[i] = (byte)Mathf.RoundToInt(Mathf.Lerp(previousBytes[i], currentBytes[i], blend));
                Texture2D swap = current; current = previous; previous = swap;
                System.Array.Copy(field.rgba, currentBytes, currentBytes.Length);
                if (first) System.Array.Copy(currentBytes, previousBytes, currentBytes.Length);
                previous.SetPixelData(previousBytes, 0);
                previous.Apply(false, false);
                current.SetPixelData(currentBytes, 0);
                current.Apply(false, false);
                blend = first || ReducedMotion ? 1f : 0f;
                Vector3 origin = transform.TransformPoint(new Vector3(field.originX * scale, surfaceY, field.originZ * scale));
                shoreST = new Vector4(origin.x, origin.z,
                    1f / (field.width * field.worldPerTexel * scale), 1f / (field.height * field.worldPerTexel * scale));
                ApplyProperties();
            }
            LastUploadMs = Milliseconds(started);
        }

        private static Texture2D CreateShoreTexture(ShoreFieldData field) =>
            new Texture2D(field.width, field.height, TextureFormat.RGBA32, false, true)
            { name = "Water shore field", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        private void RebuildMesh(TerrainType[] resolved)
        {
            long started = Stopwatch.GetTimestamp();
            using (MeshMarker.Auto())
            {
            var data = WaterMeshBuilder.Build(cols, rows, resolved, subdivisions, Padding, skirtCells);
            var vertices = new Vector3[data.positions.Length / 3];
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = new Vector3(data.positions[i * 3] * scale, surfaceY, data.positions[i * 3 + 2] * scale);
            var nextMesh = new Mesh { name = "Water surface", indexFormat = IndexFormat.UInt32 };
            nextMesh.vertices = vertices;
            nextMesh.subMeshCount = 2;
            nextMesh.SetTriangles(data.nearIndices, 0);
            nextMesh.SetTriangles(data.farIndices, 1);
            nextMesh.RecalculateBounds();
            // Recalculated bounds include the whole skirt; expand further for GPU Gerstner displacement.
            Bounds bounds = nextMesh.bounds;
            bounds.Expand(40f); // Supports the full wave/group/steepness slider range.
            nextMesh.bounds = bounds;
            filter.sharedMesh = nextMesh;
            Release(mesh);
            mesh = nextMesh;
            }
            LastMeshMs = Milliseconds(started);
        }

        private bool SameTerrain(TerrainType[] resolved)
        {
            if (terrain == null || resolved == null || terrain.Length != resolved.Length) return false;
            for (int i = 0; i < terrain.Length; i++) if (terrain[i] != resolved[i]) return false;
            return true;
        }

        private void Update()
        {
            PollBake();
            if (surfaceRenderer == null) return;
            Material material = surfaceRenderer.sharedMaterial;
            if (WaterMaterialFactory.NeedsFoam(material, "_FoamTex") != generatedSurfaceFoam
                || WaterMaterialFactory.NeedsSwell(material) != generatedSwell) ApplyProperties();
            if (floorRenderer != null && floorRenderer.gameObject.activeSelf)
                UpdateFloorPosition(material);
            if (blend >= 1f) return;
            blend = ReducedMotion ? 1f : Mathf.Min(1f, blend + Time.unscaledDeltaTime / 0.4f);
            ApplyProperties();
            if (blend == 1f) FinishBlend();
        }

        private void FinishBlend() => ApplyProperties(); // Keep both textures alive for the next edit.
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
            properties.SetFloat("_DetailFadeEnd", Padding);
            Material material = surfaceRenderer.sharedMaterial;
            generatedSurfaceFoam = WaterMaterialFactory.NeedsFoam(material, "_FoamTex");
            generatedSwell = WaterMaterialFactory.NeedsSwell(material);
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

        private void UpdateFloorPosition(Material material)
        {
            int mode = material.IsKeywordEnabled("_WATER_LITE") ? 0 : WaterQuality.Waves;
            double amplitude = 0;
            int count = mode == 4 ? 4 : mode == 2 ? 2 : 0;
            for (int i = 0; i < count; i++)
            {
                Vector4 wave = material.GetVector(i == 0 ? "_WaveA" : i == 1 ? "_WaveB" : i == 2 ? "_WaveC" : "_WaveD");
                amplitude += WaterWaveBounds.Amplitude(wave.z, wave.w, material.GetFloat("_WaveScale"));
            }
            float next = surfaceY - material.GetFloat("_FloorDepth") - (float)WaterWaveBounds.MaxTrough(mode,
                material.GetFloat("_NoiseWaveHeight"), amplitude, material.GetFloat("_WaveSteepnessScale"), material.GetFloat("_WaveGroupStrength"));
            if (next == floorY) return;
            floorY = next;
            floorRenderer.transform.localPosition = new Vector3(0, next, 0);
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
                UpdateFloorPosition(surfaceRenderer.sharedMaterial);
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
                reflectionProbe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
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

        private void SubmitProbe()
        {
            long started = Stopwatch.GetTimestamp();
            using (ProbeMarker.Auto()) probeRenderId = reflectionProbe.RenderProbe();
            LastProbeMs = Milliseconds(started);
            UnityEngine.Debug.Log(PlacementTimings, this);
        }

        private void LateUpdate()
        {
            if (reflectionProbe == null || !reflectionProbe.isActiveAndEnabled || !probeDirty || current == null) return;
            if (requestVersion != appliedVersion || blend < 1f || (boardView != null && !boardView.PlinthsSettled)) return;
            if (Time.unscaledTime < probeNotBefore || Time.unscaledTime - lastProbeAt < 1.5f) return;
            if (probeRenderId >= 0 && !reflectionProbe.IsFinishedRendering(probeRenderId)) return;
            SubmitProbe();
            lastProbeAt = Time.unscaledTime;
            probeDirty = false;
        }
        private void OnDestroy()
        {
            if (bakeTask != null)
                bakeTask.ContinueWith(task => { var observed = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
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







