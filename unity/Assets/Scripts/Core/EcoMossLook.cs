using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Как выглядит светящийся мох (пятна мха в js/world.js): каждое пятно — сетка 7 × 7 м из 8 × 8
    /// клеток, лежит по дну на 0,14 м выше него. Своя яркость вершины <see cref="Mesh.Base"/>: круг
    /// радиусом 3,4 м с мягким краем (полная до 1 м) и пятнистость по шуму, как в браузере. Яркость
    /// пятна целиком — <see cref="EcoMoss.Patch.Glow"/>: объеденное тускнеет до 0,15, отрастая —
    /// разгорается. Сцена складывает все 22 пятна в один меш со светом, который прибавляется к дну.
    /// </summary>
    public static class EcoMossLook
    {
        /// <summary>Клеток по стороне пятна; вершин по стороне — на одну больше.</summary>
        public const int Cells = 8;
        public const int Side = Cells + 1;
        public const int VertsPerPatch = Side * Side;
        public const int TrisPerPatch = Cells * Cells * 2;
        /// <summary>Над дном (м).</summary>
        public const float Above = 0.14f;
        /// <summary>Цвет мха: цвет вершин (0,45; 1; 0,75) × цвет материала 0x9dffd0, как в браузере.</summary>
        public static readonly float[] Color = { 0.45f * 0x9d / 255f, 1f, 0.75f * 0xd0 / 255f };

        /// <summary>Меш всех пятен: вершины, своя яркость каждой вершины, треугольники (смотрят вверх).</summary>
        public sealed class Mesh
        {
            public EcoVec[] Verts;
            public float[] Base;
            public int[] Tris;
        }

        /// <summary>Своя яркость вершины пятна в локальной точке (lx, lz) осей Unity; px, pz — середина пятна.</summary>
        public static float BaseAt(float lx, float lz, float px, float pz)
        {
            float r = (float)Math.Sqrt(lx * lx + lz * lz);
            // в браузере x со знаком минус: G.noise(lx·1,3 + p.x, lz·1,3)
            double n = Noise.Value(-lx * 1.3 - px, lz * 1.3);
            return (float)(Noise.Smooth(EcoMoss.Radius, 1.0, r) * (0.55 + n * 0.6));
        }

        /// <summary>Собрать меш всех пятен мха по дну ground (null — пятна плоские на высоте пятна).</summary>
        public static Mesh Build(IReadOnlyList<EcoMoss.Patch> patches, Func<float, float, float> ground)
        {
            int n = patches.Count;
            var m = new Mesh
            {
                Verts = new EcoVec[n * VertsPerPatch],
                Base = new float[n * VertsPerPatch],
                Tris = new int[n * TrisPerPatch * 3],
            };
            float half = EcoMoss.Size / 2f, step = EcoMoss.Size / Cells;
            int t = 0;
            for (int p = 0; p < n; p++)
            {
                var c = patches[p].Pos;
                int v0 = p * VertsPerPatch;
                for (int iz = 0; iz < Side; iz++)
                    for (int ix = 0; ix < Side; ix++)
                    {
                        float lx = -half + ix * step, lz = -half + iz * step;
                        float x = c.X + lx, z = c.Z + lz;
                        float y = (ground != null ? ground(x, z) : c.Y) + Above;
                        int v = v0 + iz * Side + ix;
                        m.Verts[v] = new EcoVec(x, y, z);
                        m.Base[v] = BaseAt(lx, lz, c.X, c.Z);
                    }
                for (int iz = 0; iz < Cells; iz++)
                    for (int ix = 0; ix < Cells; ix++)
                    {
                        int a = v0 + iz * Side + ix, b = a + 1, d = a + Side, e = d + 1;
                        // лицом вверх в Unity: cross(b − a, c − a) смотрит в +y
                        m.Tris[t++] = a; m.Tris[t++] = d; m.Tris[t++] = b;
                        m.Tris[t++] = b; m.Tris[t++] = d; m.Tris[t++] = e;
                    }
            }
            return m;
        }

        /// <summary>Яркость вершины v сейчас: своя × яркость пятна.</summary>
        public static float Brightness(Mesh m, IReadOnlyList<EcoMoss.Patch> patches, int v) =>
            m.Base[v] * patches[v / VertsPerPatch].Glow;
    }
}
