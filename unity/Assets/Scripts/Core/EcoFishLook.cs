using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Как выглядит светляк-полосник по концепту ART-011 (`docs/concept-art/svetlyak-ognevka-vidy.png`):
    /// тёмное веретено #344F63 с четырьмя поперечно-диагональными полосами живого света и плоский хвост
    /// с подсвеченными концами. Размер — как в браузере (тело 0,14 × 0,2 × 0,56 м, с хвостом 0,7 м):
    /// в концепте рыбка 0,25 м, но такую стаю не видно с корабля. Полосы — цвет стаи (голубая гамма,
    /// <see cref="EcoFish.Colors"/>) × своя яркость рыбки, в тревоге полосы горят ярче. Нос смотрит
    /// по скорости, рыбка виляет на ±0,25 рад. Съеденная стягивается в точку. Тело замкнуто и смотрит
    /// наружу, хвост — в обе стороны (как DoubleSide в браузере).
    /// </summary>
    public static class EcoFishLook
    {
        /// <summary>Во сколько раз ярче полосы стаи в тревоге.</summary>
        public const float AlarmGlow = 1.6f;
        /// <summary>Виляние: размах (рад) и частота (рад/с).</summary>
        public const float Wiggle = 0.25f, WiggleRate = 12f;
        /// <summary>Тело: полуширина, полувысота, нос и корма по z (м).</summary>
        public const float HalfWidth = 0.07f, HalfHeight = 0.1f, Nose = 0.28f, Back = -0.25f;
        /// <summary>Наклон полос: верх кольца сдвинут к носу, низ — к хвосту (м).</summary>
        public const float Slant = 0.015f;
        /// <summary>Цвет тела по ART-011.</summary>
        public const uint BodyColor = 0x344F63;
        /// <summary>Доля цвета полос на концах хвоста.</summary>
        public const float FinGlow = 0.6f;

        /// <summary>Чем окрашена вершина.</summary>
        public enum Paint { Body, Stripe, Fin }

        /// <summary>Кольца тела от носа к хвосту: z середины кольца и полоса ли это (четыре полосы).</summary>
        public static readonly float[] RingZ = { 0.22f, 0.16f, 0.10f, 0.04f, -0.02f, -0.08f, -0.14f, -0.19f, -0.23f };
        public static readonly bool[] RingStripe = { false, true, false, true, false, true, false, true, false };

        /// <summary>Вершины рыбки в её осях: x вправо, y вверх, z — нос.</summary>
        public static readonly EcoVec[] Shape;
        public static readonly Paint[] Paints;
        /// <summary>Треугольники одной рыбки: тело наружу, хвост в обе стороны.</summary>
        public static readonly int[] Tris;
        /// <summary>Вершина первого кольца-полосы (для тестов и сцены).</summary>
        public static readonly int StripeVertex;

        public static int VertsPerFish => Shape.Length;

        static EcoFishLook()
        {
            var v = new List<EcoVec>(); var paint = new List<Paint>(); var t = new List<int>();
            int Add(EcoVec p, Paint c) { v.Add(p); paint.Add(c); return v.Count - 1; }
            void Tri(int a, int b, int c)
            {
                // наружу от оси тела: cross(b − a, c − a) смотрит от точки (0, 0, z) середины треугольника
                var ab = v[b] - v[a]; var ac = v[c] - v[a];
                var n = new EcoVec(ab.Y * ac.Z - ab.Z * ac.Y, ab.Z * ac.X - ab.X * ac.Z, ab.X * ac.Y - ab.Y * ac.X);
                var m = (v[a] + v[b] + v[c]) * (1f / 3f);
                var o = new EcoVec(m.X, m.Y, m.Z * 0.5f);
                if (n.X * o.X + n.Y * o.Y + n.Z * o.Z < 0f) { int s = b; b = c; c = s; }
                t.Add(a); t.Add(b); t.Add(c);
            }

            int nose = Add(new EcoVec(0, 0, Nose), Paint.Body);
            int stripe = -1;
            var ring = new int[RingZ.Length];
            for (int r = 0; r < RingZ.Length; r++)
            {
                float z = RingZ[r];
                float l = z >= 0 ? Nose : -Back;
                float s = (float)Math.Sqrt(Math.Max(0.0, 1.0 - (z / l) * (z / l)));
                var c = RingStripe[r] ? Paint.Stripe : Paint.Body;
                ring[r] = Add(new EcoVec(HalfWidth * s, 0, z), c);
                Add(new EcoVec(0, HalfHeight * s, z + Slant), c);
                Add(new EcoVec(-HalfWidth * s, 0, z), c);
                Add(new EcoVec(0, -HalfHeight * s, z - Slant), c);
                if (stripe < 0 && RingStripe[r]) stripe = ring[r];
            }
            int back = Add(new EcoVec(0, 0, Back), Paint.Body);
            for (int j = 0; j < 4; j++)
            {
                int k = (j + 1) % 4;
                Tri(nose, ring[0] + j, ring[0] + k);
                int last = ring[RingZ.Length - 1];
                Tri(back, last + k, last + j);
            }
            for (int r = 0; r + 1 < RingZ.Length; r++)
                for (int j = 0; j < 4; j++)
                {
                    int k = (j + 1) % 4;
                    Tri(ring[r] + j, ring[r] + k, ring[r + 1] + k);
                    Tri(ring[r] + j, ring[r + 1] + k, ring[r + 1] + j);
                }
            int root = Add(new EcoVec(0, 0, -0.22f), Paint.Body);
            int up = Add(new EcoVec(0, 0.12f, -0.42f), Paint.Fin);
            int down = Add(new EcoVec(0, -0.12f, -0.42f), Paint.Fin);
            t.Add(root); t.Add(up); t.Add(down);
            t.Add(root); t.Add(down); t.Add(up);

            Shape = v.ToArray(); Paints = paint.ToArray(); Tris = t.ToArray(); StripeVertex = stripe;
        }

        /// <summary>Яркость полос: своя яркость рыбки, в тревоге стаи — ярче в <see cref="AlarmGlow"/> раз.</summary>
        public static float Brightness(EcoFish.Fish f) => f.Bright * (f.Shoal.Alarm > 0f ? AlarmGlow : 1f);

        /// <summary>Цвет полос рыбки: цвет стаи × <see cref="Brightness"/> (может быть больше 1 — горит ярко).</summary>
        public static void Stripe(EcoFish.Fish f, out float r, out float g, out float b)
        {
            uint c = f.Shoal.Color;
            float k = Brightness(f) / 255f;
            r = ((c >> 16) & 255) * k; g = ((c >> 8) & 255) * k; b = (c & 255) * k;
        }

        /// <summary>Цвет вершины рыбки по краске: тело тёмное всегда, полосы и концы хвоста — светом стаи.</summary>
        public static void VertexColor(EcoFish.Fish f, Paint p, out float r, out float g, out float b)
        {
            if (p == Paint.Body)
            {
                r = ((BodyColor >> 16) & 255) / 255f; g = ((BodyColor >> 8) & 255) / 255f; b = (BodyColor & 255) / 255f;
                return;
            }
            Stripe(f, out r, out g, out b);
            if (p == Paint.Fin) { r *= FinGlow; g *= FinGlow; b *= FinGlow; }
        }

        /// <summary>Куда смотрит нос: по скорости (вверх-вниз тоже), с вилянием вокруг вертикали.</summary>
        public static EcoVec Forward(EcoFish.Fish f, float t)
        {
            var v = f.Agent.V;
            var dir = v.LengthSq > 1e-8f ? v * (1f / v.Length) : new EcoVec(0, 0, 1);
            float w = (float)Math.Sin(t * WiggleRate + f.Phase) * Wiggle;
            float cw = (float)Math.Cos(w), sw = (float)Math.Sin(w);
            return new EcoVec(dir.X * cw + dir.Z * sw, dir.Y, -dir.X * sw + dir.Z * cw);
        }

        /// <summary>
        /// Записать вершины рыбки в мире в dst начиная с at (<see cref="VertsPerFish"/> штук).
        /// Съеденная рыбка — все вершины в одной точке, треугольники пустые.
        /// </summary>
        public static void Place(EcoFish.Fish f, float t, EcoVec[] dst, int at)
        {
            var p = f.Agent.P;
            if (!f.Agent.Alive)
            {
                for (int i = 0; i < Shape.Length; i++) dst[at + i] = p;
                return;
            }
            var fw = Forward(f, t);
            // вправо = up × вперёд; рыбка, идущая отвесно, берёт ось x
            var right = new EcoVec(fw.Z, 0, -fw.X);
            float rl = right.Length;
            right = rl > 1e-4f ? right * (1f / rl) : new EcoVec(1, 0, 0);
            // вверх = вперёд × вправо
            var up = new EcoVec(fw.Y * right.Z - fw.Z * right.Y,
                                fw.Z * right.X - fw.X * right.Z,
                                fw.X * right.Y - fw.Y * right.X);
            for (int i = 0; i < Shape.Length; i++)
            {
                var s = Shape[i];
                dst[at + i] = p + right * s.X + up * s.Y + fw * s.Z;
            }
        }
    }
}
