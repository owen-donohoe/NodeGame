using UnityEngine;

namespace NodeWar.View
{
    public static class WaterMaterialFactory
    {
        private static Texture2D surfaceFoam, swell;
        private static Material fallback, runtime;
        private static int runtimeFeatures = -1, runtimeWaves = -1, runtimeTier = -1;

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

        public static Material Shared()
        {
            Material asset = Resources.Load<Material>("Water/NodeWarWater");
            if (asset != null) return asset;
            if (fallback == null)
                fallback = new Material(Resources.Load<Shader>("Water/NodeWarWater"))
                { name = "NodeWarWater fallback", hideFlags = HideFlags.HideAndDontSave };
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
            if (fallback != null) Object.Destroy(fallback);
            fallback = null;
            if (runtime != null) Release(runtime);
            runtime = null;
            runtimeFeatures = runtimeWaves = runtimeTier = -1;
        }
    }
}
