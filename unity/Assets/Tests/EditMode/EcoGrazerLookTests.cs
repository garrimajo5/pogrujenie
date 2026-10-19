using System;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>Как рисуются скребун (ART-006) и светящийся мох (пятна мха в js/world.js).</summary>
    public class EcoGrazerLookTests
    {
        static EcoWorld World(out EcoMoss moss, out EcoGrazer grazers)
        {
            var w = new EcoWorld(EcoRandom.WorldSeed) { Ground = Seabed.Height };
            w.Ship.Pos = new EcoVec(500, 0, 500);
            moss = w.Add(new EcoMoss());
            grazers = w.Add(new EcoGrazer(moss));
            return w;
        }

        static EcoVec Cross(EcoVec a, EcoVec b) =>
            new EcoVec(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        static float Dot(EcoVec a, EcoVec b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        [Test]
        public void СкребунПоКонцепту()
        {
            var r = EcoGrazerLook.Rest;
            Assert.AreEqual(r.Length, EcoGrazerLook.Paint.Length);
            Assert.AreEqual(r.Length, EcoGrazerLook.Part.Length);
            Assert.AreEqual(0, EcoGrazerLook.Tris.Length % 3);
            foreach (int i in EcoGrazerLook.Tris) Assert.That(i, Is.InRange(0, r.Length - 1));
            Assert.Less(EcoGrazerLook.Tris.Length / 3 * EcoGrazer.Count, 5000, "десять скребунов — меньше 5 тыс. треугольников");

            float minY = float.MaxValue, maxZ = float.MinValue, minZ = float.MaxValue, maxX = 0;
            var legs = new bool[6];
            for (int i = 0; i < r.Length; i++)
            {
                minY = Math.Min(minY, r[i].Y);
                maxX = Math.Max(maxX, Math.Abs(r[i].X));
                int p = EcoGrazerLook.Part[i];
                if (p >= 0 && p < 6) legs[p] = true;
                else { maxZ = Math.Max(maxZ, r[i].Z); minZ = Math.Min(minZ, r[i].Z); }
            }
            Assert.AreEqual(0f, minY, 1e-4f, "стоит на стопах");
            Assert.AreEqual(1.5f, maxZ - minZ, 0.05f, "панцирь 1,5 м");
            Assert.That(maxX, Is.InRange(0.9f, 1.1f), "лапы шире панциря");
            Assert.IsTrue(Array.TrueForAll(legs, b => b), "ровно шесть лап");
            Assert.AreEqual(6, EcoGrazerLook.Hips.Length);
            Assert.Contains(EcoGrazerLook.Rim, EcoGrazerLook.Paint, "кромка панциря светлее");
        }

        [Test]
        public void ЧастиЗамкнутыИСмотрятНаружу()
        {
            // объём со знаком по каждой части: положительный — все грани смотрят наружу (лицевые в Unity)
            var r = EcoGrazerLook.Rest;
            var vol = new float[8];
            var t = EcoGrazerLook.Tris;
            for (int i = 0; i < t.Length; i += 3)
            {
                int p = EcoGrazerLook.Part[t[i]] + 1;
                Assert.AreEqual(p, EcoGrazerLook.Part[t[i + 1]] + 1, "треугольник в одной части");
                vol[p] += Dot(r[t[i]], Cross(r[t[i + 1]], r[t[i + 2]])) / 6f;
            }
            Assert.Greater(vol[0], 0.3f, "панцирь и нижний корпус");
            for (int p = 1; p < 8; p++) Assert.Greater(vol[p], 0f, "часть " + (p - 1));
        }

        [Test]
        public void ЛапыШагаютТройками()
        {
            int a = 0, b = 0;
            for (int leg = 0; leg < 6; leg++) if (EcoGrazerLook.Tripod(leg) > 0) a++; else b++;
            Assert.AreEqual(3, a); Assert.AreEqual(3, b);
            Assert.AreEqual(EcoGrazerLook.Tripod(0), EcoGrazerLook.Tripod(4), "левая передняя и правая средняя — вместе");
            Assert.AreNotEqual(EcoGrazerLook.Tripod(0), EcoGrazerLook.Tripod(3));

            World(out _, out var gr);
            var g = gr.All[0];
            g.Agent.Set(EcoState.Seek);
            g.Agent.V = new EcoVec(0.9f, 0, 0);
            Assert.IsTrue(EcoGrazerLook.Walking(g));
            float maxLift = 0;
            for (int i = 0; i < 100; i++)
            {
                float t = i * 0.031f;
                int up = 0;
                for (int leg = 0; leg < 6; leg++)
                {
                    EcoGrazerLook.LegPose(g, leg, t, out float ang, out float lift);
                    Assert.LessOrEqual(Math.Abs(ang), EcoGrazerLook.Swing + 1e-5f);
                    if (lift > 1e-4f) up++;
                    maxLift = Math.Max(maxLift, lift);
                }
                Assert.LessOrEqual(up, 3, "опора всегда хотя бы на трёх лапах");
            }
            Assert.Greater(maxLift, 0.05f, "стопы поднимаются");

            g.Agent.Set(EcoState.Graze, 10f);
            g.Agent.V = EcoVec.Zero;
            EcoGrazerLook.LegPose(g, 2, 0.4f, out float a0, out float l0);
            Assert.AreEqual(0f, a0); Assert.AreEqual(0f, l0, "пасётся — лапы стоят");
            float maxScrape = 0;
            for (int i = 0; i < 60; i++) maxScrape = Math.Max(maxScrape, EcoGrazerLook.Scrape(g, i * 0.03f));
            Assert.Greater(maxScrape, 0.04f, "скребок ходит, пока пасётся");
        }

        [Test]
        public void СтоитНаДнеНосомПоКурсу()
        {
            World(out _, out var gr);
            var g = gr.All[3];
            g.Dir = (float)(Math.PI / 2); // вперёд — +x
            var v = new EcoVec[EcoGrazerLook.VertsPerGrazer];
            EcoGrazerLook.Place(g, 0f, v, 0);
            var p = g.Agent.P;
            float minY = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < v.Length; i++)
            {
                minY = Math.Min(minY, v[i].Y);
                if (EcoGrazerLook.Part[i] == EcoGrazerLook.ScraperPart) maxX = Math.Max(maxX, v[i].X);
            }
            Assert.AreEqual(p.Y - EcoGrazer.Lift, minY, 1e-3f, "стопы на дне");
            Assert.AreEqual(Seabed.Height(p.X, p.Z), minY, 0.01f);
            Assert.Greater(maxX - p.X, 0.6f, "скребок спереди, по курсу");
        }

        [Test]
        public void СъеденныйНеВиден()
        {
            World(out _, out var gr);
            var g = gr.All[1];
            gr.Kill(g);
            var v = new EcoVec[EcoGrazerLook.VertsPerGrazer];
            EcoGrazerLook.Place(g, 1f, v, 0);
            foreach (var x in v) Assert.AreEqual(g.Agent.P, x);
        }

        [Test]
        public void ПятнаМхаЛежатПоДну()
        {
            var w = World(out var moss, out _);
            var m = EcoMossLook.Build(moss.Patches, Seabed.Height);
            Assert.AreEqual(22 * 81, m.Verts.Length);
            Assert.AreEqual(22 * 128 * 3, m.Tris.Length);
            for (int i = 0; i < m.Verts.Length; i += 7)
            {
                var p = m.Verts[i];
                Assert.AreEqual(Seabed.Height(p.X, p.Z) + EcoMossLook.Above, p.Y, 1e-3f);
            }
            for (int i = 0; i < m.Tris.Length; i += 3)
            {
                var n = Cross(m.Verts[m.Tris[i + 1]] - m.Verts[m.Tris[i]], m.Verts[m.Tris[i + 2]] - m.Verts[m.Tris[i]]);
                Assert.Greater(n.Y, 0f, "грань смотрит вверх");
            }
            // середина пятна светится, углы 7 × 7 — нет (круг 3,4 м)
            int center = 4 * EcoMossLook.Side + 4;
            Assert.Greater(m.Base[center], 0.2f);
            Assert.AreEqual(0f, m.Base[0], 1e-6f);
            Assert.AreEqual(0f, m.Base[EcoMossLook.VertsPerPatch - 1], 1e-6f);
        }

        [Test]
        public void ОбъеденныйМохТускнеет()
        {
            var w = World(out var moss, out _);
            var m = EcoMossLook.Build(moss.Patches, Seabed.Height);
            var p = moss.Patches[5];
            int center = p.Index * EcoMossLook.VertsPerPatch + 4 * EcoMossLook.Side + 4;
            p.Food = 1f;
            float full = EcoMossLook.Brightness(m, moss.Patches, center);
            Assert.AreEqual(m.Base[center], full, 1e-5f);
            p.Food = 0f;
            Assert.AreEqual(m.Base[center] * EcoMoss.MinGlow, EcoMossLook.Brightness(m, moss.Patches, center), 1e-5f,
                "объеденное — 0,15 яркости");
        }
    }
}
