using System.Collections;
using NUnit.Framework;
using Pogrujenie.Core;
using Pogrujenie.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// Образцы на дне в сцене и подбор лебёдкой трюма через открытый нижний шлюз:
    /// кнопку действия держат, образец поднимается к шлюзу и ложится в трюм.
    /// </summary>
    public class SamplesSceneTests
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

        /// <summary>Корабль носом к −Z, шлюз в 4 м над образцом; шлюз открыт.</summary>
        void OverSampleWithOpenLock(Sample s)
        {
            var m = boot.Motion;
            m.Yaw = 0f; m.Speed = m.Vy = m.Turn = 0f;
            m.X = s.X; m.Z = s.Z - Airlock.Z; m.Y = s.Y + 4f - Airlock.Y;
            boot.Layout.Lock.Toggle();
            for (int i = 0; i < 25; i++) { boot.Layout.Update(0.1f); boot.Step(0.1f); }
            Assert.IsTrue(boot.Layout.Lock.FullyOpen);
        }

        [UnityTest]
        public IEnumerator ОбразцыЛежатНаДнеОднимМешем()
        {
            yield return null;
            Assert.IsNotNull(boot.SampleView);
            Assert.AreSame(boot.Samples, boot.SampleView.Model);
            Assert.AreEqual(14, boot.SampleView.Shown);
            Assert.AreEqual(14 * 8, boot.SampleView.Mesh.triangles.Length / 3, "октаэдр на образец");
            Assert.AreEqual(14 * 6, boot.SampleView.Mesh.colors.Length, "цвет в вершинах");
            Assert.IsFalse(boot.SampleView.Carried.gameObject.activeSelf, "ничего не поднимается");
            var b = boot.SampleView.Mesh.bounds;
            var s = boot.Samples.All[0];
            Assert.IsTrue(b.Contains(new Vector3(s.X, s.Y, s.Z)), "образцы — в мировых координатах");
            Assert.AreEqual("Pogrujenie/Seabed", boot.SampleView.GetComponent<MeshRenderer>().sharedMaterial.shader.name);
            Assert.Less(boot.GetComponentsInChildren<Renderer>().Length, 120, "образцы добавили два вызова отрисовки");
        }

        [UnityTest]
        public IEnumerator ЛебёдкаПоднимаетОбразецУдержаниемКнопки()
        {
            yield return null;
            var s = boot.Samples.All[0];
            OverSampleWithOpenLock(s);
            StringAssert.Contains("внизу светящийся мох, 4 м", boot.SampleHint());

            // у кормовой кромки шлюза, лицом к корме — лебёдка
            boot.Player.Place(0f, Samples.PostZ + 0.1f, 0f);
            yield return null;
            yield return null;
            Assert.AreSame(boot.Winch, boot.Player.Target);
            Assert.AreEqual("Достать образец", boot.Controls.ActionLabel);

            boot.Controls.HoldAction(true);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(GrabState.Reach, boot.Samples.State);
            StringAssert.StartsWith("Захват", boot.Controls.ActionLabel);
            yield return new WaitForSeconds(3f);
            boot.Controls.HoldAction(false);
            yield return null;

            Assert.IsTrue(s.Taken);
            Assert.AreEqual(1, boot.Samples.Cargo.Count, "образец в трюме");
            Assert.AreEqual(SampleKind.Moss, boot.Samples.Cargo[0].Kind);
            Assert.AreEqual(13, boot.SampleView.Shown, "на дне его больше нет");
            Assert.IsFalse(boot.SampleView.Carried.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator КороткоеНажатиеНеПоднимает()
        {
            yield return null;
            var s = boot.Samples.All[0];
            OverSampleWithOpenLock(s);
            boot.Player.Place(0f, Samples.PostZ + 0.1f, 0f);
            yield return null;
            yield return null;
            Assert.AreSame(boot.Winch, boot.Player.Target);
            boot.Controls.PressAction();
            yield return new WaitForSeconds(1.5f);
            Assert.IsFalse(s.Taken, "нажатие без удержания не захватывает");
            Assert.AreEqual(GrabState.Idle, boot.Samples.State);
            Assert.AreEqual(0, boot.Samples.Cargo.Count);
            Assert.IsTrue(boot.Layout.Lock.Open, "и шлюз не закрывает");
        }

        [UnityTest]
        public IEnumerator ПоднимаемыйОбразецИдётОтДнаКШлюзу()
        {
            yield return null;
            var s = boot.Samples.All[0];
            OverSampleWithOpenLock(s);
            // без кадров между шагами: удержание поста и шаг сцены по 0,1 с
            for (int i = 0; i < 15 && boot.Samples.State != GrabState.Back; i++) { boot.Winch.Hold(0.1f); boot.Step(0.1f); }
            Assert.AreEqual(GrabState.Back, boot.Samples.State);
            Assert.AreEqual(13, boot.SampleView.Shown);
            var carried = boot.SampleView.Carried;
            Assert.IsTrue(carried.gameObject.activeSelf);
            Samples.LockPoint(boot.Motion, out float lx, out float ly, out float lz);
            var lockPos = new Vector3(lx, ly, lz);
            var bed = new Vector3(s.X, s.Y, s.Z);
            Assert.Less(Vector3.Distance(lockPos, boot.Ship.TransformPoint(new Vector3(0, Airlock.Y, Airlock.Z))), 0.01f,
                "LockPoint совпадает с крышкой в сцене");
            float d0 = Vector3.Distance(carried.position, lockPos);
            Assert.Less(Vector3.Distance(carried.position, bed), 1.5f, "начинает у дна");
            for (int i = 0; i < 6; i++) boot.Step(0.1f);
            Assert.Less(Vector3.Distance(carried.position, lockPos), d0 - 1f, "поднимается к шлюзу");
            for (int i = 0; i < 8; i++) boot.Step(0.1f);
            Assert.IsFalse(carried.gameObject.activeSelf, "дошёл до шлюза");
            Assert.AreEqual(1, boot.Samples.Cargo.Count);
        }
    }
}
