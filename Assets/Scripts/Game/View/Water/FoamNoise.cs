using System;

namespace NodeWar.View
{
    /// <summary>
    /// Tileable, smooth foam noise (domain-warped gradient fBm). Thresholding it gives soft blobs and
    /// streaks rather than the regular dots a sine pattern produces. Pure maths so it can be tested.
    /// </summary>
    public static class FoamNoise
    {
        /// <summary>RGBA bytes: R surface foam, G shore breakup, B fine noise, A 255.</summary>
        public static byte[] Bake(int size, int seed)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            var rgba = new byte[size * size * 4];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    double u = (double)x / size, v = (double)y / size;
                    int at = (y * size + x) * 4;
                    rgba[at] = ToByte(Warped(u, v, seed, 4));
                    rgba[at + 1] = ToByte(Warped(u, v, seed + 101, 6));
                    rgba[at + 2] = ToByte(Fbm(u, v, seed + 211, 16, 2));
                    rgba[at + 3] = 255;
                }
            return rgba;
        }

        /// <summary>RGBA tangent normals for UnpackNormal (RGB or RG/AG); alpha stays one.</summary>
        public static byte[] BakeNormals(int size, int seed)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            var rgba = new byte[size * size * 4];
            double delta = 1.0 / size;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    double u = x * delta, v = y * delta;
                    double nx = -(Fbm(u + delta, v, seed, 4, 2) - Fbm(u - delta, v, seed, 4, 2)) / (2 * delta) * 0.15;
                    double ny = -(Fbm(u, v + delta, seed, 4, 2) - Fbm(u, v - delta, seed, 4, 2)) / (2 * delta) * 0.15;
                    double inv = 1 / Math.Sqrt(nx * nx + ny * ny + 1);
                    int at = (y * size + x) * 4;
                    rgba[at] = ToByte(nx * inv * 0.5 + 0.5);
                    rgba[at + 1] = ToByte(ny * inv * 0.5 + 0.5);
                    rgba[at + 2] = ToByte(inv * 0.5 + 0.5);
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
