using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// Прожектор как в js/ship.js: наклон, раскрытие и дальность луча, видимый конус и кнопка «Свет».
    /// По отчёту qa/2026-10-19-0950-bridge-shell, замечание 3: луча на дне не было, кнопка не менялась.
    /// </summary>
    public class SearchlightTests
    {
        static float LampX => ShipPhysics.StartX;
        static float LampY => ShipPhysics.StartY + HullPlan.SearchlightY;
        static float LampZ => ShipPhysics.StartZ + HullPlan.LensZ - 0.1f;

        [Test]
        public void НаклонИРаскрытиеКакВБраузере()
        {
            Assert.AreEqual(18.02f, Searchlight.PitchDeg, 0.01f, "прицел (0, −10, −40) из (0, 0.8, −6.8)");
            Assert.AreEqual(51.57f, Searchlight.SpotAngleDeg, 0.01f, "angle 0.45 рад — половина конуса");
        }

        [Test]
        public void ДальностьРастётКакКореньДоли()
        {
            Assert.AreEqual(120f, Searchlight.Range(1f), 1e-3f, "базовая доля — 120 м");
            Assert.AreEqual(120f * 1.41421f, Searchlight.Range(2f), 0.01f, "двойная доля — в √2 раз дальше");
            Assert.AreEqual(60f, Searchlight.Range(0.1f), 1e-3f, "малая доля — не короче половины");
            Assert.AreEqual(0f, Searchlight.Range(0f), "без энергии луча нет");
            Assert.IsTrue(Searchlight.Lit(true, 1f));
            Assert.IsFalse(Searchlight.Lit(false, 1f), "погашен кнопкой");
            Assert.IsFalse(Searchlight.Lit(true, 0f), "обесточен");
        }

        [Test]
        public void НаСтартеЛучДостаётДна()
        {
            float edge = Searchlight.FloorHit(LampX, LampY, LampZ, 0f,
                Searchlight.PitchDeg + Searchlight.HalfAngle * 57.29578f, 200f);
            Assert.Greater(edge, 0f, "нижний край луча упирается в дно");
            Assert.Less(edge, Searchlight.Range(1f) * 0.5f, "и пятно на дне яркое: ближе половины дальности");
            Assert.Less(edge, Searchlight.Range(0.1f), "даже при малой доле");
            float axis = Searchlight.FloorHit(LampX, LampY, LampZ, 0f, Searchlight.PitchDeg, 200f);
            Assert.Greater(axis, 0f);
            Assert.Less(axis, Searchlight.Range(1f), "середина луча тоже ложится на дно");
        }

        [Test]
        public void УПотолкаЛучВсёРавноНаДне()
        {
            float y = ShipPhysics.Ceiling + HullPlan.SearchlightY;
            float edge = Searchlight.FloorHit(LampX, y, LampZ, 0f,
                Searchlight.PitchDeg + Searchlight.HalfAngle * 57.29578f, 200f);
            Assert.Greater(edge, 0f);
            Assert.Less(edge, Searchlight.Range(1f));
        }

        [Test]
        public void ПрежнийЛучНеДоставалДна()
        {
            // было: 32 м, наклон 15°, раскрытие 40° — до дна в 22 м под кораблём не дотягивался
            Assert.AreEqual(-1f, Searchlight.FloorHit(LampX, LampY, LampZ, 0f, 15f + 20f, 32f));
        }

        [Test]
        public void КонусЯрчеУЛинзы()
        {
            Assert.AreEqual(1f, Searchlight.BeamShade(0f), 1e-6f);
            Assert.AreEqual(0f, Searchlight.BeamShade(1f), 1e-6f);
            float prev = 2f;
            for (int i = 0; i <= 10; i++)
            {
                float v = Searchlight.BeamShade(i / 10f);
                Assert.Less(v, prev, "к концу луча темнее");
                prev = v;
            }
        }

        [Test]
        public void КнопкаСветЗаметноМеняется()
        {
            Assert.AreEqual("Свет вкл", Searchlight.ButtonLabel(true));
            Assert.AreEqual("Свет выкл", Searchlight.ButtonLabel(false));
            Searchlight.ButtonColor(true, out float r1, out float g1, out float b1, out float a1);
            Searchlight.ButtonColor(false, out float r0, out float g0, out float b0, out float a0);
            Assert.Greater(r1, b1 + 0.4f, "горит — янтарная");
            Assert.Greater(b0, r0, "погашена — бледно-голубая");
            Assert.Greater(a1 - a0, 0.5f, "горящая заметно плотнее");
            Assert.IsFalse(Searchlight.ButtonRingOnly(true), "горит — полный круг");
            Assert.IsTrue(Searchlight.ButtonRingOnly(false), "погашена — только кольцо");
        }
    }
}
