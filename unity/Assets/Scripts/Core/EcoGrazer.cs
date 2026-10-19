using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Скребуны — донные панцирники на шести лапах (initGrazers/grazerUpdate в js/creatures.js).
    /// Десять скребунов ходят по дну от пятна мха к пятну (<see cref="EcoMoss"/>) со скоростью 0,9 м/с,
    /// плавно поворачивая к цели. Ближе 1,6 м к пятну — пасутся 8–14 с и едят мох (0,05 в секунду,
    /// пятно тускнеет), потом выбирают следующее: где больше еды и ближе, с долей случая. Объеденное
    /// ниже 0,08 пятно бросают сразу. Начав пастись, скребун шлёт событие «graze» — не чаще раза в 45 с.
    /// Хищник (стеклоуст) берёт скребуна через <see cref="NearestGrazer"/> и <see cref="Kill"/>;
    /// съеденный через 40–60 с появляется у случайного пятна.
    /// </summary>
    public sealed class EcoGrazer : IEcoSystem
    {
        public const string Id = "grazer";
        /// <summary>Сколько скребунов.</summary>
        public const int Count = 10;
        /// <summary>Шаг (м/с), поворот (доля разницы курса в секунду).</summary>
        public const float WalkSpeed = 0.9f, TurnRate = 1.5f;
        /// <summary>Ближе этого к середине пятна — пасётся (м).</summary>
        public const float Reach = 1.6f;
        /// <summary>Сколько мха съедает за секунду.</summary>
        public const float EatRate = 0.05f;
        /// <summary>Пятно беднее этого бросает.</summary>
        public const float Leave = 0.08f;
        /// <summary>Пауза между сообщениями «объедает мох» у одного скребуна (с).</summary>
        public const float GrazeCooldown = 45f;
        /// <summary>Над дном (м).</summary>
        public const float Lift = 0.1f;

        /// <summary>Скребун: существо мира, курс и пятно, к которому идёт или на котором пасётся.</summary>
        public sealed class Grazer
        {
            public EcoAgent Agent;
            public int Index;
            /// <summary>Курс: вперёд — (sin Dir, cos Dir) по (x, z).</summary>
            public float Dir;
            public EcoMoss.Patch Patch;
            /// <summary>Секунды до следующего сообщения «объедает мох».</summary>
            public float GrazeCd;
            public bool Grazing => Agent.Alive && Agent.State == EcoState.Graze;
        }

        public readonly List<Grazer> All = new List<Grazer>();
        public readonly EcoMoss Moss;

        readonly int count;
        EcoRandom rnd;

        public EcoGrazer(EcoMoss moss, int count = Count)
        {
            Moss = moss ?? throw new ArgumentNullException(nameof(moss));
            this.count = count;
        }

        /// <summary>Своё зерно скребунов.</summary>
        public static uint SeedFor(uint worldSeed) => unchecked(worldSeed * 31u + 7003u);

        public void Reset(EcoWorld world)
        {
            rnd = EcoWorld.SeededFor(SeedFor(world.Seed));
            All.Clear();
            var P = Moss.Patches;
            if (P.Count == 0) return;
            for (int i = 0; i < count; i++)
            {
                var pt = P[(i * 2) % P.Count];
                float x = pt.Pos.X + rnd.Range(-3, 3), z = pt.Pos.Z + rnd.Range(-3, 3);
                var a = world.Spawn(Id, new EcoVec(x, GroundY(world, x, z, pt.Pos.Y), z));
                a.Clearance = Lift;
                var g = new Grazer { Agent = a, Index = i, Dir = rnd.Range(0, 6.28f), GrazeCd = rnd.Range(0, 20) };
                a.Set(EcoState.Seek);
                g.Patch = PickPatch(g);
                if (g.Patch != null) a.Goal = g.Patch.Pos;
                All.Add(g);
            }
        }

        /// <summary>Высота скребуна: дно мира + 0,1 м; без дна мира — fallback + 0,1 м.</summary>
        static float GroundY(EcoWorld world, float x, float z, float fallback) =>
            (world.Ground != null ? world.Ground(x, z) : fallback) + Lift;

        /// <summary>Следующее пятно (pickPatch): больше еды и ближе, ×0,7–1,3 случая; не то же самое.</summary>
        EcoMoss.Patch PickPatch(Grazer g)
        {
            EcoMoss.Patch best = null;
            float bs = -1f;
            var p = g.Agent.P;
            foreach (var pt in Moss.Patches)
            {
                if (pt == g.Patch) continue;
                float d = EcoVec.FlatDistance(pt.Pos, p);
                float s = pt.Food / (1f + d / 25f) * rnd.Range(0.7f, 1.3f);
                if (s > bs) { bs = s; best = pt; }
            }
            return best;
        }

        /// <summary>Съеденный скребун пропадает на 40–60 с, потом появляется у случайного пятна.</summary>
        public void Kill(Grazer g)
        {
            if (!g.Agent.Alive) return;
            g.Agent.Alive = false;
            g.Agent.V = EcoVec.Zero;
            g.Agent.Timer = rnd.Range(40, 60);
        }

        /// <summary>Ближайший живой скребун не дальше radius (null — нет).</summary>
        public Grazer NearestGrazer(EcoVec pos, float radius)
        {
            Grazer best = null;
            float bd = radius;
            foreach (var g in All)
            {
                if (!g.Agent.Alive) continue;
                float d = EcoVec.Distance(g.Agent.P, pos);
                if (d < bd) { bd = d; best = g; }
            }
            return best;
        }

        public void Step(EcoWorld world, float dt)
        {
            foreach (var g in All) StepGrazer(world, g, dt);
        }

        void StepGrazer(EcoWorld world, Grazer g, float dt)
        {
            var a = g.Agent;
            if (!a.Alive)
            {
                if (a.Timer > 0f || Moss.Patches.Count == 0) return;
                var pt = rnd.Pick(Moss.Patches);
                a.P = new EcoVec(pt.Pos.X + rnd.Range(-3, 3), pt.Pos.Y, pt.Pos.Z + rnd.Range(-3, 3));
                a.Alive = true;
                g.Patch = PickPatch(g);
                a.Set(EcoState.Seek);
            }
            g.GrazeCd -= dt;
            if (a.State != EcoState.Graze)
            {
                var tp = g.Patch;
                if (tp == null) { g.Patch = PickPatch(g); a.V = EcoVec.Zero; return; }
                a.Goal = tp.Pos;
                float dx = tp.Pos.X - a.P.X, dz = tp.Pos.Z - a.P.Z;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d < Reach)
                {
                    a.Set(EcoState.Graze, rnd.Range(8, 14));
                    a.V = EcoVec.Zero;
                    if (g.GrazeCd <= 0f)
                    {
                        g.GrazeCd = GrazeCooldown;
                        world.Emit(Id, "graze", a.P,
                            () => $"{Species.Cap(world.Name(Id))} объедает светящийся мох");
                    }
                }
                else
                {
                    float da = (float)Math.Atan2(dx, dz) - g.Dir;
                    da = (float)Math.Atan2(Math.Sin(da), Math.Cos(da));
                    g.Dir += da * Math.Min(1f, dt * TurnRate);
                    float sx = (float)Math.Sin(g.Dir), sz = (float)Math.Cos(g.Dir);
                    a.V = new EcoVec(sx * WalkSpeed, 0, sz * WalkSpeed);
                    a.P = new EcoVec(a.P.X + a.V.X * dt, a.P.Y, a.P.Z + a.V.Z * dt);
                }
            }
            else
            {
                EcoMoss.Eat(g.Patch, dt * EatRate);
                if (a.Expired || g.Patch.Food < Leave)
                {
                    g.Patch = PickPatch(g);
                    a.Set(EcoState.Seek);
                }
            }
            a.P.X = Math.Max(-EcoAgent.Edge, Math.Min(EcoAgent.Edge, a.P.X));
            a.P.Z = Math.Max(-EcoAgent.Edge, Math.Min(EcoAgent.Edge, a.P.Z));
            a.P.Y = GroundY(world, a.P.X, a.P.Z, g.Patch != null ? g.Patch.Pos.Y : a.P.Y - Lift);
        }
    }
}
