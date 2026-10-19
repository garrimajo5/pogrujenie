namespace Pogrujenie.Core
{
    /// <summary>Модель художника на мостике: имя в Resources/Models, место в осях корабля, масштаб.</summary>
    public struct Prop
    {
        public string Model, Name;
        public float X, Y, Z, SX, SY, SZ;
        public Prop(string model, string name, float x, float y, float z, float sx = 1f, float sy = 1f, float sz = 1f)
        { Model = model; Name = name; X = x; Y = y; Z = z; SX = sx; SY = sy; SZ = sz; }
    }

    /// <summary>
    /// Принятые модели MOD-001…004 (Meshy по ART-002, исходники в models/): кресло пилота, корпус голостола,
    /// стойка и колесо штурвала, пульт пилота с тремя экранами. Модели смотрят в −Z (к носу), опорная точка — центр основания на полу;
    /// у колеса — центр ступицы, лицом к пилоту (+Z). Колесо — дочерняя деталь трансформа «Штурвал»
    /// (Bootstrap.Wheel), поэтому крутится вслед за поворотом корабля, стойка стоит на месте.
    /// </summary>
    public static class BridgeProps
    {
        public const string Wheel = "shturval-koleso", Stand = "shturval-stoyka", Chair = "kreslo-pilota", Table = "golostol-korpus";
        /// <summary>MOD-004: корпус пульта пилота и три плоскости экранов (одна сетка, игра красит их сама).</summary>
        public const string Console = "pult-pilota", Screens = "pult-ekrany";

        /// <summary>Штурвал: центр колеса и наклон к пилоту (как раньше, чтобы из кресла было видно обод).</summary>
        public const float WheelY = 0.95f, WheelZ = -5.25f, WheelTilt = -20f;
        /// <summary>Вал стойки в модели: высота оси и передний торец (м, до масштаба); полутолщина колеса.</summary>
        public const float ShaftY = 0.7125f, ShaftFront = 0.137f, WheelHalfDepth = 0.083f;
        /// <summary>Стойку тянем по высоте, чтобы вал вышел на центр колеса.</summary>
        public const float StandScaleY = WheelY / ShaftY;
        public const float StandZ = WheelZ - WheelHalfDepth - ShaftFront;

        public const float ChairZ = -3.95f, TableZ = -1.2f;

        /// <summary>
        /// Пульт MOD-004: 2,5 × 1,42 × 0,65 м, лицом к пилоту (+Z), опорная точка — центр основания.
        /// Стоит вплотную к носовой стене (её внутренняя грань z −5,9). В игре окно начинается с 0,8 м, а не от пола,
        /// как на концепте, поэтому пульт ниже (1,05 м): из кресла дно видно над ним уже с 18° вниз. По глубине пульт
        /// ужат до 0,49 м, чтобы не задевать колесо штурвала (z −5,25), стойка штурвала встаёт в проём между тумбами.
        /// </summary>
        public const float ConsoleW = 2.5f, ConsoleH = 1.42f, ConsoleD = 0.65f;
        public const float ConsoleSY = 0.74f, ConsoleSZ = 0.75f, NoseInner = -5.9f;
        public const float ConsoleZ = NoseInner + ConsoleD * ConsoleSZ / 2f;
        /// <summary>Передняя (к пилоту) грань пульта и его верх в игре.</summary>
        public const float ConsoleFront = NoseInner + ConsoleD * ConsoleSZ, ConsoleTop = ConsoleH * ConsoleSY;
        /// <summary>Свечение экранов пульта (#0B1C24 в текстуре — тёмные; светятся ровным голубым, пока на них ничего не рисуем).</summary>
        public const float ScreenGlowR = 0.06f, ScreenGlowG = 0.26f, ScreenGlowB = 0.32f;

        /// <summary>Неподвижные модели мостика (колесо ставит Bootstrap на трансформ штурвала).</summary>
        public static readonly Prop[] Fixed =
        {
            new Prop(Chair, "Кресло пилота", 0, 0, ChairZ),
            new Prop(Table, "Голостол сонара", 0, 0, TableZ),
            new Prop(Stand, "Стойка штурвала", 0, 0, StandZ, 1f, StandScaleY, 1f),
            new Prop(Console, "Пульт пилота", 0, 0, ConsoleZ, 1f, ConsoleSY, ConsoleSZ),
            new Prop(Screens, "Экраны пульта", 0, 0, ConsoleZ, 1f, ConsoleSY, ConsoleSZ),
        };

        /// <summary>Детали, у которых голубые огни текстуры светятся (огни голостола и кнопки пульта #67DDEB).</summary>
        public static bool GlowingLights(string model) => model == Table || model == Console;

        /// <summary>Детали, которые светятся целиком (экраны пульта).</summary>
        public static bool GlowingScreen(string model) => model == Screens;
    }
}
