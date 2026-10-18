using System;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>Отсеки «Ковчега-7»: где можно стоять, люки, посты и геометрия стен.</summary>
    public class ShipLayoutTests
    {
        const float Nose = (float)Math.PI, Stern = 0f; // курс лицом к носу и к корме

        static void OpenFully(ShipLayout l, int door)
        {
            l.Doors[door].Open = true;
            for (int i = 0; i < 20; i++) l.Update(0.05f);
        }

        [TestCase(0f, -3f, Room.Bridge)]
        [TestCase(0f, 5f, Room.Corridor)]
        [TestCase(0f, 11f, Room.Lab)]
        [TestCase(0f, 16f, Room.Engine)]
        [TestCase(0f, 22f, Room.Cargo)]
        public void ОтсекПоКоординате(float x, float z, Room room)
        {
            Assert.AreEqual(room, ShipLayout.RoomAt(z));
        }

        [Test]
        public void ВОтсекахСтоятьМожноЗаСтенамиНельзя()
        {
            var l = new ShipLayout();
            Assert.IsTrue(l.CanStand(-3f, -3f), "мостик");
            Assert.IsTrue(l.CanStand(0, 5f), "коридор");
            Assert.IsTrue(l.CanStand(3f, 11f), "лаборатория");
            Assert.IsTrue(l.CanStand(2f, 15f), "машинное");
            Assert.IsTrue(l.CanStand(0, 21f), "трюм");
            Assert.IsFalse(l.CanStand(0, -6.2f), "за носовым окном вода");
            Assert.IsFalse(l.CanStand(5.5f, -2f), "за бортом мостика");
            Assert.IsFalse(l.CanStand(2f, 5f), "коридор узкий: 2,2 м");
            Assert.IsFalse(l.CanStand(0, 24.5f), "за кормой");
        }

        [Test]
        public void ЗакрытыйЛюкНеПропускает()
        {
            var l = new ShipLayout();
            Assert.IsFalse(l.CanStand(0, 2f), "люк мостик — коридор закрыт");
            l.Doors[0].Open = true;
            l.Update(0.3f);
            Assert.IsFalse(l.Doors[0].Passable, "за 0,3 с люк открыт только наполовину");
            Assert.IsFalse(l.CanStand(0, 2f));
            l.Update(0.25f);
            Assert.IsTrue(l.Doors[0].Passable, "за 0,55 с открылся");
            Assert.IsTrue(l.CanStand(0, 2f));
            Assert.IsFalse(l.CanStand(0.9f, 2f), "в проёме у косяка не встать");
        }

        [Test]
        public void ЛюкЗакрываетсяИСтворкаУезжаетВбок()
        {
            var l = new ShipLayout();
            OpenFully(l, 1);
            Assert.AreEqual(1f, l.Doors[1].T, 1e-5f);
            Assert.AreEqual(Door.Slide, l.Doors[1].Offset, 1e-4f);
            l.Doors[1].Open = false;
            for (int i = 0; i < 20; i++) l.Update(0.05f);
            Assert.AreEqual(0f, l.Doors[1].T);
            Assert.AreEqual(0f, l.Doors[1].Offset);
            Assert.IsFalse(l.CanStand(0, 8f));
        }

        [Test]
        public void ВсеЧетыреЛюкаВедутДальше()
        {
            var l = new ShipLayout();
            for (int i = 0; i < 4; i++)
            {
                float z = ShipLayout.DoorZ[i];
                Assert.IsFalse(l.CanStand(0, z), $"люк {i + 1} закрыт");
                OpenFully(l, i);
                Assert.IsTrue(l.CanStand(0, z), $"люк {i + 1} открыт");
            }
        }

        [Test]
        public void ПрепятствияНаМостике()
        {
            var l = new ShipLayout();
            Assert.IsFalse(l.CanStand(0, -1.2f), "голостол сонара");
            Assert.IsFalse(l.CanStand(0, -5.4f), "пульт штурвала");
            Assert.IsFalse(l.CanStand(0, -3.95f), "кресло пилота");
            Assert.IsTrue(l.CanStand(-3f, -1.2f), "в обход голостола");
        }

        [Test]
        public void КнопкаДействияБерётЛюкПередИгроком()
        {
            var l = new ShipLayout();
            var it = l.Nearest(0, 1f, Stern);
            Assert.IsNotNull(it, "люк в 1 м перед игроком");
            Assert.AreEqual("Открыть люк", it.Label());
            it.Do();
            Assert.IsTrue(l.Doors[0].Open);
            Assert.AreEqual("Закрыть люк", it.Label());
        }

        [Test]
        public void ЛюкЗаСпинойНеБерётся()
        {
            var l = new ShipLayout();
            Assert.IsNull(l.Nearest(0, 0.8f, Nose), "люк в 1,2 м за спиной");
            Assert.IsNotNull(l.Nearest(0, 1.3f, Nose), "вплотную (0,7 м) — берётся с любой стороны");
            Assert.IsNull(l.Nearest(0, -1f, Stern), "дальше 1,5 м — не достать");
        }

        [Test]
        public void ИзДвухПостовБлижайший()
        {
            var l = new ShipLayout();
            var near = new Interactable(0.2f, 1.2f, 1.5f, () => "Пост", () => { });
            l.Inter.Add(near);
            Assert.AreSame(near, l.Nearest(0, 1f, Stern));
            near.On = () => false;
            Assert.AreEqual("Открыть люк", l.Nearest(0, 1f, Stern).Label(), "выключенный пост пропускается");
        }

        [Test]
        public void СтеныНеЗаходятВОтсекиГдеХодит()
        {
            // ни один брусок стены не перекрывает точку, где можно стоять (с открытыми люками)
            var l = new ShipLayout();
            for (int i = 0; i < 4; i++) OpenFully(l, i);
            int checkedPoints = 0;
            for (float z = -6f; z <= 24f; z += 0.25f)
                for (float x = -5f; x <= 5f; x += 0.25f)
                {
                    if (!l.CanStand(x, z)) continue;
                    checkedPoints++;
                    foreach (var b in ShipLayout.Boxes)
                    {
                        if (b.Kind == BoxKind.Floor || b.Kind == BoxKind.Ceil || b.Kind == BoxKind.Lamp) continue;
                        if (b.MinY > 1.8f) continue; // над головой
                        bool inside = x > b.MinX - 0.05f && x < b.MaxX + 0.05f && z > b.MinZ - 0.05f && z < b.MaxZ + 0.05f;
                        Assert.IsFalse(inside, $"{b.Name} мешает стоять в ({x}; {z})");
                    }
                }
            Assert.Greater(checkedPoints, 500);
        }

        [Test]
        public void ПереборкиПерекрываютВсёКромеЛюка()
        {
            // в каждой переборке проём только по центру: от −0,8 до 0,8 м и не выше 2,3 м
            foreach (float z in ShipLayout.DoorZ)
            {
                bool gapCovered = false; int pieces = 0;
                foreach (var b in ShipLayout.Boxes)
                {
                    if (Math.Abs(b.Z - z) > 0.01f || b.D > 0.3f) continue;
                    pieces++;
                    if (b.MinX < 0.79f && b.MaxX > -0.79f && b.MinY < 2.29f) gapCovered = true;
                }
                Assert.AreEqual(3, pieces, $"переборка z={z}: две стенки и перемычка");
                Assert.IsFalse(gapCovered, $"проём люка z={z} свободен");
            }
        }

        [Test]
        public void НосовоеОкноОткрытоНаОкеан()
        {
            // на высоте глаз прямо перед штурвалом в носовой стене нет бруска
            foreach (var b in ShipLayout.Boxes)
            {
                if (Math.Abs(b.Z + 6f) > 0.2f) continue;
                bool blocksView = b.MinX < 0 && b.MaxX > 0 && b.MinY < 1.65f && b.MaxY > 1.65f;
                Assert.IsFalse(blocksView, b.Name);
            }
        }
    }
}
