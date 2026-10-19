using System.Linq;
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
            // пол трюма вокруг проёма и детали люка шлюза — по одному мешу на группу
            int cube = Shapes.MeshOf(Shape.Cube).vertexCount;
            foreach (var g in new[] { "Пол трюма", "Кромка люка шлюза", "Решётка люка", "Шахта шлюза", "Носовая стена", "Борт мостика" })
            {
                Assert.IsTrue(boot.View.Merged.ContainsKey(g), g);
                int pieces = ShipLayout.Boxes.Count(b => ShipView.MergeGroup(b.Name) == g);
                Assert.Greater(pieces, 1, g);
                Assert.AreEqual(pieces * cube, boot.View.Merged[g].sharedMesh.vertexCount, g);
            }
            Assert.AreEqual(6, boot.View.Merged.Count);
            yield return null;
        }

        [UnityTest]
        public IEnumerator МоделиХудожникаНаМостике()
        {
            // MOD-001…003: кресло, голостол, стойка и колесо штурвала — меши из Resources/Models с текстурой на нашем шейдере
            Assert.AreEqual(BridgeProps.Fixed.Length, boot.View.Props.Count);
            foreach (var p in BridgeProps.Fixed)
            {
                var r = boot.View.Props[p.Name];
                Check(r, p.Model);
                Assert.AreEqual(new Vector3(p.X, p.Y, p.Z), r.transform.localPosition, p.Name);
            }
            Check(boot.WheelModel, BridgeProps.Wheel);
            Assert.AreSame(boot.Wheel, boot.WheelModel.transform.parent, "колесо крутится вместе со штурвалом");
            Assert.AreEqual(Vector3.zero, boot.WheelModel.transform.localPosition);

            var table = boot.View.Props["Голостол сонара"].sharedMaterial;
            Assert.AreEqual(1f, table.GetFloat("_TexGlow"), "огни голостола светятся");
            Assert.AreEqual(0f, boot.View.Props["Кресло пилота"].sharedMaterial.GetFloat("_TexGlow"));
            Assert.IsNull(boot.View.transform.Find("Спинка кресла"), "бруски кресла убраны");
            Assert.Less(boot.GetComponentsInChildren<Renderer>().Length, 120);
            yield return null;
        }

        static void Check(MeshRenderer r, string model)
        {
            Assert.IsNotNull(r, model);
            var mat = r.sharedMaterial;
            Assert.AreEqual(Shapes.ShaderName, mat.shader.name, model + ": наш шейдер, не Standard");
            Assert.IsNotNull(mat.mainTexture, model + ": текстура из Resources");
            Assert.AreEqual(1024, mat.mainTexture.width, model);
            var mesh = r.GetComponent<MeshFilter>().sharedMesh;
            var pack = MeshPack.Read(Resources.Load<TextAsset>(Models.Folder + model).bytes);
            Assert.AreEqual(pack.Triangles * 3, mesh.triangles.Length, model);
            Assert.AreEqual(pack.Vertices, mesh.vertexCount, model);
        }

        [UnityTest]
        public IEnumerator ОболочкаМостикаПоКраскамОдинМешНаКраску()
        {
            // ART-002: рама и переплёты окна, скос потолка, рёбра, плафоны, решётки — по мешу на краску
            int cube = Shapes.MeshOf(Shape.Cube).vertexCount;
            var paints = BridgePlan.Parts.Select(p => p.Paint).Distinct().ToArray();
            Assert.AreEqual(paints.Length, boot.View.Bridge.Count);
            foreach (var paint in paints)
            {
                int pieces = BridgePlan.Parts.Count(p => p.Paint == paint);
                Assert.AreEqual(pieces * cube, boot.View.Bridge[paint].sharedMesh.vertexCount, paint.ToString());
            }
            // скос потолка в меше наклонён: самая высокая вершина металла у потолка, а не над ним
            var bounds = boot.View.Bridge[BridgePaint.Metal].sharedMesh.bounds;
            Assert.AreEqual(BridgePlan.Ceiling, bounds.max.y, 0.05f);
            Assert.AreEqual(-6.1f, bounds.min.z, 0.02f, "переплёты в проёме носовой стены");

            var lamp = boot.View.Bridge[BridgePaint.Plafond].GetComponent<Renderer>().sharedMaterial;
            Assert.Greater(lamp.GetColor("_Glow").r, 0.5f, "плафоны светятся тёплым");
            Assert.Greater(lamp.GetColor("_Glow").r, lamp.GetColor("_Glow").b);
            var wall = boot.View.Merged["Борт мостика"].GetComponent<Renderer>().sharedMaterial;
            Assert.AreEqual(0x8C / 255f, wall.color.r, 0.01f, "борта мостика — металл #8C806B");
            Assert.AreEqual(0x6B / 255f, wall.color.b, 0.01f);
            var floor = boot.View.transform.Find("Пол мостика").GetComponent<Renderer>().sharedMaterial;
            Assert.AreEqual(0x48 / 255f, floor.color.g, 0.01f, "пол мостика #34484C");
            var corridor = boot.View.transform.Find("Пол коридора").GetComponent<Renderer>().sharedMaterial;
            Assert.AreNotSame(floor, corridor, "коридор в прежней окраске");
            yield return null;
        }
    }
}
