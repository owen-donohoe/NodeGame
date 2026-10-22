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
            public int Mark;
            public Texture2D Texture;
        }

        public static readonly Color Ink = new Color32(45, 35, 25, 255);
        private static readonly Color Parchment = new Color32(244, 233, 213, 255);
        private readonly List<Entry> textures = new List<Entry>();
        // Shape coverage depends only on kind + size, so tints reuse one rasterisation.
        private readonly Dictionary<long, Coverage> coverage = new Dictionary<long, Coverage>();

        private sealed class Coverage { public byte[] Mask; public byte[] Alpha; }

        private readonly int screenHeight;

        public StagePaperArt(int screenHeight) { this.screenHeight = screenHeight; }

        public Texture2D TextureFor(PaperKind kind, Color tint, bool silhouette = false, int mark = 0)
        {
            // Selection backings only need the alpha mask, independent of item tint.
            if (silhouette) tint = Color.white;
            for (int i = 0; i < textures.Count; i++)
            {
                Entry entry = textures[i];
                if (entry.Kind == kind && entry.Tint == tint && entry.Silhouette == silhouette && entry.Mark == mark)
                    return entry.Texture;
            }

            int size = PaperRasterizer.TextureSize(kind, screenHeight);
            long key = ((long)(int)kind << 32) | (uint)size;
            if (!coverage.TryGetValue(key, out Coverage shape))
            {
                shape = new Coverage();
                shape.Mask = PaperRasterizer.RasterizeCoverage(kind, size, out shape.Alpha);
                coverage[key] = shape;
            }
            var texture = Bake(kind, tint, size, silhouette, mark, 0, shape.Mask, shape.Alpha);
            textures.Add(new Entry { Kind = kind, Tint = tint, Silhouette = silhouette, Mark = mark, Texture = texture });
            return texture;
        }

        /// <summary>
        /// Generated paper art at a fixed size. The identity badge (mark) and the side-variant
        /// pips tell items apart when palette colours collide.
        /// </summary>
        public static Texture2D Bake(PaperKind kind, Color tint, int size, bool silhouette, int mark, int variant)
            => Bake(kind, tint, size, silhouette, mark, variant, null, null);

        private static Texture2D Bake(PaperKind kind, Color tint, int size, bool silhouette, int mark, int variant,
            byte[] mask, byte[] alpha)
        {
            if (mask == null) mask = PaperRasterizer.RasterizeCoverage(kind, size, out alpha);
            var pixels = new Color32[mask.Length];
            Color paper = Color.Lerp(Parchment, tint, 0.5f + 0.08f * Mathf.Clamp(variant, 0, 3));
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int index = y * size + x;
                    float px = (x + 0.5f) * 100 / size, py = (y + 0.5f) * 100 / size;
                    Color colour;
                    if (silhouette) colour = Color.white;
                    else if (mask[index] == 1 && !StageArt.Mark(mark, px, py) && !Pip(variant, px, py)) colour = paper * PaperRasterizer.Grain(x, y);
                    else colour = Ink;
                    colour.a = alpha[index] / 255f;
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
            return texture;
        }

        // One ink pip per side-variant step, low on the body.
        private static bool Pip(int variant, float x, float y)
        {
            for (int i = 0; i < variant; i++)
                if (x >= 37 + i * 9 && x <= 42 + i * 9 && y >= 24 && y <= 30) return true;
            return false;
        }

        // ItemTint's palette, from Assets/UI/Styles/Tokens.uss (--pal-tile-0 .. -7).
        public static readonly Color32[] Tints =
        {
            new Color32(58, 110, 108, 255), new Color32(160, 82, 45, 255),
            new Color32(72, 82, 140, 255), new Color32(104, 112, 52, 255),
            new Color32(112, 64, 112, 255), new Color32(78, 92, 104, 255),
            new Color32(158, 122, 40, 255), new Color32(140, 62, 62, 255)
        };

        public void Dispose()
        {
            for (int i = 0; i < textures.Count; i++) Object.Destroy(textures[i].Texture);
            textures.Clear();
            coverage.Clear();
        }
    }
}
