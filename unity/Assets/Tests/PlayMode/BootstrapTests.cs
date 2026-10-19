using System.Collections;
using NUnit.Framework;
using Pogrujenie.Core;
using Pogrujenie.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pogrujenie.Tests
{
    /// <summary>Заготовка сцены: корабль, камера, светляки и прожектор, который питается от реактора.</summary>
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
        public IEnumerator СобираетКорабльКамеруИСветляков()
        {
            yield return null;
            Assert.AreSame(boot, Bootstrap.Instance);
            Assert.IsNotNull(boot.Ship);
            Assert.IsNotNull(boot.Cam);
            Assert.AreEqual(24, boot.Fireflies.Length);
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
            Assert.AreEqual(Bootstrap.LampRange * Mathf.Sqrt(2f), boot.Lamp.range, 0.01f, "двойная доля — луч в √2 раз дальше, как в браузере");
            Assert.AreEqual(Mathf.Sqrt(2f), boot.Beam.transform.localScale.z, 0.01f, "и видимый конус длиннее");
            boot.Reactor.Set(ShipSystem.Lamp, 0);
            yield return null;
            Assert.IsFalse(boot.Lamp.enabled, "без энергии прожектор гаснет");
            Assert.IsFalse(boot.Beam.enabled, "и конуса луча не видно");
        }

        [UnityTest]
        public IEnumerator СветлякиКружатПередНосом()
        {
            Vector3 a = boot.Fireflies[0].position;
            yield return new WaitForSeconds(0.3f);
            Vector3 b = boot.Fireflies[0].position;
            Assert.Greater(Vector3.Distance(a, b), 0.001f, "стая движется");
            Assert.Less(b.z, boot.Ship.position.z, "стая перед носом");
        }
    }
}
