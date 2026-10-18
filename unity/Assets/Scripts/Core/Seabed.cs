using System;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Шум как в js/util.js (G.noise, G.fbm): тот же хеш на целочисленной решётке, те же числа.
    /// Считается в double, чтобы совпадать с браузерной версией до знаков после запятой.
    /// </summary>
    public static class Noise
    {
        static double Hash(int ix, int iz)
        {
            unchecked
            {
                uint h = (uint)(ix * 374761393 + iz * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return h / 4294967295.0;
            }
        }

        /// <summary>Гладкий шум 0..1 (G.noise).</summary>
        public static double Value(double x, double z)
        {
            double fx0 = Math.Floor(x), fz0 = Math.Floor(z);
            int ix = (int)fx0, iz = (int)fz0;
            double fx = x - fx0, fz = z - fz0;
            double ux = fx * fx * (3 - 2 * fx), uz = fz * fz * (3 - 2 * fz);
            double a = Hash(ix, iz), b = Hash(ix + 1, iz), c = Hash(ix, iz + 1), d = Hash(ix + 1, iz + 1);
            return a + (b - a) * ux + (c - a) * uz + (a - b - c + d) * ux * uz;
        }

        /// <summary>Сумма октав (G.fbm).</summary>
        public static double Fbm(double x, double z, int oct)
        {
            double s = 0, amp = 0.5, f = 1, n = 0;
            for (int i = 0; i < oct; i++)
            {
                s += amp * Value(x * f + i * 17.3, z * f - i * 9.1);
                n += amp; amp *= 0.5; f *= 2;
            }
            return s / n;
        }

        public static double Smooth(double e0, double e1, double x)
        {
            double t = (x - e0) / (e1 - e0);
            t = t < 0 ? 0 : t > 1 ? 1 : t;
            return t * t * (3 - 2 * t);
        }
    }

    /// <summary>
    /// Отмель «Подлёдный сад»: рельеф как W.height в js/world.js — плато вокруг рифа, гряды,
    /// склон в глубину у правого борта (в браузере восток +X, в Unity −X) и скальные стены по краям карты.
    /// Координаты Unity: x из браузерной версии меняет знак. Лёд — на y = 0.
    /// </summary>
    public static class Seabed
    {
        public const float Size = 700f;   // сторона карты, м
        public const int Segments = 180;  // клеток на сторону: 181×181 вершин, 64 800 треугольников
        public const float Top = -7f;     // выше дно не поднимается
        public const float IceY = 0f;

        /// <summary>Высота дна в браузерных координатах (W.height).</summary>
        public static double HeightJs(double x, double z)
        {
            double n = Noise.Fbm(x * 0.006, z * 0.006, 4);
            double h = -64 + (n - 0.5) * 34;
            double r = Math.Sqrt((x + 10) * (x + 10) + (z + 10) * (z + 10));
            double plat = Noise.Smooth(250, 110, r);
            h += plat * 25;
            double rn = Noise.Value(x * 0.028 + 3.1, z * 0.028 - 1.7);
            double ridge = 1 - Math.Abs(rn * 2 - 1);
            h += ridge * ridge * ridge * 9 * plat;
            h -= Noise.Smooth(150, 240, x) * 55;
            double e = Math.Max(Math.Abs(x), Math.Abs(z));
            h += Noise.Smooth(285, 345, e) * 80;
            h += (Noise.Value(x * 0.13, z * 0.13) - 0.5) * 1.4;
            return Math.Min(h, Top);
        }

        /// <summary>Высота дна под точкой Unity (x, z).</summary>
        public static float Height(float x, float z) => (float)HeightJs(-x, z);

        /// <summary>
        /// Цвет дна как в js/world.js: песок, на крутых склонах скала, пятнами мох, в глубине темнеет.
        /// ny — y нормали (1 — ровно). Возвращает r, g, b в 0..1.
        /// </summary>
        public static void ColorAt(float x, float y, float z, float ny, out float r, out float g, out float b)
        {
            double jx = -x; // шум цвета — в браузерных координатах
            double slope = 1 - ny;
            double k = slope * 3.2; k = k < 0 ? 0 : k > 1 ? 1 : k;
            double cr = Lerp(0.80, 0.42, k), cg = Lerp(0.74, 0.28, k), cb = Lerp(0.60, 0.48, k);
            double m = Noise.Smooth(0.55, 0.8, Noise.Value(jx * 0.03 + 7, z * 0.03)) * 0.7;
            cr = Lerp(cr, 0.20, m); cg = Lerp(cg, 0.48, m); cb = Lerp(cb, 0.44, m);
            double d = Noise.Smooth(-55, -110, y);
            cr = Lerp(cr, 0.08, d); cg = Lerp(cg, 0.13, d); cb = Lerp(cb, 0.22, d);
            double v = 0.82 + Noise.Value(jx * 0.4, z * 0.4) * 0.3;
            r = (float)(cr * v); g = (float)(cg * v); b = (float)(cb * v);
        }

        static double Lerp(double a, double b, double t) => a + (b - a) * t;

        /// <summary>
        /// Сетка дна: Segments × Segments клеток со стороной Size с центром в (0, 0).
        /// positions — x, y, z подряд; normals — так же; colors — r, g, b подряд; triangles смотрят вверх.
        /// </summary>
        public static void BuildGrid(out float[] positions, out float[] normals, out float[] colors, out int[] triangles,
            int segments = Segments, float size = Size)
        {
            int n = segments + 1;
            float step = size / segments, half = size / 2f;
            var hs = new float[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                    hs[j * n + i] = Height(-half + i * step, -half + j * step);

            positions = new float[n * n * 3];
            normals = new float[n * n * 3];
            colors = new float[n * n * 3];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    int v = j * n + i;
                    float x = -half + i * step, z = -half + j * step, y = hs[v];
                    // нормаль по соседям: разность высот вдоль x и z
                    float hl = hs[j * n + Math.Max(i - 1, 0)], hr = hs[j * n + Math.Min(i + 1, n - 1)];
                    float hd = hs[Math.Max(j - 1, 0) * n + i], hu = hs[Math.Min(j + 1, n - 1) * n + i];
                    float dx = (hr - hl) / ((Math.Min(i + 1, n - 1) - Math.Max(i - 1, 0)) * step);
                    float dz = (hu - hd) / ((Math.Min(j + 1, n - 1) - Math.Max(j - 1, 0)) * step);
                    float len = (float)Math.Sqrt(dx * dx + 1 + dz * dz);
                    float nx = -dx / len, ny = 1 / len, nz = -dz / len;
                    positions[v * 3] = x; positions[v * 3 + 1] = y; positions[v * 3 + 2] = z;
                    normals[v * 3] = nx; normals[v * 3 + 1] = ny; normals[v * 3 + 2] = nz;
                    ColorAt(x, y, z, ny, out colors[v * 3], out colors[v * 3 + 1], out colors[v * 3 + 2]);
                }

            triangles = new int[segments * segments * 6];
            int t = 0;
            for (int j = 0; j < segments; j++)
                for (int i = 0; i < segments; i++)
                {
                    int v = j * n + i;
                    // обход по часовой стрелке, если смотреть сверху: лицевая сторона вверх (Unity)
                    triangles[t++] = v; triangles[t++] = v + n; triangles[t++] = v + 1;
                    triangles[t++] = v + 1; triangles[t++] = v + n; triangles[t++] = v + n + 1;
                }
        }
    }

    /// <summary>
    /// Вода по глубине, как updateEnv в js/game.js: у льда бирюзовая и светлая, к 95 м тёмная;
    /// туман гуще с глубиной, рассеянный свет и свет сквозь лёд слабеют.
    /// </summary>
    public static class Water
    {
        public static readonly float[] Shallow = { 0x1a / 255f, 0x66 / 255f, 0x76 / 255f }; // 0x1a6676
        public static readonly float[] Deep = { 0x04 / 255f, 0x1a / 255f, 0x22 / 255f };    // 0x041a22

        /// <summary>0 у льда (до 8 м), 1 на глубине 95 м и ниже.</summary>
        public static float K(float depth) => (float)Noise.Smooth(8, 95, depth);

        /// <summary>Плотность тумана (экспоненциальный квадрат, как FogExp2).</summary>
        public static float FogDensity(float depth) => 0.015f + 0.01f * K(depth);

        /// <summary>Сила рассеянного света сверху (полусфера).</summary>
        public static float Ambient(float depth) => 0.95f - 0.55f * K(depth);

        /// <summary>Сила света сквозь лёд (направленный).</summary>
        public static float Sun(float depth) => 0.55f - 0.4f * K(depth);

        public static void FogColor(float depth, out float r, out float g, out float b)
        {
            float k = K(depth);
            r = Shallow[0] + (Deep[0] - Shallow[0]) * k;
            g = Shallow[1] + (Deep[1] - Shallow[1]) * k;
            b = Shallow[2] + (Deep[2] - Shallow[2]) * k;
        }
    }
}
