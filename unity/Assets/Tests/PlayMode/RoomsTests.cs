using System.Collections;
using NUnit.Framework;
using Pogrujenie.Core;
using Pogrujenie.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pogrujenie.Tests
{
    /// <summary>Отсеки в сцене: люки открываются кнопкой действия, через закрытый люк не пройти.</summary>
    public class RoomsTests
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
        public IEnumerator ЧерезЗакрытыйЛюкНеПройти()
        {
            boot.Player.Place(0, 1f, 0f); // у люка в коридор, лицом к корме
            boot.Controls.PadOverride = new Vector2Int(0, 1);
            yield return new WaitForSeconds(0.6f);
            boot.Controls.PadOverride = null;
            Assert.AreEqual(Room.Bridge, boot.Player.Room);
            Assert.Less(boot.Player.Walker.Z, 2f - ShipLayout.R + 0.01f, "упёрлись в люк");
        }

        [UnityTest]
        public IEnumerator КнопкойОткрылиЛюкИПрошлиВКоридор()
        {
            boot.Player.Place(0, 1f, 0f);
            yield return null;
            Assert.AreEqual("Открыть люк", boot.Controls.ActionLabel);
            Assert.IsTrue(boot.Controls.ActionEnabled);
            boot.Controls.PressAction();
            yield return new WaitForSeconds(0.8f);
            Assert.IsTrue(boot.Layout.Doors[0].Passable, "люк открыт");
            Assert.Greater(boot.View.DoorMeshes[0].localPosition.x, 1.5f, "створка уехала вбок");
            boot.Controls.PadOverride = new Vector2Int(0, 1);
            yield return new WaitForSeconds(1.2f);
            boot.Controls.PadOverride = null;
            Assert.AreEqual(Room.Corridor, boot.Player.Room);
            Assert.Greater(boot.Player.Walker.Z, 2.5f);
        }

        [UnityTest]
        public IEnumerator ВдалиОтПостовКнопкаПустая()
        {
            boot.Player.Place(-3f, -2f, (float)System.Math.PI * 1.5f); // у левого борта, лицом к нему
            yield return null;
            yield return null;
            Assert.IsFalse(boot.Controls.ActionEnabled);
            Assert.AreEqual("", boot.Controls.ActionLabel);
            bool lamp = boot.LampOn;
            boot.Controls.PressAction();
            yield return null;
            yield return null;
            Assert.AreEqual(lamp, boot.LampOn, "нажатие вдали от постов ничего не делает");
        }

        [UnityTest]
        public IEnumerator ОтсекиПостроеныИВБюджете()
        {
            Assert.AreEqual(ShipLayout.Boxes.Length, boot.View.BoxCount);
            Assert.AreEqual(4, boot.View.DoorMeshes.Length);
            int renderers = boot.GetComponentsInChildren<Renderer>().Length;
            Assert.Less(renderers, 120, "мешей на весь корабль немного: бюджет 260 вызовов отрисовки");
            yield return null;
        }
    }
}
