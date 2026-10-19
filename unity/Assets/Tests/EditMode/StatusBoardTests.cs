using System.Collections.Generic;
using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// Строка состояния по строкам экрана (Core/StatusBoard.cs). По отчёту qa/2026-10-19-0950-bridge-shell,
    /// замечание 2: в трюме строка не помещалась и «открыт» обрезалось на второй строке.
    /// </summary>
    public class StatusBoardTests
    {
        static readonly string[] Hold =
        {
            "Трюм", "глубина 20 м", "ход 0.0 м/с", "реактор 6/8", "прожектор 2",
            "шлюз открыт", "образцов рядом нет", "образцов в трюме 3", "Шлюз открыт",
        };

        static float Letters(string t) => t.Length;

        [Test]
        public void ДлиннаяСтрокаТрюмаРазбитаПоЧастямИВсеСтрокиПомещаются()
        {
            List<string> lines = StatusBoard.Lines(Hold, Letters, 60);
            Assert.Greater(lines.Count, 1, "в 60 букв всё не помещается");
            foreach (var l in lines) Assert.LessOrEqual(l.Length, 60, l);
            string all = string.Join(" · ", lines);
            StringAssert.Contains("шлюз открыт", all, "слово «открыт» не потерялось");
            StringAssert.Contains("образцов рядом нет", all);
            StringAssert.Contains("образцов в трюме 3", all);
            foreach (var l in lines)
            {
                Assert.IsFalse(l.StartsWith(" ") || l.StartsWith("·") || l.EndsWith("·"), "строка не начинается и не кончается разделителем: " + l);
            }
        }

        [Test]
        public void ЧастьНеРвётсяПосередине()
        {
            List<string> lines = StatusBoard.Lines(Hold, Letters, 30);
            var parts = new List<string>();
            foreach (var l in lines) parts.AddRange(l.Split(new[] { StatusBoard.Separator }, System.StringSplitOptions.None));
            CollectionAssert.AreEqual(new[]
            {
                "Трюм", "глубина 20 м", "ход 0.0 м/с", "реактор 6/8", "прожектор 2",
                "шлюз открыт", "образцов рядом нет", "образцов в трюме 3",
            }, parts, "все части целиком и по порядку, повтор «Шлюз открыт» убран");
        }

        [Test]
        public void ПовторСообщенияНеПишетсяБезУчётаРегистра()
        {
            Assert.AreEqual("Трюм · шлюз открыт", StatusBoard.Join(new[] { "Трюм", "шлюз открыт", "Шлюз открыт" }));
            Assert.AreEqual("Трюм · Шлюз закрыт", StatusBoard.Join(new[] { "Трюм", "", null, "Шлюз закрыт" }), "пустые части пропущены");
        }

        [Test]
        public void ВШирокуюСтрокуВсёВОднуИЧастьШиреСтрокиСтоитОдна()
        {
            Assert.AreEqual(1, StatusBoard.Lines(Hold, Letters, 500).Count);
            var lines = StatusBoard.Lines(new[] { "Машинное отделение", "глубина 20 м" }, Letters, 10);
            CollectionAssert.AreEqual(new[] { "Машинное отделение", "глубина 20 м" }, lines);
            Assert.AreEqual(0, StatusBoard.Lines(new string[0], Letters, 10).Count);
        }
    }
}
