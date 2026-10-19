namespace Pogrujenie.Core
{
    /// <summary>
    /// Сообщение на приборах в конце строки состояния («Вы за штурвалом», «Шлюз открыт»…).
    /// Как всплывашка js/game.js (Game.toast): держится 4,2 с и гаснет. Сообщение-состояние
    /// («Вы за штурвалом») можно погасить сразу, когда состояние кончилось, — только если на
    /// приборах всё ещё оно, чужое сообщение не трогаем.
    /// По отчёту qa/2026-10-19-0950-bridge-shell: «Вы за штурвалом» висело после «Встать» до следующего сообщения.
    /// </summary>
    public sealed class StatusMessage
    {
        /// <summary>Сколько секунд держится сообщение (Game.toast в js/game.js — 4200 мс).</summary>
        public const float Duration = 4.2f;

        float left;

        /// <summary>Текст на приборах; пусто — сообщения нет.</summary>
        public string Text { get; private set; } = "";

        public bool Visible => Text.Length > 0;

        /// <summary>Показать сообщение на <paramref name="seconds"/> с (по умолчанию <see cref="Duration"/>).</summary>
        public void Say(string msg, float seconds = Duration)
        {
            Text = msg ?? "";
            left = Text.Length > 0 ? seconds : 0f;
        }

        /// <summary>Погасить сообщение, если на приборах сейчас именно оно.</summary>
        public void Clear(string msg)
        {
            if (Text == msg) { Text = ""; left = 0f; }
        }

        /// <summary>Шаг времени: по истечении срока сообщение гаснет.</summary>
        public void Update(float dt)
        {
            if (Text.Length == 0 || dt <= 0f) return;
            left -= dt;
            if (left <= 0f) { Text = ""; left = 0f; }
        }
    }
}
