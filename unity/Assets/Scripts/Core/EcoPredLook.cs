using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Как выглядит серпохвост (initPreds/predRender в js/creatures.js): голова-эллипсоид 1,1 × 1 × 2,6 м
    /// с тёмной челюстью-конусом и двумя красными глазами, за ней цепочка из десяти сегментов (тоньше к
    /// хвосту, по два оранжевых огня по бокам каждого) и хвост-серп — вертикальная дуга за последним
    /// сегментом. Сегменты тянутся за головой (<see cref="Follow"/>: звено не длиннее 0,62 м) и
    /// извиваются волной, тем чаще, чем быстрее хищник. Два меша: тело (освещается сценой) и огни
    /// (глаза и боковые огни светятся сами). Все части выпуклые и замкнутые, треугольники смотрят наружу.
    /// </summary>
    public static class EcoPredLook
    {
        /// <summary>Точек цепочки: голова и десять сегментов (PSEG в браузере).</summary>
        public const int Segs = 11;
        /// <summary>Наибольшая длина звена (м) и шаг цепочки при появлении (м).</summary>
        public const float Link = 0.62f, Spacing = 0.6f;
        /// <summary>Волна тела: размах на звено (м), сдвиг фазы на звено (рад), частота в покое и прибавка за 1 м/с.</summary>
        public const float WaveAmp = 0.08f, WavePhase = 0.75f, WaveRate = 3f, WaveRatePerSpeed = 0.6f;

        /// <summary>Краски как в браузере (0xRRGGBB): голова, челюсть, сегменты, хвост, глаза, боковые огни.</summary>
        public const uint Head = 0x3A5272, Jaw = 0x1D2838, Seg = 0x31445E, Tail = 0x5A7394, Eye = 0xFF3B4E, Dot = 0xFF9A3C;

        /// <summary>Рамки, в которых стоят вершины: 0 — голова, 1–10 — сегменты, 11 — хвост.</summary>
        public const int HeadFrame = 0, TailFrame = Segs, Frames = Segs + 1;
        /// <summary>Части тела: 0 голова, 1 челюсть, 2–11 сегменты, 12 хвост. Огни: 0–1 глаза, дальше по два на сегмент.</summary>
        public const int HeadPart = 0, JawPart = 1, TailPart = Segs + 1;

        /// <summary>Тело в осях своих рамок: x вправо, y вверх, z вперёд.</summary>
        public static readonly EcoVec[] BodyRest;
        public static readonly uint[] BodyPaint;
        public static readonly int[] BodyFrame, BodyPart, BodyTris;
        /// <summary>Огни (глаза и боковые) в осях своих рамок.</summary>
        public static readonly EcoVec[] LightRest;
        public static readonly uint[] LightPaint;
        public static readonly int[] LightFrame, LightPart, LightTris;

        public static int BodyVerts => BodyRest.Length;
        public static int LightVerts => LightRest.Length;

        /// <summary>Радиус сегмента k (1–10): 0,55 у головы, к хвосту тоньше.</summary>
        public static float SegRadius(int k) => 0.55f * (1f - (k - 1) / (float)Segs * 0.8f);

        /// <summary>Цепочка серпохвоста: где сейчас голова и сегменты (у каждого хищника своя).</summary>
        public sealed class Chain
        {
            public readonly EcoVec[] P = new EcoVec[Segs];
            /// <summary>Расставлена ли цепочка (первый раз — прямо за головой).</summary>
            public bool Ready;
        }

        sealed class Builder
        {
            public readonly List<EcoVec> V = new List<EcoVec>();
            public readonly List<uint> Paint = new List<uint>();
            public readonly List<int> Frame = new List<int>(), Part = new List<int>(), Tris = new List<int>();

            public int Add(EcoVec p, uint c, int frame, int part)
            {
                V.Add(p); Paint.Add(c); Frame.Add(frame); Part.Add(part);
                return V.Count - 1;
            }

            /// <summary>Треугольник наружу от середины выпуклой части: cross(b − a, c − a) смотрит от center.</summary>
            public void Tri(int a, int b, int c, EcoVec center)
            {
                var n = Cross(V[b] - V[a], V[c] - V[a]);
                var mid = (V[a] + V[b] + V[c]) * (1f / 3f) - center;
                if (n.X * mid.X + n.Y * mid.Y + n.Z * mid.Z < 0f) { int t = b; b = c; c = t; }
                Tris.Add(a); Tris.Add(b); Tris.Add(c);
            }

            public void Quad(int a, int b, int c, int d, EcoVec center) { Tri(a, b, c, center); Tri(a, c, d, center); }

            /// <summary>Эллипсоид: rings колец по around точек и два полюса (по y).</summary>
            public void Ellipsoid(EcoVec c, EcoVec ax, int around, int rings, uint color, int frame, int part)
            {
                int first = V.Count;
                for (int r = 1; r <= rings; r++)
                {
                    double phi = -Math.PI / 2 + Math.PI * r / (rings + 1);
                    for (int j = 0; j < around; j++)
                    {
                        double th = 2 * Math.PI * j / around;
                        Add(new EcoVec(c.X + ax.X * (float)(Math.Cos(phi) * Math.Sin(th)), c.Y + ax.Y * (float)Math.Sin(phi),
                                       c.Z + ax.Z * (float)(Math.Cos(phi) * Math.Cos(th))), color, frame, part);
                    }
                }
                int bottom = Add(new EcoVec(c.X, c.Y - ax.Y, c.Z), color, frame, part);
                int top = Add(new EcoVec(c.X, c.Y + ax.Y, c.Z), color, frame, part);
                for (int r = 0; r < rings - 1; r++)
                    for (int j = 0; j < around; j++)
                    {
                        int k = (j + 1) % around;
                        Quad(first + r * around + j, first + r * around + k, first + (r + 1) * around + k, first + (r + 1) * around + j, c);
                    }
                for (int j = 0; j < around; j++)
                {
                    int k = (j + 1) % around;
                    Tri(first + j, first + k, bottom, c);
                    Tri(first + (rings - 1) * around + j, first + (rings - 1) * around + k, top, c);
                }
            }

            /// <summary>Октаэдр — огонёк радиуса s.</summary>
            public void Octahedron(EcoVec c, float s, uint color, int frame, int part)
            {
                int b = V.Count;
                Add(c + new EcoVec(s, 0, 0), color, frame, part); Add(c + new EcoVec(-s, 0, 0), color, frame, part);
                Add(c + new EcoVec(0, s, 0), color, frame, part); Add(c + new EcoVec(0, -s, 0), color, frame, part);
                Add(c + new EcoVec(0, 0, s), color, frame, part); Add(c + new EcoVec(0, 0, -s), color, frame, part);
                for (int x = 0; x < 2; x++)
                    for (int y = 2; y < 4; y++)
                        for (int z = 4; z < 6; z++)
                            Tri(b + x, b + y, b + z, c);
            }
        }

        static EcoPredLook()
        {
            var body = new Builder();
            // голова и челюсть-конус (js: сфера 0,55 × 0,5 × 1,3; конус r 0,35, h 1,1 в (0, −0,22, 0,95) остриём вперёд)
            body.Ellipsoid(EcoVec.Zero, new EcoVec(0.55f, 0.5f, 1.3f), 10, 4, Head, HeadFrame, HeadPart);
            int jb = body.V.Count;
            for (int j = 0; j < 6; j++)
            {
                double a = 2 * Math.PI * j / 6;
                body.Add(new EcoVec(0.35f * (float)Math.Cos(a), -0.22f + 0.35f * (float)Math.Sin(a), 0.4f), Jaw, HeadFrame, JawPart);
            }
            int tip = body.Add(new EcoVec(0, -0.22f, 1.5f), Jaw, HeadFrame, JawPart);
            int jbase = body.Add(new EcoVec(0, -0.22f, 0.4f), Jaw, HeadFrame, JawPart);
            var jc = new EcoVec(0, -0.22f, 0.675f);
            for (int j = 0; j < 6; j++)
            {
                int k = (j + 1) % 6;
                body.Tri(jb + j, jb + k, tip, jc);
                body.Tri(jb + j, jb + k, jbase, jc);
            }
            // сегменты: эллипсоиды r·0,9 × r × r·1,5
            for (int k = 1; k < Segs; k++)
            {
                float r = SegRadius(k);
                body.Ellipsoid(EcoVec.Zero, new EcoVec(r * 0.9f, r, r * 1.5f), 8, 3, Seg, k, 1 + k);
            }
            // хвост-серп: дуга радиуса 1,1 на 0,95π в вертикальной плоскости за последним сегментом
            // (js: тор 1,1 / 0,08 в (0, 0, −0,9), поворот (0, π/2, −π/2)), лезвие шире к середине
            const int arc = 8;
            int tb = body.V.Count;
            var axis = new EcoVec[arc + 1];
            for (int i = 0; i <= arc; i++)
            {
                double a = 0.95 * Math.PI * i / arc;
                float ca = (float)Math.Cos(a), sa = (float)Math.Sin(a);
                var c = new EcoVec(0, -1.1f * ca, -0.9f - 1.1f * sa);
                var n = new EcoVec(0, -ca, -sa);
                float hn = 0.05f + 0.14f * (float)Math.Sin(Math.PI * i / arc), hx = 0.06f;
                axis[i] = c;
                for (int q = 0; q < 4; q++)
                {
                    float sn = q == 1 || q == 2 ? hn : -hn, sx = q >= 2 ? hx : -hx;
                    body.Add(new EcoVec(sx, c.Y + n.Y * sn, c.Z + n.Z * sn), Tail, TailFrame, TailPart);
                }
            }
            for (int i = 0; i < arc; i++)
            {
                var m = (axis[i] + axis[i + 1]) * 0.5f;
                for (int q = 0; q < 4; q++)
                {
                    int p = (q + 1) % 4;
                    body.Quad(tb + i * 4 + q, tb + i * 4 + p, tb + (i + 1) * 4 + p, tb + (i + 1) * 4 + q, m);
                }
            }
            body.Quad(tb, tb + 1, tb + 2, tb + 3, (axis[0] + axis[1]) * 0.5f);
            body.Quad(tb + arc * 4, tb + arc * 4 + 1, tb + arc * 4 + 2, tb + arc * 4 + 3, (axis[arc] + axis[arc - 1]) * 0.5f);

            BodyRest = body.V.ToArray(); BodyPaint = body.Paint.ToArray(); BodyFrame = body.Frame.ToArray();
            BodyPart = body.Part.ToArray(); BodyTris = body.Tris.ToArray();

            var lights = new Builder();
            lights.Octahedron(new EcoVec(-0.33f, 0.16f, 0.72f), 0.1f, Eye, HeadFrame, 0);
            lights.Octahedron(new EcoVec(0.33f, 0.16f, 0.72f), 0.1f, Eye, HeadFrame, 1);
            for (int k = 1; k < Segs; k++)
            {
                float r = SegRadius(k);
                lights.Octahedron(new EcoVec(-0.95f * r * 0.9f, 0.25f * r, 0), 0.08f, Dot, k, 2 + (k - 1) * 2);
                lights.Octahedron(new EcoVec(0.95f * r * 0.9f, 0.25f * r, 0), 0.08f, Dot, k, 3 + (k - 1) * 2);
            }
            LightRest = lights.V.ToArray(); LightPaint = lights.Paint.ToArray(); LightFrame = lights.Frame.ToArray();
            LightPart = lights.Part.ToArray(); LightTris = lights.Tris.ToArray();
        }

        public static EcoVec Cross(EcoVec a, EcoVec b) =>
            new EcoVec(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        /// <summary>Куда смотрит голова: по скорости, почти стоит — на +z (lookAt в браузере).</summary>
        public static EcoVec Forward(EcoPred.Pred pr)
        {
            var v = pr.Agent.V;
            return v.LengthSq > 0.01f ? v.Normalized : new EcoVec(0, 0, 1);
        }

        /// <summary>Вправо от направления f, горизонтально: (f.z, 0, −f.x); f отвесно — +x.</summary>
        public static EcoVec Right(EcoVec f)
        {
            var r = new EcoVec(f.Z, 0, -f.X);
            return r.LengthSq > 1e-6f ? r.Normalized : new EcoVec(1, 0, 0);
        }

        /// <summary>
        /// Подтянуть цепочку за головой: голова — в месте хищника, каждое звено не длиннее <see cref="Link"/>.
        /// Первый раз сегменты встают прямо за головой через <see cref="Spacing"/>.
        /// </summary>
        public static void Follow(Chain c, EcoPred.Pred pr)
        {
            var head = pr.Agent.P;
            if (!c.Ready)
            {
                var back = Forward(pr) * -Spacing;
                for (int k = 0; k < Segs; k++) c.P[k] = head + back * k;
                c.Ready = true;
                return;
            }
            c.P[0] = head;
            for (int k = 1; k < Segs; k++)
            {
                var d = c.P[k] - c.P[k - 1];
                float l = d.Length;
                if (l > Link) c.P[k] = c.P[k - 1] + d * (Link / l);
            }
        }

        /// <summary>Частота волны тела (рад/с): 3 + 0,6 × скорость.</summary>
        public static float WaveFreq(EcoPred.Pred pr) => WaveRate + pr.Agent.V.Length * WaveRatePerSpeed;

        /// <summary>Рамки частей в момент t: начало, вправо, вверх, вперёд (по <see cref="Frames"/> штук).</summary>
        public static void FramesAt(EcoPred.Pred pr, Chain c, float t, EcoVec[] origin, EcoVec[] right, EcoVec[] up, EcoVec[] fwd)
        {
            var f = Forward(pr);
            origin[HeadFrame] = c.P[0]; fwd[HeadFrame] = f;
            right[HeadFrame] = Right(f); up[HeadFrame] = Cross(f, right[HeadFrame]);
            float freq = WaveFreq(pr);
            for (int k = 1; k < Segs; k++)
            {
                var d = c.P[k - 1] - c.P[k];
                var tv = d.LengthSq > 1e-6f ? d.Normalized : new EcoVec(0, 0, 1);
                var side = Right(tv);
                float w = (float)Math.Sin(t * freq - k * WavePhase) * WaveAmp * k;
                origin[k] = c.P[k] + side * w;
                fwd[k] = tv; right[k] = side; up[k] = Cross(tv, side);
            }
            // хвост — у последнего сегмента, смотрит на предпоследнюю точку цепочки
            var last = origin[Segs - 1];
            var td = c.P[Segs - 2] - last;
            var tf = td.LengthSq > 1e-6f ? td.Normalized : fwd[Segs - 1];
            origin[TailFrame] = last; fwd[TailFrame] = tf;
            right[TailFrame] = Right(tf); up[TailFrame] = Cross(tf, right[TailFrame]);
        }

        static readonly EcoVec[] fo = new EcoVec[Frames], fr = new EcoVec[Frames], fu = new EcoVec[Frames], ff = new EcoVec[Frames];

        /// <summary>
        /// Записать вершины тела (<see cref="BodyVerts"/>) в body с bodyAt и огней (<see cref="LightVerts"/>)
        /// в lights с lightAt — по цепочке c в момент t. Цепочку перед этим подтягивает <see cref="Follow"/>.
        /// </summary>
        public static void Place(EcoPred.Pred pr, Chain c, float t, EcoVec[] body, int bodyAt, EcoVec[] lights, int lightAt)
        {
            FramesAt(pr, c, t, fo, fr, fu, ff);
            for (int i = 0; i < BodyRest.Length; i++)
            {
                var r = BodyRest[i]; int f = BodyFrame[i];
                body[bodyAt + i] = fo[f] + fr[f] * r.X + fu[f] * r.Y + ff[f] * r.Z;
            }
            for (int i = 0; i < LightRest.Length; i++)
            {
                var r = LightRest[i]; int f = LightFrame[i];
                lights[lightAt + i] = fo[f] + fr[f] * r.X + fu[f] * r.Y + ff[f] * r.Z;
            }
        }
    }
}
