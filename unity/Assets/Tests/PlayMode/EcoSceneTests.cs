using System.Collections;
using NUnit.Framework;
using Pogrujenie.Core;
using Pogrujenie.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// Живой мир отмели в сцене (EcoView): стаи светляков-полосников одним мешем по EcoFish,
    /// цвет стаи, ярче в тревоге; корабль передаёт в мир место, ход и прожектор; события — на приборы.
    /// </summary>
    public class EcoSceneTests
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
        public IEnumerator ВсеСтаиОднимМешем()
        {
            yield return null;
            var eco = boot.Eco;
            Assert.AreEqual(132, eco.Fish.All.Count);
            Assert.AreEqual(132 * EcoFishLook.VertsPerFish, eco.FishMesh.vertexCount);
            Assert.AreEqual(132 * EcoFishLook.Tris.Length, eco.FishMesh.triangles.Length);
            Assert.AreEqual(1, eco.GetComponentsInChildren<Renderer>().Length, "132 рыбки — один вызов отрисовки");
            Assert.AreEqual("Pogrujenie/Seabed", eco.FishRenderer.sharedMaterial.shader.name, "цвет из вершин");
            foreach (Transform t in boot.transform)
                Assert.AreNotEqual("Светляк", t.name, "тестовых шариков-светляков больше нет");
            Assert.Less(boot.GetComponentsInChildren<Renderer>().Length, 120);
        }

        [UnityTest]
        public IEnumerator РыбкиНарисованыТамЖеГдеВМире()
        {
            yield return null;
            var eco = boot.Eco;
            foreach (var f in eco.Fish.All)
            {
                if (!f.Agent.Alive) continue;
                var p = new Vector3(f.Agent.P.X, f.Agent.P.Y, f.Agent.P.Z);
                Assert.Less(Vector3.Distance(eco.DrawnAt(f.Index), p), 0.3f, "рыбка " + f.Index);
            }
            float t0 = eco.World.Time;
            yield return new WaitForSeconds(0.3f);
            Assert.Greater(eco.World.Time, t0, "мир идёт вместе со сценой");
        }

        [UnityTest]
        public IEnumerator ЦветСтаиЯрчеВТревоге()
        {
            yield return null;
            var eco = boot.Eco;
            var sh = eco.Fish.Shoals[0];
            var f = sh.Members[0];
            sh.Alarm = 0f;
            eco.Sync();
            var calm = eco.DrawnColor(f.Index);
            uint c = EcoFish.Colors[0];
            Assert.AreEqual(((c >> 16) & 255) / 255f * f.Bright, calm.r, 0.01f, "цвет стаи × яркость рыбки");
            Assert.AreEqual(((c >> 8) & 255) / 255f * f.Bright, calm.g, 0.01f);
            sh.Alarm = 3f;
            eco.Sync();
            var alarm = eco.DrawnColor(f.Index);
            Assert.AreEqual(calm.g * EcoFishLook.AlarmGlow, alarm.g, 0.01f, "в тревоге стая горит ярче");
            var other = eco.Fish.Shoals[1].Members[0];
            Assert.AreNotEqual(calm, eco.DrawnColor(other.Index), "у стай свои цвета");
        }

        [UnityTest]
        public IEnumerator КорабльПередаётМестоИПрожектор()
        {
            yield return null;
            boot.Step(0.02f);
            var ship = boot.Eco.World.Ship;
            var pos = boot.Ship.position;
            Assert.AreEqual(pos.x, ship.Pos.X, 0.01f);
            Assert.AreEqual(pos.y, ship.Pos.Y, 0.01f);
            Assert.AreEqual(pos.z, ship.Pos.Z, 0.01f);
            Assert.AreEqual(boot.Motion.Speed, ship.Speed, 1e-4f);
            Assert.IsTrue(ship.Light, "прожектор горит");
            boot.ToggleLamp();
            boot.Step(0.02f);
            Assert.IsFalse(ship.Light, "прожектор погашен — мир это знает");
        }

        [UnityTest]
        public IEnumerator КорабльНаХодуПугаетСтаю()
        {
            yield return null;
            var eco = boot.Eco;
            var sh = eco.Fish.Shoals[0];
            var c = sh.Center;
            eco.SetShip(new Vector3(c.X + 3f, c.Y, c.Z), 3f, true);
            eco.Step(0.05f);
            Assert.Greater(sh.Alarm, 0f, "стая встревожена");
            Assert.IsTrue(sh.ThreatIsShip);
            var f = sh.Members[0];
            Assert.Greater(eco.DrawnColor(f.Index).maxColorComponent,
                0.99f * EcoFishLook.AlarmGlow * f.Bright * ((EcoFish.Colors[0] >> 8) & 255) / 255f, "и горит ярче");
        }

        [UnityTest]
        public IEnumerator СобытияМираВидныНаПриборах()
        {
            yield return null;
            var w = boot.Eco.World;
            var pos = boot.Ship.position;
            w.Emit(EcoFish.Id, "flee", new EcoVec(pos.x + 20f, pos.y, pos.z), () => "Стая уходит в кораллы");
            boot.Step(0.02f);
            Assert.IsTrue(boot.Status.Visible);
            StringAssert.Contains("Стая уходит в кораллы", boot.Status.Text);
            boot.Status.Clear(boot.Status.Text);
            w.Emit(EcoFish.Id, "flee", new EcoVec(pos.x + 200f, pos.y, pos.z), () => "Далёкая стая");
            boot.Step(0.02f);
            Assert.IsFalse(boot.Status.Text.Contains("Далёкая стая"), "дальше 80 м экипаж не слышит");
        }
    }
}
