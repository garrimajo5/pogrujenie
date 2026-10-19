using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Pogrujenie.Core;
using Pogrujenie.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// Корпус по ART-001 в сцене: 28 деталей по HullPlan, пять материалов, бюджет отрисовки,
    /// из отсеков корпус не заслоняет окна, прожектор светит из линзы.
    /// </summary>
    public class HullSceneTests
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
        public IEnumerator КорпусСобранПоПлану()
        {
            yield return null;
            Assert.IsNotNull(boot.Hull);
            Assert.AreSame(boot.Ship, boot.Hull.transform.parent, "корпус едет вместе с кораблём");
            Assert.AreEqual(HullPlan.Parts.Length, boot.Hull.Parts.Count);
            Assert.AreEqual(2, boot.Hull.Rotors.Length);
            for (int i = 0; i < HullPlan.Parts.Length; i++)
            {
                var p = HullPlan.Parts[i];
                var t = boot.Hull.Parts[i];
                Assert.AreEqual(p.Name, t.name);
                Assert.Less(Vector3.Distance(new Vector3(p.X, p.Y, p.Z), t.localPosition), 1e-4f, p.Name);
                Assert.AreEqual(HullView.ShapeOf(p.Form).ToString(), t.GetComponent<MeshFilter>().sharedMesh.name, p.Name);
                // точка внутри детали по плану внутри и по трансформу Unity
                var local = t.InverseTransformPoint(boot.Hull.transform.TransformPoint(new Vector3(p.X, p.Y + 0.01f, p.Z)));
                Assert.Less(local.magnitude, 0.5f, p.Name);
            }
        }

        [UnityTest]
        public IEnumerator ПятьМатериаловИБюджетТреугольников()
        {
            yield return null;
            var mats = new HashSet<Material>();
            int tris = 0;
            foreach (var r in boot.Hull.GetComponentsInChildren<MeshRenderer>())
            {
                mats.Add(r.sharedMaterial);
                tris += r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3;
                Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, r.shadowCastingMode);
            }
            Assert.AreEqual(5, mats.Count, "металл, швы, окна, линза, метка шлюза");
            Assert.Less(tris, 12000, "корпус — малая доля бюджета 350 тыс. треугольников");
            Assert.Less(boot.GetComponentsInChildren<Renderer>().Length, 120, "вся сцена далеко от 260 вызовов отрисовки");
            var glass = boot.Hull.MaterialOf(HullPaint.Window);
            Assert.Greater(glass.GetColor("_Glow").r, 0.5f, "окна светятся тёплым");
            Assert.AreEqual(0f, boot.Hull.MaterialOf(HullPaint.Metal).GetColor("_Glow").maxColorComponent, 1e-4f, "металл не светится");
        }

        [UnityTest]
        public IEnumerator ИзОтсековОкнаНеЗаслоненыКорпусом()
        {
            yield return null;
            // пластина видна только лицом: из отсека к ней обращена изнанка, сквозь окно видно воду
            var helm = boot.Ship.TransformPoint(new Vector3(0, 1.6f, -3.9f));
            var lab = boot.Ship.TransformPoint(new Vector3(0, 1.6f, 11f));
            for (int i = 0; i < HullPlan.Parts.Length; i++)
            {
                var p = HullPlan.Parts[i];
                if (p.Node != HullNode.Glass && p.Node != HullNode.LabWindow) continue;
                var t = boot.Hull.Parts[i];
                var from = p.Node == HullNode.Glass ? helm : lab;
                Assert.Greater(Vector3.Dot(t.forward, t.position - from), 0f, p.Name + ": изнанкой к отсеку");
            }
            // из кресла пилота взгляд на нос проходит внутри корпуса до стекла: на пути только замкнутый корпус
            for (float z = -3.9f; z > HullPlan.NoseCut + 0.05f; z -= 0.25f)
                Assert.IsTrue(HullPlan.Inside(0, 1.6f, z), "до стекла — внутри корпуса");
        }

        [UnityTest]
        public IEnumerator ПрожекторСветитИзЛинзы()
        {
            yield return null;
            var lens = boot.Hull.Parts.Find(t => t.name == "Линза прожектора");
            Assert.IsNotNull(lens);
            Assert.Less(Vector3.Distance(lens.position, boot.Lamp.transform.position), 0.3f, "луч выходит из линзы");
            Assert.Less(boot.Lamp.transform.localPosition.z, HullPlan.NoseCut, "перед носом, а не внутри корпуса");
            Assert.Less(Vector3.Dot(boot.Lamp.transform.forward, boot.Ship.forward), -0.9f, "светит вперёд, к носу (−Z)");
        }
    }
}
