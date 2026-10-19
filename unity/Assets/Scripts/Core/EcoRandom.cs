using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Случай по зерну — mulberry32 из js/util.js, число в число: при одном зерне те же значения,
    /// что G.rng() в браузере. Начальное зерно мира — 20261018; новый вид берёт своё зерно
    /// (как G.seed(7031) в js/creatures.js), чтобы не перетасовывать остальной мир.
    /// </summary>
    public sealed class EcoRandom
    {
        public const uint WorldSeed = 20261018;

        uint a;

        public EcoRandom(uint seed = WorldSeed) { a = seed; }

        /// <summary>Внутреннее состояние: по нему можно продолжить ту же последовательность.</summary>
        public uint State { get => a; set => a = value; }

        /// <summary>Число от 0 (включительно) до 1 (G.rng).</summary>
        public double Next()
        {
            unchecked
            {
                a += 0x6D2B79F5u;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        /// <summary>Число от a до b (G.rr).</summary>
        public float Range(float min, float max) => (float)(min + (max - min) * Next());

        /// <summary>Целое от 0 до n−1.</summary>
        public int Index(int n) => n <= 0 ? 0 : Math.Min(n - 1, (int)Math.Floor(Next() * n));

        /// <summary>Случайный элемент (G.pick).</summary>
        public T Pick<T>(IList<T> items) => items[Index(items.Count)];

        /// <summary>Истина с вероятностью p.</summary>
        public bool Chance(float p) => Next() < p;
    }
}
