using System;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Как выглядит светляк-полосник (fishGeo и рисование в updateFish, js/creatures.js): тело —
    /// вытянутый октаэдр 0,14 × 0,2 × 0,56 м, хвост — вертикальный треугольник позади; нос смотрит
    /// по скорости, рыбка виляет на ±0,25 рад. Цвет — цвет стаи × своя яркость рыбки, в тревоге
    /// стая горит ярче. Съеденная рыбка стягивается в точку (не видна). Сцена складывает все рыбки
    /// в один меш, поэтому треугольники идут в обе стороны (как DoubleSide в браузере).
    /// </summary>
    public static class EcoFishLook
    {
        /// <summary>Во сколько раз ярче стая в тревоге.</summary>
        public const float AlarmGlow = 1.6f;
        /// <summary>Виляние: размах (рад) и частота (рад/с).</summary>
        public const float Wiggle = 0.25f, WiggleRate = 12f;

        /// <summary>Вершины рыбки в её осях: x вправо, y вверх, z — нос. 0–5 тело, 6–8 хвост.</summary>
        public static readonly EcoVec[] Shape =
        {
            new EcoVec(0.07f, 0, 0), new EcoVec(-0.07f, 0, 0),
            new EcoVec(0, 0.1f, 0), new EcoVec(0, -0.1f, 0),
            new EcoVec(0, 0, 0.28f), new EcoVec(0, 0, -0.28f),
            new EcoVec(0, 0, -0.22f), new EcoVec(0, 0.12f, -0.42f), new EcoVec(0, -0.12f, -0.42f),
        };

        /// <summary>Доля цвета у хвоста (в браузере хвост 0,7 цвета тела).</summary>
        public const float TailShade = 0.7f;

        /// <summary>Треугольники одной рыбки: 8 граней тела и хвост, каждая в обе стороны.</summary>
        public static readonly int[] Tris = BuildTris();

        public static int VertsPerFish => Shape.Length;

        static int[] BuildTris()
        {
            int[] one = { 0, 2, 4, 4, 2, 1, 1, 2, 5, 5, 2, 0, 4, 3, 0, 1, 3, 4, 5, 3, 1, 0, 3, 5, 6, 7, 8 };
            var t = new int[one.Length * 2];
            for (int i = 0; i < one.Length; i += 3)
            {
                t[i] = one[i]; t[i + 1] = one[i + 1]; t[i + 2] = one[i + 2];
                int j = one.Length + i;
                t[j] = one[i]; t[j + 1] = one[i + 2]; t[j + 2] = one[i + 1];
            }
            return t;
        }

        /// <summary>Яркость рыбки: своя яркость, в тревоге стаи — ярче в <see cref="AlarmGlow"/> раз.</summary>
        public static float Brightness(EcoFish.Fish f) => f.Bright * (f.Shoal.Alarm > 0f ? AlarmGlow : 1f);

        /// <summary>Цвет тела рыбки (0–1 и выше, если горит ярко): цвет стаи × <see cref="Brightness"/>.</summary>
        public static void Color(EcoFish.Fish f, out float r, out float g, out float b)
        {
            uint c = f.Shoal.Color;
            float k = Brightness(f) / 255f;
            r = ((c >> 16) & 255) * k; g = ((c >> 8) & 255) * k; b = (c & 255) * k;
        }

        /// <summary>Куда смотрит нос: по скорости (вверх-вниз тоже), с вилянием вокруг вертикали.</summary>
        public static EcoVec Forward(EcoFish.Fish f, float t)
        {
            var v = f.Agent.V;
            var dir = v.LengthSq > 1e-8f ? v * (1f / v.Length) : new EcoVec(0, 0, 1);
            float w = (float)Math.Sin(t * WiggleRate + f.Phase) * Wiggle;
            float cw = (float)Math.Cos(w), sw = (float)Math.Sin(w);
            return new EcoVec(dir.X * cw + dir.Z * sw, dir.Y, -dir.X * sw + dir.Z * cw);
        }

        /// <summary>
        /// Записать вершины рыбки в мире в dst начиная с at (<see cref="VertsPerFish"/> штук).
        /// Съеденная рыбка — все вершины в одной точке, треугольники пустые.
        /// </summary>
        public static void Place(EcoFish.Fish f, float t, EcoVec[] dst, int at)
        {
            var p = f.Agent.P;
            if (!f.Agent.Alive)
            {
                for (int i = 0; i < Shape.Length; i++) dst[at + i] = p;
                return;
            }
            var fw = Forward(f, t);
            // вправо = up × вперёд; рыбка, идущая отвесно, берёт ось x
            var right = new EcoVec(fw.Z, 0, -fw.X);
            float rl = right.Length;
            right = rl > 1e-4f ? right * (1f / rl) : new EcoVec(1, 0, 0);
            // вверх = вперёд × вправо
            var up = new EcoVec(fw.Y * right.Z - fw.Z * right.Y,
                                fw.Z * right.X - fw.X * right.Z,
                                fw.X * right.Y - fw.Y * right.X);
            for (int i = 0; i < Shape.Length; i++)
            {
                var s = Shape[i];
                dst[at + i] = p + right * s.X + up * s.Y + fw * s.Z;
            }
        }
    }
}
