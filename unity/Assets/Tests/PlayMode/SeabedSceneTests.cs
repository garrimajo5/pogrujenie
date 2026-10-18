using System.Collections;
using NUnit.Framework;
using Pogrujenie.Core;
using Pogrujenie.Game;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pogrujenie.Tests
{
    /// <summary>Отмель в сцене: рельеф и лёд, туман и свет по глубине, бюджет телефона.</summary>
    public class SeabedSceneTests
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
        public IEnumerator ДноОтмелиПоРельефуИЛёдНадГоловой()
        {
            yield return null;
            var floor = boot.Seafloor.Floor.sharedMesh;
            Assert.AreEqual((Seabed.Segments + 1) * (Seabed.Segments + 1), floor.vertexCount);
            Assert.AreEqual("Pogrujenie/Seabed", boot.Seafloor.Floor.GetComponent<MeshRenderer>().sharedMaterial.shader.name,
                "шейдер с цветом вершин попал в сборку");
            Assert.AreEqual(Seabed.Height(0f, 95f), Seabed.Height(boot.Ship.position.x, boot.Ship.position.z), 1f,
                "корабль над отмелью у места старта");
            Assert.Less(Seabed.Height(boot.Ship.position.x, boot.Ship.position.z), boot.Ship.position.y - 15f,
                "до дна больше 15 м");
            var ice = boot.Seafloor.Ice.sharedMesh;
            Assert.AreEqual(Seabed.IceY, ice.bounds.center.y, 1e-3f);
            Assert.Less(ice.normals[0].y, -0.99f, "лёд виден снизу");
            Assert.Greater(Seabed.IceY, boot.Ship.position.y);
        }

        [UnityTest]
        public IEnumerator КорабльНеПроходитСквозьДно()
        {
            boot.Motion.InVert = -1f;
            yield return new WaitForSeconds(0.2f);
            boot.Motion.Y = -60f; // будто нырнули ниже дна
            yield return null;
            yield return null;
            float ground = Seabed.Height(boot.Motion.X, boot.Motion.Z);
            Assert.GreaterOrEqual(boot.Motion.Y, ground + ShipPhysics.Clearance - 0.01f, "дно выталкивает корабль");
            boot.Motion.InVert = 0f;
        }

        [UnityTest]
        public IEnumerator ТуманИСветМеняютсяСГлубиной()
        {
            yield return null;
            Assert.AreEqual(FogMode.ExponentialSquared, RenderSettings.fogMode, "туман как FogExp2 в браузере");
            float d0 = boot.Motion.Depth;
            Assert.AreEqual(Water.FogDensity(d0), RenderSettings.fogDensity, 1e-4f);
            Color fog0 = RenderSettings.fogColor;
            Assert.AreEqual(fog0, boot.Cam.backgroundColor, "фон — цвет воды");
            float sun0 = boot.Sun.intensity;
            Color sky0 = RenderSettings.ambientSkyColor;

            // уходим к склону у правого борта, там глубоко
            boot.Motion.X = -250f; boot.Motion.Z = 10f; boot.Motion.Y = -85f;
            yield return null;
            yield return null;
            Assert.Greater(boot.Motion.Depth, 80f);
            Assert.Greater(RenderSettings.fogDensity, Water.FogDensity(d0) + 0.005f, "в глубине туман гуще");
            Assert.Less(RenderSettings.fogColor.g, fog0.g - 0.1f, "и вода темнее");
            Assert.Less(boot.Sun.intensity, sun0 - 0.2f, "свет сквозь лёд слабеет");
            Assert.Less(RenderSettings.ambientSkyColor.grayscale, sky0.grayscale, "рассеянный свет слабеет");
        }

        [UnityTest]
        public IEnumerator СценаВБюджетеТелефона()
        {
            yield return null;
            int renderers = 0;
            long tris = 0;
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                renderers++;
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    for (int s = 0; s < mf.sharedMesh.subMeshCount; s++)
                        tris += mf.sharedMesh.GetIndexCount(s) / 3;
            }
            Assert.Less(renderers, 260, "вызовов отрисовки меньше 260 (не больше одного на объект)");
            Assert.Less(tris, 350000, "треугольников меньше 350 тыс.");
            Assert.Greater(tris, 64800, "дно отмели в сцене");
        }
    }
}
