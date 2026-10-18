using System;

namespace Pogrujenie.Core
{
    /// <summary>Системы корабля, между которыми инженер делит энергию реактора.</summary>
    public enum ShipSystem { Engines, Sonar, Life, Zap, Lamp }

    /// <summary>
    /// Машинное отделение: реактор даёт 100 единиц, инженер делит их между пятью системами
    /// (не больше 60 на одну, шаг 5). Доля меняет силу системы: 1 — как при базовой доле, 0 — обесточена.
    /// Перегрев: у чёрного курильщика или раз в несколько минут реактор держит только 60 единиц.
    /// Лишняя нагрузка греет его; на 100° — аварийный сброс, всё обесточено на 12 с.
    /// Перенос js/power.js из браузерной версии; чистый C# без UnityEngine, чтобы тестировать в EditMode.
    /// </summary>
    public sealed class Reactor
    {
        public const int Total = 100, Max = 60, Step = 5, Safe = 60;
        public const float ScramTime = 12f, HeatUp = 0.12f, Cool = 0.25f, MinHotTime = 20f,
            Period = 420f, FirstHot = 240f, VentCooldown = 90f;

        public static readonly ShipSystem[] Systems =
            { ShipSystem.Engines, ShipSystem.Sonar, ShipSystem.Life, ShipSystem.Zap, ShipSystem.Lamp };

        static readonly int[] Defaults = { 30, 20, 25, 15, 10 };

        public static string Name(ShipSystem s)
        {
            switch (s)
            {
                case ShipSystem.Engines: return "Двигатели";
                case ShipSystem.Sonar: return "Сонар";
                case ShipSystem.Life: return "Жизнеобеспечение";
                case ShipSystem.Zap: return "Разряд по корпусу";
                default: return "Прожектор";
            }
        }

        public static int Default(ShipSystem s) => Defaults[(int)s];

        readonly int[] share = new int[5];

        public bool Hot { get; private set; }
        public float Heat { get; private set; }
        public float HotTime { get; private set; }
        public float Scram { get; private set; }
        public float NextHot { get; private set; }
        public float VentCd { get; private set; }

        /// <summary>Сообщения для ленты событий: (заголовок, текст).</summary>
        public event Action<string, string> Message;

        public Reactor() { Reset(); }

        public void Reset()
        {
            Array.Copy(Defaults, share, share.Length);
            Hot = false; Heat = 0; HotTime = 0; Scram = 0; NextHot = FirstHot; VentCd = 0;
        }

        public int this[ShipSystem s] => share[(int)s];

        public int Used
        {
            get { int a = 0; foreach (var v in share) a += v; return a; }
        }

        public int Free => Total - Used;

        /// <summary>Сколько единиц сейчас безопасно: 60 при перегреве, иначе 100.</summary>
        public int Limit => Hot ? Safe : Total;

        /// <summary>Множитель системы относительно базовой доли; при аварийном сбросе 0.</summary>
        public float K(ShipSystem s) => Scram > 0 ? 0f : share[(int)s] / (float)Defaults[(int)s];

        /// <summary>Ставит долю: округляет до шага 5, не больше 60 и не больше свободных единиц.</summary>
        public int Set(ShipSystem s, int value)
        {
            int others = Used - share[(int)s];
            int rounded = (int)Math.Floor(value / (double)Step + 0.5) * Step;
            share[(int)s] = Math.Max(0, Math.Min(Math.Min(Max, rounded), Total - others));
            return share[(int)s];
        }

        public int Add(ShipSystem s, int delta) => Set(s, share[(int)s] + delta);

        public bool StartHot(bool vent)
        {
            if (Hot) return false;
            Hot = true; HotTime = 0; Heat = Math.Max(Heat, 20f);
            Message?.Invoke("Перегрев реактора",
                (vent ? "Тёплая вода у курильщика не охлаждает реактор. " : "") +
                $"Безопасно только {Safe} единиц — урежьте нагрузку в машинном отделении.");
            return true;
        }

        /// <param name="dt">шаг времени, с</param>
        /// <param name="nearVent">корабль ближе 45 м к чёрному курильщику</param>
        public void Update(float dt, bool nearVent)
        {
            VentCd -= dt;
            if (Scram > 0)
            {
                Scram -= dt;
                if (Scram <= 0) { Scram = 0; Heat = 50; Message?.Invoke("Реактор перезапущен", "Системы снова под напряжением."); }
            }
            if (!Hot)
            {
                NextHot -= dt;
                if (nearVent && VentCd <= 0) { VentCd = VentCooldown; StartHot(true); }
                else if (NextHot <= 0) StartHot(false);
                if (!Hot) Heat = Math.Max(0, Heat - dt * 2);
                return;
            }
            HotTime += dt;
            int over = Used - Safe;
            if (Scram <= 0)
                Heat = Math.Max(0, Math.Min(100, Heat + (over > 0 ? over * HeatUp : (over - 5) * Cool) * dt));
            if (Heat >= 100 && Scram <= 0)
            {
                Scram = ScramTime;
                Message?.Invoke("Аварийный сброс реактора", $"Все системы обесточены на {ScramTime:0} секунд.");
            }
            if (Heat <= 0 && HotTime > MinHotTime)
            {
                Hot = false; NextHot = Period;
                Message?.Invoke("Реактор остыл", "Можно снова раздать все 100 единиц.");
            }
        }
    }
}
