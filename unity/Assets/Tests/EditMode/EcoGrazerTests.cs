using System.Collections.Generic;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>Скребуны и светящийся мох (grazerUpdate в js/creatures.js, W.patches в js/world.js).</summary>
    public class EcoGrazerTests
    {
        static EcoWorld World(out EcoMoss moss, out EcoGrazer grazers, uint seed = EcoRandom.WorldSeed)
        {
            var w = new EcoWorld(seed) { Ground = Seabed.Height };
            w.Ship.Pos = new EcoVec(500, 0, 500);
            moss = w.Add(new EcoMoss());
            grazers = w.Add(new EcoGrazer(moss));
            return w;
        }

        /// <summary>Ровное дно на −40 м, два пятна в 10 м друг от друга и один скребун.</summary>
        static EcoWorld Pair(out EcoMoss moss, out EcoGrazer grazers)
        {
            var w = new EcoWorld { Ground = (x, z) => -40f };
            w.Ship.Pos = new EcoVec(500, 0, 500);
            moss = w.Add(new EcoMoss(new EcoVec(0, -40, 0), new EcoVec(0, -40, 10)));
            foreach (var p in moss.Patches) p.Food = 1f;
            grazers = w.Add(new EcoGrazer(moss, 1));
            return w;
        }

        static void RunUntil(EcoWorld w, System.Func<bool> done, float max)
        {
            for (float t = 0; t < max && !done(); t += EcoWorld.MaxStep) w.Step(EcoWorld.MaxStep);
            Assert.IsTrue(done(), $"не дождались за {max} с");
        }

        [Test]
        public void ДвадцатьДваПятнаИДесятьСкребунов()
        {
            var w = World(out var moss, out var gr);
            Assert.AreEqual(22, moss.Patches.Count);
            // первое пятно — как в браузере (30,83; 94,44), x со знаком минус
            Assert.AreEqual(-30.831f, moss.Patches[0].Pos.X, 0.01f);
            Assert.AreEqual(94.437f, moss.Patches[0].Pos.Z, 0.01f);
            foreach (var p in moss.Patches)
            {
                Assert.That(p.Food, Is.InRange(0.6f, 1f));
                Assert.AreEqual(Seabed.Height(p.Pos.X, p.Pos.Z), p.Pos.Y, 0.1f, "пятно лежит на дне");
            }
            Assert.AreEqual(EcoGrazer.Count, gr.All.Count);
            Assert.AreEqual(10, w.Count("grazer"));
            for (int i = 0; i < gr.All.Count; i++)
            {
                var g = gr.All[i];
                var home = moss.Patches[(i * 2) % 22].Pos;
                Assert.AreEqual("grazer", g.Agent.SpeciesId);
                Assert.LessOrEqual(EcoVec.FlatDistance(g.Agent.P, home), 4.25f, "скребун у своего пятна");
                Assert.AreEqual(Seabed.Height(g.Agent.P.X, g.Agent.P.Z) + EcoGrazer.Lift, g.Agent.P.Y, 1e-3f);
                Assert.IsNotNull(g.Patch, "сразу знает, к какому пятну идти");
                Assert.AreEqual(EcoState.Seek, g.Agent.State);
            }
        }

        [Test]
        public void МохОтрастаетДоПолного()
        {
            var w = new EcoWorld { Ground = Seabed.Height };
            var moss = w.Add(new EcoMoss()); // без скребунов
            var p = moss.Patches[3];
            p.Food = 0f;
            Assert.AreEqual(EcoMoss.MinGlow, p.Glow, 1e-5f, "объеденное пятно едва светится");
            w.Run(10f);
            Assert.AreEqual(0.12f, p.Food, 0.005f, "за 10 с отрастает 0,12");
            w.Run(80f);
            Assert.AreEqual(1f, p.Food, 1e-5f, "за 90 с — полное, не больше 1");
            Assert.AreEqual(1f, p.Glow, 1e-5f);
            Assert.AreEqual(0.575f, EcoMoss.Glow(0.5f), 1e-5f);
        }

        [Test]
        public void СкребунИдётКПятнуПоДнуИПасётся()
        {
            var w = Pair(out var moss, out var gr);
            var g = gr.All[0];
            for (float t = 0; t < 30f && !g.Grazing; t += EcoWorld.MaxStep)
            {
                w.Step(EcoWorld.MaxStep);
                if (!g.Grazing)
                {
                    Assert.AreEqual(EcoState.Seek, g.Agent.State);
                    Assert.AreEqual(EcoGrazer.WalkSpeed, g.Agent.V.Length, 1e-3f, "идёт шагом 0,9 м/с");
                }
                Assert.AreEqual(-40f + EcoGrazer.Lift, g.Agent.P.Y, 1e-4f, "идёт по дну");
            }
            Assert.IsTrue(g.Grazing, "дошёл до пятна и пасётся");
            Assert.Less(EcoVec.FlatDistance(g.Agent.P, g.Patch.Pos), EcoGrazer.Reach);
            Assert.AreEqual(0f, g.Agent.V.Length, 1e-5f, "пасётся на месте");
        }

        [Test]
        public void ПодСкребуномМохГаснетАПотомОтрастает()
        {
            var w = Pair(out var moss, out var gr);
            var g = gr.All[0];
            RunUntil(w, () => g.Grazing, 30f);
            var eaten = g.Patch;
            var other = moss.Patches[eaten.Index == 0 ? 1 : 0];
            float f0 = eaten.Food;
            w.Run(1f);
            Assert.IsTrue(g.Grazing, "пасётся не меньше 8 с");
            Assert.AreEqual(f0 - (EcoGrazer.EatRate - EcoMoss.Regrow), eaten.Food, 0.003f, "минус 0,05 в секунду, плюс отрастание");
            Assert.Less(eaten.Glow, EcoMoss.Glow(f0), "пятно тускнеет");
            Assert.AreEqual(1f, other.Food, 1e-5f, "соседнее не тронуто");

            // объеденное пятно бросает сразу и идёт к другому
            eaten.Food = 0.05f;
            w.Step(EcoWorld.MaxStep);
            Assert.AreEqual(EcoState.Seek, g.Agent.State);
            Assert.AreSame(other, g.Patch, "выбрал другое пятно");
            float low = eaten.Food;
            w.Run(8f);
            Assert.IsFalse(g.Grazing, "до соседнего пятна больше 8 м, шагом 0,9 м/с — ещё в пути");
            Assert.AreEqual(low + 8f * EcoMoss.Regrow, eaten.Food, 0.005f, "брошенное пятно отрастает");
        }

        [Test]
        public void СообщениеОбедаетМохНеЧащеРазаВ45Секунд()
        {
            var w = Pair(out _, out var gr);
            var times = new List<float>();
            var texts = new List<string>();
            w.Bus.Emitted += ev => { if (ev.SpeciesId == "grazer") { times.Add(w.Time); texts.Add(ev.Text); Assert.AreEqual("graze", ev.Note); Assert.IsTrue(ev.IsObservation); } };
            w.Run(150f);
            Assert.That(times.Count, Is.InRange(1, 4), "пасётся и сообщает");
            for (int i = 1; i < times.Count; i++)
                Assert.GreaterOrEqual(times[i] - times[i - 1], EcoGrazer.GrazeCooldown - 0.01f);
            Assert.AreEqual("Донный панцирник объедает светящийся мох", texts[0], "до сканирования — описательное имя");
            w.Scanned = id => id == "grazer";
            var p = w.Emit("grazer", "graze", EcoVec.Zero, () => $"{Species.Cap(w.Name("grazer"))} объедает светящийся мох");
            Assert.AreEqual("Скребун объедает светящийся мох", p.Text);
        }

        [Test]
        public void ВсеСкребуныПасутсяИОстаютсяНаДне()
        {
            var w = World(out var moss, out var gr);
            var grazed = new bool[gr.All.Count];
            int events = 0;
            w.Bus.Emitted += ev => { if (ev.SpeciesId == "grazer") events++; };
            for (int s = 0; s < 300 * 20; s++)
            {
                w.Step(EcoWorld.MaxStep);
                if (s % 20 != 0) continue;
                for (int i = 0; i < gr.All.Count; i++)
                {
                    var g = gr.All[i];
                    var p = g.Agent.P;
                    if (g.Grazing) grazed[i] = true;
                    Assert.AreEqual(Seabed.Height(p.X, p.Z) + EcoGrazer.Lift, p.Y, 1e-3f, "на дне");
                    Assert.LessOrEqual(System.Math.Abs(p.X), EcoAgent.Edge);
                    Assert.LessOrEqual(System.Math.Abs(p.Z), EcoAgent.Edge);
                }
                foreach (var pt in moss.Patches) Assert.That(pt.Food, Is.InRange(0f, 1f));
            }
            for (int i = 0; i < grazed.Length; i++) Assert.IsTrue(grazed[i], $"скребун {i} за 5 минут попасся");
            Assert.That(events, Is.InRange(10, 10 * 8), "каждый сообщает, но не чаще раза в 45 с");
        }

        [Test]
        public void СъеденныйСкребунВозвращаетсяУПятна()
        {
            var w = World(out var moss, out var gr);
            var g = gr.All[4];
            Assert.AreSame(g, gr.NearestGrazer(g.Agent.P, 0.5f));
            gr.Kill(g);
            Assert.IsFalse(g.Agent.Alive);
            Assert.AreEqual(9, w.Count("grazer"));
            Assert.AreNotSame(g, gr.NearestGrazer(g.Agent.P, 0.5f), "съеденного не найти");
            w.Run(39f);
            Assert.IsFalse(g.Agent.Alive, "не раньше 40 с");
            RunUntil(w, () => g.Agent.Alive, 22f);
            Assert.AreEqual(10, w.Count("grazer"));
            Assert.LessOrEqual(EcoVec.FlatDistance(g.Agent.P, moss.Nearest(g.Agent.P).Pos), 4.3f, "появился у пятна мха");
            Assert.AreEqual(EcoState.Seek, g.Agent.State);
            Assert.IsNotNull(g.Patch);
        }

        [Test]
        public void ОдноЗерноОдинМир()
        {
            var a = World(out var ma, out var ga);
            var b = World(out var mb, out var gb);
            var c = World(out _, out var gc, 777);
            a.Run(60f); b.Run(60f); c.Run(60f);
            bool differs = false;
            for (int i = 0; i < ga.All.Count; i++)
            {
                Assert.AreEqual(ga.All[i].Agent.P, gb.All[i].Agent.P);
                if (!ga.All[i].Agent.P.Equals(gc.All[i].Agent.P)) differs = true;
            }
            for (int i = 0; i < ma.Patches.Count; i++) Assert.AreEqual(ma.Patches[i].Food, mb.Patches[i].Food);
            Assert.IsTrue(differs, "другое зерно — другой мир");

            a.Reset();
            var fresh = World(out _, out var gf);
            for (int i = 0; i < ga.All.Count; i++) Assert.AreEqual(gf.All[i].Agent.P, ga.All[i].Agent.P, "сброс — с начала");
        }

        [Test]
        public void СкребуныНеСдвигаютСветляков()
        {
            var a = new EcoWorld { Ground = Seabed.Height };
            var fa = a.Add(new EcoFish());
            var b = new EcoWorld { Ground = Seabed.Height };
            var fb = b.Add(new EcoFish());
            var mb = b.Add(new EcoMoss());
            b.Add(new EcoGrazer(mb));
            a.Ship.Pos = b.Ship.Pos = new EcoVec(500, 0, 500);
            a.Run(20f); b.Run(20f);
            for (int i = 0; i < fa.All.Count; i++) Assert.AreEqual(fa.All[i].Agent.P, fb.All[i].Agent.P);
        }
    }
}
