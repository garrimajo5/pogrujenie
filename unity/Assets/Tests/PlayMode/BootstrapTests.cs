using System.Collections;
using NUnit.Framework;
using Pogrujenie.Core;
using Pogrujenie.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pogrujenie.Tests
{
    /// <summary>Заготовка сцены: корабль, камера, живой мир отмели и прожектор, который питается от реактора.</summary>
    public class BootstrapTests
    {
        Bootstrap boot;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            boot = new GameObject("Погружение").AddComponent<Bootstrap>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (boot != null) Object.Destroy(boot.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator СобираетКорабльКамеруИЖивойМир()
        {
            yield return null;
            Assert.AreSame(boot, Bootstrap.Instance);
            Assert.IsNotNull(boot.Ship);
            Assert.IsNotNull(boot.Cam);
            Assert.IsNotNull(boot.Eco);
            Assert.AreEqual(132, boot.Eco.Fish.All.Count, "шесть стай светляков по 22 вместо тестовой стайки");
            Assert.IsTrue(RenderSettings.fog, "в воде туман");
            Assert.Less(boot.Lamp.transform.position.z, boot.Ship.position.z, "прожектор на носу, нос в −Z");
        }

        [UnityTest]
        public IEnumerator ПрожекторБерётСилуИзРеактора()
        {
            Assert.IsTrue(boot.Lamp.enabled);
            Assert.AreEqual(Bootstrap.LampRange, boot.Lamp.range, 0.01f);
            boot.Reactor.Set(ShipSystem.Engines, 20); // освобождаем 10 единиц
            boot.Reactor.Set(ShipSystem.Lamp, 20);
            yield return null;
            Assert.AreEqual(Bootstrap.LampRange * 2f, boot.Lamp.range, 0.01f, "двойная доля — луч вдвое дальше");
            boot.Reactor.Set(ShipSystem.Lamp, 0);
            yield return null;
            Assert.IsFalse(boot.Lamp.enabled, "без энергии прожектор гаснет");
        }

        [UnityTest]
        public IEnumerator СветлякиПлаваютПередНосом()
        {
            Vector3 a = boot.Eco.DrawnAt(0);
            yield return new WaitForSeconds(0.3f);
            Vector3 b = boot.Eco.DrawnAt(0);
            Assert.Greater(Vector3.Distance(a, b), 0.001f, "стая движется");
            Assert.Less(b.z, boot.Ship.position.z, "ближняя стая перед носом");
        }
    }
}
