using System;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>Экранное управление: крестовина, кнопка действия, свайп и ходьба по мостику.</summary>
    public class TouchControlsTests
    {
        static TouchRouter Router(float w = 2400, float h = 1080)
        {
            TouchLayout.Compute(0, 0, w, h, out var pad, out var act);
            var r = new TouchRouter();
            r.SetLayout(pad, act);
            return r;
        }

        [TestCase(1f, 0f, 1, 0)]
        [TestCase(0f, 1f, 0, 1)]
        [TestCase(-1f, 0f, -1, 0)]
        [TestCase(0f, -1f, 0, -1)]
        [TestCase(1f, 1f, 1, 1)]
        [TestCase(-1f, -1f, -1, -1)]
        [TestCase(1f, 0.3f, 1, 0)]  // 17° — ещё вправо
        [TestCase(1f, 0.6f, 1, 1)]  // 31° — уже наискосок
        public void КрестовинаНаВосемьНаправлений(float dx, float dy, int ex, int ey)
        {
            DPad.Direction(dx * 80, dy * 80, 100, out int x, out int y);
            Assert.AreEqual(ex, x, "x");
            Assert.AreEqual(ey, y, "y");
        }

        [Test]
        public void ВЦентреКрестовиныСтоп()
        {
            DPad.Direction(10, 5, 100, out int x, out int y);
            Assert.AreEqual(0, x);
            Assert.AreEqual(0, y);
        }

        [TestCase(2400f, 1080f)] // телефон
        [TestCase(1920f, 1080f)] // ПК
        [TestCase(1600f, 720f)]  // слабый телефон
        public void КнопкиВнизуПоКраямИНеПерекрываются(float w, float h)
        {
            TouchLayout.Compute(0, 0, w, h, out var pad, out var act);
            Assert.GreaterOrEqual(pad.Left, 0f);
            Assert.GreaterOrEqual(pad.Bottom, 0f);
            Assert.LessOrEqual(act.Right, w);
            Assert.Less(pad.C.X, w / 2, "крестовина слева");
            Assert.Greater(act.C.X, w / 2, "кнопка действия справа");
            Assert.Less(pad.Top, h * 0.6f, "крестовина в нижней части экрана");
            Assert.Greater((act.C - pad.C).Length, pad.R + act.R + h * 0.2f, "между ними место для свайпа");
            Assert.Greater(act.R, h * 0.08f, "кнопку легко попасть пальцем");
        }

        [Test]
        public void КнопкиУчитываютВырезЭкрана()
        {
            // безопасная область телефона с вырезом слева: 90 пикселей отступа
            TouchLayout.Compute(90, 0, 2310, 1080, out var pad, out var act);
            Assert.GreaterOrEqual(pad.Left, 90f);
            Assert.LessOrEqual(act.Right, 2400f);
        }

        [Test]
        public void ПалецНаКрестовинеВедётИгрока()
        {
            var r = Router();
            var c = r.Pad.C;
            r.Begin(1, new P2(c.X, c.Y + r.Pad.R * 0.7f));
            Assert.AreEqual(0, r.MoveX);
            Assert.AreEqual(1, r.MoveY, "вверх — вперёд");
            // палец можно вести по кругу и даже за край крестовины
            r.Move(1, new P2(c.X + r.Pad.R * 1.5f, c.Y));
            Assert.AreEqual(1, r.MoveX);
            Assert.AreEqual(0, r.MoveY);
            r.End(1);
            Assert.AreEqual(0, r.MoveX, "отпустил — стоп");
            Assert.AreEqual(0, r.MoveY);
        }

        [Test]
        public void СвайпВнеКнопокПоворачиваетВзгляд()
        {
            var r = Router();
            r.Begin(5, new P2(1200, 600));
            r.Move(5, new P2(1250, 590));
            Assert.AreEqual(50f, r.LookDX, 1e-4f);
            Assert.AreEqual(-10f, r.LookDY, 1e-4f);
            Assert.AreEqual(0, r.MoveX, "свайп не двигает игрока");
            r.EndFrame();
            Assert.AreEqual(0f, r.LookDX, "за кадр смещение применяется один раз");
        }

        [Test]
        public void КрестовинаИСвайпОдновременно()
        {
            var r = Router();
            var c = r.Pad.C;
            r.Begin(1, new P2(c.X, c.Y + r.Pad.R * 0.7f));
            r.Begin(2, new P2(1500, 500));
            r.Move(2, new P2(1530, 500));
            Assert.AreEqual(1, r.MoveY, "идём вперёд");
            Assert.AreEqual(30f, r.LookDX, 1e-4f, "и смотрим вправо");
            r.End(2);
            Assert.AreEqual(1, r.MoveY, "свайп кончился — ходьба продолжается");
        }

        [Test]
        public void КнопкаДействияНажатиеИУдержание()
        {
            var r = Router();
            r.Begin(3, r.Action.C);
            Assert.IsTrue(r.ActionPressed);
            Assert.IsTrue(r.ActionDown);
            r.EndFrame();
            Assert.IsFalse(r.ActionPressed, "нажатие — один кадр");
            Assert.IsTrue(r.ActionDown, "удержание — пока палец на кнопке");
            r.Move(3, new P2(r.Action.C.X - 400, r.Action.C.Y));
            Assert.AreEqual(0f, r.LookDX, "палец с кнопки не крутит взгляд");
            r.End(3);
            Assert.IsFalse(r.ActionDown);
        }

        [Test]
        public void ИгрокИдётКНосуИУпираетсяВСтену()
        {
            var w = new Walker();
            Assert.AreEqual(Math.PI, w.Yaw, 1e-5, "стартуем лицом к носу");
            float z0 = w.Z;
            for (int i = 0; i < 10; i++) w.Move(0, 1, 0.1f);
            Assert.AreEqual(z0 - 2.4f, w.Z, 0.01f, "1 с вперёд — 2,4 м к носу (−Z)");
            for (int i = 0; i < 50; i++) w.Move(0, 1, 0.1f);
            Assert.AreEqual(-6f + Walker.Radius, w.Z, 0.25f, "упёрлись в переднюю стену мостика");
            Assert.Greater(w.Z, -6f + Walker.Radius);
        }

        [Test]
        public void ВдольСтеныИгрокСкользит()
        {
            var w = new Walker { Z = -5.6f };
            w.Yaw = (float)(Math.PI * 0.75); // смотрим наискосок в стену
            float x0 = w.X;
            for (int i = 0; i < 10; i++) w.Move(0, 1, 0.1f);
            Assert.Greater(Math.Abs(w.X - x0), 1f, "по стене скользим вбок, а не стоим");
            Assert.Greater(w.Z, -6f + Walker.Radius);
        }

        [Test]
        public void ВправоКрестовинойЭтоВправоОтВзгляда()
        {
            var w = new Walker();
            // лицом к носу (−Z) правый борт в Unity — это −X
            w.Move(1, 0, 0.5f);
            Assert.Less(w.X, -1f);
            Assert.AreEqual(-3f, w.Z, 1e-4f);
        }

        [Test]
        public void СвайпВправоПоворачиваетВправоВверхСмотритВверх()
        {
            var w = new Walker();
            w.Look(100, 0);
            Assert.Greater(w.Yaw, Math.PI, "поворот вправо");
            // после поворота вправо «вперёд» уходит к правому борту (−X)
            Assert.Less(w.ForwardX, 0f);
            w.Look(0, 50);
            Assert.Greater(w.Pitch, 0f, "палец вверх — взгляд вверх");
            w.Look(0, 100000);
            Assert.AreEqual(Walker.MaxPitch, w.Pitch, 1e-5f, "не дальше зенита");
        }
    }
}
