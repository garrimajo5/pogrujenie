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
    /// Принятые модели MOD-001…003 (Meshy по ART-002, исходники в models/): кресло пилота, корпус голостола,
    /// стойка и колесо штурвала. Модели смотрят в −Z (к носу), опорная точка — центр основания на полу;
    /// у колеса — центр ступицы, лицом к пилоту (+Z). Колесо — дочерняя деталь трансформа «Штурвал»
    /// (Bootstrap.Wheel), поэтому крутится вслед за поворотом корабля, стойка стоит на месте.
    /// </summary>
    public static class BridgeProps
    {
        public const string Wheel = "shturval-koleso", Stand = "shturval-stoyka", Chair = "kreslo-pilota", Table = "golostol-korpus";

        /// <summary>Штурвал: центр колеса и наклон к пилоту (как раньше, чтобы из кресла было видно обод).</summary>
        public const float WheelY = 0.95f, WheelZ = -5.25f, WheelTilt = -20f;
        /// <summary>Вал стойки в модели: высота оси и передний торец (м, до масштаба); полутолщина колеса.</summary>
        public const float ShaftY = 0.7125f, ShaftFront = 0.137f, WheelHalfDepth = 0.083f;
        /// <summary>Стойку тянем по высоте, чтобы вал вышел на центр колеса.</summary>
        public const float StandScaleY = WheelY / ShaftY;
        public const float StandZ = WheelZ - WheelHalfDepth - ShaftFront;

        public const float ChairZ = -3.95f, TableZ = -1.2f;

        /// <summary>Неподвижные модели мостика (колесо ставит Bootstrap на трансформ штурвала).</summary>
        public static readonly Prop[] Fixed =
        {
            new Prop(Chair, "Кресло пилота", 0, 0, ChairZ),
            new Prop(Table, "Голостол сонара", 0, 0, TableZ),
            new Prop(Stand, "Стойка штурвала", 0, 0, StandZ, 1f, StandScaleY, 1f),
        };

        /// <summary>Детали, у которых голубые огни текстуры светятся (огни голостола #67DDEB).</summary>
        public static bool GlowingLights(string model) => model == Table;
    }
}
