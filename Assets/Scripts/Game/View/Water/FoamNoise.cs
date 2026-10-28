using System;

namespace NodeWar.View
{
    /// <summary>Periodic view-only Voronoi foam and gradient noise.</summary>
    public static class FoamNoise
    {
        /// <summary>R is unthresholded F2-F1 edges, G/B are decorrelated fBm, A is opaque.</summary>
        public static byte[] BakeVoronoi(int size, int seed, int cells, double warp)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            if (cells < 2) throw new ArgumentOutOfRangeException(nameof(cells));
            if (double.IsNaN(warp) || double.IsInfinity(warp) || warp < 0)
                throw new ArgumentOutOfRangeException(nameof(warp));
            var rgba = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    double u = (double)x / size, v = (double)y / size;
                    int at = (y * size + x) * 4;
                    rgba[at] = ToByte(Voronoi(u, v, seed, cells, warp));
                    rgba[at + 1] = ToByte((Fbm(u, v, seed + 101, 4, 3) - 0.5) * 2.5 + 0.5);
                    rgba[at + 2] = ToByte((Fbm(u, v, seed + 211, 4, 3) - 0.5) * 2.5 + 0.5);
                    rgba[at + 3] = 255;
                }
            return rgba;
        }

        public static double Voronoi(double u, double v, int seed, int cells, double warp)
        {
            double x = u * cells + (Fbm(u, v, seed + 7, 4, 2) - 0.5) * warp;
            double y = v * cells + (Fbm(u, v, seed + 13, 4, 2) - 0.5) * warp;
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            double first = double.PositiveInfinity, second = double.PositiveInfinity;
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int gx = ix + dx, gy = iy + dy;
                    int wx = (gx % cells + cells) % cells, wy = (gy % cells + cells) % cells;
                    double px = gx + 0.15 + Jitter(wx, wy, seed) * 0.7;
                    double py = gy + 0.15 + Jitter(wx, wy, seed + 37) * 0.7;
                    double d = (px - x) * (px - x) + (py - y) * (py - y);
                    if (d < first) { second = first; first = d; }
                    else if (d < second) second = d;
                }
            return Clamp01(Math.Sqrt(second) - Math.Sqrt(first));
        }

        private static double Jitter(int x, int y, int seed)
        {
            uint h = unchecked((uint)(x * 73856093) ^ (uint)(y * 19349663) ^ (uint)(seed * 83492791));
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h & 0xffff) / 65535.0;
        }

        // Derivatives are dh/du and dh/dv, packed as 0.5 + derivative / 32.
        public const double DerivativeRange = 16;
        public static byte[] BakeSwell(int size, int seed)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            var rgba = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    rgba[(y * size + x) * 4] = ToByte(Fbm((double)x / size, (double)y / size, seed, 2, 3));
            double Height(int x, int y) => rgba[(((y + size) % size) * size + (x + size) % size) * 4] / 255.0;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int at = (y * size + x) * 4;
                    rgba[at + 1] = ToByte(0.5 + (Height(x + 1, y) - Height(x - 1, y)) * size / (4 * DerivativeRange));
                    rgba[at + 2] = ToByte(0.5 + (Height(x, y + 1) - Height(x, y - 1)) * size / (4 * DerivativeRange));
                    rgba[at + 3] = 255;
                }
            return rgba;
        }

        /// <summary>Fbm sampled at a position pushed around by two other fBm fields, for organic shapes.</summary>
        public static double Warped(double u, double v, int seed, int period)
        {
            double wu = Fbm(u, v, seed + 7, period, 2) - 0.5;
            double wv = Fbm(u, v, seed + 13, period, 2) - 0.5;
            return Fbm(u + wu * 0.35, v + wv * 0.35, seed, period, 3);
        }

        /// <summary>Tileable fBm in 0..1; <paramref name="period"/> lattice cells per tile at the base octave.</summary>
        public static double Fbm(double u, double v, int seed, int period, int octaves)
        {
            double sum = 0, amplitude = 1, total = 0;
            for (int o = 0; o < octaves; o++)
            {
                int p = period << o;
                sum += Gradient(u * p, v * p, p, seed + o * 31) * amplitude;
                total += amplitude;
                amplitude *= 0.5;
            }
            // Gradient noise is about -0.7..0.7; centre and stretch to 0..1.
            return Clamp01(sum / total * 0.75 + 0.5);
        }

        private static double Gradient(double x, double y, int period, int seed)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            double fx = x - x0, fy = y - y0;
            double n00 = Dot(x0, y0, fx, fy, period, seed);
            double n10 = Dot(x0 + 1, y0, fx - 1, fy, period, seed);
            double n01 = Dot(x0, y0 + 1, fx, fy - 1, period, seed);
            double n11 = Dot(x0 + 1, y0 + 1, fx - 1, fy - 1, period, seed);
            double sx = Fade(fx), sy = Fade(fy);
            return Lerp(Lerp(n00, n10, sx), Lerp(n01, n11, sx), sy);
        }

        private static double Dot(int ix, int iy, double dx, double dy, int period, int seed)
        {
            // Wrapping the lattice makes every octave, and so the texture, tile seamlessly.
            ix = ((ix % period) + period) % period;
            iy = ((iy % period) + period) % period;
            uint h = (uint)(ix * 73856093) ^ (uint)(iy * 19349663) ^ (uint)(seed * 83492791);
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            double angle = (h & 0xffff) / 65536.0 * Math.PI * 2;
            return Math.Cos(angle) * dx + Math.Sin(angle) * dy;
        }

        private static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
        private static byte ToByte(double v) => (byte)Math.Round(Clamp01(v) * 255);
    }
}
