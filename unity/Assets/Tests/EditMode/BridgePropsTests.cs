using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// Модели художника MOD-001…003 (Meshy по ART-002): меши PGM1 из models/*.glb, габариты и бюджеты из заказов
    /// в art/REQUESTS.md, места на мостике (BridgeProps).
    /// </summary>
    public class BridgePropsTests
    {
        static MeshPack Load(string model) => MeshPack.Read(File.ReadAllBytes(Path.Combine("Assets", "Resources", "Models", model + ".bytes")));

        // модель, ширина x, высота y, глубина z (м), бюджет треугольников из заказа
        [TestCase(BridgeProps.Wheel, 0.55f, 0.55f, 0.166f, 2000)]
        [TestCase(BridgeProps.Stand, 0.35f, 0.8f, 0.35f, 1500)]
        [TestCase(BridgeProps.Chair, 0.65f, 1.2f, 0.7f, 3000)]
        [TestCase(BridgeProps.Table, 2.2f, 0.9f, 2.2f, 4000)]
        [TestCase(BridgeProps.Console, 2.5f, 1.42f, 0.65f, 3000)]
        public void ГабаритыИБюджетКакВЗаказе(string model, float w, float h, float d, int budget)
        {
            var m = Load(model);
            m.Bounds(out var min, out var max);
            Assert.AreEqual(w, max[0] - min[0], 0.01f, "ширина");
            Assert.AreEqual(h, max[1] - min[1], 0.01f, "высота");
            Assert.AreEqual(d, max[2] - min[2], 0.01f, "глубина");
            Assert.LessOrEqual(m.Triangles, budget, "бюджет треугольников");
            Assert.Greater(m.Triangles, 500);
            Assert.AreEqual(0f, (min[0] + max[0]) / 2, 0.01f, "по x модель по центру");
            Assert.AreEqual(0f, (min[2] + max[2]) / 2, 0.01f, "по z модель по центру");
            if (model == BridgeProps.Wheel) Assert.AreEqual(0f, (min[1] + max[1]) / 2, 0.01f, "колесо — от центра ступицы");
            else Assert.AreEqual(0f, min[1], 0.001f, "опорная точка на полу");
        }

        [TestCase(BridgeProps.Wheel)]
        [TestCase(BridgeProps.Stand)]
        [TestCase(BridgeProps.Chair)]
        [TestCase(BridgeProps.Table)]
        [TestCase(BridgeProps.Console)]
        [TestCase(BridgeProps.Screens)]
        public void ТреугольникиЛицомПоНормалямИUVВТекстуре(string model)
        {
            // зеркало по x при переводе из glTF разворачивает обход: лицевая сторона (как у Shapes) — по нормали
            var m = Load(model);
            int good = 0;
            for (int t = 0; t < m.Tri.Length; t += 3)
            {
                int a = m.Tri[t] * 3, b = m.Tri[t + 1] * 3, c = m.Tri[t + 2] * 3;
                float e1x = m.Pos[b] - m.Pos[a], e1y = m.Pos[b + 1] - m.Pos[a + 1], e1z = m.Pos[b + 2] - m.Pos[a + 2];
                float e2x = m.Pos[c] - m.Pos[a], e2y = m.Pos[c + 1] - m.Pos[a + 1], e2z = m.Pos[c + 2] - m.Pos[a + 2];
                float cx = e1y * e2z - e1z * e2y, cy = e1z * e2x - e1x * e2z, cz = e1x * e2y - e1y * e2x;
                float nx = m.Nrm[a] + m.Nrm[b] + m.Nrm[c], ny = m.Nrm[a + 1] + m.Nrm[b + 1] + m.Nrm[c + 1], nz = m.Nrm[a + 2] + m.Nrm[b + 2] + m.Nrm[c + 2];
                if (cx * nx + cy * ny + cz * nz > 0) good++;
            }
            Assert.Greater(good, m.Triangles * 0.99f, "лицевая сторона треугольников смотрит по нормали");
            Assert.IsTrue(m.Uv.All(u => u >= -1e-4f && u <= 1.0001f), "UV внутри текстуры");
        }

        [Test]
        public void КреслоСмотритНаНос()
        {
            // спинка (выше 0,95 м) — со стороны кормы (+Z), край сиденья — к носу (−Z)
            var m = Load(BridgeProps.Chair);
            double sum = 0; int n = 0;
            for (int i = 0; i < m.Vertices; i++)
                if (m.Pos[i * 3 + 1] > 0.95f) { sum += m.Pos[i * 3 + 2]; n++; }
            Assert.Greater(n, 50);
            Assert.Greater(sum / n, 0.15, "спинка у кормовой стороны кресла");
        }

        [Test]
        public void КолесоНаВалуСтойки()
        {
            float shaftY = BridgeProps.ShaftY * BridgeProps.StandScaleY;
            Assert.AreEqual(BridgeProps.WheelY, shaftY, 1e-4f, "вал стойки на высоте центра колеса");
            float shaftFront = BridgeProps.StandZ + BridgeProps.ShaftFront;
            Assert.AreEqual(BridgeProps.WheelZ - BridgeProps.WheelHalfDepth, shaftFront, 1e-4f, "вал упирается в ступицу сзади");
            // вал действительно в модели стойки: вершины впереди колонны на высоте вала
            var m = Load(BridgeProps.Stand);
            float front = 0;
            for (int i = 0; i < m.Vertices; i++)
                if (Math.Abs(m.Pos[i * 3 + 1] - BridgeProps.ShaftY) < 0.04f) front = Math.Max(front, m.Pos[i * 3 + 2]);
            Assert.AreEqual(BridgeProps.ShaftFront, front, 0.01f);
            Assert.Greater(BridgeProps.StandScaleY, 1f);
            Assert.Less(BridgeProps.StandScaleY, 1.5f, "стойку тянем умеренно");
        }

        [Test]
        public void МоделиНаМестахПрепятствийИНеМешаютХодить()
        {
            var l = new ShipLayout();
            Prop P(string model) => BridgeProps.Fixed.Single(p => p.Model == model);
            bool Obstacle(float x, float z) => l.Obstacles.Any(o => Math.Abs(o.X - x) < 1e-4f && Math.Abs(o.Z - z) < 1e-4f);
            Assert.IsTrue(Obstacle(P(BridgeProps.Chair).X, P(BridgeProps.Chair).Z), "кресло там, где препятствие кресла");
            Assert.IsTrue(Obstacle(P(BridgeProps.Table).X, P(BridgeProps.Table).Z), "голостол там, где препятствие голостола");
            int checkedPoints = 0;
            foreach (var p in BridgeProps.Fixed)
            {
                // круг модели на полу: самая дальняя от центра вершина (голостол круглый, углы его рамки пусты)
                var m = Load(p.Model);
                double rMax = 0;
                for (int i = 0; i < m.Vertices; i++)
                    rMax = Math.Max(rMax, Math.Sqrt(Math.Pow(m.Pos[i * 3] * p.SX, 2) + Math.Pow(m.Pos[i * 3 + 2] * p.SZ, 2)));
                for (float z = -6f; z <= 2f; z += 0.1f)
                    for (float x = -5f; x <= 5f; x += 0.1f)
                    {
                        if (!l.CanStand(x, z)) continue;
                        checkedPoints++;
                        bool inside = Math.Sqrt((x - p.X) * (x - p.X) + (z - p.Z) * (z - p.Z)) < rMax;
                        Assert.IsFalse(inside, $"{p.Name} мешает стоять в ({x}; {z})");
                    }
            }
            Assert.Greater(checkedPoints, 5000);
        }

        [Test]
        public void СтарыеБрускиКреслаУбраны()
        {
            Assert.IsFalse(ShipLayout.Boxes.Any(b => b.Name == "Кресло пилота" || b.Name == "Спинка кресла"));
            Assert.AreEqual(5, BridgeProps.Fixed.Length, "кресло, голостол, стойка, пульт и его экраны; колесо — на штурвале");
            Assert.IsTrue(BridgeProps.GlowingLights(BridgeProps.Table));
            Assert.IsFalse(BridgeProps.GlowingLights(BridgeProps.Chair));
        }

        [TestCase(BridgeProps.Wheel)]
        [TestCase(BridgeProps.Stand)]
        [TestCase(BridgeProps.Chair)]
        [TestCase(BridgeProps.Table)]
        public void ТекстураЧитаетсяЦеликом(string model)
        {
            var t = TexturePack.Read(File.ReadAllBytes(Path.Combine("Assets", "Resources", "Models", model + "-tex.bytes")));
            Assert.AreEqual(1024, t.Width);
            Assert.AreEqual(1024, t.Height);
            Assert.AreEqual(1024 * 1024 * 3, t.Rgb.Length);
            Assert.IsTrue(t.Rgb.Any(b => b > 40), "не пустая");
        }

        [Test]
        public void БитаяТекстураНеЧитается()
        {
            Assert.Throws<InvalidDataException>(() => TexturePack.Read(new byte[] { 1, 2, 3, 4, 0, 0, 0, 0, 0, 0, 0, 0 }));
            var good = File.ReadAllBytes(Path.Combine("Assets", "Resources", "Models", BridgeProps.Wheel + "-tex.bytes"));
            var cut = good.Take(good.Length / 2).ToArray();
            Assert.Throws<InvalidDataException>(() => TexturePack.Read(cut), "обрезанный файл");
        }

        [Test]
        public void БитыйФайлНеЧитается()
        {
            Assert.Throws<InvalidDataException>(() => MeshPack.Read(new byte[] { 1, 2, 3, 4, 0, 0, 0, 0, 0, 0, 0, 0 }));
            var bad = File.ReadAllBytes(Path.Combine("Assets", "Resources", "Models", BridgeProps.Wheel + ".bytes"));
            bad[bad.Length - 1] = 0xFF; bad[bad.Length - 2] = 0xFF; // последний индекс 65535 — вне меша
            Assert.Throws<InvalidDataException>(() => MeshPack.Read(bad));
        }

        [Test]
        public void ЭкраныПультаЛицомКПилоту()
        {
            // MOD-004: три наклонные плоскости одной сеткой, над тёмными полями корпуса
            var m = Load(BridgeProps.Screens);
            Assert.AreEqual(6, m.Triangles, "три прямоугольника");
            m.Bounds(out var min, out var max);
            Assert.AreEqual(2.06f, max[0] - min[0], 0.02f, "три экрана по ширине пульта");
            Assert.Greater(min[1], 0.95f, "экраны над рабочей поверхностью");
            Assert.LessOrEqual(max[1], BridgeProps.ConsoleH);
            for (int i = 0; i < m.Vertices; i++)
            {
                Assert.Greater(m.Nrm[i * 3 + 2], 0.8f, "смотрят к пилоту, в +Z");
                Assert.Greater(m.Nrm[i * 3 + 1], 0.1f, "и чуть вверх — наклонены");
            }
            // корпус тоже лицом к пилоту: верх (козырёк экранов) отодвинут к носу
            var c = Load(BridgeProps.Console);
            double sum = 0; int n = 0;
            for (int i = 0; i < c.Vertices; i++)
                if (c.Pos[i * 3 + 1] > 1.3f) { sum += c.Pos[i * 3 + 2]; n++; }
            Assert.Greater(n, 50);
            Assert.Less(sum / n, -0.05, "козырёк у носовой стороны");
        }

        [Test]
        public void ПультУНосаНеЗадеваетШтурвал()
        {
            Assert.AreEqual(-6f + 0.1f, BridgeProps.NoseInner, 1e-5f, "внутренняя грань носовой стены (ShipLayout, «Нос: под окном»)");
            Assert.AreEqual(BridgeProps.NoseInner, BridgeProps.ConsoleZ - BridgeProps.ConsoleD * BridgeProps.ConsoleSZ / 2, 1e-5f, "спиной к стене");
            Assert.Less(BridgeProps.ConsoleFront, BridgeProps.WheelZ - BridgeProps.WheelHalfDepth - 0.05f, "между пультом и колесом зазор");
            // стойка штурвала (0,35 м) встаёт в проём между тумбами: внизу у середины пульта вершин нет
            var c = Load(BridgeProps.Console);
            for (int i = 0; i < c.Vertices; i++)
                if (c.Pos[i * 3 + 1] < 0.6f)
                    Assert.Greater(Math.Abs(c.Pos[i * 3]), 0.175f + 0.1f, "тумбы не в стойке");
            // тумбы — препятствия: у краёв пульта стоять нельзя
            var l = new ShipLayout();
            Assert.IsFalse(l.CanStand(1.2f, -5.5f), "правая тумба");
            Assert.IsFalse(l.CanStand(-1.2f, -5.5f), "левая тумба");
            Assert.IsTrue(l.CanStand(1.9f, -5.5f), "за краем пульта можно");
        }

        [Test]
        public void ИзКреслаДноВидноНадПультом()
        {
            // луч из глаз пилота вниз на 18° (туда ложится середина прожектора) проходит над пультом и сквозь окно
            float eyeY = CameraView.SeatEye, eyeZ = CameraView.SeatZ;
            float y = eyeY - (eyeZ - BridgeProps.NoseInner) * (float)Math.Tan(18.0 * Math.PI / 180.0);
            Assert.Greater(y, BridgeProps.ConsoleTop, "пульт ниже луча зрения");
            Assert.Greater(y, BridgePlan.WindowBottom, "луч в окне");
            Assert.Less(BridgeProps.ConsoleTop, 1.2f);
            Assert.Greater(BridgeProps.ConsoleTop, BridgePlan.WindowBottom, "пульт заходит на окно снизу, как на концепте");
        }
    }
}
