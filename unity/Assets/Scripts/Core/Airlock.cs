using System;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Нижний шлюз трюма по ART-001 (docs/concept-art/kovcheg-7.md: «Нижняя крышка шлюза открывается
    /// отдельным узлом»). Круглая крышка Ø2,2 м под трюмом на петле у носовой кромки: открывается
    /// за 2 с и откидывается вниз на 100°. Пост — кнопка действия в трюме («Открыть шлюз»/«Закрыть шлюз»).
    /// Через открытый шлюз позже пойдёт подбор образцов со дна (как js/manip.js).
    /// </summary>
    public sealed class Airlock
    {
        /// <summary>Центр крышки в осях корабля, её радиус и толщина, м.</summary>
        public const float Z = 22f, Y = -1.15f, Radius = 1.1f, Thick = 0.2f;
        /// <summary>Петля — на носовой кромке крышки.</summary>
        public const float HingeZ = Z - Radius;
        /// <summary>Время открытия, с; угол открытой крышки, градусы.</summary>
        public const float OpenTime = 2f, MaxAngle = 100f;
        /// <summary>Пост шлюза в трюме: над крышкой, радиус кнопки действия.</summary>
        public const float PostZ = Z, PostR = 1.4f;

        /// <summary>Команда: открыть (true) или закрыть.</summary>
        public bool Open { get; private set; }
        /// <summary>Насколько открыта крышка, от 0 до 1.</summary>
        public float T { get; private set; }

        /// <summary>Сообщение на приборы: «Шлюз открывается», «Шлюз открыт» и т. п.</summary>
        public event Action<string> Message;

        public bool FullyOpen => T >= 1f;
        public bool Closed => T <= 0f;

        /// <summary>Угол крышки, градусы: плавный разгон и торможение.</summary>
        public float Angle { get { float t = T; return t * t * (3 - 2 * t) * MaxAngle; } }

        public string Label => Open ? "Закрыть шлюз" : "Открыть шлюз";

        public void Toggle()
        {
            Open = !Open;
            Message?.Invoke(Open ? "Шлюз открывается" : "Шлюз закрывается");
        }

        public void Reset() { Open = false; T = 0f; }

        public void Update(float dt)
        {
            if (dt <= 0f) return;
            float was = T;
            T = Math.Max(0f, Math.Min(1f, T + (Open ? 1f : -1f) * dt / OpenTime));
            if (was < 1f && T >= 1f) Message?.Invoke("Шлюз открыт");
            if (was > 0f && T <= 0f) Message?.Invoke("Шлюз закрыт");
        }

        /// <summary>
        /// Положение крышки в осях корабля: центр (y, z) и поворот вокруг оси x, градусы
        /// (как Quaternion.Euler(rx, 0, 0) в Unity: положительный угол уводит +z вниз).
        /// Крышка поворачивается вокруг петли у носовой кромки, кормовая кромка уходит вниз.
        /// </summary>
        public void CoverPose(out float y, out float z, out float rx)
        {
            rx = Angle;
            double r = rx * Math.PI / 180.0;
            y = Y - (float)(Radius * Math.Sin(r));
            z = HingeZ + (float)(Radius * Math.Cos(r));
        }
    }
}
