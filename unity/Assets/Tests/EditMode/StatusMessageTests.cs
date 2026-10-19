using NUnit.Framework;
using Pogrujenie.Core;

namespace Pogrujenie.Tests
{
    /// <summary>
    /// Сообщение на приборах гаснет через 4,2 с, как Game.toast в js/game.js.
    /// По отчёту qa/2026-10-19-0950-bridge-shell: «Вы за штурвалом» висело после «Встать».
    /// </summary>
    public class StatusMessageTests
    {
        static void Run(StatusMessage m, float seconds)
        {
            int n = (int)System.Math.Round(seconds / 0.05f);
            for (int i = 0; i < n; i++) m.Update(0.05f);
        }

        [Test]
        public void ПустоПриСтарте()
        {
            var m = new StatusMessage();
            Assert.IsFalse(m.Visible);
            Assert.AreEqual("", m.Text);
        }

        [Test]
        public void ДержитсяЧетыреСекундыИГаснет()
        {
            var m = new StatusMessage();
            m.Say("Шлюз открыт");
            Run(m, 4f);
            Assert.AreEqual("Шлюз открыт", m.Text, "4 с ещё видно");
            Run(m, 0.3f);
            Assert.IsFalse(m.Visible, "через 4,3 с погасло");
        }

        [Test]
        public void НовоеСообщениеЗаменяетСтароеИСрокСначала()
        {
            var m = new StatusMessage();
            m.Say("Шлюз открывается");
            Run(m, 3f);
            m.Say("Шлюз открыт");
            Run(m, 3f);
            Assert.AreEqual("Шлюз открыт", m.Text, "срок отсчитывается от нового сообщения");
        }

        [Test]
        public void СвойСрокДляДлинногоСообщения()
        {
            var m = new StatusMessage();
            m.Say("Пробоина!", 6f);
            Run(m, 5.5f);
            Assert.IsTrue(m.Visible);
            Run(m, 1f);
            Assert.IsFalse(m.Visible);
        }

        [Test]
        public void ClearГаситТолькоСвоёСообщение()
        {
            var m = new StatusMessage();
            m.Say("Вы за штурвалом");
            m.Clear("Вы за штурвалом");
            Assert.IsFalse(m.Visible, "встали — сообщение погасло сразу");

            m.Say("Перегрев реактора");
            m.Clear("Вы за штурвалом");
            Assert.AreEqual("Перегрев реактора", m.Text, "чужое сообщение не трогаем");
        }

        [Test]
        public void ПустоеСообщениеНеДержится()
        {
            var m = new StatusMessage();
            m.Say("Шлюз открыт");
            m.Say(null);
            Assert.IsFalse(m.Visible);
            Run(m, 1f);
            Assert.IsFalse(m.Visible);
        }
    }
}
