using UnityEngine;

namespace NodeWar.View
{
    public static class WaterMaterialFactory
    {
        private static Texture2D packedFoam;

        public static Material Create(int tier)
        {
            var material = new Material(Resources.Load<Shader>("Water/NodeWarWater"));
            material.name = "NodeWarWater";
            material.DisableKeyword("_WATER_MEDIUM");
            material.DisableKeyword("_WATER_HIGH");
            material.DisableKeyword("_WATER_ULTRA");
            if (tier > 0) material.EnableKeyword(tier == 1 ? "_WATER_MEDIUM" : tier == 2 ? "_WATER_HIGH" : "_WATER_ULTRA");
            // Shader properties define the shared defaults, including both exact palettes.
            material.SetTexture("_FoamTex", Foam());
            return material;
        }

        private static Texture2D Foam()
        {
            if (packedFoam != null) return packedFoam;
            const int size = 128;
            packedFoam = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "Water packed foam", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            uint seed = 0x71ab39u;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    seed = seed * 1664525u + 1013904223u;
                    float u = x * Mathf.PI * 2f / size, v = y * Mathf.PI * 2f / size;
                    float pattern = Mathf.Sin(u * 5 + Mathf.Sin(v * 3)) * Mathf.Cos(v * 4 + Mathf.Sin(u * 2));
                    byte surface = (byte)Mathf.RoundToInt(Mathf.Pow(Mathf.Clamp01(pattern * 0.5f + 0.5f), 3) * 255);
                    byte shore = (byte)Mathf.RoundToInt((Mathf.Sin(u * 7 + Mathf.Cos(v * 5)) * 0.5f + 0.5f) * 255);
                    pixels[y * size + x] = new Color32(surface, shore, (byte)(seed >> 24), 255);
                }
            packedFoam.SetPixels32(pixels);
            packedFoam.Apply(false, true);
            return packedFoam;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetFoam()
        {
            if (packedFoam != null) Object.Destroy(packedFoam);
            packedFoam = null;
        }
    }
}
