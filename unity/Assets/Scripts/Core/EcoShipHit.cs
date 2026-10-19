namespace Pogrujenie.Core
{
    /// <summary>
    /// Удар существа по корпусу (Game.hullHit в js/game.js). Экосистема только сообщает об ударе —
    /// кто, чем, с какой силой и где; что удар делает с кораблём (корпус, тряска, течь), решает корабль
    /// (поток А), подписавшись на <see cref="EcoWorld.ShipStruck"/>.
    /// </summary>
    public readonly struct ShipHit
    {
        /// <summary>Вид, который ударил (например "pred").</summary>
        public readonly string SpeciesId;
        /// <summary>Чем: "bite" — укус в броске.</summary>
        public readonly string Kind;
        /// <summary>Сила удара в процентах корпуса, как в браузере (серпохвост — 12).</summary>
        public readonly int Damage;
        /// <summary>Где было существо в момент удара.</summary>
        public readonly EcoVec Pos;

        public ShipHit(string species, string kind, int damage, EcoVec pos)
        {
            SpeciesId = species; Kind = kind; Damage = damage; Pos = pos;
        }

        public override string ToString() => $"{SpeciesId}:{Kind} −{Damage} {Pos}";
    }
}
