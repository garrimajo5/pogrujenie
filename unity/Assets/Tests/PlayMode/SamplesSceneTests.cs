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

        /// <summary>
        /// По отчёту qa/2026-10-19-0950-bridge-shell, замечание 2: в трюме у открытого шлюза строка
        /// «… · образцов рядом нет · Шлюз открыт» не помещалась. Теперь части — по строкам экрана, без повтора.
        /// </summary>
        [UnityTest]
        public IEnumerator СтрокаСостоянияВТрюмеУОткрытогоШлюзаПомещается()
        {
            boot.Player.Place(0f, 23f, Mathf.PI);
            Assert.AreEqual(Room.Cargo, boot.Player.Room);
            boot.Layout.Lock.Toggle();
            for (int i = 0; i < 25; i++) { boot.Layout.Update(0.1f); boot.Step(0.1f); }
            Assert.IsTrue(boot.Layout.Lock.FullyOpen);
            Assert.IsTrue(boot.Status.Visible, "на приборах «Шлюз открыт»");
            string line = boot.StatusLine();
            StringAssert.StartsWith("Трюм · ", line);
            int first = line.ToLowerInvariant().IndexOf("шлюз открыт");
            Assert.GreaterOrEqual(first, 0, line);
            Assert.AreEqual(-1, line.ToLowerInvariant().IndexOf("шлюз открыт", first + 1), "«шлюз открыт» один раз: " + line);

            var lines = boot.StatusLines(t => t.Length, 56);
            Assert.Greater(lines.Count, 1);
            foreach (var l in lines) Assert.LessOrEqual(l.Length, 56, l);
            Assert.AreEqual(line, string.Join(StatusBoard.Separator, lines), "по строкам — те же части");
            yield return null;
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

        [UnityTest]
        public IEnumerator ТросЛебёдкиОтШкиваКЗахвату_ЗахватВыходитИзШлюза()
        {
            yield return null;
            var v = boot.SampleView;
            boot.Step(0.1f);
            Assert.IsTrue(v.Cable.gameObject.activeSelf, "трос виден");
            Assert.IsTrue(v.Hook.gameObject.activeSelf, "захват виден");

            // шлюз закрыт: захват висит в шахте над крышкой
            var local = boot.Ship.InverseTransformPoint(v.Hook.position);
            Assert.AreEqual(0f, local.x, 0.01f);
            Assert.AreEqual(Airlock.Z, local.z, 0.01f);
            Assert.Greater(local.y, Airlock.Y + Airlock.Thick, "выше крышки");
            Assert.Less(local.y, 0f, "ниже пола трюма — видно сквозь решётку");
            AssertCable(v);

            var s = boot.Samples.All[0];
            OverSampleWithOpenLock(s);
            local = boot.Ship.InverseTransformPoint(v.Hook.position);
            Assert.Less(local.y, Airlock.Y, "шлюз открыт — захват под днищем");
            Assert.IsFalse(HullPlan.Inside(local.x, local.y, local.z), "в воде");
            var bed = new Vector3(s.X, s.Y, s.Z);
            float rest = Vector3.Distance(v.Hook.position, bed);

            // держим лебёдку: захват тянется к образцу, трос — за ним
            for (int i = 0; i < 7; i++) { boot.Winch.Hold(0.1f); boot.Step(0.1f); }
            Assert.AreEqual(GrabState.Reach, boot.Samples.State);
            Assert.Less(Vector3.Distance(v.Hook.position, bed), rest * 0.7f, "захват на пути к образцу");
            AssertCable(v);

            // захватили: образец висит под захватом и поднимается вместе с ним
            for (int i = 0; i < 10 && boot.Samples.State != GrabState.Back; i++) { boot.Winch.Hold(0.1f); boot.Step(0.1f); }
            Assert.AreEqual(GrabState.Back, boot.Samples.State);
            boot.Step(0.3f);
            Assert.IsTrue(v.Carried.gameObject.activeSelf);
            Assert.Less(Vector3.Distance(v.Carried.position, v.Hook.position), 0.6f, "образец на захвате");
            Assert.Less(v.Carried.position.y, v.Hook.position.y, "висит под ним");
            AssertCable(v);
        }

        /// <summary>Трос — цилиндр от шкива под решёткой люка до захвата.</summary>
        void AssertCable(SamplesView v)
        {
            var top = boot.Ship.TransformPoint(new Vector3(0f, Samples.PulleyY, Airlock.Z));
            var axis = v.Cable.rotation * Vector3.up;
            float half = v.Cable.lossyScale.y; // цилиндр формы высотой 2
            Assert.Less(Vector3.Distance(v.Cable.position - axis * half, top), 0.02f, "трос от шкива");
            Assert.Less(Vector3.Distance(v.Cable.position + axis * half, v.Hook.position), 0.02f, "до захвата");
            Assert.AreEqual(SamplesView.CableWidth, v.Cable.lossyScale.x, 1e-4f);
        }
    }
}
