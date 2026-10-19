using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// Анимация корпуса по заметкам ART-001: роторы крутятся от тяги и энергии двигателей,
    /// рули глубины следуют за командой всплыть/погрузиться.
    /// </summary>
    public class HullMotionTests
    {
        static HullMotion Run(float seconds, float thrust, float vert, float k, HullMotion m = null)
        {
            m = m ?? new HullMotion();
            for (float t = 0; t < seconds; t += 0.05f) m.Update(0.05f, thrust, vert, k);
            return m;
        }

        [Test]
        public void БезТягиРоторыСтоятРулиРовно()
        {
            var m = Run(5f, 0f, 0f, 1f);
            Assert.AreEqual(0f, m.Spin, 1e-6f);
            Assert.AreEqual(0f, m.RotorAngle, 1e-6f);
            Assert.AreEqual(0f, m.Pitch, 1e-6f);
        }

        [Test]
        public void ПолныйХодРаскручиваетРоторыПлавно()
        {
            var m = Run(0.2f, 1f, 0f, 1f);
            Assert.Greater(m.Spin, 0f);
            Assert.Less(m.Spin, 0.5f, "раскручиваются не мгновенно");
            Run(6f, 1f, 0f, 1f, m);
            Assert.AreEqual(1f, m.Spin, 0.01f, "при базовой доле — полные обороты");
            float a = m.RotorAngle;
            m.Update(0.02f, 1f, 0f, 1f);
            float da = (m.RotorAngle - a + 360f) % 360f;
            Assert.AreEqual(HullMotion.MaxRps * 360f * 0.02f, da, 0.5f, "угол растёт со скоростью оборотов");
            Assert.That(m.RotorAngle, Is.InRange(0f, 360f));
        }

        [Test]
        public void ЗаднийХодКрутитРоторыОбратно()
        {
            var m = Run(6f, -1f, 0f, 1f);
            Assert.AreEqual(-1f, m.Spin, 0.01f);
            float a = m.RotorAngle;
            m.Update(0.02f, -1f, 0f, 1f);
            float back = (a - m.RotorAngle + 360f) % 360f;
            Assert.AreEqual(HullMotion.MaxRps * 360f * 0.02f, back, 0.5f, "угол убывает");
            Assert.That(m.RotorAngle, Is.InRange(0f, 360f));
        }

        [Test]
        public void ОборотыЗависятОтДолиДвигателей()
        {
            Assert.AreEqual(2f, Run(8f, 1f, 0f, 2f).Spin, 0.02f, "доля 60 — вдвое быстрее");
            Assert.AreEqual(0.5f, Run(8f, 1f, 0f, 0.5f).Spin, 0.01f);
            var off = Run(6f, 1f, 0f, 1f);
            Run(8f, 1f, 0f, 0f, off);
            Assert.AreEqual(0f, off.Spin, 0.01f, "без энергии роторы останавливаются");
        }

        [Test]
        public void РулиСледуютЗаВсплытиемИПогружением()
        {
            var m = Run(0.1f, 0f, 1f, 1f);
            Assert.Greater(m.Pitch, 0f);
            Assert.Less(m.Pitch, HullMotion.MaxPitch * 0.5f, "поворачиваются с запаздыванием");
            Run(3f, 0f, 1f, 1f, m);
            Assert.AreEqual(HullMotion.MaxPitch, m.Pitch, 0.1f, "всплытие — передняя кромка вверх");
            Run(4f, 0f, -1f, 1f, m);
            Assert.AreEqual(-HullMotion.MaxPitch, m.Pitch, 0.1f, "погружение — вниз");
            Run(4f, 0f, 0f, 0f, m);
            Assert.AreEqual(0f, m.Pitch, 0.1f, "отпустили кнопку — рули ровно");
            Assert.AreEqual(HullMotion.MaxPitch, Run(4f, 0f, 5f, 1f).Pitch, 0.1f, "больше предела не отклоняются");
        }

        [Test]
        public void ПараРоторовКрутитсяНавстречу()
        {
            var m = Run(1f, 1f, 0f, 1f);
            Assert.Greater(m.RotorAngle, 0f);
            Assert.AreEqual(m.RotorAngle, m.RotorAngleOf(1f));
            Assert.AreEqual(-m.RotorAngle, m.RotorAngleOf(-1f));
            m.Reset();
            Assert.AreEqual(0f, m.RotorAngle);
            Assert.AreEqual(0f, m.Spin);
        }
    }
}
