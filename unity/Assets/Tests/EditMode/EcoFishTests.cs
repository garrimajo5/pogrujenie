using System.Collections.Generic;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>Светляки-полосники (updateFish в js/creatures.js): стаи у кораллов, тревога от хищника и корабля.</summary>
    public class EcoFishTests
    {
        static EcoWorld World(out EcoFish fish, uint seed = EcoRandom.WorldSeed)
        {
            var w = new EcoWorld(seed) { Ground = Seabed.Height };
            w.Ship.Pos = new EcoVec(500, 0, 500); // корабль далеко и не мешает
            fish = w.Add(new EcoFish());
            return w;
        }

        static float MeanToHome(EcoFish.Shoal sh)
        {
            float s = 0; int n = 0;
            foreach (var f in sh.Members) if (f.Agent.Alive) { s += EcoVec.Distance(f.Agent.P, sh.Home); n++; }
            return s / n;
        }

        static float MeanTo(EcoFish.Shoal sh, EcoVec p)
        {
            float s = 0; int n = 0;
            foreach (var f in sh.Members) if (f.Agent.Alive) { s += EcoVec.Distance(f.Agent.P, p); n++; }
            return s / n;
        }

        [Test]
        public void ШестьСтайПо22УКораллов()
        {
            var w = World(out var fish);
            Assert.AreEqual(6, fish.Shoals.Count);
            Assert.AreEqual(132, fish.All.Count);
            Assert.AreEqual(132, w.Count("fish"));
            // дом первой стаи — как в браузере, x со знаком минус
            Assert.AreEqual(-15.375f, fish.Shoals[0].Home.X, 0.01f);
            Assert.AreEqual(75.99f, fish.Shoals[0].Home.Z, 0.01f);
            for (int i = 0; i < fish.All.Count; i++)
            {
                var f = fish.All[i];
                Assert.AreEqual(i, f.Index);
                Assert.AreEqual("fish", f.Agent.SpeciesId);
                var d = f.Agent.P - f.Shoal.Home;
                Assert.That(d.X, Is.InRange(-3f, 3f)); Assert.That(d.Y, Is.InRange(0f, 3f)); Assert.That(d.Z, Is.InRange(-3f, 3f));
                Assert.That(f.Bright, Is.InRange(0.75f, 1.15f));
            }
            foreach (var sh in fish.Shoals)
                Assert.AreEqual(3.5f, sh.Home.Y - Seabed.Height(sh.Home.X, sh.Home.Z), 0.25f, "дом стаи на 3,5 м над дном");
        }

        [Test]
        public void СтаяБродитУДомаИДержитсяВместе()
        {
            var w = World(out var fish);
            for (int s = 0; s < 6; s++)
            {
                w.Run(30f);
                foreach (var sh in fish.Shoals)
                {
                    Assert.AreEqual(EcoFish.PerShoal, sh.Alive);
                    Assert.AreEqual(0f, sh.Alarm, "без угроз тревоги нет");
                    foreach (var f in sh.Members)
                    {
                        var p = f.Agent.P;
                        Assert.Less(EcoVec.FlatDistance(p, sh.Home), 30f, "стая не уходит от дома");
                        Assert.Less(EcoVec.Distance(p, sh.Center), 6f, "рыбки держатся стаи");
                        Assert.Greater(p.Y - Seabed.Height(p.X, p.Z), -0.5f, "не под дном");
                        Assert.Less(p.Y, -3f, "не ко льду");
                        Assert.LessOrEqual(f.Agent.V.Length, EcoFish.CalmSpeed + 1e-3f);
                        Assert.AreEqual(EcoState.Graze, f.Agent.State);
                    }
                }
            }
            Assert.AreEqual(0, w.Bus.Pending.Count, "спокойная стая молчит");
        }

        [Test]
        public void ОтХищникаСтаяУходитДомойИПрячется()
        {
            var w = World(out var fish);
            var sh = fish.Shoals[0];
            w.Run(8f); // стая отошла от дома
            float before = MeanToHome(sh);
            Assert.Greater(before, 8f);
            var pred = w.Spawn("pred", sh.Center + new EcoVec(5, 0, 0)); // стоит на месте
            w.Step(0.05f);
            Assert.Greater(sh.Alarm, 0f);
            foreach (var f in sh.Members) Assert.AreNotEqual(EcoState.Graze, f.Agent.State);
            var ev = w.Bus.Take();
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual("fish", ev[0].SpeciesId);
            Assert.AreEqual("flee", ev[0].Note);
            Assert.IsTrue(ev[0].IsObservation);
            Assert.AreEqual("Мелкая рыба: стая уходит в кораллы — рядом хищник", ev[0].Text);

            w.Run(8f);
            Assert.Less(MeanToHome(sh), 5f, "стая у дома в кораллах");
            int hiding = 0;
            foreach (var f in sh.Members)
                if (f.Agent.State == EcoState.Hide)
                {
                    hiding++;
                    Assert.LessOrEqual(f.Agent.V.Length, EcoFish.HideSpeed + 1e-3f, "прячутся медленно");
                }
            Assert.GreaterOrEqual(hiding, 15);
            Assert.AreEqual(EcoFish.FleeCooldown - 8f, sh.FleeCd, 0.02f, "сообщение не чаще раза в 30 с");
            Assert.AreEqual(0f, fish.Shoals[4].Alarm, "дальняя стая спокойна");

            pred.Alive = false; // хищник ушёл — тревога спадает
            w.Run(4f);
            Assert.AreEqual(0f, sh.Alarm);
        }

        [Test]
        public void КорабльНаХодуПугаетБезСообщения()
        {
            var w = World(out var fish);
            var sh = fish.Shoals[0];
            w.Run(8f);
            w.Ship.Pos = sh.Center + new EcoVec(3, 0, 0);
            w.Ship.Speed = 0.5f; // медленно — не пугает
            w.Step(0.05f);
            Assert.AreEqual(0f, sh.Alarm);

            w.Ship.Speed = -2f; // задний ход тоже шумит
            w.Step(0.05f);
            Assert.Greater(sh.Alarm, 0f);
            Assert.IsTrue(sh.ThreatIsShip);
            float near = MeanTo(sh, w.Ship.Pos);
            w.Run(2f);
            Assert.Greater(MeanTo(sh, w.Ship.Pos), near + 4f, "рыбки шарахаются от корабля");
            Assert.AreEqual(0, w.Bus.Pending.Count, "корабль не хищник — сообщения нет");
        }

        [Test]
        public void СъеденнаяРыбкаВозвращаетсяУДома()
        {
            var w = World(out var fish);
            w.Run(1f);
            var f = fish.All[5];
            var at = f.Agent.P;
            fish.Kill(f);
            Assert.IsFalse(f.Agent.Alive);
            Assert.AreEqual(131, w.Count("fish"));
            Assert.AreNotSame(f, fish.NearestFish(at, 50f));
            Assert.IsNull(fish.NearestFish(at, 50f, x => true), "все в укрытии — ловить некого");
            w.Run(24f);
            Assert.IsFalse(f.Agent.Alive, "не раньше 25 с");
            w.Run(16.5f);
            Assert.IsTrue(f.Agent.Alive);
            Assert.AreEqual(132, w.Count("fish"));
            Assert.Less(EcoVec.Distance(f.Agent.P, f.Shoal.Home), 32f, "вернулась к своей стае");
        }

        static string Snapshot(EcoFish fish)
        {
            var parts = new List<string>();
            foreach (var f in fish.All) parts.Add($"{f.Agent.P.X:R},{f.Agent.P.Y:R},{f.Agent.P.Z:R}");
            return string.Join("|", parts);
        }

        [Test]
        public void ОдноЗерноОдниСтаиДругоеДругие()
        {
            var a = World(out var fa);
            var b = World(out var fb);
            var c = World(out var fc, 7);
            a.Run(10f); b.Run(10f); c.Run(10f);
            Assert.AreEqual(Snapshot(fa), Snapshot(fb));
            Assert.AreNotEqual(Snapshot(fa), Snapshot(fc));

            a.Reset();
            Assert.AreEqual(132, fa.All.Count);
            a.Run(10f);
            Assert.AreEqual(Snapshot(fb), Snapshot(fa), "сброс возвращает стаи в начало");
        }

        [Test]
        public void ПослеСканированияСветлякНазываетсяПоИмени()
        {
            var w = World(out var fish);
            w.Scanned = sp => sp == "fish";
            w.Spawn("pred", fish.Shoals[2].Center);
            w.Step(0.05f);
            var ev = w.Bus.Take();
            Assert.AreEqual(1, ev.Count);
            Assert.AreEqual("Светляк-полосник: стая уходит в кораллы — рядом хищник", ev[0].Text);
        }
    }
}
