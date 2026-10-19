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
            bus.Emit("leech", "cleaned", new EcoVec(0, -10, 0));
            CollectionAssert.AreEqual(new[] { "catch", "cleaned" }, heard, "подписчик узнаёт сразу");
            Assert.AreEqual(2, bus.Pending.Count);

            scanned = true; // текст собирается в момент показа
            var got = bus.Take();
            Assert.AreEqual(2, got.Count);
            Assert.AreEqual("Серпохвост поймал светляка-полосника", got[0].Text);
            Assert.IsTrue(got[0].IsObservation);
            Assert.IsNull(got[1].Text);
            Assert.IsFalse(got[1].IsObservation, "у пиявника нет заметки cleaned — только сигнал");
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
    }
}
