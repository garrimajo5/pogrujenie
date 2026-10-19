using System;
using System.Collections.Generic;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>Ядро экосистемы: реестр видов, случай по зерну, шина событий, состояния и шаг мира.</summary>
    public class EcoCoreTests
    {
        // ---------- Случай по зерну (js/util.js, mulberry32) ----------

        [Test]
        public void СлучайСовпадаетСБраузером()
        {
            // значения G.rng() из node для зерна мира и для зерна огнецвета G.seed(7031)
            var r = new EcoRandom();
            Assert.AreEqual(0.6241667082, r.Next(), 1e-9);
            Assert.AreEqual(0.7992063749, r.Next(), 1e-9);
            Assert.AreEqual(0.4817302453, r.Next(), 1e-9);
            var s = EcoWorld.SeededFor(7031);
            Assert.AreEqual(0.5156495699, s.Next(), 1e-9);
            Assert.AreEqual(0.9910667124, s.Next(), 1e-9);
            Assert.AreEqual(0.6177227497, s.Next(), 1e-9);
        }

        [Test]
        public void СлучайВГраницах()
        {
            var r = new EcoRandom(42);
            var items = new[] { "а", "б", "в" };
            var hit = new HashSet<string>();
            for (int i = 0; i < 2000; i++)
            {
                double x = r.Next();
                Assert.That(x, Is.GreaterThanOrEqualTo(0).And.LessThan(1));
                float v = r.Range(-3f, 5f);
                Assert.That(v, Is.GreaterThanOrEqualTo(-3f).And.LessThanOrEqualTo(5f));
                int k = r.Index(7);
                Assert.That(k, Is.InRange(0, 6));
                hit.Add(r.Pick(items));
            }
            Assert.AreEqual(3, hit.Count, "выпадает каждый элемент");
        }

        [Test]
        public void ОдноЗерноОднаПоследовательность()
        {
            var a = new EcoRandom(5); var b = new EcoRandom(5); var c = new EcoRandom(6);
            bool differs = false;
            for (int i = 0; i < 50; i++)
            {
                double x = a.Next();
                Assert.AreEqual(x, b.Next());
                if (x != c.Next()) differs = true;
            }
            Assert.IsTrue(differs, "другое зерно — другие числа");
        }

        // ---------- Реестр видов (G.SPECIES) ----------

        [Test]
        public void РеестрВидовКакВБраузере()
        {
            Assert.AreEqual(26, Species.Count);
            Assert.AreEqual("fish", Species.All[0].Id);
            Assert.AreEqual("echo", Species.All[25].Id);
            Assert.AreEqual(1, Species.IndexOf("pred"));
            Assert.AreEqual(-1, Species.IndexOf("кракен"));
            int notes = 0;
            var ids = new HashSet<string>();
            foreach (var s in Species.All)
            {
                Assert.IsTrue(ids.Add(s.Id), "id не повторяются: " + s.Id);
                Assert.IsFalse(string.IsNullOrEmpty(s.Name), s.Id);
                Assert.IsFalse(string.IsNullOrEmpty(s.Acc), s.Id);
                Assert.IsFalse(string.IsNullOrEmpty(s.UNom), s.Id);
                Assert.IsFalse(string.IsNullOrEmpty(s.UAcc), s.Id);
                Assert.IsFalse(string.IsNullOrEmpty(s.Desc), s.Id);
                Assert.IsNotEmpty(s.Notes, s.Id);
                var keys = new HashSet<string>();
                foreach (var n in s.Notes)
                {
                    Assert.IsTrue(keys.Add(n.Key), s.Id + ": ключ заметки повторяется " + n.Key);
                    Assert.IsFalse(string.IsNullOrEmpty(n.Text), s.Id + "." + n.Key);
                }
                notes += s.Notes.Length;
            }
            Assert.AreEqual(77, notes, "заметок для журнала столько же, сколько в браузере");
        }

        [Test]
        public void НазваниеВПадежах()
        {
            Assert.AreEqual("мелкая рыба", Species.Called("fish", NameCase.Nom, false));
            Assert.AreEqual("мелкую рыбу", Species.Called("fish", NameCase.Acc, false));
            Assert.AreEqual("мелкую рыбу", Species.Called("fish", NameCase.Gen, false), "нет родительного — винительный");
            Assert.AreEqual("светляк-полосник", Species.Called("fish", NameCase.Nom, true));
            Assert.AreEqual("светляка-полосника", Species.Called("fish", NameCase.Acc, true));
            Assert.AreEqual("донного анемона", Species.Called("hvaten", NameCase.Gen, false));
            Assert.AreEqual("хватня", Species.Called("hvaten", NameCase.Gen, true));
            Assert.AreEqual("Серпохвост", Species.Cap(Species.Called("pred", NameCase.Nom, true)));
            Assert.IsTrue(Species.Get("grazer").HasNote("graze"));
            Assert.IsFalse(Species.Get("grazer").HasNote("catch"));
            Assert.Throws<ArgumentException>(() => Species.Get("кракен"));
        }

        // ---------- Шина событий (E.emit) ----------

        [Test]
        public void ШинаОтдаётСобытияПоПорядкуИОчищается()
        {
            var bus = new EcoBus();
            var heard = new List<string>();
            bus.Emitted += e => heard.Add(e.Note);
            bool scanned = false;
            bus.Emit("pred", "catch", new EcoVec(1, -10, 2),
                () => $"{Species.Cap(Species.Called("pred", NameCase.Nom, scanned))} поймал {Species.Called("fish", NameCase.Acc, scanned)}");
            bus.Emit("leech", "signal", new EcoVec(0, -10, 0));
            CollectionAssert.AreEqual(new[] { "catch", "signal" }, heard, "подписчик узнаёт сразу");
            Assert.AreEqual(2, bus.Pending.Count);

            scanned = true; // текст собирается в момент показа
            var got = bus.Take();
            Assert.AreEqual(2, got.Count);
            Assert.AreEqual("Серпохвост поймал светляка-полосника", got[0].Text);
            Assert.IsTrue(got[0].IsObservation);
            Assert.IsNull(got[1].Text);
            Assert.IsFalse(got[1].IsObservation, "у пиявника нет заметки signal — только сигнал");
            Assert.IsTrue(new EcoEvent("leech", "cleaned", EcoVec.Zero, null).IsObservation, "cleaned — заметка журнала пиявника");
            Assert.AreEqual(0, bus.Pending.Count);
            Assert.Throws<ArgumentException>(() => bus.Emit("кракен", "x", EcoVec.Zero));
        }

        [Test]
        public void ДальниеСобытияЭкипажНеСлышит()
        {
            var bus = new EcoBus();
            bus.Emit("fish", "flee", new EcoVec(0, -10, 50));
            bus.Emit("fish", "kelp", new EcoVec(0, -10, 200));
            var near = bus.Take(new EcoVec(0, -10, 0));
            Assert.AreEqual(1, near.Count);
            Assert.AreEqual("flee", near[0].Note);
            Assert.AreEqual(0, bus.Pending.Count, "дальние выброшены");
        }

        // ---------- Существо: состояния и движение ----------

        [Test]
        public void СуществоПлывётКЦелиИНеПроскакивает()
        {
            var a = new EcoAgent("pred", new EcoVec(0, -20, 0));
            var goal = new EcoVec(30, -20, -40);
            for (int i = 0; i < 600; i++) a.SteerTo(goal, 6f, 0.05f);
            Assert.Less(a.DistanceTo(goal), 0.5f);
            Assert.Less(a.V.Length, 0.5f, "у цели почти стоит");
        }

        [Test]
        public void СуществоУбегаетОтУгрозы()
        {
            var a = new EcoAgent("fish", new EcoVec(0, -20, 0));
            var threat = new EcoVec(5, -20, 0);
            float d0 = a.DistanceTo(threat);
            for (int i = 0; i < 40; i++) a.SteerAway(threat, 5f, 0.05f);
            Assert.Greater(a.DistanceTo(threat), d0 + 3f);
            Assert.Less(a.P.X, 0f, "уходит в сторону от угрозы");
            Assert.AreEqual(-20f, a.P.Y, 1e-4f, "по горизонтали");
        }

        [Test]
        public void СуществоНеУходитПодДноЗаЛёдИЗаКрай()
        {
            Func<float, float, float> ground = (x, z) => -30f;
            var a = new EcoAgent("grazer", new EcoVec(0, -25, 0));
            for (int i = 0; i < 200; i++) a.SteerTo(new EcoVec(0, -60, 0), 5f, 0.05f, ground);
            Assert.AreEqual(-30f + a.Clearance, a.P.Y, 1e-4f, "держится над дном");
            for (int i = 0; i < 200; i++) a.SteerTo(new EcoVec(0, 10, 0), 5f, 0.05f, ground);
            Assert.AreEqual(a.Ceiling, a.P.Y, 1e-4f, "не выше потолка");
            for (int i = 0; i < 2000; i++) a.SteerTo(new EcoVec(900, -10, -900), 9f, 0.05f, ground);
            Assert.AreEqual(EcoAgent.Edge, a.P.X, 1e-4f);
            Assert.AreEqual(-EcoAgent.Edge, a.P.Z, 1e-4f);
        }

        [Test]
        public void ТаймерСостоянияИстекает()
        {
            var a = new EcoAgent("fish", EcoVec.Zero);
            var b = new EcoAgent("pred", EcoVec.Zero);
            a.Set(EcoState.Hide, 1f, b);
            Assert.AreEqual(EcoState.Hide, a.State);
            Assert.AreSame(b, a.Target);
            a.Tick(0.6f); Assert.IsFalse(a.Expired);
            a.Tick(0.6f); Assert.IsTrue(a.Expired);
            Assert.AreEqual(0f, a.Timer);
            Assert.Throws<ArgumentException>(() => new EcoAgent("кракен", EcoVec.Zero));
        }

        // ---------- Мир: шаг без рендера ----------

        /// <summary>
        /// Учебная связь двух видов для проверки ядра: скребуны пасутся у дома, серпохвост бродит и,
        /// заметив скребуна ближе 15 м, охотится; пойманный скребун гибнет. Скребун, к которому
        /// хищник ближе 8 м, убегает, потом прячется. Числа условные — настоящие виды придут своими задачами.
        /// </summary>
        sealed class Toy : IEcoSystem
        {
            public int Caught;

            public void Reset(EcoWorld w)
            {
                Caught = 0;
                var r = EcoWorld.SeededFor(1234);
                for (int i = 0; i < 6; i++)
                    w.Spawn("grazer", new EcoVec(r.Range(-20, 20), -20, r.Range(-20, 20)));
                w.Spawn("pred", new EcoVec(0, -15, 40));
            }

            public void Step(EcoWorld w, float dt)
            {
                foreach (var a in w.Agents)
                {
                    if (!a.Alive) continue;
                    if (a.SpeciesId == "pred") Pred(w, a, dt);
                    else Grazer(w, a, dt);
                }
            }

            void Pred(EcoWorld w, EcoAgent p, float dt)
            {
                if (p.State != EcoState.Hunt)
                {
                    var prey = w.Nearest("grazer", p.P, 15f, g => g.State != EcoState.Hide);
                    if (prey != null) { p.Set(EcoState.Hunt, 10f, prey); w.Emit("pred", "catch", p.P); }
                    else
                    {
                        if (p.Expired || EcoVec.Distance(p.P, p.Goal) < 2f)
                        {
                            p.Goal = new EcoVec(w.Random.Range(-30, 30), -15, w.Random.Range(-30, 30));
                            p.Timer = 8f;
                        }
                        p.SteerTo(p.Goal, 3f, dt, w.Ground);
                    }
                    return;
                }
                var t = p.Target;
                if (t == null || !t.Alive || t.State == EcoState.Hide || p.Expired) { p.Set(EcoState.Graze); return; }
                p.SteerTo(t.P, 7f, dt, w.Ground);
                if (p.DistanceTo(t.P) < 1.2f) { t.Alive = false; Caught++; p.Set(EcoState.Graze); }
            }

            void Grazer(EcoWorld w, EcoAgent g, float dt)
            {
                var pred = w.Nearest("pred", g.P, 8f);
                switch (g.State)
                {
                    case EcoState.Graze:
                        if (pred != null) { g.Set(EcoState.Flee, 3f, pred); break; }
                        if (EcoVec.Distance(g.P, g.Goal) < 0.5f)
                            g.Goal = g.Home + new EcoVec(w.Random.Range(-3, 3), 0, w.Random.Range(-3, 3));
                        g.SteerTo(g.Goal, 0.6f, dt, w.Ground);
                        break;
                    case EcoState.Flee:
                        g.SteerAway(g.Target.P, 4f, dt, w.Ground);
                        if (g.Expired) g.Set(EcoState.Hide, 6f);
                        break;
                    case EcoState.Hide:
                        g.Hold(dt, w.Ground);
                        if (g.Expired) g.Set(EcoState.Seek);
                        break;
                    case EcoState.Seek:
                        g.SteerTo(g.Home, 1.5f, dt, w.Ground);
                        if (EcoVec.Distance(g.P, g.Home) < 1f) { g.Set(EcoState.Graze); g.Goal = g.P; }
                        break;
                }
            }
        }

        static string Snapshot(EcoWorld w)
        {
            var parts = new List<string>();
            foreach (var a in w.Agents) parts.Add($"{a.SpeciesId}:{a.State}:{a.Alive}:{a.P.X:R},{a.P.Y:R},{a.P.Z:R}");
            return string.Join("|", parts);
        }

        static EcoWorld ToyWorld(uint seed = EcoRandom.WorldSeed)
        {
            var w = new EcoWorld(seed) { Ground = (x, z) => -24f };
            w.Add(new Toy());
            return w;
        }

        [Test]
        public void ШагДробитсяНаПодшаги()
        {
            var w = new EcoWorld();
            w.Step(0.2f);
            Assert.AreEqual(4, w.Steps);
            Assert.AreEqual(0.2f, w.Time, 1e-5f);
            w.Step(0.05f);
            Assert.AreEqual(5, w.Steps);
            w.Step(0f);
            Assert.AreEqual(5, w.Steps, "нулевой шаг ничего не делает");
        }

        [Test]
        public void МирДетерминирован()
        {
            var a = ToyWorld(); var b = ToyWorld();
            a.Run(60f); b.Run(60f);
            Assert.AreEqual(Snapshot(a), Snapshot(b), "одно зерно — один и тот же мир");

            var c = ToyWorld(99);
            c.Run(60f);
            Assert.AreNotEqual(Snapshot(a), Snapshot(c), "другое зерно — другой путь хищника");
        }

        [Test]
        public void СбросВозвращаетМирВНачало()
        {
            var w = ToyWorld();
            string start = Snapshot(w);
            w.Run(30f);
            Assert.AreNotEqual(start, Snapshot(w));
            w.Reset();
            Assert.AreEqual(start, Snapshot(w));
            Assert.AreEqual(0f, w.Time);
            Assert.AreEqual(0, w.Bus.Pending.Count);
            w.Run(30f);
            var again = ToyWorld(); again.Run(30f);
            Assert.AreEqual(Snapshot(again), Snapshot(w), "после сброса мир идёт тем же путём");
        }

        [Test]
        public void ВсеПятьСостоянийВСимуляции()
        {
            var w = ToyWorld();
            var seen = new HashSet<EcoState>();
            for (int i = 0; i < 2400; i++)
            {
                w.Step(0.05f);
                foreach (var a in w.Agents) seen.Add(a.State);
            }
            CollectionAssert.AreEquivalent((EcoState[])Enum.GetValues(typeof(EcoState)), seen);
            var toy = (Toy)w.Systems[0];
            Assert.Greater(toy.Caught, 0, "хищник кого-то поймал");
            Assert.AreEqual(6 - toy.Caught, w.Count("grazer"));
            Assert.AreEqual(6, w.Count("grazer", aliveOnly: false));
            Assert.Greater(w.Bus.Take().Count, 0, "охота шлёт события");
            foreach (var a in w.Agents) Assert.GreaterOrEqual(a.P.Y, -24f + a.Clearance - 1e-4f, "никто не под дном");
        }
    }
}
