using UnityEngine;

namespace NodeWar.View
{
    public static class WaterMaterialFactory
    {
        private static Texture2D surfaceFoam, intersectionFoam, normals;
        private static Material fallback, runtime;

        public static Material Runtime => runtime != null ? runtime : RecreateRuntime();

        public static Material RecreateRuntime()
        {
            if (runtime != null) Object.Destroy(runtime);
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

        public static bool NeedsNormals(Material material)
        {
            Texture texture = material.GetTexture("_NormalsTex");
            return texture == null || texture == Texture2D.normalTexture || texture.name == "bump";
        }

        public static Texture2D SurfaceFoam()
        {
            if (surfaceFoam == null) surfaceFoam = Bake("Water surface foam", FoamNoise.BakeVoronoi(512, 0x71ab, 6, 0.65), 512);
            return surfaceFoam;
        }

        public static Texture2D IntersectionFoam()
        {
            if (intersectionFoam == null) intersectionFoam = Bake("Water intersection foam", FoamNoise.BakeVoronoi(512, 0x36c1, 6, 0.65), 512);
            return intersectionFoam;
        }

        public static Texture2D Normals()
        {
            if (normals == null) normals = Bake("Water normals", FoamNoise.BakeNormals(256, 0x5e19));
            return normals;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTextures()
        {
            if (surfaceFoam != null) Object.Destroy(surfaceFoam);
            if (intersectionFoam != null) Object.Destroy(intersectionFoam);
            if (normals != null) Object.Destroy(normals);
            surfaceFoam = intersectionFoam = normals = null;
            if (fallback != null) Object.Destroy(fallback);
            fallback = null;
            if (runtime != null) Object.Destroy(runtime);
            runtime = null;
        }
    }
}
