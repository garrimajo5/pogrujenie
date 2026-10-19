using System;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Прожектор «Ковчега-7» как в js/ship.js: SpotLight(0xe6fbff, 2.8, 120, 0.45, …) из-под носа
    /// с прицелом на (0, −10, −40) — вперёд и вниз на 18°, и видимый конус луча в воде (S.beam:
    /// открытый цилиндр 0,3 → 13 м длиной 55 м, светлее у линзы). Дальность и размер конуса растут
    /// как √доли «Прожектор» (не меньше половины при малой доле), при доле 0 света нет.
    /// Кнопка «Свет» за штурвалом: горит — янтарная и «Свет вкл» (#b-light.lit в браузере),
    /// погашен — тусклое кольцо и «Свет выкл».
    /// По отчёту qa/2026-10-19-0950-bridge-shell: кнопка не менялась на вид, луча на дне не было
    /// (луч 32 м под 15° не доставал до дна в 22 м под кораблём).
    /// </summary>
    public static class Searchlight
    {
        /// <summary>Дальность при базовой доле, м (S.spot.distance = 120).</summary>
        public const float BaseRange = 120f;
        /// <summary>Половина угла раскрытия, рад (angle 0.45 у THREE.SpotLight).</summary>
        public const float HalfAngle = 0.45f;
        /// <summary>Прицел относительно прожектора: (0, −10, −40) − (0, 0.8, −6.8).</summary>
        public const float AimDown = 10.8f, AimAhead = 33.2f;
        public const float Intensity = 3f;

        /// <summary>Видимый конус: длина и радиусы у линзы и на конце, м.</summary>
        public const float BeamLength = 55f, BeamTip = 0.3f, BeamEnd = 13f;
        /// <summary>Яркость конуса (opacity 0.06 при сложении цветов).</summary>
        public const float BeamOpacity = 0.06f;

        /// <summary>Наклон луча вниз от горизонта, градусы (≈ 18°).</summary>
        public static float PitchDeg => (float)(Math.Atan2(AimDown, AimAhead) * 180.0 / Math.PI);

        /// <summary>Полный угол конуса для Light.spotAngle, градусы (≈ 51,6°).</summary>
        public static float SpotAngleDeg => (float)(HalfAngle * 2.0 * 180.0 / Math.PI);

        /// <summary>Светит ли прожектор: включён и у него есть энергия.</summary>
        public static bool Lit(bool on, float k) => on && k > 0f;

        /// <summary>Масштаб луча от доли: √max(0,25; k) (js/ship.js, bk).</summary>
        public static float Scale(float k) => k <= 0f ? 0f : (float)Math.Sqrt(Math.Max(0.25f, k));

        /// <summary>Дальность луча при доле k, м.</summary>
        public static float Range(float k) => BaseRange * Scale(k);

        /// <summary>Яркость видимого конуса вдоль луча: t = 0 у линзы, 1 — на конце (как цвета вершин S.beam).</summary>
        public static float BeamShade(float t)
        {
            float k = 1f - Math.Max(0f, Math.Min(1f, t));
            return k * k * 0.9f + 0.1f * k;
        }

        /// <summary>Подпись кнопки «Свет».</summary>
        public static string ButtonLabel(bool lit) => lit ? "Свет вкл" : "Свет выкл";

        /// <summary>Цвет кнопки «Свет»: горит — янтарная и плотная, погашен — бледная и прозрачная.</summary>
        public static void ButtonColor(bool lit, out float r, out float g, out float b, out float a)
        {
            if (lit) { r = 1f; g = 0.74f; b = 0.32f; a = 0.9f; } // янтарь, как --amber в браузере
            else { r = 0.75f; g = 0.95f; b = 1f; a = 0.3f; }
        }

        /// <summary>Погашенная кнопка рисуется только кольцом, горящая — полным кругом.</summary>
        public static bool ButtonRingOnly(bool lit) => !lit;

        /// <summary>
        /// Где луч касается дна: идём от прожектора (x, y, z) по лучу, наклонённому вниз на
        /// <paramref name="downDeg"/> градусов, при курсе yaw (нос в (−sin yaw, −cos yaw)).
        /// Возвращает расстояние до дна, м, или −1, если дна нет ближе <paramref name="maxDist"/>.
        /// </summary>
        public static float FloorHit(float x, float y, float z, float yaw, float downDeg, float maxDist, float step = 0.25f)
        {
            double a = downDeg * Math.PI / 180.0;
            float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            float dx = -(float)Math.Sin(yaw) * c, dz = -(float)Math.Cos(yaw) * c, dy = -s;
            for (float t = step; t <= maxDist; t += step)
            {
                float px = x + dx * t, py = y + dy * t, pz = z + dz * t;
                if (py <= Seabed.Height(px, pz)) return t;
            }
            return -1f;
        }
    }
}
