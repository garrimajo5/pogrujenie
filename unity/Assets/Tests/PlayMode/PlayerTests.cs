using System.Collections;
using NUnit.Framework;
using Pogrujenie.Core;
using Pogrujenie.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pogrujenie.Tests
{
    /// <summary>Игрок на мостике: крестовина, свайп и кнопка действия в настоящей сцене.</summary>
    public class PlayerTests
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
        public IEnumerator КрестовинаВедётКамеру()
        {
            var cam = boot.Cam.transform;
            float x0 = cam.localPosition.x;
            Assert.AreEqual(Walker.Eye, cam.localPosition.y, 0.01f, "камера на высоте глаз");
            Assert.Less(cam.forward.z, -0.9f, "смотрим на нос");
            // вправо от взгляда (лицом к носу правый борт — −X); вперёд мешает кресло пилота
            boot.Controls.PadOverride = new Vector2Int(1, 0);
            yield return new WaitForSeconds(0.5f);
            boot.Controls.PadOverride = null;
            Assert.Less(cam.localPosition.x, x0 - 0.5f, "отошли к правому борту");
            float x1 = cam.localPosition.x;
            yield return null;
            yield return null;
            Assert.AreEqual(x1, cam.localPosition.x, 0.01f, "отпустили крестовину — стоим");
        }

        [UnityTest]
        public IEnumerator КреслоПилотаНеПропускает()
        {
            boot.Controls.PadOverride = new Vector2Int(0, 1);
            yield return new WaitForSeconds(0.6f);
            boot.Controls.PadOverride = null;
            Assert.Greater(boot.Player.Walker.Z, -3.95f + 0.7f - 0.01f, "кресло на z=−3,95 стоит на пути");
        }

        [UnityTest]
        public IEnumerator СвайпПоворачиваетВзгляд()
        {
            var cam = boot.Cam.transform;
            boot.Controls.InjectLook(new Vector2(200, 0));
            yield return null;
            Assert.Less(cam.forward.x, -0.5f, "свайп вправо — смотрим на правый борт (−X)");
        }

        [UnityTest]
        public IEnumerator КнопкаДействияГаситИВключаетПрожектор()
        {
            Assert.IsTrue(boot.Lamp.enabled);
            boot.Controls.PressAction();
            yield return null;
            yield return null;
            Assert.IsFalse(boot.LampOn);
            Assert.IsFalse(boot.Lamp.enabled, "прожектор погас");
            Assert.AreEqual("Включить прожектор", boot.Controls.ActionLabel);
            boot.Controls.PressAction();
            yield return null;
            yield return null;
            Assert.IsTrue(boot.Lamp.enabled, "снова светит");
            Assert.AreEqual("Погасить прожектор", boot.Controls.ActionLabel);
        }

        [UnityTest]
        public IEnumerator БезЭнергииКнопкаНеЗажигаетПрожектор()
        {
            boot.Reactor.Set(ShipSystem.Lamp, 0);
            boot.Controls.PressAction(); // погасить
            yield return null;
            boot.Controls.PressAction(); // попытка включить
            yield return null;
            yield return null;
            Assert.IsTrue(boot.LampOn);
            Assert.IsFalse(boot.Lamp.enabled, "доля прожектора 0 — темно");
        }

        [UnityTest]
        public IEnumerator ОдноНажатиеОдноПереключение()
        {
            boot.Controls.PressAction();
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsFalse(boot.LampOn, "нажатие сработало один раз, а не каждый кадр");
        }
    }
}
