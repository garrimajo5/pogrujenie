using System.Collections;
using NUnit.Framework;
using Pogrujenie.Core;
using Pogrujenie.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pogrujenie.Tests
{
    /// <summary>Штурвал в сцене: сесть, вести корабль крестовиной, всплыть, свет, встать; течение.</summary>
    public class HelmSceneTests
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

        IEnumerator Sit()
        {
            boot.Player.Place(0, -3f, Mathf.PI);
            yield return null;
            Assert.AreEqual("Сесть за штурвал", boot.Controls.ActionLabel);
            boot.Controls.PressAction();
            yield return null;
            yield return null;
            Assert.IsTrue(boot.Player.Piloting, "сели за штурвал");
        }

        [UnityTest]
        public IEnumerator КнопкойСадимсяЗаШтурвал()
        {
            yield return Sit();
            var cam = boot.Cam.transform;
            Assert.AreEqual(Player.SeatZ, cam.localPosition.z, 0.01f, "камера в кресле пилота");
            Assert.AreEqual(0f, cam.localPosition.x, 0.01f);
            Assert.Less(cam.forward.z, -0.9f, "смотрим на нос");
            Assert.AreEqual("Встать", boot.Controls.ActionLabel);
            Assert.IsTrue(boot.Controls.PilotButtons, "появились «Всплыть», «Вниз» и «Свет»");
        }

        /// <summary>По отчёту qa/2026-10-19-0150-unity-shader-fix: за штурвалом рама окна уходила за край кадра.</summary>
        [UnityTest]
        public IEnumerator ЗаШтурваломВидноРамуОкнаИШтурвал()
        {
            yield return Sit();
            var cam = boot.Cam;
            var ship = cam.transform.parent;
            try
            {
                foreach (var aspect in new[] { 2400f / 1080f, 1080f / 2400f })
                {
                    cam.aspect = aspect;
                    yield return null;
                    Assert.AreEqual(CameraView.Fov(aspect), cam.fieldOfView, 0.01f, "угол обзора как в браузере");
                    var frame = cam.WorldToViewportPoint(ship.TransformPoint(new Vector3(0, 2.7f, -5.9f)));
                    Assert.Greater(frame.z, 0f, "рама впереди");
                    Assert.Less(frame.y, 1f, "рама над окном не за верхним краем, соотношение " + aspect);
                    Assert.Greater(frame.y, 0.5f, "рама в верхней половине кадра");
                    var wheel = cam.WorldToViewportPoint(boot.Wheel.position);
                    Assert.Greater(wheel.y, -0.05f, "штурвал у нижнего края, не ниже");
                    Assert.Less(wheel.y, 0.5f, "штурвал в нижней половине");
                }
            }
            finally { cam.ResetAspect(); }
        }

        [UnityTest]
        public IEnumerator КрестовинаВедётКорабльАНеИгрока()
        {
            yield return Sit();
            Vector3 p0 = boot.Ship.position;
            float wz = boot.Player.Walker.Z;
            boot.Controls.PadOverride = new Vector2Int(0, 1);
            yield return new WaitForSeconds(1.5f);
            boot.Controls.PadOverride = null;
            Assert.Less(boot.Ship.position.z, p0.z - 1.2f, "корабль пошёл носом вперёд (−Z)");
            Assert.Greater(boot.Motion.Speed, 1f);
            Assert.AreEqual(wz, boot.Player.Walker.Z, 1e-4f, "игрок сидит, по мостику не ходит");
            Assert.AreEqual(Player.SeatZ, boot.Cam.transform.localPosition.z, 0.01f, "камера едет вместе с кораблём");
        }

        [UnityTest]
        public IEnumerator ШтурвалВправоПоворачиваетКорабль()
        {
            yield return Sit();
            boot.Controls.PadOverride = new Vector2Int(1, 1);
            yield return new WaitForSeconds(1.5f);
            boot.Controls.PadOverride = null;
            Assert.Greater(boot.Motion.Yaw, 0.1f, "курс вправо");
            float y = boot.Ship.eulerAngles.y;
            Assert.Greater(y, 5f);
            Assert.Less(y, 90f);
            Assert.Less(boot.Lamp.transform.position.x, boot.Ship.position.x, "нос (с прожектором) ушёл к правому борту, −X");
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(0f, boot.Wheel.localEulerAngles.z)), 1f, "штурвал повёрнут");
        }

        [UnityTest]
        public IEnumerator БезЭнергииДвигателейКорабльНеИдёт()
        {
            boot.Reactor.Set(ShipSystem.Engines, 0);
            yield return Sit();
            Vector3 p0 = boot.Ship.position;
            boot.Controls.PadOverride = new Vector2Int(0, 1);
            yield return new WaitForSeconds(1f);
            boot.Controls.PadOverride = null;
            Assert.AreEqual(0f, boot.Motion.Speed, 1e-4f);
            Assert.Less(Vector3.Distance(p0, boot.Ship.position), 0.6f, "только дрейф по течению");
        }

        [UnityTest]
        public IEnumerator КнопкиВсплытьИВниз()
        {
            yield return Sit();
            float y0 = boot.Ship.position.y;
            boot.Controls.VertOverride = -1;
            yield return new WaitForSeconds(1f);
            Assert.Less(boot.Ship.position.y, y0 - 0.5f, "погружаемся");
            float y1 = boot.Ship.position.y;
            boot.Controls.VertOverride = 1;
            yield return new WaitForSeconds(1.5f);
            boot.Controls.VertOverride = null;
            Assert.Greater(boot.Ship.position.y, y1, "всплываем");
        }

        [UnityTest]
        public IEnumerator КнопкаСветЗаШтурвалом()
        {
            yield return Sit();
            Assert.IsTrue(boot.Lamp.enabled);
            boot.Controls.PressLight();
            yield return null;
            yield return null;
            Assert.IsFalse(boot.LampOn);
            Assert.IsFalse(boot.Lamp.enabled, "прожектор погас");
            Assert.IsTrue(boot.Player.Piloting, "«Свет» не поднимает из-за штурвала");
            boot.Controls.PressLight();
            yield return null;
            yield return null;
            Assert.IsTrue(boot.Lamp.enabled, "снова светит");
        }

        [UnityTest]
        public IEnumerator ВсталИКрестовинаСноваВедётИгрока()
        {
            yield return Sit();
            boot.Controls.PressAction();
            yield return null;
            yield return null;
            Assert.IsFalse(boot.Player.Piloting, "встали");
            Assert.IsFalse(boot.Controls.PilotButtons, "кнопки пилота спрятаны");
            Assert.AreEqual(Player.StandZ, boot.Player.Walker.Z, 0.01f, "стоим за креслом");
            Assert.AreEqual("Сесть за штурвал", boot.Controls.ActionLabel, "кнопкой не сели обратно в том же кадре");
            Vector3 p0 = boot.Ship.position;
            boot.Controls.PadOverride = new Vector2Int(1, 0);
            yield return new WaitForSeconds(0.5f);
            boot.Controls.PadOverride = null;
            Assert.Less(boot.Player.Walker.X, -0.5f, "идём по мостику");
            Assert.Less(Vector3.Distance(p0, boot.Ship.position), 0.3f, "корабль без пилота не набирает ход");
        }

        /// <summary>По отчёту qa/2026-10-19-0950-bridge-shell: после «Встать» в строке оставалось «Вы за штурвалом».</summary>
        [UnityTest]
        public IEnumerator ВсталИСообщениеЗаШтурваломГаснет()
        {
            yield return Sit();
            StringAssert.Contains(Player.HelmMessage, boot.StatusLine(), "сели — сообщение на приборах");
            boot.Controls.PressAction();
            yield return null;
            yield return null;
            Assert.IsFalse(boot.Player.Piloting, "встали");
            StringAssert.DoesNotContain(Player.HelmMessage, boot.StatusLine(), "встали — сообщение погасло сразу");
            Assert.IsFalse(boot.Status.Visible);
        }

        [UnityTest]
        public IEnumerator СообщениеНаПриборахГаснетЧерезЧетыреСекунды()
        {
            boot.Say("Течение сменилось");
            StringAssert.Contains("· Течение сменилось", boot.StatusLine());
            for (int i = 0; i < 16; i++) boot.Step(0.25f); // 4 с
            StringAssert.Contains("Течение сменилось", boot.StatusLine(), "4 с ещё видно");
            for (int i = 0; i < 2; i++) boot.Step(0.25f); // 4,5 с
            StringAssert.DoesNotContain("Течение сменилось", boot.StatusLine(), "погасло");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ВсталНаХодуКорабльТеряетТягу()
        {
            yield return Sit();
            boot.Controls.PadOverride = new Vector2Int(0, 1);
            yield return new WaitForSeconds(0.5f);
            boot.Controls.PressAction();
            yield return null;
            yield return null;
            boot.Controls.PadOverride = null;
            Assert.AreEqual(0f, boot.Motion.InThrust, "тяга сброшена");
        }

        [UnityTest]
        public IEnumerator ТечениеСноситКорабльБезПилота()
        {
            Vector3 p0 = boot.Ship.position;
            float t0 = Time.time;
            yield return new WaitForSeconds(2f);
            float dt = Time.time - t0;
            Vector3 d = boot.Ship.position - p0;
            Assert.AreEqual(SeaCurrent.Speed * dt, d.magnitude, 0.15f, "дрейф 0,3 м/с");
            Assert.AreEqual(boot.Current.X, d.x / d.magnitude * SeaCurrent.Speed, 0.03f, "по направлению течения");
            Assert.AreEqual(boot.Current.Z, d.z / d.magnitude * SeaCurrent.Speed, 0.03f);
        }

        [UnityTest]
        public IEnumerator НаПриборахГлубинаИХод()
        {
            yield return Sit();
            Assert.AreEqual(20f, boot.Motion.Depth, 0.5f, "старт на глубине 20 м, как в браузере");
            Assert.Greater(boot.Ship.position.y, Seabed.Height(boot.Ship.position.x, boot.Ship.position.z) + ShipPhysics.Clearance, "над дном");
        }
    }
}
