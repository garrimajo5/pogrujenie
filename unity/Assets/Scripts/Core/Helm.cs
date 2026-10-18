using System;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Течение отмели (js/creatures.js, currentUpdate): 0,3 м/с, раз в 3 минуты меняет направление
    /// по кругу из шести и плавно к нему поворачивает. Углы — как в браузерной версии; вектор в осях
    /// Unity, поэтому x меняет знак: X = −sin угла · 0,3, Z = cos угла · 0,3.
    /// </summary>
    public sealed class SeaCurrent
    {
        public const float Speed = 0.3f, Period = 180f, TurnRate = 0.08f;
        public static readonly float[] Dirs = { 0.7f, 2.4f, -1.9f, 0.1f, 2.9f, -0.9f };

        public float Angle { get; private set; }
        public float Target { get; private set; }
        public int Index { get; private set; }
        /// <summary>Сколько секунд до смены направления.</summary>
        public float Timer { get; private set; }
        public float X => -(float)Math.Sin(Angle) * Speed;
        public float Z => (float)Math.Cos(Angle) * Speed;

        /// <summary>Течение сменилось: новый номер направления.</summary>
        public event Action<int> Changed;

        public SeaCurrent() { Reset(); }

        public void Reset() { Index = 0; Angle = Target = Dirs[0]; Timer = Period; }

        /// <summary>Вектор течения для угла из браузерной версии, в осях Unity.</summary>
        public static void Vector(float angle, out float x, out float z)
        {
            x = -(float)Math.Sin(angle) * Speed;
            z = (float)Math.Cos(angle) * Speed;
        }

        public void Update(float dt)
        {
            Timer -= dt;
            if (Timer <= 0)
            {
                Timer += Period;
                Index = (Index + 1) % Dirs.Length;
                Target = Dirs[Index];
                Changed?.Invoke(Index);
            }
            float da = Target - Angle;
            da = (float)Math.Atan2(Math.Sin(da), Math.Cos(da));
            Angle += da * Math.Min(1f, dt * TurnRate);
        }
    }

    /// <summary>
    /// Ход «Ковчега-7» (js/ship.js, S.update): тяга крестовиной вперёд-назад, поворот штурвалом,
    /// всплытие и погружение кнопками. Сила двигателей — множитель доли «Двигатели» в реакторе (k):
    /// при базовой доле полный ход ≈ 6,7 м/с, задний ≈ 3,6 м/с, при нуле и аварийном сбросе корабль
    /// только дрейфует по течению. Координаты мира Unity; курс Yaw — поворот корабля вокруг Y:
    /// нос смотрит в (−sin Yaw, −cos Yaw), Yaw растёт — поворот вправо (к правому борту, −X).
    /// </summary>
    public sealed class ShipPhysics
    {
        public const float Forward = 3.0f, Reverse = 1.6f, Drag = 0.45f, TurnLag = 2.5f, TurnRate = 0.42f,
            Climb = 2.6f, ClimbLag = 1.6f, Ceiling = -5f, Deep = -96f, Edge = 300f, Clearance = 3f;

        public static readonly float StartX = 0f, StartY = -20f, StartZ = 95f;

        public float X, Y, Z, Yaw, Speed, Vy, Turn;

        /// <summary>Ввод пилота: тяга и поворот от −1 до 1 (крестовина), вертикаль от −1 до 1 (кнопки).</summary>
        public float InThrust, InTurn, InVert;

        /// <summary>Высота дна под точкой (x, z). Корабль держится на Clearance выше. null — дна нет.</summary>
        public Func<float, float, float> Ground;

        public ShipPhysics() { Reset(); }

        public void Reset()
        {
            X = StartX; Y = StartY; Z = StartZ;
            Yaw = Speed = Vy = Turn = 0;
            InThrust = InTurn = InVert = 0;
        }

        public float NoseX => -(float)Math.Sin(Yaw);
        public float NoseZ => -(float)Math.Cos(Yaw);

        /// <summary>Глубина в метрах (положительная).</summary>
        public float Depth => -Y;

        /// <param name="dt">шаг, с</param>
        /// <param name="k">сила двигателей: Reactor.K(ShipSystem.Engines)</param>
        /// <param name="curX">течение по X, м/с</param>
        /// <param name="curZ">течение по Z, м/с</param>
        public void Update(float dt, float k, float curX, float curZ)
        {
            float thrust = Clamp(InThrust, -1, 1), turn = Clamp(InTurn, -1, 1), vert = Clamp(InVert, -1, 1);
            Speed += (thrust * (thrust < 0 ? Reverse : Forward) * k - Speed * Drag) * dt;
            Turn += (turn - Turn) * Math.Min(1f, dt * TurnLag);
            Yaw += Turn * TurnRate * dt;
            if (Yaw > Math.PI) Yaw -= (float)(2 * Math.PI);
            else if (Yaw < -Math.PI) Yaw += (float)(2 * Math.PI);
            Vy += (vert * Climb * k - Vy) * Math.Min(1f, dt * ClimbLag);

            X += (NoseX * Speed + curX) * dt;
            Z += (NoseZ * Speed + curZ) * dt;
            Y += Vy * dt;
            X = Clamp(X, -Edge, Edge);
            Z = Clamp(Z, -Edge, Edge);

            if (Y > Ceiling) { Y = Ceiling; if (Vy > 0) Vy = 0; }
            float floor = Deep;
            if (Ground != null) floor = Math.Max(floor, Ground(X, Z) + Clearance);
            if (Y < floor) { Y = Math.Min(floor, Ceiling); if (Vy < 0) Vy = 0; }
        }

        static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
    }
}
