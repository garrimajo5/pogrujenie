using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Светляки-полосники (initFish/updateFish в js/creatures.js): шесть стай по 22 рыбки у кораллов
    /// рифа. Стая бродит у дома, держится вместе (к центру, в ногу, не толкаясь), рыбки не опускаются
    /// на дно и не поднимаются ко льду. Серпохвост ближе 16 м — тревога: стая уходит к дому в кораллы
    /// и там прячется (медленно, ≤ 1 м/с), событие «flee» раз в 30 с. Корабль ближе 11 м на ходу
    /// быстрее 1,2 м/с пугает без события: рыбки шарахаются, но домой не уходят.
    /// Хищники берут рыбок через <see cref="NearestFish"/> и <see cref="Kill"/>; съеденная
    /// появляется у дома через 25–40 с.
    /// </summary>
    public sealed class EcoFish : IEcoSystem
    {
        public const string Id = "fish";
        /// <summary>Рыбок в стае.</summary>
        public const int PerShoal = 22;
        /// <summary>Ближе этого хищник поднимает тревогу (м).</summary>
        public const float PredAlarm = 16f;
        /// <summary>Ближе этого корабль на ходу пугает стаю (м).</summary>
        public const float ShipAlarm = 11f;
        /// <summary>Быстрее этого корабль пугает (м/с).</summary>
        public const float ShipFast = 1.2f;
        /// <summary>Пауза между сообщениями «стая уходит» (с).</summary>
        public const float FleeCooldown = 30f;
        /// <summary>Скорости: прячется у дома, в тревоге, спокойно; не медленнее MinSpeed.</summary>
        public const float HideSpeed = 1f, AlarmSpeed = 6.5f, CalmSpeed = 2.4f, MinSpeed = 0.5f;

        /// <summary>
        /// Дома стай: шесть мест кораллов (W.coralSpots), ближайших к (0, 55) и разнесённых больше чем на 35 м,
        /// плюс 1,5 м вверх — числа сняты из браузерной версии, x со знаком минус. Дом на 3,5 м над дном.
        /// Шестую стаю в браузере уносит на спине гротовик — он придёт своей задачей и поменяет ей Home и Range.
        /// </summary>
        public static readonly EcoVec[] Homes =
        {
            new EcoVec(-15.3753f, -33.0888f, 75.9897f),
            new EcoVec(26.4205f, -40.8460f, 24.4689f),
            new EcoVec(63.3665f, -35.1576f, 80.4209f),
            new EcoVec(-38.0868f, -27.2991f, 115.9097f),
            new EcoVec(-56.1066f, -36.3559f, 6.1835f),
            new EcoVec(-76.8304f, -31.1474f, 52.3365f),
        };

        /// <summary>Цвет стаи (0xRRGGBB), у каждой рыбки своя яркость <see cref="Fish.Bright"/>.</summary>
        public static readonly uint[] Colors = { 0x5ef2d6, 0xffd36b, 0x8fd0ff, 0xff8fd8, 0xb6ff7a, 0x7fe7ff };

        /// <summary>Стая.</summary>
        public sealed class Shoal
        {
            public int Index;
            public EcoVec Home, Goal, Center, Velocity;
            /// <summary>Радиус блуждания вокруг дома (м).</summary>
            public float Range = 14f;
            /// <summary>Секунды тревоги (стая уходит домой или шарахается).</summary>
            public float Alarm;
            public float FleeCd, GoalTimer;
            /// <summary>Сколько рыбок живо.</summary>
            public int Alive;
            /// <summary>От чего шарахается сейчас (null — угрозы нет).</summary>
            public EcoVec? Threat;
            /// <summary>Угроза — корабль, а не хищник: домой не уходят.</summary>
            public bool ThreatIsShip;
            public uint Color;
            public readonly List<Fish> Members = new List<Fish>();
        }

        /// <summary>Рыбка: существо мира и её место в стае.</summary>
        public sealed class Fish
        {
            public EcoAgent Agent;
            public Shoal Shoal;
            /// <summary>Номер среди всех рыбок (место в инстансах).</summary>
            public int Index;
            /// <summary>Фаза покачивания.</summary>
            public float Phase;
            /// <summary>Яркость цвета стаи, 0,75–1,15.</summary>
            public float Bright;
        }

        public readonly List<Shoal> Shoals = new List<Shoal>();
        public readonly List<Fish> All = new List<Fish>();

        EcoRandom rnd;

        /// <summary>Своё зерно стай: от зерна мира, чтобы другой мир был другим, а остальные виды не сдвигались.</summary>
        public static uint SeedFor(uint worldSeed) => unchecked(worldSeed * 31u + 7001u);

        public void Reset(EcoWorld world)
        {
            rnd = EcoWorld.SeededFor(SeedFor(world.Seed));
            Shoals.Clear(); All.Clear();
            for (int si = 0; si < Homes.Length; si++)
            {
                var h = Homes[si];
                var sh = new Shoal { Index = si, Home = h, Goal = h, Center = h, Color = Colors[si % Colors.Length] };
                Shoals.Add(sh);
                for (int k = 0; k < PerShoal; k++)
                {
                    var p = h + new EcoVec(rnd.Range(-3, 3), rnd.Range(0, 3), rnd.Range(-3, 3));
                    var a = world.Spawn(Id, p);
                    a.Home = h;
                    a.Clearance = 0.4f;
                    a.V = new EcoVec(rnd.Range(-1, 1), 0, rnd.Range(-1, 1));
                    var f = new Fish { Agent = a, Shoal = sh, Index = All.Count, Phase = rnd.Range(0, 6) };
                    f.Bright = rnd.Range(0.75f, 1.15f);
                    All.Add(f); sh.Members.Add(f);
                }
                sh.Alive = PerShoal;
            }
        }

        /// <summary>Съесть рыбку: пропадает на 25–40 с, потом появляется у дома стаи.</summary>
        public void Kill(Fish f)
        {
            if (!f.Agent.Alive) return;
            f.Agent.Alive = false;
            f.Agent.V = EcoVec.Zero;
            f.Agent.Timer = rnd.Range(25, 40);
        }

        /// <summary>Ближайшая живая рыбка не дальше radius; skip — кого не видно (в укрытии).</summary>
        public Fish NearestFish(EcoVec pos, float radius, Func<Fish, bool> skip = null)
        {
            Fish best = null;
            float bd = radius;
            foreach (var f in All)
            {
                if (!f.Agent.Alive || (skip != null && skip(f))) continue;
                float d = EcoVec.Distance(f.Agent.P, pos);
                if (d < bd) { bd = d; best = f; }
            }
            return best;
        }

        public void Step(EcoWorld world, float dt)
        {
            foreach (var sh in Shoals) StepShoal(world, sh, dt);
            float t = world.Time;
            foreach (var f in All) StepFish(world, f, dt, t);
        }

        void StepShoal(EcoWorld world, Shoal sh, float dt)
        {
            var c = EcoVec.Zero; var v = EcoVec.Zero; int n = 0;
            foreach (var f in sh.Members)
                if (f.Agent.Alive) { c = c + f.Agent.P; v = v + f.Agent.V; n++; }
            sh.Alive = n;
            if (n > 0) { sh.Center = c * (1f / n); sh.Velocity = v * (1f / n); }
            else { sh.Center = sh.Home; sh.Velocity = EcoVec.Zero; }
            sh.Alarm = Math.Max(0f, sh.Alarm - dt); sh.FleeCd -= dt; sh.GoalTimer -= dt;

            if (sh.GoalTimer <= 0f)
            {
                sh.GoalTimer = rnd.Range(6, 12);
                float r = sh.Range;
                float gx = sh.Home.X + rnd.Range(-r, r), gz = sh.Home.Z + rnd.Range(-r, r);
                float lift = rnd.Range(2, 7);
                float ground = world.Ground != null ? world.Ground(gx, gz) : sh.Home.Y - 3.5f;
                sh.Goal = new EcoVec(gx, Math.Min(ground + lift, -4f), gz);
            }

            sh.Threat = null; sh.ThreatIsShip = false;
            float best = PredAlarm;
            foreach (var a in world.Agents)
            {
                if (!a.Alive || a.SpeciesId != "pred") continue;
                float d = EcoVec.Distance(a.P, sh.Center);
                if (d < best) { best = d; sh.Threat = a.P; }
            }
            if (sh.Threat.HasValue)
            {
                if (sh.Alarm <= 0f && sh.FleeCd <= 0f)
                {
                    sh.FleeCd = FleeCooldown;
                    world.Emit(Id, "flee", sh.Center,
                        () => $"{Species.Cap(world.Name(Id))}: стая уходит в кораллы — рядом хищник");
                }
                sh.Alarm = 3f;
            }
            else if (EcoVec.Distance(world.Ship.Pos, sh.Center) < ShipAlarm && Math.Abs(world.Ship.Speed) > ShipFast)
            {
                sh.Threat = world.Ship.Pos; sh.ThreatIsShip = true;
                sh.Alarm = Math.Max(sh.Alarm, 1.5f);
            }
        }

        void StepFish(EcoWorld world, Fish f, float dt, float t)
        {
            var a = f.Agent;
            var sh = f.Shoal;
            if (!a.Alive)
            {
                if (a.Timer > 0f) return;
                a.Alive = true;
                a.P = sh.Home + new EcoVec(rnd.Range(-2, 2), rnd.Range(0, 2), rnd.Range(-2, 2));
                a.V = new EcoVec(0, 0.2f, 0);
            }
            bool alarm = sh.Alarm > 0f;
            var acc = (sh.Center - a.P) * 0.5f + (sh.Velocity - a.V) * 0.6f;
            var goal = alarm && !sh.ThreatIsShip ? sh.Home : sh.Goal;
            acc = acc + ClampLength(goal - a.P, 8f) * (alarm ? 1.2f : 0.35f);
            foreach (var o in sh.Members)
            {
                if (o == f || !o.Agent.Alive) continue;
                var dp = a.P - o.Agent.P;
                float d2 = dp.LengthSq;
                if (d2 < 0.6f && d2 > 1e-5f) acc = acc + dp * (1.8f / (d2 + 0.05f));
            }
            if (sh.Threat.HasValue)
            {
                var away = a.P - sh.Threat.Value;
                float d = away.Length;
                if (d < 14f && d > 0.01f) acc = acc + away * ((10f * (1f - d / 14f) + 2f) / d);
            }
            acc = acc + new EcoVec((float)Math.Sin(t * 1.7f + f.Phase) * 0.6f,
                                   (float)Math.Sin(t * 1.3f + f.Phase * 2f) * 0.3f,
                                   (float)Math.Cos(t * 1.5f + f.Phase) * 0.6f);
            bool hasGround = world.Ground != null;
            float h = hasGround ? world.Ground(a.P.X, a.P.Z) : 0f;
            if (hasGround && a.P.Y < h + 1.2f) acc.Y += (h + 1.2f - a.P.Y) * 6f;
            if (a.P.Y > -3f) acc.Y -= 4f;

            a.V = a.V + acc * dt;
            bool hide = alarm && EcoVec.Distance(a.P, sh.Home) < 4f;
            float maxS = hide ? HideSpeed : (alarm ? AlarmSpeed : CalmSpeed);
            float sp = a.V.Length;
            if (sp > maxS) a.V = a.V * (maxS / sp);
            else if (sp < MinSpeed) a.V = a.V * (MinSpeed / (sp + 1e-3f));
            a.P = a.P + a.V * dt;
            if (hasGround && a.P.Y < h + a.Clearance) a.P.Y = h + a.Clearance;
            a.P.X = Math.Max(-EcoAgent.Edge, Math.Min(EcoAgent.Edge, a.P.X));
            a.P.Z = Math.Max(-EcoAgent.Edge, Math.Min(EcoAgent.Edge, a.P.Z));
            a.State = hide ? EcoState.Hide : alarm ? EcoState.Flee : EcoState.Graze;
        }

        static EcoVec ClampLength(EcoVec v, float max)
        {
            float l = v.Length;
            return l > max ? v * (max / l) : v;
        }
    }
}
