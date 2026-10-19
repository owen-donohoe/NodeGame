using System.Collections.Generic;
using UnityEngine;

namespace NodeWar.Lobby
{
    /// <summary>One texture per silhouette/tint during the stage's lifetime, shared by all flats.</summary>
    internal sealed class StagePaperArt
    {
        private sealed class Entry
        {
            public PaperKind Kind;
            public Color Tint;
            public bool Silhouette;
            public Texture2D Texture;
        }

        public static readonly Color Ink = new Color32(45, 35, 25, 255);
        private static readonly Color Parchment = new Color32(244, 233, 213, 255);
        private readonly List<Entry> textures = new List<Entry>();

        public Texture2D TextureFor(PaperKind kind, Color tint, bool silhouette = false)
        {
            // Selection backings only need the alpha mask, independent of item tint.
            if (silhouette) tint = Color.white;
            for (int i = 0; i < textures.Count; i++)
            {
                Entry entry = textures[i];
                if (entry.Kind == kind && entry.Tint == tint && entry.Silhouette == silhouette)
                    return entry.Texture;
            }

            const int size = 128;
            byte[] mask = PaperRasterizer.Rasterize(kind, size, size);
            var pixels = new Color32[mask.Length];
            Color paper = Color.Lerp(Parchment, tint, 0.32f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int index = y * size + x;
                    Color colour;
                    if (silhouette) colour = Color.white;
                    else if (mask[index] == 1) colour = paper * PaperRasterizer.Grain(x, y);
                    else colour = Ink;
                    colour.a = mask[index] == 0 ? 0f : 1f;
                    pixels[index] = colour;
                }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = "Paper " + kind + (silhouette ? " silhouette" : " tinted"),
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            textures.Add(new Entry { Kind = kind, Tint = tint, Silhouette = silhouette, Texture = texture });
            return texture;
        }

        public void Dispose()
        {
            for (int i = 0; i < textures.Count; i++) Object.Destroy(textures[i].Texture);
            textures.Clear();
        }
    }
}
