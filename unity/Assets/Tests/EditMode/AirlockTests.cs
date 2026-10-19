using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// Нижний шлюз трюма по ART-001: пост кнопки действия в трюме, крышка плавно откидывается вниз
    /// на петле у носовой кромки и закрывается обратно на место детали «Нижний шлюз» из HullPlan.
    /// </summary>
    public class AirlockTests
    {
        const float Stern = 0f; // лицом к корме: вперёд = (sin 0, cos 0) = +Z

        static void Run(Airlock a, float seconds)
        {
            int n = (int)System.Math.Round(seconds / 0.05f);
            for (int i = 0; i < n; i++) a.Update(0.05f);
        }

        [Test]
        public void ЗакрытПриСтартеИОткрываетсяЗаДвеСекунды()
        {
            var a = new Airlock();
            Assert.IsTrue(a.Closed);
            Assert.AreEqual("Открыть шлюз", a.Label);
            Run(a, 3f);
            Assert.IsTrue(a.Closed, "без команды не открывается");

            a.Toggle();
            Assert.AreEqual("Закрыть шлюз", a.Label);
            Run(a, 0.5f);
            Assert.IsFalse(a.Closed);
            Assert.IsFalse(a.FullyOpen, "не мгновенно");
            Assert.Less(a.Angle, Airlock.MaxAngle * 0.25f, "плавный разгон");
            Run(a, 1.6f);
            Assert.IsTrue(a.FullyOpen, "открыт чуть больше чем за OpenTime");
            Assert.AreEqual(Airlock.MaxAngle, a.Angle, 1e-3f);
        }

        [Test]
        public void КомандаНаполовинуРазворачиваетКрышку()
        {
            var a = new Airlock();
            a.Toggle();
            Run(a, 1f);
            float half = a.T;
            Assert.AreEqual(0.5f, half, 0.03f);
            a.Toggle(); // передумали: крышка едет обратно с того же места
            a.Update(0.05f);
            Assert.Less(a.T, half);
            Run(a, 1f);
            Assert.IsTrue(a.Closed);
            Assert.AreEqual(0f, a.Angle, 1e-6f);
        }

        [Test]
        public void СообщенияНаПриборы()
        {
            var a = new Airlock();
            var said = new List<string>();
            a.Message += said.Add;
            a.Toggle();
            Run(a, 3f);
            a.Toggle();
            Run(a, 3f);
            CollectionAssert.AreEqual(new[] { "Шлюз открывается", "Шлюз открыт", "Шлюз закрывается", "Шлюз закрыт" }, said);
        }

        [Test]
        public void КрышкаНаМестеДеталиКорпуса()
        {
            var part = HullPlan.Parts.Single(p => p.Node == HullNode.Airlock);
            var a = new Airlock();
            a.CoverPose(out float y, out float z, out float rx);
            Assert.AreEqual(part.Y, y, 1e-5f);
            Assert.AreEqual(part.Z, z, 1e-5f);
            Assert.AreEqual(0f, rx, 1e-6f);
            Assert.AreEqual(Airlock.Radius * 2, part.SX, 1e-5f, "диаметр крышки — диаметр детали");
            Assert.AreEqual(Airlock.Radius * 2, part.SZ, 1e-5f);
            Assert.AreEqual(Room.Cargo, ShipLayout.RoomAt(Airlock.HingeZ), "петля под трюмом");
        }

        [Test]
        public void ОткрытаяКрышкаВиситНаПетлеКормовойКромкойВниз()
        {
            var a = new Airlock();
            a.Toggle();
            Run(a, 3f);
            a.CoverPose(out float y, out float z, out float rx);
            Assert.AreEqual(Airlock.MaxAngle, rx, 1e-3f);
            // поворот как у деталей корпуса (Quaternion.Euler(rx, 0, 0)): кромки крышки из её осей в оси корабля
            var pose = new HullPart { RX = rx };
            float nx = 0, ny = 0, nz = -Airlock.Radius; // носовая кромка — петля
            pose.Rotate(ref nx, ref ny, ref nz);
            Assert.AreEqual(Airlock.Y, y + ny, 1e-4f, "петля не сдвинулась");
            Assert.AreEqual(Airlock.HingeZ, z + nz, 1e-4f);
            float sx = 0, sy = 0, sz = Airlock.Radius; // кормовая кромка
            pose.Rotate(ref sx, ref sy, ref sz);
            Assert.Less(y + sy, Airlock.Y - Airlock.Radius * 1.9f, "кормовая кромка ушла вниз");
            Assert.Less(z, Airlock.Z, "крышка повисла под петлёй");
            Assert.IsFalse(HullPlan.Inside(0, y + sy, z + sz), "открытая крышка снаружи корпуса");
        }

        [Test]
        public void ПостШлюзаВТрюме()
        {
            var l = new ShipLayout();
            var post = l.Nearest(0, 21f, Stern);
            Assert.IsNotNull(post);
            Assert.AreEqual("Открыть шлюз", post.Label());
            post.Do();
            Assert.IsTrue(l.Lock.Open);
            Assert.AreEqual("Закрыть шлюз", l.Nearest(0, 21f, Stern).Label());
            for (int i = 0; i < 25; i++) l.Update(0.1f);
            Assert.IsTrue(l.Lock.FullyOpen, "крышку двигает ShipLayout.Update вместе с люками");
            // из машинного отделения до поста не дотянуться: ближайший — люк в трюм
            Assert.AreEqual("Открыть люк", l.Nearest(0, 18.6f, Stern).Label());
            // у люка трюма лицом к нему — люк, а не шлюз
            Assert.AreEqual("Открыть люк", l.Nearest(0, 20.2f, (float)System.Math.PI).Label());
        }

        [Test]
        public void МеткаШлюзаВПолуНеМешаетХодить()
        {
            var mark = ShipLayout.Boxes.Single(b => b.Name == "Люк шлюза в полу");
            Assert.AreEqual(BoxKind.Lamp, mark.Kind);
            Assert.AreEqual(Airlock.Z, mark.Z, 1e-5f);
            Assert.Less(mark.MaxY, 0.05f, "лежит на полу");
            var l = new ShipLayout();
            Assert.IsTrue(l.CanStand(0, Airlock.Z), "по крышке можно ходить");
        }
    }
}
