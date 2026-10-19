using System;
using System.Linq;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// Оболочка мостика по ART-002 (docs/concept-art/mostik.md, решения в art/REQUESTS.md): носовое окно из трёх
    /// секций с переплётами, скос потолка к окну, 10 плафонов, решётки пола, палитра концепта.
    /// </summary>
    public class BridgePlanTests
    {
        static BridgePart[] Named(string name) => BridgePlan.Parts.Where(p => p.Name == name).ToArray();

        [Test]
        public void НосовоеОкноИзТрёхСекций()
        {
            var mullions = Named("Переплёт окна").OrderBy(p => p.X).ToArray();
            Assert.AreEqual(2, mullions.Length, "два переплёта — три секции");
            float left = -BridgePlan.WindowHalf, right = BridgePlan.WindowHalf;
            var edges = new[] { left, mullions[0].X - mullions[0].W / 2, mullions[0].X + mullions[0].W / 2,
                                mullions[1].X - mullions[1].W / 2, mullions[1].X + mullions[1].W / 2, right };
            float[] widths = { edges[1] - edges[0], edges[3] - edges[2], edges[5] - edges[4] };
            foreach (var w in widths) Assert.Greater(w, 2f, "каждая секция шире 2 м");
            Assert.GreaterOrEqual(widths[1], widths[0], "средняя секция перед пилотом не уже боковых");
            Assert.AreEqual(-mullions[0].X, mullions[1].X, 1e-4f, "переплёты симметричны");
            foreach (var m in mullions)
            {
                Assert.AreEqual(BridgePlan.WindowBottom, m.Y - m.H / 2, 1e-4f, "переплёт от низа проёма");
                Assert.AreEqual(BridgePlan.WindowTop, m.Y + m.H / 2, 1e-4f, "до верха проёма");
                Assert.AreEqual(BridgePaint.Metal, m.Paint);
            }
            Assert.AreEqual(4, BridgePlan.Parts.Count(p => p.Name.StartsWith("Рама окна", StringComparison.Ordinal)), "рама: низ, верх, два бока");
        }

        [Test]
        public void ПилотВидитОкеанСквозьСредниюСекцию()
        {
            // ни одна деталь оболочки не закрывает середину окна на высоте глаз пилота
            foreach (var p in BridgePlan.Parts)
                foreach (var y in new[] { 1.2f, 1.62f, 2.2f })
                {
                    bool blocks = p.X - p.W / 2 < 0.9f && p.X + p.W / 2 > -0.9f && p.MinY < y && p.MaxY > y && p.Z < -5f;
                    Assert.IsFalse(blocks, $"{p.Name} закрывает окно на высоте {y}");
                }
        }

        [Test]
        public void СкосПотолкаОтВерхаОкнаДоПотолка()
        {
            var slope = Named("Скос потолка").Single();
            Assert.Less(slope.RX, -15f, "наклонён: передний край ниже заднего");
            Assert.Greater(slope.RX, -40f);
            // средняя линия бруска: передний край у верха окна, задний у потолка
            double r = slope.RX * Math.PI / 180.0;
            float hz = slope.D / 2;
            float frontY = slope.Y + (float)(hz * Math.Sin(r)), frontZ = slope.Z - (float)(hz * Math.Cos(r));
            float backY = slope.Y - (float)(hz * Math.Sin(r)), backZ = slope.Z + (float)(hz * Math.Cos(r));
            Assert.AreEqual(BridgePlan.WindowTop, frontY, 0.02f, "скос начинается от верхней кромки окна");
            Assert.AreEqual(BridgePlan.WallInner, frontZ, 0.02f, "у носовой стены");
            Assert.AreEqual(BridgePlan.Ceiling, backY, 0.02f, "и уходит в потолок");
            Assert.AreEqual(2 * BridgePlan.SideInner, slope.W, 1e-4f, "от борта до борта");
        }

        [Test]
        public void НаклонКакQuaternionEulerВUnity()
        {
            var slope = Named("Скос потолка").Single();
            var q = UnityEngine.Quaternion.Euler(slope.RX, 0, 0);
            var got = slope.Corners().ToArray();
            int i = 0;
            foreach (float sx in new[] { -0.5f, 0.5f })
                foreach (float sy in new[] { -0.5f, 0.5f })
                    foreach (float sz in new[] { -0.5f, 0.5f })
                    {
                        var v = q * new UnityEngine.Vector3(sx * slope.W, sy * slope.H, sz * slope.D);
                        Assert.AreEqual(slope.X + v.x, got[i].x, 1e-4f);
                        Assert.AreEqual(slope.Y + v.y, got[i].y, 1e-4f);
                        Assert.AreEqual(slope.Z + v.z, got[i].z, 1e-4f);
                        i++;
                    }
        }

        [Test]
        public void ДесятьПлафоновПоПятьНаБорт()
        {
            var lamps = Named("Плафон");
            Assert.AreEqual(10, lamps.Length);
            Assert.AreEqual(5, lamps.Count(p => p.X < 0));
            Assert.AreEqual(5, lamps.Count(p => p.X > 0));
            foreach (var p in lamps)
            {
                Assert.AreEqual(BridgePaint.Plafond, p.Paint);
                Assert.Greater(Math.Abs(p.X) + p.W / 2, BridgePlan.SideInner - 0.1f, "на борту");
                Assert.Greater(p.MinY, 2f, "над головой и над будущими бортовыми постами");
                Assert.IsTrue(p.Z > -6f && p.Z < 2f, "на мостике");
            }
            Assert.AreEqual(10, Named("Подложка плафона").Length, "у каждого плафона подложка-шов");
            Assert.IsFalse(ShipLayout.Boxes.Any(b => b.Name == "Лампа мостика"), "старые полосы на потолке убраны");
        }

        [Test]
        public void РешёткиПолаВровеньСПолом()
        {
            var grates = Named("Решётка пола");
            Assert.AreEqual(11, grates.Length, "по пять вдоль бортов и одна у люка");
            Assert.Greater(Named("Прут решётки").Length, grates.Length * 5, "прутья на каждой решётке");
            foreach (var p in grates.Concat(Named("Прут решётки")))
            {
                Assert.GreaterOrEqual(p.MinY, -1e-4f, "не проваливается в пол");
                Assert.LessOrEqual(p.MaxY, 0.03f, "по решётке ходят, не спотыкаясь");
            }
            Assert.IsTrue(grates.Any(g => Math.Abs(g.X) < 0.01f && g.Z > 1f), "перед люком в коридор");
        }

        [Test]
        public void ОболочкаНеМешаетХодить()
        {
            var l = new ShipLayout();
            l.Doors[0].Open = true;
            for (int i = 0; i < 20; i++) l.Update(0.05f);
            int checkedPoints = 0;
            for (float z = -6f; z <= 2.5f; z += 0.1f)
                for (float x = -5f; x <= 5f; x += 0.1f)
                {
                    if (!l.CanStand(x, z)) continue;
                    checkedPoints++;
                    foreach (var p in BridgePlan.Parts)
                    {
                        if (p.MaxY <= 0.03f || p.MinY > 1.8f) continue; // решётки под ногами, плафоны и скос над головой
                        bool inside = x > p.X - p.W / 2 - 0.05f && x < p.X + p.W / 2 + 0.05f
                                   && z > p.Corners().Min(c => c.z) - 0.05f && z < p.Corners().Max(c => c.z) + 0.05f;
                        Assert.IsFalse(inside, $"{p.Name} мешает стоять в ({x}; {z})");
                    }
                }
            Assert.Greater(checkedPoints, 2000);
        }

        [Test]
        public void ОболочкаВнутриКорпусаИНаМостике()
        {
            foreach (var p in BridgePlan.Parts)
                foreach (var c in p.Corners())
                {
                    Assert.IsTrue(HullPlan.Inside(c.x, c.y, c.z), $"{p.Name}: угол ({c.x}; {c.y}; {c.z}) торчит из корпуса");
                    Assert.IsTrue(c.z >= -6.11f && c.z <= 1.9f, $"{p.Name}: z {c.z} вне мостика");
                    Assert.IsTrue(Math.Abs(c.x) <= BridgePlan.SideInner + 1e-4f, $"{p.Name}: x {c.x} в борту");
                    Assert.IsTrue(c.y >= -1e-4f && c.y <= BridgePlan.Ceiling + 0.05f, $"{p.Name}: y {c.y} вне отсека");
                }
        }

        [Test]
        public void ПалитраКонцепта()
        {
            Assert.AreEqual(0x8C806B, BridgePlan.Hex(BridgePaint.Metal));
            Assert.AreEqual(0x514D43, BridgePlan.Hex(BridgePaint.Seam));
            Assert.AreEqual(0x0B1C24, BridgePlan.Hex(BridgePaint.Shadow));
            Assert.AreEqual(0x34484C, BridgePlan.Hex(BridgePaint.Deck));
            Assert.AreEqual(0xF6DEAC, BridgePlan.Hex(BridgePaint.Plafond));
            BridgePlan.Color(BridgePaint.Metal, out float r, out float g, out float b);
            Assert.AreEqual(0x8C / 255f, r, 1e-6f);
            Assert.AreEqual(0x80 / 255f, g, 1e-6f);
            Assert.AreEqual(0x6B / 255f, b, 1e-6f);
        }

        [Test]
        public void БрускиМостикаВПалитреОстальныеОтсекиКакБыли()
        {
            Box Find(string name) => ShipLayout.Boxes.First(b => b.Name == name);
            Assert.AreEqual(BridgePaint.Deck, BridgePlan.PaintOf(Find("Пол мостика")));
            Assert.AreEqual(BridgePaint.Seam, BridgePlan.PaintOf(Find("Потолок мостика")));
            Assert.AreEqual(BridgePaint.Metal, BridgePlan.PaintOf(Find("Борт мостика")));
            Assert.AreEqual(BridgePaint.Metal, BridgePlan.PaintOf(Find("Нос: над окном")));
            Assert.IsFalse(ShipLayout.Boxes.Any(b => b.Name == "Пульт штурвала"), "брусок пульта заменён моделью MOD-004");
            Assert.IsNull(BridgePlan.PaintOf(Find("Пол коридора")));
            Assert.IsNull(BridgePlan.PaintOf(Find("Стена коридора")));
            Assert.IsNull(BridgePlan.PaintOf(Find("Корма")));
            // переборка к коридору общая с коридором — в его окраске
            foreach (var b in ShipLayout.Boxes.Where(b => Math.Abs(b.Z - 2f) < 0.01f))
                Assert.IsNull(BridgePlan.PaintOf(b), b.Name);
        }
    }
}
