using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace NodeWar.View
{
    public static class WaterMaterialFactory
    {
        private static Texture2D surfaceFoam, swell;
        private static Material fallback, runtime, far;
        private static int runtimeFeatures = -1, runtimeWaves = -1, runtimeTier = -1;

        private static readonly HashSet<int> warmedModes = new HashSet<int>();

        /// <summary>URP variants are SRP passes. Dynamic-branch feature keywords do not create variants.</summary>
        public static void WarmCurrentTier()
        {
            int mode = WaterQuality.Current == 0 ? -1 : WaterQuality.Waves;
            if (!warmedModes.Add(mode)) return;
            var variants = new ShaderVariantCollection { name = "Board water warm-up" };
            Shader water = Runtime.shader;
            var keywords = new List<string>();
            if (mode == -1) keywords.Add("_WATER_LITE");
            else if (mode != 0) keywords.Add(mode == 1 ? "_WATER_WAVES_NOISE" : mode == 2 ? "_WATER_WAVES_2" : "_WATER_WAVES_4");
            // Forward shadows and depth-normal encoding are separate pass variants.
            for (int shadows = 0; shadows < 3; shadows++)
            {
                var combination = new List<string>(keywords);
                if (shadows > 0) combination.Add(shadows == 1 ? "_MAIN_LIGHT_SHADOWS" : "_MAIN_LIGHT_SHADOWS_CASCADE");
                variants.Add(new ShaderVariantCollection.ShaderVariant(water, PassType.ScriptableRenderPipeline, combination.ToArray()));
            }
            var normals = new List<string>(keywords) { "_GBUFFER_NORMALS_OCT" };
            variants.Add(new ShaderVariantCollection.ShaderVariant(water, PassType.ScriptableRenderPipeline, normals.ToArray()));
            AddSimpleVariant(variants, Resources.Load<Shader>("Water/WaterFar"));
            AddSimpleVariant(variants, Resources.Load<Shader>("Water/WaterFloor"));
            Shader plinth = Resources.Load<Shader>("Board/NodeWarPlinth");
            AddSimpleVariant(variants, plinth);
            variants.Add(new ShaderVariantCollection.ShaderVariant(plinth, PassType.ScriptableRenderPipeline, "_GBUFFER_NORMALS_OCT"));
            variants.Add(new ShaderVariantCollection.ShaderVariant(plinth, PassType.ShadowCaster));
            variants.Add(new ShaderVariantCollection.ShaderVariant(plinth, PassType.ShadowCaster, "_CASTING_PUNCTUAL_LIGHT_SHADOW"));
            variants.WarmUp();
            Release(variants);
        }

        private static void AddSimpleVariant(ShaderVariantCollection collection, Shader shader) =>
            collection.Add(new ShaderVariantCollection.ShaderVariant(shader, PassType.ScriptableRenderPipeline));
        public static Material ForQuality(int features, int waves, int tier)
        {
            if (runtime == null || features != runtimeFeatures || waves != runtimeWaves || tier != runtimeTier)
            {
                RecreateRuntime();
                runtimeFeatures = features; runtimeWaves = waves; runtimeTier = tier;
            }
            else runtime.CopyPropertiesFromMaterial(Shared()); // Keep deliberate bench edits live.
            return runtime;
        }

        public static Material Runtime => runtime != null ? runtime : RecreateRuntime();

        public static Material RecreateRuntime()
        {
            if (runtime != null) Release(runtime);
            // Render state belongs to this runtime copy; bench edits deliberately target Shared().
            runtime = new Material(Shared()) { name = "NodeWarWater runtime", hideFlags = HideFlags.HideAndDontSave };
            return runtime;
        }

        public static Material Far()
        {
            if (far == null) far = new Material(Resources.Load<Shader>("Water/WaterFar")) { name = "Water far ocean", hideFlags = HideFlags.HideAndDontSave };
            Material source = Runtime;
            far.SetColor("_OceanDeepColor", source.GetColor("_OceanDeepColor"));
            far.SetColor("_HorizonColor", source.GetColor("_HorizonColor"));
            far.SetFloat("_HorizonDistance", source.GetFloat("_HorizonDistance"));
            far.SetFloat("_ReflectionMin", source.GetFloat("_ReflectionMin"));
            far.SetFloat("_ReflectionStrength", source.GetFloat("_ReflectionStrength"));
            far.SetFloat("_HorizonEnabled", WaterQuality.Has(GraphicsFeature.HorizonFresnel) ? 1 : 0);
            return far;
        }

        public static Material Shared()
        {
            if (fallback != null) return fallback;
            Material asset = Resources.Load<Material>("Water/NodeWarWater");
            Shader shader = Resources.Load<Shader>("Water/NodeWarWater");
            // The bench edits this view-owned tuning copy. Never dirty the imported .mat.
            fallback = asset != null ? new Material(asset) : new Material(shader);
            fallback.name = "NodeWarWater tuning";
            fallback.hideFlags = HideFlags.HideAndDontSave;
            if (asset != null)
            {
                // The shipped material serializes the previous defaults. Migrate only those
                // known values, preserving deliberate artist overrides and assigned textures.
                var defaults = new Material(shader);
                void MigrateFloat(string property, float previous)
                {
                    if (Mathf.Approximately(fallback.GetFloat(property), previous))
                        fallback.SetFloat(property, defaults.GetFloat(property));
                }
                void MigrateWave(string property, Vector4 previous)
                {
                    if (fallback.GetVector(property) == previous)
                        fallback.SetVector(property, defaults.GetVector(property));
                }
                MigrateFloat("_SwashSpeed", 0.25f);
                MigrateFloat("_SwashOpacity", 0.85f);
                MigrateFloat("_SwashWidth", 0.08f);
                MigrateFloat("_SurfaceFoamWidth", 0.07f);
                MigrateFloat("_ShoreWaveDamping", 1f);
                MigrateFloat("_WaveScale", 1.5f);
                MigrateFloat("_BodyStepA", 0.25f);
                MigrateFloat("_BodyStepB", 0.6f);
                MigrateWave("_WaveB", new Vector4(0.422618f, 0.906308f, 0.22f, 11));
                MigrateWave("_WaveC", new Vector4(-0.766044f, 0.642788f, 0.14f, 6));
                MigrateWave("_WaveD", new Vector4(-0.422618f, -0.906308f, 0.1f, 3.5f));
                // Deep-lake defaults: shallow is a tween from lake toward dark sea green; deep is dark sea green.
                Color previousDeepLake = new Color(0.243137f, 0.560784f, 0.819608f, 1f);
                foreach (string property in new[] { "_DeepLakeShallowColor", "_DeepLakeDeepColor" })
                    if (fallback.GetColor(property) == previousDeepLake)
                        fallback.SetColor(property, defaults.GetColor(property));
                Release(defaults);
            }
            return fallback;
        }

        public static bool NeedsFoam(Material material, string slot)
        {
            Texture texture = material.GetTexture(slot);
            return texture == null || texture == Texture2D.blackTexture || texture.name == "black";
        }

        public static bool NeedsSwell(Material material)
        {
            Texture texture = material.GetTexture("_SwellTex");
            return texture == null || texture == Texture2D.blackTexture || texture.name == "black";
        }

        public static Texture2D SurfaceFoam()
        {
            if (surfaceFoam == null) surfaceFoam = Bake("Water surface foam", FoamNoise.BakeVoronoi(256, 0x71ab, 6, 0.65));
            return surfaceFoam;
        }

        public static Texture2D Swell()
        {
            if (swell == null) swell = Bake("Water swell", FoamNoise.BakeSwell(256, 0x5e19));
            return swell;
        }

        private static Texture2D Bake(string name, byte[] rgba, int size = 256)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixelData(rgba, 0);
            texture.Apply(true, true);
            return texture;
        }

        private static void Release(Object value)
        {
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTextures()
        {
            if (surfaceFoam != null) Object.Destroy(surfaceFoam);
            if (swell != null) Object.Destroy(swell);
            surfaceFoam = swell = null;
            warmedModes.Clear();
            if (fallback != null) Object.Destroy(fallback);
            fallback = null;
            if (far != null) Release(far);
            far = null;
            if (runtime != null) Release(runtime);
            runtime = null;
            runtimeFeatures = runtimeWaves = runtimeTier = -1;
        }
    }
}
