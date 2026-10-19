using System;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Анимация корпуса по заметкам ART-001 (docs/concept-art/kovcheg-7.md, «Огни и анимация»):
    /// роторы крутятся от тяги, рули глубины следуют за подъёмом и спуском.
    /// Тяга роторов — команда пилота × сила двигателей Reactor.K(Engines): без энергии роторы
    /// плавно останавливаются, на заднем ходу крутятся в обратную сторону. Рули отклоняются
    /// по команде «Всплыть»/«Вниз» с запаздыванием; угол Pitch положительный — передняя кромка
    /// вверх (всплытие), как поворот RX у детали HullPart.
    /// </summary>
    public sealed class HullMotion
    {
        /// <summary>Обороты на полной тяге при базовой доле двигателей, об/с; запаздывание раскрутки, 1/с.</summary>
        public const float MaxRps = 2.5f, SpinLag = 1.5f;
        /// <summary>Наибольшее отклонение рулей, градусы; запаздывание рулей, 1/с.</summary>
        public const float MaxPitch = 25f, PitchLag = 3f;

        /// <summary>Обороты в долях MaxRps: 1 — полный вперёд при базовой доле, −1 — полный задний; при доле 60 — до 2.</summary>
        public float Spin { get; private set; }
        /// <summary>Угол поворота роторов, градусы, от 0 до 360.</summary>
        public float RotorAngle { get; private set; }
        /// <summary>Отклонение рулей глубины, градусы.</summary>
        public float Pitch { get; private set; }

        public void Reset() { Spin = RotorAngle = Pitch = 0f; }

        /// <param name="dt">шаг, с</param>
        /// <param name="thrust">тяга пилота от −1 до 1 (ShipPhysics.InThrust)</param>
        /// <param name="vert">всплытие/погружение от −1 до 1 (ShipPhysics.InVert)</param>
        /// <param name="k">сила двигателей: Reactor.K(ShipSystem.Engines)</param>
        public void Update(float dt, float thrust, float vert, float k)
        {
            if (dt <= 0f) return;
            float target = Clamp(thrust, -1f, 1f) * Math.Max(0f, k);
            Spin += (target - Spin) * Math.Min(1f, dt * SpinLag);
            RotorAngle = (RotorAngle + Spin * MaxRps * 360f * dt) % 360f;
            if (RotorAngle < 0f) RotorAngle += 360f;
            Pitch += (Clamp(vert, -1f, 1f) * MaxPitch - Pitch) * Math.Min(1f, dt * PitchLag);
        }

        /// <summary>Угол ротора на борту sx (−1 или 1): пара крутится навстречу, чтобы не закручивать корабль.</summary>
        public float RotorAngleOf(float sx) => sx < 0f ? -RotorAngle : RotorAngle;

        static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
    }
}
