using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Серпохвосты — крупные хищники рифа (initPreds/predUpdate в js/creatures.js). Три хищника на местах
    /// из браузера (x со знаком минус) патрулируют отмель, чаще у стай светляков. Голод растёт на 0,03 в
    /// секунду; голоднее 0,45 — охота на ближайшего светляка не дальше 50 м (7,6 м/с, 12 с), поймал
    /// ближе 1,6 м — рыбка съедена (<see cref="EcoFish.Kill"/>), событие «catch», 5 с ест.
    /// Корабль манит: прожектор или сонар (последние 12 с) ближе 32 м, шум двигателей быстрее 3 м/с ближе
    /// 24 м. Голоднее 0,2 и без паузы — бросок на корабль (9,5 м/с, до 10 с) с событием «ship», «ping»
    /// или «hear». Ближе 8,5 м к кораблю — укус: удар по корпусу <see cref="EcoWorld.HitShip"/>
    /// (сила 12, что он делает с кораблём — решает корабль), хищник уходит на 60 м и 25 с не нападает.
    /// Сонар (<see cref="OnPing"/>) зовёт хищников ближе 150 м проверить звук, разряд
    /// (<see cref="OnZap"/>) отпугивает ближних. Пиявники, станции чистки, вуали и укрытия стай
    /// придут вместе со своими видами.
    /// </summary>
    public sealed class EcoPred : IEcoSystem
    {
        public const string Id = "pred";

        /// <summary>Места появления (x, z) — js: [[-60, 20], [70, -20], [10, -110]], x со знаком минус.</summary>
        public static readonly float[,] Starts = { { 60f, 20f }, { -70f, -20f }, { -10f, -110f } };

        /// <summary>Скорости по режимам (м/с).</summary>
        public const float PatrolSpeed = 3f, HuntSpeed = 7.6f, InvestigateSpeed = 5.5f, ChargeSpeed = 9.5f,
            RetreatSpeed = 6f, FleeSpeed = 9f, EatSpeed = 1f;
        /// <summary>Голод: прирост в секунду, с какого охотится, с какого нападает на корабль, с какого бросает охоту ради корабля.</summary>
        public const float HungerRate = 0.03f, HuntHunger = 0.45f, AttackHunger = 0.2f, HuntToShipHunger = 0.6f;
        /// <summary>Видит светляка не дальше (м), хватает ближе (м).</summary>
        public const float HuntRadius = 50f, CatchDist = 1.6f;
        /// <summary>Видит свет прожектора и слышит сонар не дальше (м).</summary>
        public const float Reach = 32f;
        /// <summary>Слышит двигатели ближе (м) и быстрее (м/с).</summary>
        public const float HearRadius = 24f, HearSpeed = 3f;
        /// <summary>Сколько секунд после импульса сонара корабль ещё «звучит».</summary>
        public const float PingMemory = 12f;
        /// <summary>Сонар зовёт хищников ближе (м); слышно экипажу, если ближайший ближе (м).</summary>
        public const float PingCall = 150f, PingHeard = 110f;
        /// <summary>Укус ближе (м) к кораблю, сила удара, пауза после укуса (с).</summary>
        public const float BiteDist = 8.5f;
        public const int BiteDamage = 12;
        public const float BiteCooldown = 25f;
        /// <summary>Радиус разряда по корпусу при полной доле реактора (м).</summary>
        public const float ZapRadius = 20f;
        /// <summary>Потолок цели и места появления (м).</summary>
        public const float GoalCeiling = -6f;

        /// <summary>Режим хищника (pr.state в браузере); общий <see cref="EcoState"/> — у его существа.</summary>
        public enum Mode { Patrol, Hunt, Eat, Investigate, Charge, Retreat, Flee }

        /// <summary>Серпохвост: существо мира, режим, голод, пауза перед нападением и добыча.</summary>
        public sealed class Pred
        {
            public EcoAgent Agent;
            public int Index;
            public Mode Mode;
            /// <summary>Голод 0–1.</summary>
            public float Hunger;
            /// <summary>Секунды до того, как снова может напасть на корабль (pr.cd).</summary>
            public float Cooldown;
            /// <summary>За кем охотится.</summary>
            public EcoFish.Fish Prey;
            /// <summary>Где появился (после сброса — сюда).</summary>
            public EcoVec Start;
        }

        public readonly List<Pred> All = new List<Pred>();
        /// <summary>Светляки, на которых охотится; null — охотиться не на кого.</summary>
        public readonly EcoFish Fish;

        EcoRandom rnd;

        public EcoPred(EcoFish fish = null) { Fish = fish; }

        /// <summary>Своё зерно серпохвостов.</summary>
        public static uint SeedFor(uint worldSeed) => unchecked(worldSeed * 31u + 7004u);

        public void Reset(EcoWorld world)
        {
            rnd = EcoWorld.SeededFor(SeedFor(world.Seed));
            All.Clear();
            for (int i = 0; i < Starts.GetLength(0); i++)
            {
                float x = Starts[i, 0], z = Starts[i, 1];
                float y = world.Ground != null ? Math.Min(world.Ground(x, z) + 8f, GoalCeiling) : GoalCeiling;
                var p = new EcoVec(x, y, z);
                var a = world.Spawn(Id, p);
                a.V = new EcoVec(0, 0, 1);
                a.Set(EcoState.Graze);
                All.Add(new Pred { Agent = a, Index = i, Mode = Mode.Patrol, Hunger = rnd.Range(0.1f, 0.5f), Cooldown = 20f, Start = p });
            }
        }

        /// <summary>Импульс сонара из точки pos (E.onPing): хищники ближе 150 м плывут проверить звук.</summary>
        public void OnPing(EcoWorld world, EcoVec pos)
        {
            world.Ship.LastPing = world.Time;
            bool heard = false;
            foreach (var pr in All)
            {
                if (pr.Mode == Mode.Charge || pr.Mode == Mode.Retreat || pr.Mode == Mode.Flee) continue;
                float d = pr.Agent.DistanceTo(pos);
                if (d >= PingCall) continue;
                SetMode(pr, Mode.Investigate, 25f);
                pr.Prey = null;
                pr.Agent.Goal = pos;
                if (d < PingHeard) heard = true;
            }
            if (heard) world.Emit(Id, "ping", pos, () => $"{Species.Cap(world.Name(Id))} услышал сонар и плывёт на звук");
        }

        /// <summary>Разряд по корпусу (E.onZap): хищники ближе 20 м × доля разряда уходят на 50 м и 30 с не нападают.</summary>
        public void OnZap(EcoWorld world, float share = 1f)
        {
            var c = world.Ship.Pos;
            foreach (var pr in All)
            {
                var a = pr.Agent;
                if (a.DistanceTo(c) >= ZapRadius * share) continue;
                SetMode(pr, Mode.Flee, 8f);
                pr.Prey = null;
                pr.Cooldown = 30f;
                var away = a.P - c;
                if (away.LengthSq < 0.01f) away = new EcoVec(1, 0, 0);
                var g = a.P + away.Normalized * 50f;
                g.Y = Math.Min(g.Y, GoalCeiling);
                a.Goal = g;
                var at = a.P;
                world.Emit(Id, "zap", at, () => $"Разряд отпугнул {world.Name(Id, NameCase.Acc)}");
            }
        }

        public void Step(EcoWorld world, float dt)
        {
            foreach (var pr in All) StepPred(world, pr, dt);
        }

        static void SetMode(Pred pr, Mode m, float timer)
        {
            pr.Mode = m;
            pr.Agent.Timer = timer;
            pr.Agent.State = m == Mode.Hunt || m == Mode.Charge ? EcoState.Hunt
                : m == Mode.Investigate ? EcoState.Seek
                : m == Mode.Retreat || m == Mode.Flee ? EcoState.Flee
                : EcoState.Graze;
        }

        /// <summary>Манит ли корабль: свет или сонар ближе 32 м, шум двигателей ближе 24 м.</summary>
        public static bool Lured(EcoWorld world, float dShip)
        {
            var s = world.Ship;
            bool seen = dShip < Reach && (s.Light || world.Time - s.LastPing < PingMemory);
            bool heard = dShip < HearRadius && Math.Abs(s.Speed) > HearSpeed;
            return seen || heard;
        }

        void StartCharge(EcoWorld world, Pred pr)
        {
            SetMode(pr, Mode.Charge, 10f);
            pr.Prey = null;
            var s = world.Ship;
            var at = pr.Agent.P;
            if (s.Light)
                world.Emit(Id, "ship", at, () => $"{Species.Cap(world.Name(Id))} разворачивается к кораблю — его привлёк свет");
            else if (world.Time - s.LastPing < PingMemory)
                world.Emit(Id, "ping", at, () => $"{Species.Cap(world.Name(Id))} разворачивается к кораблю — его привлёк сонар");
            else
                world.Emit(Id, "hear", at, () => $"{Species.Cap(world.Name(Id))} услышал двигатели и разворачивается к кораблю");
        }

        float Floor(EcoWorld world, float x, float z, float fallback) =>
            world.Ground != null ? world.Ground(x, z) : fallback;

        void StepPred(EcoWorld world, Pred pr, float dt)
        {
            var a = pr.Agent;
            pr.Hunger = Math.Min(1f, pr.Hunger + dt * HungerRate);
            pr.Cooldown -= dt;
            var ship = world.Ship.Pos;
            float dShip = a.DistanceTo(ship);
            bool canAttack = pr.Cooldown <= 0f && pr.Hunger > AttackHunger && Lured(world, dShip);

            float speed = PatrolSpeed;
            switch (pr.Mode)
            {
                case Mode.Patrol:
                    if (a.Expired || a.DistanceTo(a.Goal) < 4f)
                    {
                        a.Timer = rnd.Range(10, 20);
                        float gx, gz;
                        if (Fish != null && Fish.Shoals.Count > 0 && rnd.Next() < 0.6)
                        {
                            var h = rnd.Pick(Fish.Shoals).Home;
                            gx = h.X + rnd.Range(-25, 25); gz = h.Z + rnd.Range(-25, 25);
                        }
                        else { gx = rnd.Range(-150, 170); gz = rnd.Range(-170, 150); }
                        float gy = Math.Min(Floor(world, gx, gz, GoalCeiling - 14f) + rnd.Range(4, 14), GoalCeiling);
                        a.Goal = new EcoVec(gx, gy, gz);
                    }
                    if (pr.Hunger > HuntHunger && Fish != null)
                    {
                        var f = Fish.NearestFish(a.P, HuntRadius);
                        if (f != null) { SetMode(pr, Mode.Hunt, 12f); pr.Prey = f; }
                    }
                    if (canAttack) StartCharge(world, pr);
                    break;

                case Mode.Hunt:
                    speed = HuntSpeed;
                    if (pr.Prey == null || !pr.Prey.Agent.Alive || a.Expired)
                    {
                        SetMode(pr, Mode.Patrol, 0f); pr.Prey = null;
                        break;
                    }
                    a.Goal = pr.Prey.Agent.P + pr.Prey.Agent.V * 0.4f;
                    if (a.DistanceTo(pr.Prey.Agent.P) < CatchDist)
                    {
                        Fish.Kill(pr.Prey);
                        pr.Hunger = 0f; pr.Prey = null;
                        SetMode(pr, Mode.Eat, 5f);
                        world.Emit(Id, "catch", a.P,
                            () => $"{Species.Cap(world.Name(Id))} поймал {world.Name(EcoFish.Id, NameCase.Acc)}");
                    }
                    else if (canAttack && pr.Hunger > HuntToShipHunger) StartCharge(world, pr);
                    break;

                case Mode.Eat:
                    speed = EatSpeed;
                    if (a.Expired) SetMode(pr, Mode.Patrol, 0f);
                    break;

                case Mode.Investigate:
                    speed = InvestigateSpeed;
                    if (a.DistanceTo(a.Goal) < 10f || a.Expired) SetMode(pr, Mode.Patrol, 0f);
                    if (canAttack) StartCharge(world, pr);
                    break;

                case Mode.Charge:
                    speed = ChargeSpeed;
                    a.Goal = ship;
                    if (dShip < BiteDist)
                    {
                        world.HitShip(new ShipHit(Id, "bite", BiteDamage, a.P));
                        SetMode(pr, Mode.Retreat, 14f);
                        pr.Cooldown = BiteCooldown;
                        var away = a.P - ship; away.Y = 0;
                        if (away.LengthSq < 0.01f) away = new EcoVec(1, 0, 0);
                        var g = a.P + away.Normalized * 60f;
                        g.Y = Math.Min(Floor(world, g.X, g.Z, GoalCeiling - 8f) + 8f, GoalCeiling);
                        a.Goal = g;
                    }
                    else if (a.Expired) { SetMode(pr, Mode.Patrol, 0f); pr.Cooldown = 10f; }
                    break;

                case Mode.Retreat:
                case Mode.Flee:
                    speed = pr.Mode == Mode.Flee ? FleeSpeed : RetreatSpeed;
                    if (a.Expired) SetMode(pr, Mode.Patrol, 0f);
                    break;
            }

            // прямо к цели с постоянной скоростью; в охоте и броске поворачивает резче
            var d = a.Goal - a.P;
            var want = d.LengthSq > 1e-4f ? d.Normalized * speed : d;
            a.Agility = pr.Mode == Mode.Hunt || pr.Mode == Mode.Charge ? 1.6f : 1.1f;
            a.Drive(want, dt, world.Ground);
        }
    }
}
