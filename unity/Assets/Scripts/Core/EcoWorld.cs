using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    /// <summary>Модуль экосистемы — один вид или связь видов (как блоки E.update в js/creatures.js).</summary>
    public interface IEcoSystem
    {
        /// <summary>Расставить существ заново (мир сброшен, случай — с начала).</summary>
        void Reset(EcoWorld world);

        /// <summary>Шаг на dt секунд (не больше <see cref="EcoWorld.MaxStep"/>).</summary>
        void Step(EcoWorld world, float dt);
    }

    /// <summary>Что экосистема знает о корабле: где он, как быстро идёт, горит ли прожектор.</summary>
    public sealed class EcoShip
    {
        public EcoVec Pos;
        public float Speed;
        public bool Light;
    }

    /// <summary>
    /// Живой мир отмели без рендера: случай по зерну, шина событий, существа и модули видов.
    /// Шаг делится на подшаги не длиннее 0,05 с, модули идут в порядке добавления, поэтому при одном
    /// зерне и одном вводе мир повторяется точь-в-точь — на этом стоят тесты. Сцена (Game) только
    /// рисует существ по <see cref="Agents"/> и читает события из <see cref="Bus"/>.
    /// </summary>
    public sealed class EcoWorld
    {
        public const float MaxStep = 0.05f;

        readonly List<IEcoSystem> systems = new List<IEcoSystem>();

        public uint Seed { get; private set; }
        public EcoRandom Random { get; private set; }
        public readonly EcoBus Bus = new EcoBus();
        public readonly List<EcoAgent> Agents = new List<EcoAgent>();
        public readonly EcoShip Ship = new EcoShip();
        /// <summary>Высота дна в точке (x, z) в осях Unity, например Seabed.Height; null — дна нет.</summary>
        public Func<float, float, float> Ground;
        /// <summary>Секунды с последнего сброса.</summary>
        public float Time { get; private set; }
        /// <summary>Сколько подшагов сделано с последнего сброса.</summary>
        public int Steps { get; private set; }

        public EcoWorld(uint seed = EcoRandom.WorldSeed)
        {
            Seed = seed;
            Random = new EcoRandom(seed);
        }

        public IReadOnlyList<IEcoSystem> Systems => systems;

        /// <summary>Подключить модуль: он сразу расставляет своих существ.</summary>
        public T Add<T>(T system) where T : IEcoSystem
        {
            systems.Add(system);
            system.Reset(this);
            return system;
        }

        /// <summary>Сбросить мир: случай с начала (то же или новое зерно), существа заново, очередь событий пуста.</summary>
        public void Reset() => Reset(Seed);

        public void Reset(uint seed)
        {
            Seed = seed;
            Random = new EcoRandom(seed);
            Bus.Clear();
            Agents.Clear();
            Time = 0; Steps = 0;
            foreach (var s in systems) s.Reset(this);
        }

        /// <summary>Шаг мира на dt секунд, дробится на подшаги ≤ MaxStep.</summary>
        public void Step(float dt)
        {
            if (dt <= 0) return;
            int n = Math.Max(1, (int)Math.Ceiling(dt / MaxStep - 1e-4));
            float h = dt / n;
            for (int i = 0; i < n; i++)
            {
                Time += h; Steps++;
                foreach (var a in Agents) a.Tick(h);
                for (int k = 0; k < systems.Count; k++) systems[k].Step(this, h);
            }
        }

        /// <summary>Прогнать мир seconds секунд шагами dt (для тестов).</summary>
        public void Run(float seconds, float dt = MaxStep)
        {
            int n = (int)Math.Round(seconds / dt);
            for (int i = 0; i < n; i++) Step(dt);
        }

        /// <summary>Своё зерно для вида (G.seed(7031)): новый вид не перетасовывает остальной мир.</summary>
        public static EcoRandom SeededFor(uint seed) => new EcoRandom(seed);

        public EcoAgent Spawn(string species, EcoVec pos)
        {
            var a = new EcoAgent(species, pos);
            Agents.Add(a);
            return a;
        }

        public int Count(string species, bool aliveOnly = true)
        {
            int c = 0;
            foreach (var a in Agents) if (a.SpeciesId == species && (!aliveOnly || a.Alive)) c++;
            return c;
        }

        /// <summary>Ближайшее живое существо вида не дальше maxDist (null — нет такого).</summary>
        public EcoAgent Nearest(string species, EcoVec pos, float maxDist = float.MaxValue, Func<EcoAgent, bool> where = null)
        {
            EcoAgent best = null;
            float bd = maxDist;
            foreach (var a in Agents)
            {
                if (!a.Alive || a.SpeciesId != species || (where != null && !where(a))) continue;
                float d = EcoVec.Distance(a.P, pos);
                if (d <= bd) { bd = d; best = a; }
            }
            return best;
        }

        /// <summary>Отправить событие экосистемы (E.emit).</summary>
        public EcoEvent Emit(string species, string note, EcoVec pos, Func<string> message = null) =>
            Bus.Emit(species, note, pos, message);
    }
}
