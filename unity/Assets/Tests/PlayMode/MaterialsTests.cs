using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Pogrujenie.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// По отчёту qa/2026-10-18-unity-pervyy-zapusk: в APK корпус, отсеки и светляки были розовыми
    /// (Standard не попал в сборку), а CreatePrimitive сыпал ошибками о вырезанных коллайдерах.
    /// Все шейдеры сцены должны лежать в Resources, коллайдеров быть не должно.
    /// </summary>
    public class MaterialsTests
    {
        Bootstrap boot;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (boot != null) Object.Destroy(boot.gameObject);
            yield return null;
        }

        [Test]
        public void ШейдерДеталейЛежитВResources()
        {
            var s = Resources.Load<Shader>(Shapes.ShaderResource);
            Assert.IsNotNull(s, "Lit.shader в Resources — значит, попадёт в APK");
            Assert.AreEqual(Shapes.ShaderName, s.name);
        }

        [UnityTest]
        public IEnumerator ВсеМатериалыСценыНаШейдерахИзResources()
        {
            boot = new GameObject("Погружение").AddComponent<Bootstrap>();
            yield return null;
            var allowed = new HashSet<Shader>(Resources.LoadAll<Shader>(""));
            int checkedCount = 0;
            foreach (var r in boot.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    Assert.IsNotNull(m, r.name + ": без материала");
                    Assert.IsTrue(allowed.Contains(m.shader),
                        $"{r.name}: шейдер {m.shader.name} не из Resources — в сборке будет розовым");
                    checkedCount++;
                }
            Assert.Greater(checkedCount, 50, "проверены корпус, отсеки, штурвал, светляки и дно");
        }

        [UnityTest]
        public IEnumerator ЦветДеталейСохраняется()
        {
            boot = new GameObject("Погружение").AddComponent<Bootstrap>();
            yield return null;
            var hull = boot.Ship.Find("Корпус").GetComponent<Renderer>().sharedMaterial;
            Assert.AreEqual(Shapes.ShaderName, hull.shader.name);
            Assert.AreEqual(0.55f, hull.color.r, 0.01f, "корпус охристый, как раньше");
            var fly = boot.Fireflies[0].GetComponent<Renderer>().sharedMaterial;
            Assert.Greater(fly.GetColor("_Glow").g, 0.1f, "светляки светятся сами");
        }

        [UnityTest]
        public IEnumerator СборкаСценыНеСоздаётКоллайдеров()
        {
            boot = new GameObject("Погружение").AddComponent<Bootstrap>();
            // Awake уже отработал; Destroy отложен до конца кадра, поэтому коллайдеры от CreatePrimitive ещё видны
            Assert.AreEqual(0, boot.GetComponentsInChildren<Collider>(true).Length,
                "физика в APK вырезана: каждый коллайдер — ошибка в логе");
            yield return null;
            Assert.AreEqual(0, Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Length);
        }

        [TestCase(Shape.Cube, 1f, 1f)]
        [TestCase(Shape.Sphere, 1f, 1f)]
        [TestCase(Shape.Cylinder, 1f, 2f)]
        [TestCase(Shape.Capsule, 1f, 2f)]
        public void МешиКакУПримитивовИЛицомНаружу(Shape s, float width, float height)
        {
            var mesh = Shapes.MeshOf(s);
            var size = mesh.bounds.size;
            Assert.AreEqual(width, size.x, 0.01f, "ширина");
            Assert.AreEqual(height, size.y, 0.01f, "высота");
            Assert.AreEqual(width, size.z, 0.01f, "глубина");
            Assert.AreEqual(0f, mesh.bounds.center.magnitude, 0.01f, "центр в нуле");
            var v = mesh.vertices;
            var t = mesh.triangles;
            Assert.Greater(t.Length, 0);
            for (int i = 0; i < t.Length; i += 3)
            {
                Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                var face = Vector3.Cross(b - a, c - a); // у Unity лицевая сторона — по этой нормали
                Assert.Greater(Vector3.Dot(face, (a + b + c) / 3f), 0f, $"{s}: треугольник {i / 3} смотрит внутрь");
            }
            Assert.AreSame(mesh, Shapes.MeshOf(s), "меш один на все детали");
        }
    }
}
