using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Пятна светящегося мха — еда скребунов (W.patches в js/world.js). 22 пятна на дне на тех же
    /// местах, что в браузере (x со знаком минус). У пятна есть еда <see cref="Patch.Food"/> от 0 до 1:
    /// она всё время отрастает на 0,012 в секунду (от нуля до полного — 83 с), скребун на пятне
    /// съедает 0,05 в секунду. Яркость пятна <see cref="Glow"/> — 0,15 у объеденного, 1 у нетронутого.
    /// Модуль подключают в мир раньше скребунов.
    /// </summary>
    public sealed class EcoMoss : IEcoSystem
    {
        /// <summary>Сколько еды отрастает за секунду.</summary>
        public const float Regrow = 0.012f;
        /// <summary>Яркость объеденного пятна.</summary>
        public const float MinGlow = 0.15f;
        /// <summary>Размер пятна (м): квадрат 7 × 7, светится круг радиусом до 3,4 м.</summary>
        public const float Size = 7f, Radius = 3.4f;

        /// <summary>
        /// Места пятен в осях Unity: placeMany(22, −10, 30, 170, −56, −12, 18) из js/world.js —
        /// числа сняты из браузерной версии, x со знаком минус, y — дно в этой точке.
        /// Первые восемь — те же пятна, у которых лежат образцы мха (<see cref="Samples"/>).
        /// </summary>
        public static readonly EcoVec[] Spots =
        {
            new EcoVec(-30.8309f, -37.6724f, 94.4369f),
            new EcoVec(25.4157f, -35.7588f, -79.7119f),
            new EcoVec(112.4688f, -41.8346f, 74.6209f),
            new EcoVec(43.2558f, -43.8208f, 34.7669f),
            new EcoVec(24.8791f, -51.2604f, 173.5895f),
            new EcoVec(-109.8195f, -34.7911f, -20.3002f),
            new EcoVec(-18.7095f, -44.7823f, 22.6775f),
            new EcoVec(-116.9517f, -39.6591f, -47.6367f),
            new EcoVec(165.5336f, -40.6051f, -25.1137f),
            new EcoVec(140.0866f, -49.3867f, 108.3999f),
            new EcoVec(20.0083f, -41.8121f, -46.8639f),
            new EcoVec(97.8924f, -34.3003f, 37.5374f),
            new EcoVec(-6.2860f, -43.1480f, -23.6787f),
            new EcoVec(118.4317f, -45.1394f, 95.0969f),
            new EcoVec(37.5094f, -43.0731f, -11.6377f),
            new EcoVec(64.0230f, -41.8639f, -119.1368f),
            new EcoVec(100.9378f, -37.6128f, -93.3913f),
            new EcoVec(-78.8336f, -38.8845f, -57.8510f),
            new EcoVec(39.3468f, -43.5194f, -52.0988f),
            new EcoVec(-0.0778f, -37.9034f, -127.1762f),
            new EcoVec(-71.2277f, -34.6013f, 78.1338f),
            new EcoVec(119.3342f, -31.9155f, 16.7775f)
        };

        /// <summary>Пятно мха.</summary>
        public sealed class Patch
        {
            public int Index;
            /// <summary>Середина пятна на дне.</summary>
            public EcoVec Pos;
            /// <summary>Еда: 0 — объедено, 1 — нетронуто.</summary>
            public float Food;
            /// <summary>Яркость пятна (mossAlpha).</summary>
            public float Glow => EcoMoss.Glow(Food);
        }

        public readonly List<Patch> Patches = new List<Patch>();

        readonly EcoVec[] spots;
        EcoRandom rnd;

        /// <summary>Пятна на местах из браузера.</summary>
        public EcoMoss() : this(Spots) { }

        /// <summary>Свои места пятен (для тестов).</summary>
        public EcoMoss(params EcoVec[] spots) { this.spots = spots; }

        /// <summary>Своё зерно мха: от зерна мира, остальные виды не сдвигаются.</summary>
        public static uint SeedFor(uint worldSeed) => unchecked(worldSeed * 31u + 7002u);

        /// <summary>Яркость пятна по еде: 0,15 у объеденного, 1 у нетронутого.</summary>
        public static float Glow(float food) => MinGlow + (1f - MinGlow) * Math.Max(0f, Math.Min(1f, food));

        public void Reset(EcoWorld world)
        {
            rnd = EcoWorld.SeededFor(SeedFor(world.Seed));
            Patches.Clear();
            for (int i = 0; i < spots.Length; i++)
                Patches.Add(new Patch { Index = i, Pos = spots[i], Food = rnd.Range(0.6f, 1f) });
        }

        public void Step(EcoWorld world, float dt)
        {
            foreach (var p in Patches) p.Food = Math.Min(1f, p.Food + dt * Regrow);
        }

        /// <summary>Съесть с пятна amount еды (не ниже нуля).</summary>
        public static void Eat(Patch p, float amount) { p.Food = Math.Max(0f, p.Food - amount); }

        /// <summary>Ближайшее пятно по горизонтали не дальше radius (null — нет).</summary>
        public Patch Nearest(EcoVec pos, float radius = float.MaxValue)
        {
            Patch best = null;
            float bd = radius;
            foreach (var p in Patches)
            {
                float d = EcoVec.FlatDistance(p.Pos, pos);
                if (d <= bd) { bd = d; best = p; }
            }
            return best;
        }
    }
}
