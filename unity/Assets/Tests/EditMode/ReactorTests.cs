using System.Collections.Generic;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>Реактор и доли систем: те же правила, что в браузерной версии (js/power.js).</summary>
    public class ReactorTests
    {
        static void Run(Reactor r, float seconds, bool nearVent = false, float dt = 0.1f)
        {
            for (float t = 0; t < seconds - 1e-4f; t += dt) r.Update(dt, nearVent);
        }

        [Test]
        public void ПоУмолчаниюРазданыВсе100Единиц()
        {
            var r = new Reactor();
            Assert.AreEqual(100, r.Used);
            Assert.AreEqual(0, r.Free);
            Assert.AreEqual(100, r.Limit);
            foreach (var s in Reactor.Systems) Assert.AreEqual(1f, r.K(s), 1e-6f, Reactor.Name(s));
        }

        [Test]
        public void ДоляНеБольше60ИНеБольшеСвободных()
        {
            var r = new Reactor();
            Assert.AreEqual(30, r.Set(ShipSystem.Engines, 90), "свободных нет — двигатели остаются на 30");
            r.Set(ShipSystem.Lamp, 0);
            r.Set(ShipSystem.Zap, 0);
            Assert.AreEqual(55, r.Set(ShipSystem.Engines, 90), "свободно 25 — двигатели 30 + 25");
            r.Set(ShipSystem.Sonar, 0);
            Assert.AreEqual(60, r.Set(ShipSystem.Engines, 90), "больше 60 на одну систему нельзя");
            Assert.AreEqual(0, r.Set(ShipSystem.Lamp, -10), "меньше нуля нельзя");
        }

        [Test]
        public void ДоляОкругляетсяДоШага5()
        {
            var r = new Reactor();
            Assert.AreEqual(10, r.Set(ShipSystem.Lamp, 12));
            Assert.AreEqual(5, r.Set(ShipSystem.Lamp, 7)); // 7/5 = 1,4 → 5
            Assert.AreEqual(10, r.Set(ShipSystem.Lamp, 8)); // 1,6 → 10
            Assert.AreEqual(0, r.Add(ShipSystem.Lamp, -10));
            Assert.AreEqual(5, r.Add(ShipSystem.Lamp, 5));
        }

        [Test]
        public void МножительСистемыОтБазовойДоли()
        {
            var r = new Reactor();
            r.Set(ShipSystem.Lamp, 5);
            Assert.AreEqual(0.5f, r.K(ShipSystem.Lamp), 1e-6f);
            r.Set(ShipSystem.Lamp, 0);
            Assert.AreEqual(0f, r.K(ShipSystem.Lamp), "при нуле система обесточена");
        }

        [Test]
        public void ПерегревЧерез240СекундИЛимит60()
        {
            var r = new Reactor();
            Run(r, 239f);
            Assert.IsFalse(r.Hot);
            Run(r, 1.5f);
            Assert.IsTrue(r.Hot);
            Assert.AreEqual(60, r.Limit);
            Assert.GreaterOrEqual(r.Heat, 20f);
        }

        [Test]
        public void УКурильщикаПерегревСразуИНеЧащеРазаВ90Секунд()
        {
            var r = new Reactor();
            var msgs = new List<string>();
            r.Message += (title, text) => msgs.Add(title + ": " + text);
            r.Update(0.1f, true);
            Assert.IsTrue(r.Hot);
            StringAssert.Contains("курильщика", msgs[0]);
            Assert.AreEqual(90f, r.VentCd, 0.01f);
        }

        [Test]
        public void ПерегрузкаДоводитДоАварийногоСброса()
        {
            var r = new Reactor();
            var titles = new List<string>();
            r.Message += (title, text) => titles.Add(title);
            r.StartHot(false);
            // 100 при лимите 60: +4,8° в секунду, с 20° до 100° примерно за 17 с
            Run(r, 16f);
            Assert.AreEqual(0f, r.Scram);
            Run(r, 1.5f);
            Assert.Greater(r.Scram, 10f, "сброс на 12 с только начался");
            Assert.Contains("Аварийный сброс реактора", titles);
            foreach (var s in Reactor.Systems) Assert.AreEqual(0f, r.K(s), "при сбросе всё обесточено: " + Reactor.Name(s));
            Run(r, 12.1f);
            Assert.AreEqual(0f, r.Scram);
            Assert.AreEqual(1f, r.K(ShipSystem.Engines), 1e-6f, "после перезапуска системы снова работают");
            Assert.Contains("Реактор перезапущен", titles);
        }

        [Test]
        public void ЕслиУрезатьНагрузкуРеакторОстывает()
        {
            var r = new Reactor();
            var titles = new List<string>();
            r.Message += (title, text) => titles.Add(title);
            r.StartHot(false);
            r.Set(ShipSystem.Engines, 0);
            r.Set(ShipSystem.Lamp, 0); // 100 − 30 − 10 = 60
            Assert.AreEqual(60, r.Used);
            Run(r, 5f);
            Assert.Less(r.Heat, 20f, "при 60 остывает примерно на 1,25° в секунду");
            Run(r, 20f);
            Assert.IsFalse(r.Hot, "остыл, и прошло больше 20 с");
            Assert.AreEqual(100, r.Limit);
            Assert.Greater(r.NextHot, Reactor.Period - 10f, "следующий перегрев не раньше чем через 7 минут");
            Assert.Contains("Реактор остыл", titles);
        }

        [Test]
        public void СбросВозвращаетБазовыеДоли()
        {
            var r = new Reactor();
            r.Set(ShipSystem.Lamp, 0);
            r.StartHot(false);
            r.Reset();
            Assert.AreEqual(10, r[ShipSystem.Lamp]);
            Assert.IsFalse(r.Hot);
            Assert.AreEqual(Reactor.FirstHot, r.NextHot);
        }
    }
}
