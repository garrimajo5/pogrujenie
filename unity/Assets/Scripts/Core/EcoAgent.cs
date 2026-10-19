using System;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Состояние поведения существа. В браузере у каждого вида свои слова (`patrol`, `stalk`, `den`…),
    /// здесь — пять общих, к которым они сводятся; тонкости вида живут в его модуле.
    /// </summary>
    public enum EcoState
    {
        /// <summary>Пасётся или бродит у дома (patrol, graze, walk, swim).</summary>
        Graze,
        /// <summary>Идёт к цели: месту, кораблю, свету (investigate, curious, home, return, back).</summary>
        Seek,
        /// <summary>Охотится: преследует добычу или бросается на неё (hunt, stalk, strike, charge).</summary>
        Hunt,
        /// <summary>Убегает от угрозы (flee, retreat).</summary>
        Flee,
        /// <summary>Прячется и не двигается (hide, den, rock).</summary>
        Hide,
    }

    /// <summary>
    /// Существо в симуляции: положение, скорость, цель, состояние и таймер. Движение — как у серпохвоста
    /// в js/creatures.js: скорость плавно (lerp) тянется к желаемой, у цели замедляется, существо не
    /// уходит под дно (Clearance над рельефом), выше потолка (Ceiling) и за край отмели (±Edge).
    /// </summary>
    public sealed class EcoAgent
    {
        /// <summary>Край отмели по X и Z (clampArea).</summary>
        public const float Edge = 290f;

        public readonly string SpeciesId;
        public EcoVec P, V, Goal, Home;
        public EcoState State = EcoState.Graze;
        /// <summary>Секунды до конца текущего состояния (уменьшается в Tick, не ниже нуля).</summary>
        public float Timer;
        public bool Alive = true;
        /// <summary>Как быстро скорость догоняет желаемую (1/с): 1,1 обычно, 1,6 в броске.</summary>
        public float Agility = 1.1f;
        /// <summary>Высота над дном, ниже которой не опускается.</summary>
        public float Clearance = 1.8f;
        /// <summary>Потолок по Y (подо льдом).</summary>
        public float Ceiling = -4f;
        /// <summary>Кого преследует или от кого бежит.</summary>
        public EcoAgent Target;

        public EcoAgent(string species, EcoVec pos)
        {
            if (!Species.Has(species)) throw new ArgumentException("Нет вида: " + species);
            SpeciesId = species; P = Home = Goal = pos;
        }

        /// <summary>Сменить состояние на timer секунд.</summary>
        public void Set(EcoState state, float timer = 0f, EcoAgent target = null)
        {
            State = state; Timer = timer; Target = target;
        }

        /// <summary>Таймер состояния истёк.</summary>
        public bool Expired => Timer <= 0f;

        public void Tick(float dt) { Timer = Math.Max(0f, Timer - dt); }

        public float DistanceTo(EcoVec p) => EcoVec.Distance(P, p);

        /// <summary>Плыть к точке со скоростью speed; ближе к цели — медленнее (не проскакивает).</summary>
        public void SteerTo(EcoVec target, float speed, float dt, Func<float, float, float> ground = null)
        {
            Goal = target;
            var d = target - P;
            float len = d.Length;
            var want = len > 0.01f ? d * (Math.Min(speed, len * 1.5f) / len) : EcoVec.Zero;
            Drive(want, dt, ground);
        }

        /// <summary>Плыть прочь от точки по горизонтали со скоростью speed.</summary>
        public void SteerAway(EcoVec from, float speed, float dt, Func<float, float, float> ground = null)
        {
            var d = P - from;
            d.Y = 0;
            if (d.LengthSq < 1e-4f) d = new EcoVec(1, 0, 0);
            Goal = P + d.Normalized * 20f;
            Drive(d.Normalized * speed, dt, ground);
        }

        /// <summary>Стоять: скорость гаснет.</summary>
        public void Hold(float dt, Func<float, float, float> ground = null) => Drive(EcoVec.Zero, dt, ground);

        /// <summary>Скорость тянется к want, затем шаг и ограничения.</summary>
        public void Drive(EcoVec want, float dt, Func<float, float, float> ground = null)
        {
            V = EcoVec.Lerp(V, want, Math.Min(1f, dt * Agility));
            P = P + V * dt;
            Confine(ground);
        }

        /// <summary>Не под дном, не выше потолка, не за краем отмели (как после шага серпохвоста).</summary>
        public void Confine(Func<float, float, float> ground)
        {
            if (ground != null)
            {
                float floor = ground(P.X, P.Z) + Clearance;
                if (P.Y < floor) { P.Y = floor; if (V.Y < 0) V.Y *= -0.3f; }
            }
            if (P.Y > Ceiling) P.Y = Ceiling;
            P.X = Math.Max(-Edge, Math.Min(Edge, P.X));
            P.Z = Math.Max(-Edge, Math.Min(Edge, P.Z));
        }
    }
}
