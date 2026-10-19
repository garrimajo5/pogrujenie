using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Строка состояния на приборах, разложенная по строкам экрана. Части («Трюм», «глубина 20 м»,
    /// «шлюз открыт»…) склеиваются через « · », пока строка помещается в ширину; часть не рвётся
    /// посередине, а переходит на следующую строку целиком. Сообщение на приборах, которое повторяет
    /// уже показанную часть («Шлюз открыт» при «шлюз открыт»), второй раз не пишется.
    /// По отчёту qa/2026-10-19-0950-bridge-shell (замечание 2): в трюме строка не помещалась,
    /// «открыт» уходило на вторую строку и обрезалось.
    /// </summary>
    public static class StatusBoard
    {
        public const string Separator = " · ";

        /// <summary>
        /// Разложить части по строкам ширины <paramref name="width"/>; <paramref name="measure"/> — ширина
        /// текста (в сцене — GUIStyle.CalcSize, в тестах — число букв). Часть шире строки стоит одна.
        /// </summary>
        public static List<string> Lines(IEnumerable<string> parts, Func<string, float> measure, float width)
        {
            var lines = new List<string>();
            var seen = new HashSet<string>();
            string cur = "";
            foreach (var raw in parts)
            {
                string p = raw?.Trim() ?? "";
                if (p.Length == 0) continue;
                // повтор той же части (без учёта регистра) не пишем
                if (!seen.Add(p.ToLowerInvariant())) continue;
                if (cur.Length == 0) { cur = p; continue; }
                string joined = cur + Separator + p;
                if (measure(joined) <= width) cur = joined;
                else { lines.Add(cur); cur = p; }
            }
            if (cur.Length > 0) lines.Add(cur);
            return lines;
        }

        /// <summary>Все части одной строкой (для логов и тестов), без повторов.</summary>
        public static string Join(IEnumerable<string> parts) =>
            string.Join(Separator, Lines(parts, _ => 0f, float.MaxValue));
    }
}
