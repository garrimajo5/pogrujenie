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

        [Test]
        public void ТелоИХвостВОбеСтороны()
        {
            Assert.AreEqual(9, EcoFishLook.VertsPerFish);
            Assert.AreEqual(54, EcoFishLook.Tris.Length, "8 граней тела и хвост, каждая дважды");
            int half = EcoFishLook.Tris.Length / 2;
            for (int i = 0; i < half; i += 3)
            {
                var t = EcoFishLook.Tris;
                Assert.That(t[i], Is.InRange(0, 8));
                Assert.AreEqual(t[i], t[half + i]);
                Assert.AreEqual(t[i + 1], t[half + i + 2], "обратная сторона — тот же треугольник наоборот");
                Assert.AreEqual(t[i + 2], t[half + i + 1]);
            }
            // размеры как в браузере: тело 0,07 × 0,1 × 0,28 (полуоси), хвост до −0,42
            Assert.AreEqual(0.28f, EcoFishLook.Shape[4].Z, 1e-6f);
            Assert.AreEqual(-0.42f, EcoFishLook.Shape[7].Z, 1e-6f);
        }

        [Test]
        public void НосСмотритПоСкорости()
        {
            World(out var fish);
            var f = fish.All[0];
            f.Agent.V = new EcoVec(3, 0, 0);
            var p = f.Agent.P;
            var v = new EcoVec[9];
            EcoFishLook.Place(f, Straight(f), v, 0);
            Near(p + new EcoVec(0.28f, 0, 0), v[4], "нос по ходу");
            Near(p + new EcoVec(-0.28f, 0, 0), v[5], "корма сзади");
            Near(p + new EcoVec(0, 0.1f, 0), v[2], "спина вверх");
            Near(p + new EcoVec(-0.42f, 0.12f, 0), v[7], "хвост позади и вверх");
            Assert.AreEqual(0.14f, Math.Abs(v[0].Z - v[1].Z), 1e-3f, "бока поперёк хода");
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
            var v = new EcoVec[9];
            f.Agent.V = new EcoVec(0, 2, 0);
            EcoFishLook.Place(f, 1.3f, v, 0);
            Near(f.Agent.P + new EcoVec(0, 0.28f, 0), v[4], "плывёт вверх — нос вверх");
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
            var v = new EcoVec[18];
            EcoFishLook.Place(f, 0f, v, 9);
            for (int i = 9; i < 18; i++) Assert.AreEqual(f.Agent.P, v[i], "все вершины в одной точке");
        }

        [Test]
        public void ЦветСтаиИЯркостьРыбки()
        {
            World(out var fish);
            for (int s = 0; s < fish.Shoals.Count; s++)
            {
                var f = fish.Shoals[s].Members[0];
                uint c = EcoFish.Colors[s];
                EcoFishLook.Color(f, out float r, out float g, out float b);
                Assert.AreEqual(((c >> 16) & 255) / 255f * f.Bright, r, 1e-4f);
                Assert.AreEqual(((c >> 8) & 255) / 255f * f.Bright, g, 1e-4f);
                Assert.AreEqual((c & 255) / 255f * f.Bright, b, 1e-4f);
            }
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
