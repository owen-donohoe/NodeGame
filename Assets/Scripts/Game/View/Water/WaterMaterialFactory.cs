using UnityEngine;

namespace NodeWar.View
{
    public static class WaterMaterialFactory
    {
        private static Texture2D surfaceFoam, intersectionFoam, normals;
        private static Material fallback;

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
            if (surfaceFoam == null) surfaceFoam = Bake("Water surface foam", FoamNoise.Bake(256, 0x71ab));
            return surfaceFoam;
        }

        public static Texture2D IntersectionFoam()
        {
            if (intersectionFoam == null) intersectionFoam = Bake("Water intersection foam", FoamNoise.Bake(256, 0x36c1));
            return intersectionFoam;
        }

        public static Texture2D Normals()
        {
            if (normals == null) normals = Bake("Water normals", FoamNoise.BakeNormals(256, 0x5e19));
            return normals;
        }

        private static Texture2D Bake(string name, byte[] rgba)
        {
            var texture = new Texture2D(256, 256, TextureFormat.RGBA32, true, true)
            {
                name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear,
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
        }
    }
}
