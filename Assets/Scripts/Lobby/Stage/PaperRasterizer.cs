using System;

namespace NodeWar.Lobby
{
    public enum PaperKind { House, Tower, Workshop, Villager, Marketman, Stall, Canopy }

    /// <summary>Bottom-up masks: 0 transparent, 1 parchment, 2 ink. No Unity dependency.</summary>
    public static class PaperRasterizer
    {
        public static byte[] Rasterize(PaperKind kind, int width, int height, int stroke = 3)
        {
            if (width < 16 || height < 16) throw new ArgumentOutOfRangeException(nameof(width));
            var mask = new byte[width * height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    // Stable brush wobble, smaller than the stroke.
                    float px = (x + 0.5f) * 100f / width;
                    float py = (y + 0.5f) * 100f / height;
                    float wobble = (float)Math.Sin(py * 0.43f + (int)kind) * 0.5f;
                    if (Contains(kind, px + wobble, py)) mask[y * width + x] = 1;
                }
            byte[] result = Outline(mask, width, height, stroke);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (result[y * width + x] == 1 &&
                        Detail(kind, (x + 0.5f) * 100f / width, (y + 0.5f) * 100f / height))
                        result[y * width + x] = 2;
            return result;
        }

        /// <summary>An inward circular ink stroke; outside pixels stay transparent.</summary>
        public static byte[] Outline(byte[] mask, int width, int height, int radius)
        {
            if (mask == null || width <= 0 || height <= 0 || mask.Length != width * height || radius < 0)
                throw new ArgumentException("Mask dimensions and stroke must be valid.");
            var result = new byte[mask.Length];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (mask[y * width + x] == 0) continue;
                    bool edge = false;
                    for (int dy = -radius; dy <= radius && !edge; dy++)
                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            if (dx * dx + dy * dy > radius * radius) continue;
                            int sx = x + dx, sy = y + dy;
                            if (sx < 0 || sy < 0 || sx >= width || sy >= height || mask[sy * width + sx] == 0)
                            { edge = true; break; }
                        }
                    result[y * width + x] = edge ? (byte)2 : (byte)1;
                }
            return result;
        }

        // Subtle, stable paper fibre. Hashing never touches a global RNG.
        public static float Grain(int x, int y)
        {
            unchecked
            {
                uint hash = (uint)(x * 73856093 ^ y * 19349663);
                hash ^= hash >> 13;
                return 0.985f + (hash % 31) * 0.001f;
            }
        }

        public static bool Contains(PaperKind kind, float x, float y)
        {
            switch (kind)
            {
                case PaperKind.House:
                    return Rect(x, y, 17, 8, 83, 17) || Rect(x, y, 23, 14, 77, 63) ||
                        Triangle(x, y, 12, 60, 50, 93, 88, 60);
                case PaperKind.Tower:
                    return Rect(x, y, 19, 8, 81, 17) || Rect(x, y, 29, 14, 71, 82) ||
                        Rect(x, y, 24, 74, 76, 84) || Rect(x, y, 24, 82, 36, 93) ||
                        Rect(x, y, 44, 82, 56, 93) || Rect(x, y, 64, 82, 76, 93);
                case PaperKind.Workshop:
                    return Rect(x, y, 13, 8, 87, 17) || Rect(x, y, 20, 14, 80, 56) ||
                        Triangle(x, y, 12, 53, 43, 78, 88, 53) || Rect(x, y, 65, 63, 76, 91);
                case PaperKind.Villager:
                case PaperKind.Marketman:
                    return Ellipse(x, y, 50, 72, 20, 20) ||
                        Triangle(x, y, 24, 22, 50, 64, 76, 22) ||
                        Rect(x, y, 32, 8, 46, 26) || Rect(x, y, 54, 8, 68, 26) ||
                        Ellipse(x, y, 27, 37, 9, 12) || Ellipse(x, y, 73, 37, 9, 12) ||
                        (kind == PaperKind.Marketman && (Rect(x, y, 20, 83, 80, 88) ||
                            Rect(x, y, 34, 86, 66, 96)));
                case PaperKind.Stall:
                    return Rect(x, y, 10, 8, 90, 44) || Rect(x, y, 7, 40, 93, 49) ||
                        Rect(x, y, 13, 44, 18, 92) || Rect(x, y, 82, 44, 87, 92);
                case PaperKind.Canopy:
                    return Triangle(x, y, 6, 28, 24, 88, 94, 28) ||
                        Triangle(x, y, 24, 88, 76, 88, 94, 28) ||
                        Rect(x, y, 6, 20, 94, 30);
                default: return false;
            }
        }

        private static bool Detail(PaperKind kind, float x, float y)
        {
            switch (kind)
            {
                case PaperKind.House:
                case PaperKind.Tower:
                case PaperKind.Workshop:
                    return Rect(x, y, 45, 14, 57, 38) || Rect(x, y, 29, 43, 39, 53) ||
                        Rect(x, y, 61, 43, 71, 53) || (Math.Abs(y - 19) < 1 && x > 20 && x < 80);
                case PaperKind.Villager:
                case PaperKind.Marketman:
                    return Ellipse(x, y, 43, 73, 2, 3) || Ellipse(x, y, 57, 73, 2, 3) ||
                        Rect(x, y, 44, 62, 56, 64) || Rect(x, y, 48, 28, 51, 49) ||
                        (kind == PaperKind.Marketman && Rect(x, y, 34, 30, 66, 32));
                case PaperKind.Stall:
                    return Math.Abs(y - 35) < 1 || (y < 34 && (Math.Abs(x - 30) < 1 || Math.Abs(x - 70) < 1));
                case PaperKind.Canopy:
                    return y > 27 && (Math.Abs(x - 31) < 1.5f || Math.Abs(x - 69) < 1.5f) ||
                        Math.Abs(y - 30) < 1;
                default: return false;
            }
        }

        private static bool Rect(float x, float y, float left, float bottom, float right, float top)
            => x >= left && x <= right && y >= bottom && y <= top;

        private static bool Ellipse(float x, float y, float cx, float cy, float rx, float ry)
            => (x - cx) * (x - cx) / (rx * rx) + (y - cy) * (y - cy) / (ry * ry) <= 1;

        private static bool Triangle(float x, float y, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float a = (x - bx) * (ay - by) - (ax - bx) * (y - by);
            float b = (x - cx) * (by - cy) - (bx - cx) * (y - cy);
            float c = (x - ax) * (cy - ay) - (cx - ax) * (y - ay);
            return !(a < 0 || b < 0 || c < 0) || !(a > 0 || b > 0 || c > 0);
        }
    }
}
