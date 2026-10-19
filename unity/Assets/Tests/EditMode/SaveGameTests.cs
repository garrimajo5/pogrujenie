using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// Сохранение груза трюма и поднятых образцов (перенос js/save.js и M.restore):
    /// снимок той же формы, что в браузере, неверные записи отбрасываются, битый снимок не трогает игру.
    /// </summary>
    public class SaveGameTests
    {
        /// <summary>Поднять образец: корабль не нужен, держим кнопку на образце и ждём подъёма.</summary>
        static void Lift(Samples m, int i)
        {
            var s = m.All[i];
            for (int k = 0; k < 40 && m.State != GrabState.Back; k++) { m.Hold(s, 0.05f); m.Update(0.05f); }
            for (int k = 0; k < 40 && m.State != GrabState.Idle; k++) m.Update(0.05f);
            Assert.IsTrue(m.Got.Contains(i));
        }

        static string Bottom(Samples m) => string.Concat(m.All.Select(s => s.Taken ? "1" : "0"));
        static string Hold(Samples m) => string.Join(",", m.Cargo.Select(c => SaveGame.Key(c.Kind) + ":" + c.I));

        [Test]
        public void СнимокВФорматеБраузера()
        {
            var m = new Samples();
            Assert.AreEqual("{\"v\":1,\"cargo\":[],\"got\":[]}", SaveGame.Write(m));
            Lift(m, 10); Lift(m, 0);
            Assert.AreEqual("{\"v\":1,\"cargo\":[{\"kind\":\"coral\",\"i\":10},{\"kind\":\"moss\",\"i\":0}],\"got\":[0,10]}", SaveGame.Write(m));
            m.Restore(new[] { 3 }, new[] { new CargoItem(SampleKind.Shell, -1) });
            Assert.AreEqual("{\"v\":1,\"cargo\":[{\"kind\":\"shell\",\"i\":null}],\"got\":[3]}", SaveGame.Write(m), "груз не со дна — i:null");
        }

        [Test]
        public void ПоднятоеПереживаетПерезапуск()
        {
            var a = new Samples();
            Lift(a, 0); Lift(a, 13); Lift(a, 8);
            string snap = SaveGame.Write(a);

            var b = new Samples();
            int v = b.Version;
            Assert.IsTrue(SaveGame.Read(snap, b));
            Assert.AreEqual(Hold(a), Hold(b), "груз трюма по порядку");
            Assert.AreEqual("moss:0,spine:13,shell:8", Hold(b));
            CollectionAssert.AreEquivalent(new[] { 0, 8, 13 }, b.Got);
            Assert.AreEqual(Bottom(a), Bottom(b), "поднятые не лежат на дне");
            Assert.Greater(b.Version, v, "дно перерисуется");
            Assert.AreEqual(GrabState.Idle, b.State);
            Assert.AreEqual("Ракушка моллюска", b.Cargo[2].Name);
        }

        [Test]
        public void ЧитаетСнимокБраузернойВерсии()
        {
            // так пишет js/save.js: виды, места, улучшения и обучение здесь пропускаются
            string js = "{ \"v\": 1, \"species\": {\"serpohvost\": {\"scanned\": true, \"seen\": [0, 2]}}," +
                        " \"pois\": [{\"key\": \"grot\", \"found\": true, \"pinged\": false}]," +
                        " \"cargo\": [{\"kind\": \"spine\", \"i\": 13}, {\"kind\": \"moss\", \"i\": null}]," +
                        " \"got\": [13, 5], \"ups\": [\"lamp\"], \"tutorial\": {\"step\": 4, \"text\": \"Сонар \\u2014 \\\"голостол\\\"\"} }";
            var m = new Samples();
            Assert.IsTrue(SaveGame.Read(js, m));
            Assert.AreEqual("spine:13,moss:-1", Hold(m));
            CollectionAssert.AreEquivalent(new[] { 5, 13 }, m.Got, "поднятый и уже потраченный образец 5 тоже не на дне");
            Assert.IsTrue(m.All[5].Taken);
            Assert.IsTrue(m.All[13].Taken);
            Assert.AreEqual(12, m.All.Count(s => !s.Taken));
        }

        [Test]
        public void НеверныеЗаписиГрузаОтбрасываются()
        {
            string snap = "{\"v\":1,\"cargo\":[" +
                          "{\"kind\":\"moss\",\"i\":0}," +   // верно
                          "{\"kind\":\"moss\",\"i\":0}," +   // тот же образец второй раз
                          "{\"kind\":\"moss\",\"i\":10}," +  // образец 10 — огнецвет, не мох
                          "{\"kind\":\"moss\",\"i\":99}," +  // такого нет
                          "{\"kind\":\"moss\",\"i\":-1}," +  // отрицательный номер
                          "{\"kind\":\"moss\",\"i\":1.5}," + // дробный
                          "{\"kind\":\"rock\",\"i\":null}," + // неизвестный вид
                          "{\"i\":2}, 7, null, \"coral\"," +
                          "{\"kind\":\"coral\"}" +           // без номера — не со дна
                          "],\"got\":[3, 3, 99, -2, \"4\", 2.5]}";
            var m = new Samples();
            Assert.IsTrue(SaveGame.Read(snap, m));
            Assert.AreEqual("moss:0,coral:-1", Hold(m));
            CollectionAssert.AreEquivalent(new[] { 0, 3 }, m.Got);
            Assert.AreEqual(12, m.All.Count(s => !s.Taken));
        }

        [Test]
        public void БитыйСнимокНеТрогаетИгру()
        {
            var m = new Samples();
            Lift(m, 0);
            string before = SaveGame.Write(m);
            foreach (var bad in new[] { null, "", "   ", "{", "{\"v\":1,\"cargo\":[}", "[1,2]", "\"текст\"", "{\"v\":2,\"cargo\":[]}",
                                        "{\"cargo\":[]}", "{\"v\":\"1\"}", "{\"v\":1} лишнее", "nul", "{\"v\":1,\"got\":[1,]}" })
            {
                Assert.IsFalse(SaveGame.Read(bad, m), bad ?? "null");
                Assert.AreEqual(before, SaveGame.Write(m), bad ?? "null");
            }
            Assert.IsTrue(SaveGame.Read("{\"v\":1}", m), "пустой, но наш снимок — трюм пуст");
            Assert.AreEqual(0, m.Cargo.Count);
            Assert.AreEqual(14, m.All.Count(s => !s.Taken));
        }

        [Test]
        public void ИзменениеГрузаПросятЗаписать()
        {
            var m = new Samples();
            int n = 0;
            m.Changed += () => n++;
            var s = m.All[0];
            for (int k = 0; k < 10; k++) { m.Hold(s, 0.05f); m.Update(0.05f); }
            Assert.AreEqual(0, n, "захват ещё не закончен — сохранять нечего");
            Lift(m, 0);
            Assert.AreEqual(1, n, "образец лёг в трюм");
            m.Reset();
            Assert.AreEqual(2, n, "сброс тоже сохраняется");
            m.Restore(new[] { 1 }, null);
            Assert.AreEqual(2, n, "чтение сохранения само себя не перезаписывает");
        }

        [Test]
        public void ВидыПоКлючамБраузера()
        {
            var keys = new Dictionary<SampleKind, string>
            {
                { SampleKind.Moss, "moss" }, { SampleKind.Shell, "shell" }, { SampleKind.Coral, "coral" }, { SampleKind.Spine, "spine" },
            };
            foreach (var kv in keys)
            {
                Assert.AreEqual(kv.Value, SaveGame.Key(kv.Key));
                Assert.IsTrue(SaveGame.TryKind(kv.Value, out var k));
                Assert.AreEqual(kv.Key, k);
            }
            Assert.IsFalse(SaveGame.TryKind("Moss", out _));
        }
    }
}
