using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    public enum Room { Bridge, Corridor, Lab, Engine, Cargo }

    public enum BoxKind { Floor, Ceil, Wall, Wall2, Bulkhead, Dark, Lamp, Trim }

    /// <summary>Брусок геометрии отсека: размеры и центр в осях корабля, метры.</summary>
    public struct Box
    {
        public float W, H, D, X, Y, Z; public BoxKind Kind; public string Name;
        public Box(string name, BoxKind kind, float w, float h, float d, float x, float y, float z)
        { Name = name; Kind = kind; W = w; H = h; D = d; X = x; Y = y; Z = z; }
        public float MinX => X - W / 2; public float MaxX => X + W / 2;
        public float MinY => Y - H / 2; public float MaxY => Y + H / 2;
        public float MinZ => Z - D / 2; public float MaxZ => Z + D / 2;
    }

    /// <summary>Люк в переборке: открывается за ~0,55 с, проходим, когда открыт больше чем на 85%.</summary>
    public sealed class Door
    {
        public const float Speed = 1.8f, Slide = 1.75f, PassAt = 0.85f;
        public readonly float Z;
        public bool Open;
        public float T;
        public Door(float z) { Z = z; }
        public bool Passable => T > PassAt;
        /// <summary>Насколько створка уехала вбок, м.</summary>
        public float Offset { get { float t = T; return t * t * (3 - 2 * t) * Slide; } }
        public void Update(float dt) { T = Math.Max(0, Math.Min(1, T + (Open ? 1 : -1) * dt * Speed)); }
    }

    public struct Obstacle
    {
        public float X, Z, R;
        public Obstacle(float x, float z, float r) { X = x; Z = z; R = r; }
    }

    /// <summary>Пост или предмет, с которым работает кнопка действия.</summary>
    public sealed class Interactable
    {
        public float X, Z, R;
        public Func<string> Label;
        public Action Do;
        public Func<bool> On; // null — всегда доступен
        /// <summary>Пост удержания: каждый кадр, пока кнопка действия зажата (шаг, с). null — только нажатие.</summary>
        public Action<float> Hold;
        public Interactable(float x, float z, float r, Func<string> label, Action act)
        { X = x; Z = z; R = r; Label = label; Do = act; }
    }

    /// <summary>
    /// Отсеки «Ковчега-7» по браузерной версии (js/ship.js): мостик z∈[−6,2], коридор [2,8], лаборатория [8,14],
    /// машинное отделение [14,19,5], трюм [19,5,24]; четыре люка в переборках. Отсеки симметричны по x,
    /// поэтому смена знака x при переносе (левая система Unity) на стены не влияет.
    /// </summary>
    public sealed class ShipLayout
    {
        public const float R = 0.35f; // радиус игрока

        public static readonly float[] DoorZ = { 2f, 8f, 14f, 19.5f };

        public readonly Door[] Doors;
        public readonly List<Obstacle> Obstacles = new List<Obstacle>
        {
            new Obstacle(0, -5.5f, 1.0f),   // пульт штурвала
            new Obstacle(0, -3.95f, 0.35f), // кресло пилота
            new Obstacle(0, -1.2f, 1.2f),   // голостол сонара
            new Obstacle(0, 17.2f, 1.05f),  // реактор
        };
        public readonly List<Interactable> Inter = new List<Interactable>();
        /// <summary>Нижний шлюз трюма: пост «Открыть шлюз»/«Закрыть шлюз» над крышкой.</summary>
        public readonly Airlock Lock = new Airlock();

        public ShipLayout()
        {
            Doors = new Door[DoorZ.Length];
            for (int i = 0; i < DoorZ.Length; i++)
            {
                var d = Doors[i] = new Door(DoorZ[i]);
                Inter.Add(new Interactable(0, d.Z, 1.5f, () => d.Open ? "Закрыть люк" : "Открыть люк", () => d.Open = !d.Open));
            }
            Inter.Add(new Interactable(0, Airlock.PostZ, Airlock.PostR, () => Lock.Label, Lock.Toggle));
        }

        public static Room RoomAt(float z) =>
            z < 1.9f ? Room.Bridge : z < 8f ? Room.Corridor : z < 14f ? Room.Lab : z < 19.5f ? Room.Engine : Room.Cargo;

        public static string RoomName(Room r)
        {
            switch (r)
            {
                case Room.Bridge: return "Мостик";
                case Room.Corridor: return "Коридор";
                case Room.Lab: return "Лаборатория";
                case Room.Engine: return "Машинное отделение";
                default: return "Трюм";
            }
        }

        /// <summary>Можно ли стоять в точке (центр игрока): отсеки, проёмы открытых люков, без препятствий.</summary>
        public bool CanStand(float x, float z)
        {
            bool ok = false;
            if (x > -5 + R && x < 5 - R && z > -6 + R && z < 2 - R) ok = true;
            else if (x > -1.1f + R && x < 1.1f - R)
            {
                if (z >= 2.45f && z <= 7.55f) ok = true;
                else if (z > 1.4f && z < 2.6f && Doors[0].Passable) ok = true;
                else if (z > 7.4f && z < 8.6f && Doors[1].Passable) ok = true;
            }
            if (!ok && x > -4 + R && x < 4 - R && z > 8 + R && z < 14 - R) ok = true;
            if (!ok && x > -3 + R && x < 3 - R && z > 14 + R && z < 19.5f - R) ok = true;
            if (!ok && x > -1.1f + R && x < 1.1f - R && z > 13.4f && z < 14.6f && Doors[2].Passable) ok = true;
            if (!ok && x > -2.5f + R && x < 2.5f - R && z > 19.5f + R && z < 24 - R) ok = true;
            if (!ok && x > -1.1f + R && x < 1.1f - R && z > 18.9f && z < 20.1f && Doors[3].Passable) ok = true;
            if (!ok) return false;
            foreach (var o in Obstacles)
            {
                float dx = x - o.X, dz = z - o.Z;
                if (dx * dx + dz * dz < (o.R + R) * (o.R + R)) return false;
            }
            return true;
        }

        public void Update(float dt)
        {
            foreach (var d in Doors) d.Update(dt);
            Lock.Update(dt);
        }

        /// <summary>
        /// Ближайший пост в пределах его радиуса. Дальше 0,9 м — только если он перед игроком
        /// (не больше ~72° от взгляда), как в браузерной версии. Курс: вперёд = (sin yaw, cos yaw).
        /// </summary>
        public Interactable Nearest(float x, float z, float yaw)
        {
            float fx = (float)Math.Sin(yaw), fz = (float)Math.Cos(yaw);
            Interactable best = null; float bd = float.MaxValue;
            foreach (var it in Inter)
            {
                float dx = it.X - x, dz = it.Z - z, d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d > it.R || (it.On != null && !it.On())) continue;
                if (d > 0.9f && (dx * fx + dz * fz) / d < 0.3f) continue;
                if (d < bd) { bd = d; best = it; }
            }
            return best;
        }

        /// <summary>Проём в полу трюма над крышкой шлюза (квадрат, м) и глубина шахты до крышки.</summary>
        public const float LockHole = 1.6f, ShaftDepth = 0.85f;
        /// <summary>Прутья решётки над проёмом: по середине пусто, там идёт трос лебёдки.</summary>
        public static readonly float[] GrateBars = { -0.6f, -0.25f, 0.25f, 0.6f };
        /// <summary>Неподвижная геометрия отсеков: полы, потолки, стены с окнами, переборки с проёмами люков.</summary>
        // ниже GrateBars: статические поля заполняются по порядку
        public static readonly Box[] Boxes = BuildBoxes();

        static Box[] BuildBoxes()
        {
            var b = new List<Box>();
            void Add(string n, BoxKind k, float w, float h, float d, float x, float y, float z) => b.Add(new Box(n, k, w, h, d, x, y, z));

            // полы и потолки
            Add("Пол мостика", BoxKind.Floor, 10, 0.1f, 8, 0, -0.05f, -2);
            Add("Пол коридора", BoxKind.Floor, 2.2f, 0.1f, 6, 0, -0.05f, 5);
            Add("Пол лаборатории", BoxKind.Floor, 8, 0.1f, 6, 0, -0.1f, 11);
            Add("Пол машинного", BoxKind.Floor, 6, 0.1f, 5.5f, 0, -0.05f, 16.75f);
            // пол трюма — вокруг проёма люка шлюза (LockHole × LockHole над крышкой)
            Add("Пол трюма: у машинного", BoxKind.Floor, 5, 0.1f, Airlock.Z - LockHole / 2 - 19.5f, 0, -0.05f, (19.5f + Airlock.Z - LockHole / 2) / 2);
            Add("Пол трюма: у кормы", BoxKind.Floor, 5, 0.1f, 24f - Airlock.Z - LockHole / 2, 0, -0.05f, (24f + Airlock.Z + LockHole / 2) / 2);
            foreach (float sx in new[] { -1f, 1f })
                Add("Пол трюма: у борта", BoxKind.Floor, 2.5f - LockHole / 2, 0.1f, LockHole, sx * (2.5f + LockHole / 2) / 2, -0.05f, Airlock.Z);
            Add("Потолок мостика", BoxKind.Ceil, 10, 0.1f, 8, 0, 3.25f, -2);
            Add("Потолок коридора", BoxKind.Ceil, 2.2f, 0.1f, 6, 0, 2.65f, 5);
            Add("Потолок лаборатории", BoxKind.Ceil, 8, 0.1f, 6, 0, 3.45f, 11);
            Add("Потолок машинного", BoxKind.Ceil, 6, 0.1f, 5.5f, 0, 3.25f, 16.75f);
            Add("Потолок трюма", BoxKind.Ceil, 5, 0.1f, 4.5f, 0, 3.05f, 21.75f);

            // мостик: носовое окно 7,6 × 1,9 м (y от 0,8 до 2,7), глухие борта
            Add("Нос: под окном", BoxKind.Wall, 10, 0.8f, 0.2f, 0, 0.4f, -6);
            Add("Нос: над окном", BoxKind.Wall, 10, 0.5f, 0.2f, 0, 2.95f, -6);
            Add("Нос: слева от окна", BoxKind.Wall, 1.2f, 1.9f, 0.2f, -4.4f, 1.75f, -6);
            Add("Нос: справа от окна", BoxKind.Wall, 1.2f, 1.9f, 0.2f, 4.4f, 1.75f, -6);
            Add("Борт мостика", BoxKind.Wall, 0.2f, 3.2f, 8, -5, 1.6f, -2);
            Add("Борт мостика", BoxKind.Wall, 0.2f, 3.2f, 8, 5, 1.6f, -2);
            Bulkhead(b, 2, 10, 3.2f);
            // свет мостика — 10 плафонов на бортах, рама окна и скос потолка: Core/BridgePlan.cs (ART-002)
            Add("Пульт штурвала", BoxKind.Dark, 1.9f, 0.55f, 0.55f, 0, 0.275f, -5.6f);
            // кресло пилота, голостол и штурвал — модели художника MOD-001…003 (Core/BridgeProps.cs)

            // коридор
            Add("Стена коридора", BoxKind.Wall, 0.2f, 2.6f, 6, -1.1f, 1.3f, 5);
            Add("Стена коридора", BoxKind.Wall, 0.2f, 2.6f, 6, 1.1f, 1.3f, 5);
            Add("Лампа коридора", BoxKind.Lamp, 0.15f, 0.03f, 5.6f, 0, 2.59f, 5);

            // лаборатория: окна 3,6 × 1,6 м в обоих бортах
            Bulkhead(b, 8, 8, 3.4f, BoxKind.Wall2);
            foreach (float sx in new[] { -4f, 4f })
            {
                Add("Борт лаборатории: под окном", BoxKind.Wall2, 0.2f, 0.95f, 6, sx, 0.475f, 11);
                Add("Борт лаборатории: над окном", BoxKind.Wall2, 0.2f, 0.85f, 6, sx, 2.975f, 11);
                Add("Борт лаборатории: у носа", BoxKind.Wall2, 0.2f, 1.6f, 1.2f, sx, 1.75f, 8.6f);
                Add("Борт лаборатории: у кормы", BoxKind.Wall2, 0.2f, 1.6f, 1.2f, sx, 1.75f, 13.4f);
            }
            Bulkhead(b, 14, 8, 3.4f, BoxKind.Wall2);

            // машинное отделение и трюм
            Add("Борт машинного", BoxKind.Wall, 0.2f, 3.2f, 5.5f, -3, 1.6f, 16.75f);
            Add("Борт машинного", BoxKind.Wall, 0.2f, 3.2f, 5.5f, 3, 1.6f, 16.75f);
            Bulkhead(b, 19.5f, 6, 3.2f);
            Add("Борт трюма", BoxKind.Wall2, 0.2f, 3f, 4.5f, -2.5f, 1.5f, 21.75f);
            Add("Борт трюма", BoxKind.Wall2, 0.2f, 3f, 4.5f, 2.5f, 1.5f, 21.75f);
            Add("Корма", BoxKind.Wall2, 5, 3f, 0.2f, 0, 1.5f, 24);
            Add("Лампа трюма", BoxKind.Lamp, 0.15f, 0.04f, 3.8f, 0, 2.99f, 21.75f);
            // люк шлюза: светлая кромка вокруг проёма, решётка вровень с полом, шахта до крышки
            const float hh = LockHole / 2, rim = 0.12f;
            foreach (float s in new[] { -1f, 1f })
            {
                Add("Кромка люка шлюза", BoxKind.Lamp, LockHole + 2 * rim, 0.03f, rim, 0, 0.015f, Airlock.Z + s * (hh + rim / 2));
                Add("Кромка люка шлюза", BoxKind.Lamp, rim, 0.03f, LockHole, s * (hh + rim / 2), 0.015f, Airlock.Z);
                Add("Шахта шлюза", BoxKind.Dark, LockHole + 0.2f, ShaftDepth, 0.1f, 0, -ShaftDepth / 2, Airlock.Z + s * (hh + 0.05f));
                Add("Шахта шлюза", BoxKind.Dark, 0.1f, ShaftDepth, LockHole, s * (hh + 0.05f), -ShaftDepth / 2, Airlock.Z);
            }
            foreach (float x in GrateBars)
                Add("Решётка люка", BoxKind.Trim, 0.06f, 0.04f, LockHole, x, -0.02f, Airlock.Z);
            return b.ToArray();
        }

        /// <summary>Переборка шириной width с проёмом люка 1,6 × 2,3 м посередине.</summary>
        static void Bulkhead(List<Box> b, float z, float width, float height, BoxKind kind = BoxKind.Wall)
        {
            float side = width / 2 - 0.8f;
            b.Add(new Box("Переборка", kind, side, height, 0.2f, -(0.8f + side / 2), height / 2, z));
            b.Add(new Box("Переборка", kind, side, height, 0.2f, 0.8f + side / 2, height / 2, z));
            b.Add(new Box("Над люком", kind, 1.6f, height - 2.3f, 0.2f, 0, 2.3f + (height - 2.3f) / 2, z));
        }
    }
}
