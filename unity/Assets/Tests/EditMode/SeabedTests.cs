using System;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>Отмель: шум и рельеф как в js/util.js и js/world.js, сетка дна, вода по глубине.</summary>
    public class SeabedTests
    {
        // эталоны посчитаны браузерным кодом: G.noise(x, z), G.fbm(x·0.006, z·0.006, 4), G.World.height(x, z)
        static readonly double[,] Ref =
        {
            //   x      z       noise        fbm          height
            { 0, 0, 0.0, 0.332220743, -44.2477901 },
            { 3.7, -2.2, 0.259941809, 0.331207587, -43.6827091 },
            { -12.5, 40.25, 0.367289473, 0.297668869, -45.3575664 },
            { 0, 95, 0.299296324, 0.402869119, -42.3827908 },
            { 100, -50, 0.769334767, 0.435696983, -40.9791447 },
            { -200, 180, 0.532139040, 0.435770687, -66.0663133 },
            { 250, 10, 0.991272720, 0.429307593, -121.432558 },
            { 320, -330, 0.605448345, 0.791603727, -41.3066249 },
            { -17.3, 9.1, 0.823644640, 0.356367290, -37.3591621 },
        };

        [Test]
        public void ШумСовпадаетСБраузернымДоЗнака()
        {
            for (int i = 0; i < Ref.GetLength(0); i++)
            {
                double x = Ref[i, 0], z = Ref[i, 1];
                Assert.AreEqual(Ref[i, 2], Noise.Value(x, z), 1e-7, $"noise({x}, {z})");
                Assert.AreEqual(Ref[i, 3], Noise.Fbm(x * 0.006, z * 0.006, 4), 1e-7, $"fbm({x}, {z})");
            }
        }

        [Test]
        public void РельефСовпадаетСБраузерным()
        {
            for (int i = 0; i < Ref.GetLength(0); i++)
            {
                double x = Ref[i, 0], z = Ref[i, 1];
                Assert.AreEqual(Ref[i, 4], Seabed.HeightJs(x, z), 1e-5, $"height({x}, {z})");
            }
        }

        [Test]
        public void ВUnityXМеняетЗнак()
        {
            // склон в глубину у правого борта: в браузере x = +250, в Unity x = −250
            Assert.AreEqual(-121.43f, Seabed.Height(-250f, 10f), 0.01f);
            Assert.Greater(Seabed.Height(250f, 10f), -70f, "у левого борта склона нет");
            Assert.AreEqual(-42.38f, Seabed.Height(0f, 95f), 0.01f, "под стартом корабля");
        }

        [Test]
        public void ДноНеВышеСемиМетровИКраяКартыСтены()
        {
            for (float x = -350; x <= 350; x += 17.5f)
                for (float z = -350; z <= 350; z += 17.5f)
                    Assert.LessOrEqual(Seabed.Height(x, z), Seabed.Top, $"({x}, {z})");
            Assert.Greater(Seabed.Height(340f, 0f), Seabed.Height(0f, 0f) + 20f, "у края карты скала");
            Assert.Greater(Seabed.Height(0f, -340f), Seabed.Height(0f, 0f) + 20f);
        }

        [Test]
        public void СеткаДнаПоРельефуИЛицомВверх()
        {
            Seabed.BuildGrid(out var p, out var n, out var c, out var tris);
            int nv = (Seabed.Segments + 1) * (Seabed.Segments + 1);
            Assert.AreEqual(nv * 3, p.Length);
            Assert.Less(nv, 65536, "16-битные индексы");
            Assert.AreEqual(Seabed.Segments * Seabed.Segments * 6, tris.Length, "64 800 треугольников");
            Assert.AreEqual(-Seabed.Size / 2, p[0], 1e-3f);
            Assert.AreEqual(Seabed.Size / 2, p[p.Length - 3], 1e-3f);
            for (int v = 0; v < nv; v += 97)
            {
                Assert.AreEqual(Seabed.Height(p[v * 3], p[v * 3 + 2]), p[v * 3 + 1], 1e-4f, "вершина на рельефе");
                Assert.Greater(n[v * 3 + 1], 0f, "нормаль вверх");
                for (int k = 0; k < 3; k++) Assert.That(c[v * 3 + k], Is.InRange(0f, 1f), "цвет 0..1");
            }
            for (int t = 0; t < tris.Length; t += 3 * 211)
            {
                int a = tris[t] * 3, b = tris[t + 1] * 3, d = tris[t + 2] * 3;
                float ux = p[b] - p[a], uy = p[b + 1] - p[a + 1], uz = p[b + 2] - p[a + 2];
                float vx = p[d] - p[a], vy = p[d + 1] - p[a + 1], vz = p[d + 2] - p[a + 2];
                float cy = uz * vx - ux * vz; // y векторного произведения (b−a)×(d−a): лицевая сторона в Unity
                Assert.Greater(cy, 0f, "треугольник смотрит вверх");
            }
        }

        [Test]
        public void ЦветДнаПесокСкалаИТемнотаВГлубине()
        {
            Seabed.ColorAt(0, -30, 95, 1f, out float r1, out float g1, out float b1);
            Assert.Greater(r1, b1, "ровное мелкое дно — песок");
            Seabed.ColorAt(0, -110, 95, 1f, out float r2, out float g2, out float b2);
            Assert.Less(r2 + g2 + b2, (r1 + g1 + b1) * 0.5f, "на глубине 110 м дно тёмное");
            Assert.Greater(b2, r2, "и синеватое");
            Seabed.ColorAt(0, -30, 95, 0.6f, out float r3, out float g3, out float b3);
            Assert.Greater(b3 / (r3 + g3 + b3), b1 / (r1 + g1 + b1), "на крутом склоне фиолетовая скала");
        }

        [Test]
        public void КорабльНеОпускаетсяНижеДнаОтмели()
        {
            var s = new ShipPhysics { InVert = -1, Ground = Seabed.Height };
            for (int i = 0; i < 1200; i++) s.Update(0.05f, 1f, 0f, 0f);
            Assert.AreEqual(Seabed.Height(s.X, s.Z) + ShipPhysics.Clearance, s.Y, 0.01f, "3 м над дном под стартом");
            Assert.Less(s.Y, -35f, "а дно отмели глубже 32 м прежнего ровного дна");
        }

        [Test]
        public void ВодаТемнеетИГуснетСГлубиной()
        {
            Assert.AreEqual(0f, Water.K(5f), 1e-6f);
            Assert.AreEqual(1f, Water.K(120f), 1e-6f);
            Assert.AreEqual(0.015f, Water.FogDensity(0f), 1e-6f);
            Assert.AreEqual(0.025f, Water.FogDensity(95f), 1e-6f);
            Water.FogColor(0f, out float r0, out float g0, out float b0);
            Assert.AreEqual(0x66 / 255f, g0, 1e-4f, "у льда 0x1a6676");
            Water.FogColor(95f, out float r1, out float g1, out float b1);
            Assert.AreEqual(0x1a / 255f, g1, 1e-4f, "в глубине 0x041a22");
            Assert.Greater(Water.Ambient(10f), Water.Ambient(60f));
            Assert.Greater(Water.Sun(10f), Water.Sun(60f));
            Assert.AreEqual(0.4f, Water.Ambient(200f), 1e-5f);
            Assert.AreEqual(0.15f, Water.Sun(200f), 1e-5f);
        }
    }
}
