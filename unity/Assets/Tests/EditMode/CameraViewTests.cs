using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// Камера за штурвалом: угол обзора как в браузере, рама над носовым окном и штурвал в кадре.
    /// По отчёту qa/2026-10-19-0150-unity-shader-fix: за штурвалом рамы окна не было видно.
    /// </summary>
    public class CameraViewTests
    {
        const float Phone = 2400f / 1080f;
        // нижний край бруса «Нос: над окном» (y = 2,7) с внутренней стороны стены (z = −5,9)
        const float FrameY = 2.7f, FrameZ = -5.9f;

        [Test]
        public void УголОбзораКакВБраузере()
        {
            Assert.AreEqual(72f, CameraView.Fov(Phone), "альбомная — 72°");
            Assert.AreEqual(85f, CameraView.Fov(1f / Phone), "книжная — 85°");
            Assert.AreEqual(72f, CameraView.Fov(1f), "квадрат считаем альбомным");
        }

        [Test]
        public void ЗаШтурваломВидноРамуНадОкном()
        {
            foreach (var aspect in new[] { Phone, 1f / Phone, 16f / 9f })
            {
                float fov = CameraView.Fov(aspect);
                Assert.IsTrue(CameraView.InView(CameraView.SeatEye, CameraView.SeatZ, CameraView.SeatPitch, fov, aspect, 0, FrameY, FrameZ),
                    "рама над окном в кадре при соотношении " + aspect);
                // верх обода штурвала (центр y = 0,95, z = −5,25, радиус 0,3, наклон 20°)
                Assert.IsTrue(CameraView.InView(CameraView.SeatEye, CameraView.SeatZ, CameraView.SeatPitch, fov, aspect, 0, 1.23f, -5.15f),
                    "штурвал в кадре при соотношении " + aspect);
            }
        }

        [Test]
        public void СоСтарымУгломРамаУходилаЗаКрай()
        {
            // так было в APK: Unity по умолчанию 60° и наклон −0,08 — рама за верхним краем кадра
            Assert.IsFalse(CameraView.InView(CameraView.SeatEye, CameraView.SeatZ, -0.08f, 60f, Phone, 0, FrameY, FrameZ));
        }

        [Test]
        public void ТочкаЗаСпинойНеВКадре()
        {
            Assert.IsFalse(CameraView.InView(1.6f, -4f, 0f, 72f, Phone, 0, 1.6f, 0f));
            Assert.IsTrue(CameraView.InView(1.6f, -4f, 0f, 72f, Phone, 0, 1.6f, -8f), "прямо по курсу");
            Assert.IsFalse(CameraView.InView(1.6f, -4f, 0f, 72f, Phone, 9f, 1.6f, -5f), "далеко сбоку");
        }
    }
}
