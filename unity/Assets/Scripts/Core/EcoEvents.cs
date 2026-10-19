using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Событие экосистемы (E.emit в js/creatures.js): какой вид, какое наблюдение, где, и текст для
    /// ленты. Текст собирается лениво — в момент показа, когда известно, просканирован ли вид.
    /// </summary>
    public sealed class EcoEvent
    {
        public readonly string SpeciesId, Note;
        public readonly EcoVec Pos;
        public readonly Func<string> Message;

        public EcoEvent(string species, string note, EcoVec pos, Func<string> message)
        {
            SpeciesId = species; Note = note; Pos = pos; Message = message;
        }

        /// <summary>Текст для ленты событий; null — события без текста (только наблюдение).</summary>
        public string Text => Message?.Invoke();

        /// <summary>Наблюдение для журнала: у вида есть заметка с этим ключом.</summary>
        public bool IsObservation => Note != null && Species.Get(SpeciesId).HasNote(Note);
    }

    /// <summary>
    /// Шина событий экосистемы: существа шлют, игра забирает раз в кадр (handleEvents в js/game.js).
    /// Подписчик <see cref="Emitted"/> узнаёт о событии сразу — так связываются потоки: экосистема
    /// шлёт, корабль решает, что с этим делать.
    /// </summary>
    public sealed class EcoBus
    {
        /// <summary>Дальше этого от корабля события не видны экипажу (80 м, как в браузере).</summary>
        public const float Hearing = 80f;

        readonly List<EcoEvent> pending = new List<EcoEvent>();

        public event Action<EcoEvent> Emitted;

        public IReadOnlyList<EcoEvent> Pending => pending;

        public EcoEvent Emit(string species, string note, EcoVec pos, Func<string> message = null)
        {
            if (!Species.Has(species)) throw new ArgumentException("Нет вида: " + species);
            var ev = new EcoEvent(species, note, pos, message);
            pending.Add(ev);
            Emitted?.Invoke(ev);
            return ev;
        }

        /// <summary>Забрать все события по порядку и очистить очередь.</summary>
        public List<EcoEvent> Take()
        {
            var list = new List<EcoEvent>(pending);
            pending.Clear();
            return list;
        }

        /// <summary>Забрать события не дальше radius от точки (корабля), остальные выбросить.</summary>
        public List<EcoEvent> Take(EcoVec near, float radius = Hearing)
        {
            var list = new List<EcoEvent>();
            foreach (var ev in pending)
                if (EcoVec.Distance(ev.Pos, near) <= radius) list.Add(ev);
            pending.Clear();
            return list;
        }

        public void Clear() => pending.Clear();
    }
}
