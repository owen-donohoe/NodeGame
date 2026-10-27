using System.Collections.Generic;
using NodeWar.Config;
using NodeWar.Simulation;
using UnityEngine;

namespace NodeWar.View
{
    /// <summary>GFX Testing scene harness; never constructs or mutates simulation state.</summary>
    public sealed class BoardVisualsBench : MonoBehaviour
    {
        [SerializeField] private BoardConfig boardConfig;
        [Tooltip("Multiplier on BoardConfig node spacing; 1 matches the game.")]
        [SerializeField] private float nodeScale = 1f;
        [SerializeField, Range(-1, 4)] private int tierOverride = -1;
        [SerializeField] private float stepSeconds = 0.8f;
        [SerializeField] private bool autoReplay = true;
        [SerializeField] private bool frameCamera = true;

        private BoardConfig runtimeConfig;
        private BoardTerrainView view;
        private DraftPlacement[] sample;
        private readonly List<DraftPlacement> placed = new List<DraftPlacement>();
        private bool[] occupied;
        private int next;
        private float elapsed;
        private bool matched;
        private bool panelVisible = true, paused;
        private Vector2 panelScroll;
        private float frameSeconds, frameSum;
        private int frameCount;

        private void Start() => ReplayDraft();

        private void OnValidate() => ApplyTier();

        private void ApplyTier()
        {
            if (WaterQuality.Override == tierOverride) return;
            WaterQuality.SelectOverride(tierOverride);
        }

        [ContextMenu("Replay draft")]
        public void ReplayDraft()
        {
            if (!Application.isPlaying) return;
            ReleaseBoard();
            ApplyTier();
            if (boardConfig == null)
            {
                Debug.LogWarning("BoardVisualsBench needs a BoardConfig.", this);
                return;
            }
            // Match GameManager's Create(config) path without editing the shared config asset.
            runtimeConfig = Instantiate(boardConfig);
            runtimeConfig.nodeScale = boardConfig.nodeScale * Mathf.Max(0.01f, nodeScale);
            view = BoardTerrainView.Create(runtimeConfig);
            view.transform.SetParent(transform, true);
            sample = BoardVisualsBenchDraft.Create();
            occupied = new bool[runtimeConfig.Data.gridCols * runtimeConfig.Data.gridRows];
            placed.Clear();
            next = 0;
            elapsed = 0f;
            matched = false;
            ShowDraft();
            if (frameCamera) FrameCamera();
        }

        private void Update()
        {
            ApplyTier();
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null
                && UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame) panelVisible = !panelVisible;
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F1)) panelVisible = !panelVisible;
#endif
            frameSum += Time.unscaledDeltaTime;
            frameCount++;
            if (frameSum >= 0.5f)
            {
                frameSeconds = frameSum / frameCount;
                frameSum = 0f;
                frameCount = 0;
            }
            if (view == null || paused) return;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < Mathf.Max(0.05f, stepSeconds)) return;
            elapsed = 0f;
            if (matched)
            {
                if (autoReplay) ReplayDraft();
                return;
            }
            if (next >= sample.Length)
            {
                view.ShowMatch(placed.ToArray());
                matched = true;
                // Leave time to inspect the completed shoal rise before looping.
                elapsed = -Mathf.Max(0.8f, stepSeconds * 2f);
                return;
            }
            DraftPlacement placement = sample[next++];
            BoardConfigData board = runtimeConfig.Data;
            if (PlacementLegality.CanPlace(board, occupied, placement.districtType, placement.gridX, placement.gridZ))
            {
                occupied[placement.gridZ * board.gridCols + placement.gridX] = true;
                placed.Add(placement);
            }
            else Debug.LogWarning($"Skipping illegal bench placement: {placement.districtType} at ({placement.gridX},{placement.gridZ}).", this);
            ShowDraft();
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || !panelVisible) return;
            GUILayout.BeginArea(new Rect(12, 12, 330, Mathf.Max(120, Mathf.Min(Screen.height - 24, 740))), GUI.skin.box);
            panelScroll = GUILayout.BeginScrollView(panelScroll);
            GUILayout.Label($"Water tuning (F1)   {frameSeconds * 1000f:F2} ms");
            GUILayout.BeginHorizontal();
            string[] tiers = { "Low", "Medium", "High", "Ultra", "Custom" };
            for (int i = 0; i < tiers.Length; i++)
            {
                bool selected = WaterQuality.Current == i;
                if (GUILayout.Button(selected ? $"[{tiers[i]}]" : tiers[i]))
                {
                    tierOverride = i;
                    ApplyTier();
                }
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Replay draft")) ReplayDraft();
            if (GUILayout.Button(paused ? "Resume" : "Pause")) paused = !paused;
            GUILayout.EndHorizontal();
            GUILayout.Label($"Step seconds: {stepSeconds:F2}");
            stepSeconds = GUILayout.HorizontalSlider(stepSeconds, 0.05f, 3f);
            if (GUILayout.Button("Waves: " + (WaterQuality.Waves == 0 ? "Off" : WaterQuality.Waves.ToString())))
            {
                int waves = WaterFeatures.NextWaves(WaterQuality.Waves);
                SelectCustom();
                WaterQuality.CustomWaves = waves;
                WaterQuality.Apply();
            }
            FeatureToggle(GraphicsFeature.Transparency, "Transparent shallows");
            FeatureToggle(GraphicsFeature.SurfaceFoamDistortion, "Foam ripple");
            FeatureToggle(GraphicsFeature.HorizonFresnel, "Horizon sheen");
            FeatureToggle(GraphicsFeature.Specular, "Sun glints");
            FeatureToggle(GraphicsFeature.CrestColour, "Wave crests");
            FeatureToggle(GraphicsFeature.ShoreNoise, "Shore wobble");
            FeatureToggle(GraphicsFeature.Dither, "Smooth gradients");
            FeatureToggle(GraphicsFeature.Shadows, "Shadows on water");
            FeatureToggle(GraphicsFeature.DetailNormals, "Detail ripples");
            FeatureToggle(GraphicsFeature.MacroNormals, "Large swells");
            FeatureToggle(GraphicsFeature.Reflections, "Reflections");
            FeatureToggle(GraphicsFeature.LappingLines, "Lapping lines");
            FeatureToggle(GraphicsFeature.CrestFoam, "Crest foam");
            FeatureToggle(GraphicsFeature.SecondFoamLayer, "Double foam");
            Material material = WaterMaterialFactory.Shared();
            MaterialSlider(material, "_WaveFadeStart", "Wave fade start (cells)", 0f, 4f);
            MaterialSlider(material, "_WaveFadeEnd", "Wave fade end (cells)", 0.1f, 4f);
            MaterialSlider(material, "_CrestStrength", "Crest strength", 0f, 1f);
            MaterialSlider(material, "_ReflectionMin", "Reflection floor", 0f, 1f);
            MaterialSlider(material, "_ReflectionStrength", "Reflection strength", 0f, 1f);
            MaterialSlider(material, "_MacroNormalsStrength", "Macro normal strength", 0f, 2f);
            MaterialSlider(material, "_ShallowAlpha", "Shallow alpha", 0f, 1f);
            MaterialSlider(material, "_ClearDepth", "Clear depth (cells)", 0.01f, 3f);
            MaterialSlider(material, "_ShoreNoiseAmount", "Shore noise (cells)", 0f, 1f);
            MaterialSlider(material, "_LapFrequency", "Lap frequency", 0.1f, 4f);
            MaterialSlider(material, "_DepthRange", "Depth range", 0.1f, 3f);
            MaterialSlider(material, "_ColorSteps", "Colour steps", 0f, 16f, true);
            MaterialSlider(material, "_SurfaceFoamWidth", "Surface foam width", 0f, 0.3f);
            MaterialSlider(material, "_SurfaceFoamTiling", "Surface foam tiling", 0.01f, 2f);
            MaterialSlider(material, "_SurfaceFoamDistortion", "Surface foam distortion", 0f, 10f);
            MaterialSlider(material, "_IntersectionFoamDepth", "Intersection foam depth", 0.01f, 2f);
            MaterialSlider(material, "_IntersectionFoamCutoff", "Intersection foam cutoff", 0f, 1f);
            MaterialSlider(material, "_WaveSteepnessScale", "Wave steepness scale", 0f, 2f);
            MaterialSlider(material, "_Smoothness", "Smoothness", 0f, 1f);
            MaterialSlider(material, "_NormalsStrength", "Normals strength", 0f, 2f);
            if (GUILayout.Button("Reset to shader defaults")) ResetMaterial(material);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void SelectCustom()
        {
            int features = WaterQuality.Features, waves = WaterQuality.Waves;
            tierOverride = 4;
            WaterQuality.Override = 4;
            WaterQuality.CustomFeatures = features;
            WaterQuality.CustomWaves = waves;
        }

        private void FeatureToggle(GraphicsFeature feature, string label)
        {
            bool enabled = WaterQuality.Has(feature);
            bool next = GUILayout.Toggle(enabled, label);
            if (enabled == next) return;
            SelectCustom();
            int bit = (int)feature;
            WaterQuality.CustomFeatures = next ? WaterQuality.CustomFeatures | bit : WaterQuality.CustomFeatures & ~bit;
            WaterQuality.Apply();
        }

        private static void MaterialSlider(Material material, string property, string label,
            float min, float max, bool integer = false)
        {
            float value = material.GetFloat(property);
            GUILayout.Label($"{label}: {value:F2}");
            // Preserve authored values outside the slider range until the user changes the slider.
            bool previousChange = GUI.changed;
            GUI.changed = false;
            float nextValue = GUILayout.HorizontalSlider(value, min, max);
            bool sliderChanged = GUI.changed;
            GUI.changed |= previousChange;
            if (integer) nextValue = Mathf.Round(nextValue);
            if (!sliderChanged || Mathf.Approximately(value, nextValue)) return;
            material.SetFloat(property, nextValue);
            MarkMaterialDirty(material);
        }

        private static void ResetMaterial(Material material)
        {
            var defaults = new Material(material.shader);
            // Copy only shader properties: keep the asset's shader and render settings.
            Shader shader = material.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                string property = shader.GetPropertyName(i);
                switch (shader.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        material.SetColor(property, defaults.GetColor(property));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        material.SetVector(property, defaults.GetVector(property));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        material.SetFloat(property, defaults.GetFloat(property));
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        material.SetTexture(property, defaults.GetTexture(property));
                        material.SetTextureScale(property, defaults.GetTextureScale(property));
                        material.SetTextureOffset(property, defaults.GetTextureOffset(property));
                        break;
                }
            }
            Destroy(defaults);
            WaterQuality.Apply();
            MarkMaterialDirty(material);
        }

        private static void MarkMaterialDirty(Material material)
        {
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(material);
#endif
        }

        private void ShowDraft()
        {
            view.ShowDraft(placed.ToArray(), next < sample.Length,
                next < sample.Length ? sample[next].districtType : DistrictType.None);
        }

        private void FrameCamera()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            BoardConfigData board = runtimeConfig.Data;
            float scale = runtimeConfig.nodeScale;
            Vector3 centre = new Vector3((board.gridCols - 1) * scale * 0.5f, 0f, (board.gridRows - 1) * scale * 0.5f);
            float radius = Mathf.Sqrt(board.gridCols * board.gridCols + board.gridRows * board.gridRows) * scale * 0.5f;
            camera.transform.rotation = Quaternion.Euler(45f, 30f, 0f);
            float halfFov = camera.fieldOfView * Mathf.Deg2Rad * 0.5f;
            float limitingFov = Mathf.Min(halfFov, Mathf.Atan(Mathf.Tan(halfFov) * camera.aspect));
            float distance = radius * 1.15f / Mathf.Sin(limitingFov);
            camera.transform.position = centre - camera.transform.forward * distance;
            if (camera.orthographic) camera.orthographicSize = radius * 1.15f / Mathf.Min(1f, camera.aspect);
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, distance + radius * 2f);
        }

        private void ReleaseBoard()
        {
            if (view != null)
            {
                view.gameObject.SetActive(false);
                Destroy(view.gameObject);
            }
            if (runtimeConfig != null) Destroy(runtimeConfig);
            view = null;
            runtimeConfig = null;
        }

        private void OnDisable()
        {
            if (WaterQuality.Override != tierOverride) return;
            WaterQuality.Override = -1;
            WaterSurfaceView.RefreshAll();
        }

        private void OnDestroy() => ReleaseBoard();
    }
}
