using System;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Камера от первого лица: угол обзора как в браузере (js/game.js: 72° в альбомной, 85° в книжной,
    /// угол вертикальный, как у Camera.fieldOfView) и место пилота. За штурвалом взгляд прямо на нос,
    /// чтобы в кадре оставались рама над окном и штурвал (отчёт qa/2026-10-19-0150-unity-shader-fix).
    /// </summary>
    public static class CameraView
    {
        public const float Landscape = 72f, Portrait = 85f;
        public const float SeatZ = -4.3f, SeatEye = 1.62f, SeatPitch = 0f, StandPitch = -0.08f;

        /// <summary>Вертикальный угол обзора в градусах по соотношению сторон кадра (ширина / высота).</summary>
        public static float Fov(float aspect) => aspect < 1f ? Portrait : Landscape;

        /// <summary>
        /// Видна ли точка (x, y, z) корабля из глаза (0, eyeY, eyeZ), взгляд на нос (−Z) с наклоном pitch
        /// (рад, плюс — вверх), вертикальный угол fov (град), соотношение сторон aspect.
        /// </summary>
        public static bool InView(float eyeY, float eyeZ, float pitch, float fov, float aspect, float x, float y, float z)
        {
            double dy = y - eyeY, dz = eyeZ - z;
            double c = Math.Cos(pitch), s = Math.Sin(pitch);
            double fwd = dz * c + dy * s, up = dy * c - dz * s;
            if (fwd <= 0.01) return false;
            double t = Math.Tan(fov * Math.PI / 360.0);
            return Math.Abs(up / fwd) <= t && Math.Abs(x / fwd) <= t * aspect;
        }
    }
}
