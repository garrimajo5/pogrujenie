using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// Корпус «Ковчега-7» снаружи по концепту ART-001 (docs/concept-art/kovcheg-7.md):
    /// 28 частей по таблице художника, отсеки целиком внутри, окна и стекло на своих местах.
    /// </summary>
    public class HullPlanTests
    {
        static IEnumerable<HullPart> Of(HullNode n) => HullPlan.Parts.Where(p => p.Node == n);
        static HullPart One(HullNode n) => Of(n).Single();

        [Test]
        public void ДвадцатьВосемьЧастейПоТаблицеХудожника()
        {
            Assert.AreEqual(28, HullPlan.Parts.Length);
            var expect = new Dictionary<HullNode, int>
            {
                { HullNode.Body, 1 }, { HullNode.Nose, 1 }, { HullNode.Stern, 1 }, { HullNode.Glass, 1 },
                { HullNode.LabWindow, 4 }, { HullNode.Shroud, 2 }, { HullNode.Rotor, 2 }, { HullNode.Mount, 2 },
                { HullNode.Rudder, 2 }, { HullNode.Stabilizer, 2 }, { HullNode.Searchlight, 2 },
                { HullNode.Manipulator, 5 }, { HullNode.Airlock, 1 }, { HullNode.Cover, 2 },
            };
            foreach (var kv in expect) Assert.AreEqual(kv.Value, Of(kv.Key).Count(), kv.Key.ToString());
            Assert.IsTrue(HullPlan.Parts.All(p => !string.IsNullOrEmpty(p.Name)), "у каждой детали русское имя");
        }

        [Test]
        public void ОтсекиЦеликомВнутриКорпуса()
        {
            int checkedCorners = 0;
            foreach (var b in ShipLayout.Boxes)
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float sy in new[] { -1f, 1f })
                        foreach (float sz in new[] { -1f, 1f })
                        {
                            float x = b.X + sx * b.W / 2, y = b.Y + sy * b.H / 2, z = b.Z + sz * b.D / 2;
                            Assert.IsTrue(HullPlan.Inside(x, y, z), $"{b.Name}: угол ({x}; {y}; {z}) торчит из корпуса");
                            checkedCorners++;
                        }
            Assert.Greater(checkedCorners, 300);
            Assert.IsFalse(HullPlan.Inside(0, 1.6f, -8f), "перед стеклом вода");
            Assert.IsFalse(HullPlan.Inside(7.5f, 1.6f, 10f), "за бортом вода");
            Assert.IsFalse(HullPlan.Inside(0, 1.6f, 29.5f), "за кормой вода");
        }

        [Test]
        public void ПоворотКакQuaternionEulerВUnity()
        {
            foreach (var p in HullPlan.Parts)
                foreach (var v in new[] { new UnityEngine.Vector3(1, 0, 0), new UnityEngine.Vector3(0, 1, 0), new UnityEngine.Vector3(0.3f, -0.5f, 0.8f) })
                {
                    var want = UnityEngine.Quaternion.Euler(p.RX, p.RY, p.RZ) * v;
                    float x = v.x, y = v.y, z = v.z;
                    p.Rotate(ref x, ref y, ref z);
                    Assert.AreEqual(want.x, x, 1e-4f, p.Name);
                    Assert.AreEqual(want.y, y, 1e-4f, p.Name);
                    Assert.AreEqual(want.z, z, 1e-4f, p.Name);
                    p.Unrotate(ref x, ref y, ref z);
                    Assert.AreEqual(v.x, x, 1e-4f); Assert.AreEqual(v.y, y, 1e-4f); Assert.AreEqual(v.z, z, 1e-4f);
                }
        }

        [Test]
        public void СтеклоПередНосовымОкномМостика()
        {
            var g = One(HullNode.Glass);
            Assert.Less(g.Z, -6.1f, "стекло перед носовой стеной мостика");
            Assert.AreEqual(HullPlan.NoseCut, g.Z, 0.05f, "на срезе носовой оболочки");
            // носовое окно мостика: 7,6 × 1,9 м, y от 0,8 до 2,7
            Assert.GreaterOrEqual(g.SX / 2, 3.8f, "стекло не уже окна");
            Assert.LessOrEqual(g.Y - g.SY / 2, 0.8f);
            Assert.GreaterOrEqual(g.Y + g.SY / 2, 2.7f);
            float fx = 0, fy = 0, fz = 1; g.Rotate(ref fx, ref fy, ref fz);
            Assert.Less(fz, -0.99f, "лицом вперёд, к носу: из мостика видна изнанка, её не рисуют");
            // углы стекла лежат на срезе носа, а не висят в воде
            var nose = One(HullNode.Nose);
            foreach (float sx in new[] { -1f, 1f })
                foreach (float sy in new[] { -1f, 1f })
                    Assert.IsTrue(nose.Contains(sx * g.SX / 2, g.Y + sy * g.SY / 2, HullPlan.NoseCut + 0.05f), "угол стекла на носу");
        }

        [Test]
        public void ОкнаЛабораторииНаБортахУОтсека()
        {
            var w = Of(HullNode.LabWindow).ToArray();
            Assert.AreEqual(2, w.Count(p => p.X < 0), "два окна на левом борту");
            Assert.AreEqual(2, w.Count(p => p.X > 0), "два на правом");
            foreach (var p in w)
            {
                Assert.That(p.Z - p.SX / 2, Is.GreaterThan(8f).And.LessThan(14f), "у лаборатории z 8…14");
                Assert.That(p.Z + p.SX / 2, Is.GreaterThan(8f).And.LessThan(14f));
                float s = Math.Sign(p.X);
                Assert.IsTrue(HullPlan.Inside(p.X - s * 0.1f, p.Y, p.Z), "окно лежит на борту");
                Assert.IsFalse(HullPlan.Inside(p.X + s * 0.05f, p.Y, p.Z), "и смотрит в воду");
                float fx = 0, fy = 0, fz = 1; p.Rotate(ref fx, ref fy, ref fz);
                Assert.AreEqual(s, fx, 1e-4f, "лицом наружу");
            }
            Assert.IsFalse(HullPlan.Parts.Any(p => p.Node == HullNode.LabWindow && p.Y > 3.5f), "на крыше окон нет");
        }

        [Test]
        public void ДвигателиЗаКормойИРоторыВКожухах()
        {
            foreach (var s in Of(HullNode.Shroud))
            {
                Assert.Greater(s.Z - s.SY, 24.2f, "кожух за кормовой стеной трюма");
                Assert.AreEqual(HullPlan.EngineX, Math.Abs(s.X), 1e-4f);
                var r = Of(HullNode.Rotor).Single(p => Math.Sign(p.X) == Math.Sign(s.X));
                Assert.AreEqual(s.Z, r.Z, 1e-4f, "ротор в кожухе");
                Assert.Less(r.SX / 2, s.SX * 0.42f, "лопасти не задевают стенку кожуха (внутренний радиус 0,42)");
            }
        }

        [Test]
        public void ПрожекторИМанипуляторНаНосу()
        {
            var lens = Of(HullNode.Searchlight).Single(p => p.Paint == HullPaint.Lens);
            Assert.Less(lens.Z, HullPlan.NoseCut, "линза выступает перед носом");
            Assert.Less(lens.Y, One(HullNode.Glass).Y - 1f, "под стеклом");
            foreach (var m in Of(HullNode.Manipulator))
            {
                Assert.Less(m.Y, 0f, "манипулятор под полом мостика");
                Assert.Less(m.Z, -4f, "под носом");
            }
            var lockPart = One(HullNode.Airlock);
            Assert.AreEqual(Room.Cargo, ShipLayout.RoomAt(lockPart.Z), "нижний шлюз под трюмом");
            Assert.Less(lockPart.Y, 0f);
        }

        [Test]
        public void КорпусСимметриченПоБортам()
        {
            foreach (var p in HullPlan.Parts.Where(q => Math.Abs(q.X) > 1e-3f))
                Assert.IsTrue(HullPlan.Parts.Any(q => q.Node == p.Node && Math.Abs(q.X + p.X) < 1e-3f
                    && Math.Abs(q.Y - p.Y) < 1e-3f && Math.Abs(q.Z - p.Z) < 1e-3f), $"{p.Name}: нет пары на другом борту");
        }

        [Test]
        public void ПалитраИСвечениеПоКонцепту()
        {
            HullPlan.Color(HullPaint.Metal, out float r, out float g, out float b);
            Assert.AreEqual(0x8C / 255f, r, 1e-4f); Assert.AreEqual(0x80 / 255f, g, 1e-4f); Assert.AreEqual(0x6B / 255f, b, 1e-4f);
            HullPlan.Color(HullPaint.Window, out r, out g, out b);
            Assert.AreEqual(0xF6 / 255f, r, 1e-4f); Assert.AreEqual(0xDE / 255f, g, 1e-4f); Assert.AreEqual(0xAC / 255f, b, 1e-4f);
            Assert.IsFalse(HullPlan.Glows(HullPaint.Metal), "металл эмиссией не красим");
            Assert.IsFalse(HullPlan.Glows(HullPaint.Seam));
            Assert.IsTrue(HullPlan.Glows(HullPaint.Window));
            Assert.IsTrue(HullPlan.Glows(HullPaint.Lens));
            Assert.IsTrue(HullPlan.Glows(HullPaint.Lock));
        }
    }
}
