using System;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>Как рисуется светляк-полосник (fishGeo и updateFish в js/creatures.js): форма, курс, цвет стаи.</summary>
    public class EcoFishLookTests
    {
        static EcoWorld World(out EcoFish fish)
        {
            var w = new EcoWorld(EcoRandom.WorldSeed) { Ground = Seabed.Height };
            w.Ship.Pos = new EcoVec(500, 0, 500);
            fish = w.Add(new EcoFish());
            return w;
        }

        /// <summary>Момент, когда рыбка не виляет: sin(t·12 + фаза) = 0.</summary>
        static float Straight(EcoFish.Fish f) => -f.Phase / EcoFishLook.WiggleRate;

        static void Near(EcoVec expected, EcoVec actual, string msg)
        {
            Assert.Less(EcoVec.Distance(expected, actual), 1e-3f, $"{msg}: ждали {expected}, вышло {actual}");
        }

        static EcoVec Cross(EcoVec a, EcoVec b) =>
            new EcoVec(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        /// <summary>Номер вершины формы с координатами (x, y, z).</summary>
        static int Find(float x, float y, float z)
        {
            for (int i = 0; i < EcoFishLook.Shape.Length; i++)
                if (EcoVec.Distance(EcoFishLook.Shape[i], new EcoVec(x, y, z)) < 1e-4f) return i;
            Assert.Fail($"нет вершины ({x}, {y}, {z})");
            return -1;
        }

        [Test]
        public void ВеретеноСЧетырьмяПолосамиПоКонцепту()
        {
            var s = EcoFishLook.Shape;
            Assert.AreEqual(s.Length, EcoFishLook.Paints.Length);
            Assert.Less(EcoFishLook.Tris.Length / 3, 150, "до 150 треугольников, как в ART-011");
            foreach (int i in EcoFishLook.Tris) Assert.That(i, Is.InRange(0, s.Length - 1));
            int stripes = 0;
            foreach (bool b in EcoFishLook.RingStripe) if (b) stripes++;
            Assert.AreEqual(4, stripes, "четыре полосы");
            Assert.AreEqual(EcoFishLook.Paint.Stripe, EcoFishLook.Paints[EcoFishLook.StripeVertex]);
            // размеры как в браузере: нос 0,28, полуширина 0,07, полувысота 0,1, хвост до −0,42
            float maxX = 0, maxY = 0, minZ = 0, maxZ = 0;
            foreach (var p in s) { maxX = Math.Max(maxX, p.X); maxY = Math.Max(maxY, p.Y); minZ = Math.Min(minZ, p.Z); maxZ = Math.Max(maxZ, p.Z); }
            Assert.AreEqual(0.28f, maxZ, 1e-5f);
            Assert.AreEqual(-0.42f, minZ, 1e-5f);
            Assert.That(maxX, Is.InRange(0.06f, 0.07f));
            Assert.That(maxY, Is.InRange(0.1f, 0.121f), "спина и хвост");
            Assert.AreEqual(EcoFishLook.Paint.Fin, EcoFishLook.Paints[Find(0, 0.12f, -0.42f)], "концы хвоста светятся");
            Assert.AreEqual(EcoFishLook.Paint.Body, EcoFishLook.Paints[Find(0, 0, 0.28f)], "нос тёмный");
        }

        [Test]
        public void ТелоЗамкнутоИСмотритНаружуХвостВОбеСтороны()
        {
            var s = EcoFishLook.Shape;
            var t = EcoFishLook.Tris;
            int fin = Find(0, 0.12f, -0.42f);
            float vol = 0;
            var edges = new System.Collections.Generic.Dictionary<long, int>();
            int finTris = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                if (t[i] == fin || t[i + 1] == fin || t[i + 2] == fin) { finTris++; continue; }
                vol += (s[t[i]].X * Cross(s[t[i + 1]], s[t[i + 2]]).X + s[t[i]].Y * Cross(s[t[i + 1]], s[t[i + 2]]).Y
                        + s[t[i]].Z * Cross(s[t[i + 1]], s[t[i + 2]]).Z) / 6f;
                for (int k = 0; k < 3; k++)
                {
                    long e = (long)t[i + k] * 1000 + t[i + (k + 1) % 3];
                    edges[e] = edges.TryGetValue(e, out int n) ? n + 1 : 1;
                }
            }
            Assert.Greater(vol, 0f, "грани тела смотрят наружу");
            foreach (var kv in edges)
            {
                Assert.AreEqual(1, kv.Value, "ребро тела один раз в каждую сторону");
                long back = (kv.Key % 1000) * 1000 + kv.Key / 1000;
                Assert.IsTrue(edges.ContainsKey(back), "тело замкнуто");
            }
            Assert.AreEqual(2, finTris, "хвост — один треугольник в обе стороны");
        }

        [Test]
        public void НосСмотритПоСкорости()
        {
            World(out var fish);
            var f = fish.All[0];
            f.Agent.V = new EcoVec(3, 0, 0);
            var p = f.Agent.P;
            var v = new EcoVec[EcoFishLook.VertsPerFish];
            EcoFishLook.Place(f, Straight(f), v, 0);
            Near(p + new EcoVec(0.28f, 0, 0), v[Find(0, 0, 0.28f)], "нос по ходу");
            Near(p + new EcoVec(-0.25f, 0, 0), v[Find(0, 0, -0.25f)], "корма сзади");
            Near(p + new EcoVec(-0.42f, 0.12f, 0), v[Find(0, 0.12f, -0.42f)], "хвост позади и вверх");
            var side = EcoFishLook.Shape[EcoFishLook.StripeVertex];
            Assert.Greater(side.X, 0.05f, "вершина полосы — на боку");
            Near(p + new EcoVec(side.Z, 0, -side.X), v[EcoFishLook.StripeVertex], "бок поперёк хода");
        }

        [Test]
        public void ВиляетНеБольше025Рад()
        {
            World(out var fish);
            var f = fish.All[3];
            f.Agent.V = new EcoVec(0, 0, -2);
            float max = 0;
            for (int i = 0; i < 200; i++)
            {
                var fw = EcoFishLook.Forward(f, i * 0.013f);
                Assert.AreEqual(1f, fw.Length, 1e-4f);
                float a = (float)Math.Acos(Math.Min(1f, -fw.Z));
                max = Math.Max(max, a);
            }
            Assert.LessOrEqual(max, EcoFishLook.Wiggle + 1e-3f);
            Assert.Greater(max, 0.2f, "виляет заметно");
        }

        [Test]
        public void ОтвесноИСтояНеЛомается()
        {
            World(out var fish);
            var f = fish.All[5];
            var v = new EcoVec[EcoFishLook.VertsPerFish];
            f.Agent.V = new EcoVec(0, 2, 0);
            EcoFishLook.Place(f, 1.3f, v, 0);
            Near(f.Agent.P + new EcoVec(0, 0.28f, 0), v[Find(0, 0, 0.28f)], "плывёт вверх — нос вверх");
            f.Agent.V = EcoVec.Zero;
            EcoFishLook.Place(f, 1.3f, v, 0);
            foreach (var p in v) Assert.IsFalse(float.IsNaN(p.X) || float.IsNaN(p.Y) || float.IsNaN(p.Z));
        }

        [Test]
        public void СъеденнаяНеВидна()
        {
            World(out var fish);
            var f = fish.All[7];
            fish.Kill(f);
            int n = EcoFishLook.VertsPerFish;
            var v = new EcoVec[2 * n];
            EcoFishLook.Place(f, 0f, v, n);
            for (int i = n; i < 2 * n; i++) Assert.AreEqual(f.Agent.P, v[i], "все вершины в одной точке");
        }

        [Test]
        public void ПолосыЦветаСтаиТелоТёмное()
        {
            World(out var fish);
            for (int s = 0; s < fish.Shoals.Count; s++)
            {
                var f = fish.Shoals[s].Members[0];
                uint c = EcoFish.Colors[s];
                Assert.Greater(c & 255, (c >> 16) & 255, "полосы в голубой гамме: синего больше красного");
                EcoFishLook.Stripe(f, out float r, out float g, out float b);
                Assert.AreEqual(((c >> 16) & 255) / 255f * f.Bright, r, 1e-4f);
                Assert.AreEqual(((c >> 8) & 255) / 255f * f.Bright, g, 1e-4f);
                Assert.AreEqual((c & 255) / 255f * f.Bright, b, 1e-4f);
                EcoFishLook.VertexColor(f, EcoFishLook.Paint.Fin, out float fr, out float fg, out float fb);
                Assert.AreEqual(g * EcoFishLook.FinGlow, fg, 1e-4f, "концы хвоста — тише полос");
                EcoFishLook.VertexColor(f, EcoFishLook.Paint.Body, out float br, out float bg, out float bb);
                Assert.AreEqual(0x34 / 255f, br, 1e-4f, "тело #344F63");
                Assert.AreEqual(0x4F / 255f, bg, 1e-4f);
                Assert.AreEqual(0x63 / 255f, bb, 1e-4f);
            }
            CheckAlarm(fish);
        }

        static void CheckAlarm(EcoFish fish)
        {
            var f = fish.Shoals[0].Members[0];
            EcoFishLook.VertexColor(f, EcoFishLook.Paint.Body, out float r0, out _, out _);
            EcoFishLook.Stripe(f, out _, out float g0, out _);
            fish.Shoals[0].Alarm = 2f;
            EcoFishLook.VertexColor(f, EcoFishLook.Paint.Body, out float r1, out _, out _);
            EcoFishLook.Stripe(f, out _, out float g1, out _);
            Assert.AreEqual(r0, r1, 1e-6f, "в тревоге тело не меняется");
            Assert.AreEqual(g0 * EcoFishLook.AlarmGlow, g1, 1e-4f, "полосы ярче");
        }

        [Test]
        public void КорабльНаХодуЗажигаетСтаюЯрче()
        {
            var w = World(out var fish);
            var sh = fish.Shoals[2];
            var f = sh.Members[0];
            Assert.AreEqual(f.Bright, EcoFishLook.Brightness(f), 1e-6f, "спокойная стая — своя яркость");
            w.Ship.Pos = sh.Center + new EcoVec(3, 0, 0);
            w.Ship.Speed = 3f;
            w.Step(0.05f);
            Assert.AreEqual(f.Bright * EcoFishLook.AlarmGlow, EcoFishLook.Brightness(f), 1e-5f, "в тревоге ярче");
            w.Ship.Pos = new EcoVec(500, 0, 500);
            w.Run(3f);
            Assert.AreEqual(f.Bright, EcoFishLook.Brightness(f), 1e-6f, "угроза ушла — гаснет обратно");
        }
    }
}
