using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Как выглядит скребун по концепту ART-006 (`docs/concept-art/stekloust-skrebun-rakursy.png`): донный панцирник 1,5 м —
    /// купол панциря (полуэллипсоид 1,1 × 0,32 × 1,5 м, кромка светлее), мягкий нижний корпус, скребок
    /// спереди внизу и ровно шесть лап из двух сегментов и плоской стопы. Своего света нет. На ходу
    /// лапы шагают двумя тройками (опора всегда на трёх), пока пасётся — скребок ходит короткими
    /// циклами. Съеденный скребун стягивается в точку. Части выпуклые, треугольники смотрят наружу.
    /// </summary>
    public static class EcoGrazerLook
    {
        /// <summary>Купол панциря: полуоси по x, y, z и высота его основания над стопами (м).</summary>
        public const float ShellX = 0.55f, ShellY = 0.32f, ShellZ = 0.75f, ShellBase = 0.32f;
        /// <summary>Шаг лап: размах поворота у бедра (рад), подъём стопы (м), частота (рад/с).</summary>
        public const float Swing = 0.3f, StepLift = 0.06f, StepRate = 5f;
        /// <summary>Скребок пасущегося: ход вниз (м) и частота (рад/с).</summary>
        public const float ScrapeDepth = 0.05f, ScrapeRate = 7f;

        /// <summary>Краски по палитре ART-006 (0xRRGGBB).</summary>
        public const uint Shell = 0x344D54, Rim = 0x819799, Under = 0x23333A, Leg = 0x2E4148;

        /// <summary>Часть, к которой относится вершина: лапа 0–5, скребок или корпус.</summary>
        public const int Body = -1, ScraperPart = 6;

        /// <summary>Вершины скребуна в его осях: x вправо, y вверх от стоп, z — вперёд.</summary>
        public static readonly EcoVec[] Rest;
        public static readonly uint[] Paint;
        public static readonly int[] Part;
        public static readonly int[] Tris;
        /// <summary>Бёдра шести лап: 0–2 левый борт (−x) спереди назад, 3–5 правый.</summary>
        public static readonly EcoVec[] Hips;

        public static int VertsPerGrazer => Rest.Length;

        static readonly float[] poseCos = new float[6], poseSin = new float[6], poseLift = new float[6];
        static readonly List<EcoVec> v = new List<EcoVec>();
        static readonly List<uint> paint = new List<uint>();
        static readonly List<int> part = new List<int>();
        static readonly List<int> tris = new List<int>();

        static EcoGrazerLook()
        {
            BuildShell();
            AddBox(new EcoVec(0, (0.15f + ShellBase) / 2f, 0), new EcoVec(0.4f, (ShellBase - 0.15f) / 2f, 0.55f), Under, Body);
            AddBox(new EcoVec(0, 0.13f, 0.66f), new EcoVec(0.18f, 0.08f, 0.1f), Rim, ScraperPart);
            Hips = new EcoVec[6];
            for (int side = 0; side < 2; side++)
                for (int k = 0; k < 3; k++)
                {
                    int leg = side * 3 + k;
                    float s = side == 0 ? -1f : 1f, z = 0.4f - k * 0.4f;
                    var hip = new EcoVec(s * 0.38f, 0.28f, z);
                    var knee = new EcoVec(s * 0.75f, 0.42f, z + (1 - k) * 0.08f);
                    var foot = new EcoVec(s * 0.95f, 0.03f, z + (1 - k) * 0.12f);
                    Hips[leg] = hip;
                    AddBeam(hip, knee, 0.04f, Leg, leg);
                    AddBeam(knee, foot, 0.035f, Leg, leg);
                    AddBox(new EcoVec(foot.X, 0.015f, foot.Z), new EcoVec(0.1f, 0.015f, 0.1f), Leg, leg);
                }
            Rest = v.ToArray(); Paint = paint.ToArray(); Part = part.ToArray(); Tris = tris.ToArray();
        }

        static int Add(EcoVec p, uint c, int pt) { v.Add(p); paint.Add(c); part.Add(pt); return v.Count - 1; }

        /// <summary>Треугольник наружу от середины выпуклой части: cross(b − a, c − a) смотрит от center.</summary>
        static void Tri(int a, int b, int c, EcoVec center)
        {
            var ab = v[b] - v[a]; var ac = v[c] - v[a];
            var n = new EcoVec(ab.Y * ac.Z - ab.Z * ac.Y, ab.Z * ac.X - ab.X * ac.Z, ab.X * ac.Y - ab.Y * ac.X);
            var mid = (v[a] + v[b] + v[c]) * (1f / 3f) - center;
            if (n.X * mid.X + n.Y * mid.Y + n.Z * mid.Z < 0f) { int t = b; b = c; c = t; }
            tris.Add(a); tris.Add(b); tris.Add(c);
        }

        static void Quad(int a, int b, int c, int d, EcoVec center) { Tri(a, b, c, center); Tri(a, c, d, center); }

        /// <summary>Купол: 12 долек × 3 кольца и макушка, снизу закрыт плоским дном.</summary>
        static void BuildShell()
        {
            const int around = 12, rings = 3;
            var center = new EcoVec(0, ShellBase + ShellY * 0.35f, 0);
            int first = v.Count;
            for (int r = 0; r < rings; r++)
            {
                double phi = r * (Math.PI / 2) / rings;
                for (int i = 0; i < around; i++)
                {
                    double th = i * 2 * Math.PI / around;
                    var p = new EcoVec((float)(ShellX * Math.Cos(phi) * Math.Sin(th)), ShellBase + (float)(ShellY * Math.Sin(phi)),
                                       (float)(ShellZ * Math.Cos(phi) * Math.Cos(th)));
                    Add(p, r == 0 ? Rim : Shell, Body);
                }
            }
            int top = Add(new EcoVec(0, ShellBase + ShellY, 0), Shell, Body);
            int bottom = Add(new EcoVec(0, ShellBase, 0), Under, Body);
            for (int r = 0; r < rings - 1; r++)
                for (int i = 0; i < around; i++)
                {
                    int j = (i + 1) % around;
                    Quad(first + r * around + i, first + r * around + j, first + (r + 1) * around + j, first + (r + 1) * around + i, center);
                }
            for (int i = 0; i < around; i++)
            {
                int j = (i + 1) % around;
                Tri(first + (rings - 1) * around + i, first + (rings - 1) * around + j, top, center);
                Tri(first + i, first + j, bottom, center);
            }
        }

        /// <summary>Брусок с серединой c и полуразмерами h по осям.</summary>
        static void AddBox(EcoVec c, EcoVec h, uint color, int pt)
        {
            int b = v.Count;
            for (int i = 0; i < 8; i++)
                Add(new EcoVec(c.X + ((i & 1) != 0 ? h.X : -h.X), c.Y + ((i & 2) != 0 ? h.Y : -h.Y), c.Z + ((i & 4) != 0 ? h.Z : -h.Z)), color, pt);
            BoxFaces(b, c);
        }

        /// <summary>Сегмент лапы: четырёхгранный брусок толщиной 2w от p0 до p1.</summary>
        static void AddBeam(EcoVec p0, EcoVec p1, float w, uint color, int pt)
        {
            var d = (p1 - p0).Normalized;
            var u = new EcoVec(d.Z, 0, -d.X); // поперёк, горизонтально
            u = u.LengthSq > 1e-6f ? u.Normalized : new EcoVec(1, 0, 0);
            var n = new EcoVec(d.Y * u.Z - d.Z * u.Y, d.Z * u.X - d.X * u.Z, d.X * u.Y - d.Y * u.X);
            int b = v.Count;
            for (int i = 0; i < 8; i++)
            {
                var o = ((i & 4) != 0 ? p1 : p0) + u * ((i & 1) != 0 ? w : -w) + n * ((i & 2) != 0 ? w : -w);
                Add(o, color, pt);
            }
            BoxFaces(b, (p0 + p1) * 0.5f);
        }

        /// <summary>Шесть граней бруска из 8 вершин (бит 0 — первая ось, бит 1 — вторая, бит 2 — третья).</summary>
        static void BoxFaces(int b, EcoVec center)
        {
            Quad(b + 0, b + 1, b + 3, b + 2, center); // третья ось −
            Quad(b + 4, b + 5, b + 7, b + 6, center); // третья ось +
            Quad(b + 0, b + 1, b + 5, b + 4, center); // вторая ось −
            Quad(b + 2, b + 3, b + 7, b + 6, center); // вторая ось +
            Quad(b + 0, b + 2, b + 6, b + 4, center); // первая ось −
            Quad(b + 1, b + 3, b + 7, b + 5, center); // первая ось +
        }

        /// <summary>Идёт ли скребун (лапы шагают): жив, не пасётся и движется.</summary>
        public static bool Walking(EcoGrazer.Grazer g) =>
            g.Agent.Alive && g.Agent.State != EcoState.Graze && g.Agent.V.LengthSq > 0.01f;

        /// <summary>Тройка лапы: левая передняя, правая средняя, левая задняя — +1, остальные — −1.</summary>
        public static int Tripod(int leg) => ((leg / 3) + (leg % 3)) % 2 == 0 ? 1 : -1;

        /// <summary>Поворот лапы у бедра вокруг вертикали (рад) и подъём стопы (м) в момент t.</summary>
        public static void LegPose(EcoGrazer.Grazer g, int leg, float t, out float angle, out float lift)
        {
            if (!Walking(g)) { angle = 0f; lift = 0f; return; }
            float s = (float)Math.Sin(t * StepRate + g.Index * 1.3f) * Tripod(leg);
            angle = Swing * s;
            lift = StepLift * Math.Max(0f, s);
        }

        /// <summary>Насколько опущен скребок (м): ходит, только пока скребун пасётся.</summary>
        public static float Scrape(EcoGrazer.Grazer g, float t) =>
            g.Grazing ? ScrapeDepth * 0.5f * (1f + (float)Math.Sin(t * ScrapeRate + g.Index)) : 0f;

        /// <summary>Вперёд по курсу: (sin Dir, 0, cos Dir).</summary>
        public static EcoVec Forward(EcoGrazer.Grazer g) => new EcoVec((float)Math.Sin(g.Dir), 0, (float)Math.Cos(g.Dir));

        /// <summary>
        /// Записать вершины скребуна в мире в dst начиная с at (<see cref="VertsPerGrazer"/> штук).
        /// Стопы — на дне под ним (Agent.P на <see cref="EcoGrazer.Lift"/> выше дна).
        /// </summary>
        public static void Place(EcoGrazer.Grazer g, float t, EcoVec[] dst, int at)
        {
            float[] ca = poseCos, sa = poseSin, li = poseLift;
            var p = g.Agent.P;
            if (!g.Agent.Alive)
            {
                for (int i = 0; i < Rest.Length; i++) dst[at + i] = p;
                return;
            }
            var fw = Forward(g);
            var right = new EcoVec(fw.Z, 0, -fw.X);
            var origin = new EcoVec(p.X, p.Y - EcoGrazer.Lift, p.Z);
            for (int leg = 0; leg < 6; leg++)
            {
                LegPose(g, leg, t, out float ang, out float lift);
                ca[leg] = (float)Math.Cos(ang); sa[leg] = (float)Math.Sin(ang); li[leg] = lift;
            }
            float scrape = Scrape(g, t);
            for (int i = 0; i < Rest.Length; i++)
            {
                var r = Rest[i];
                int pt = Part[i];
                if (pt >= 0 && pt < 6)
                {
                    var h = Hips[pt];
                    float dx = r.X - h.X, dz = r.Z - h.Z;
                    r = new EcoVec(h.X + dx * ca[pt] + dz * sa[pt], r.Y + li[pt], h.Z - dx * sa[pt] + dz * ca[pt]);
                }
                else if (pt == ScraperPart) r = new EcoVec(r.X, r.Y - scrape, r.Z);
                dst[at + i] = origin + right * r.X + new EcoVec(0, r.Y, 0) + fw * r.Z;
            }
        }
    }
}
