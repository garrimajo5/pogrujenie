using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Формы деталей корпуса. Размеры в единичных величинах, как у мешей Game/Shapes.cs:
    /// куб 1 м; шар Ø1; цилиндр, капсула, усечённый конус и труба Ø1 высотой 2 по оси y;
    /// купол — шар Ø1, срезанный плоскостью y = −0,25; ротор — диск Ø1 толщиной 1 с пятью лопастями;
    /// клин — брусок 1 м, толстый у −z и острый у +z; пластина — квадрат 1 м в плоскости xy лицом в +z.
    /// </summary>
    public enum Form { Cube, Sphere, Cylinder, Capsule, Dome, Frustum, Tube, Rotor, Wedge, Plate }

    /// <summary>Чем покрашена деталь: металл, швы, окна, линза прожектора, метка шлюза.</summary>
    public enum HullPaint { Metal, Seam, Window, Lens, Lock }

    /// <summary>Узел корпуса по таблице ART-001 (docs/concept-art/kovcheg-7.md).</summary>
    public enum HullNode
    {
        Body, Nose, Stern, Glass, LabWindow, Shroud, Rotor, Mount, Rudder, Stabilizer,
        Searchlight, Manipulator, Airlock, Cover
    }

    /// <summary>Деталь корпуса: форма, центр, масштаб и поворот (градусы, как Quaternion.Euler в Unity).</summary>
    public struct HullPart
    {
        public string Name; public HullNode Node; public Form Form; public HullPaint Paint;
        public float X, Y, Z, SX, SY, SZ, RX, RY, RZ;

        /// <summary>Повернуть вектор, как Quaternion.Euler(RX, RY, RZ) в Unity: сначала z, потом x, потом y.</summary>
        public void Rotate(ref float x, ref float y, ref float z)
        {
            Rot(ref x, ref y, RZ); Rot(ref y, ref z, RX); Rot(ref z, ref x, RY);
        }

        /// <summary>Обратный поворот: из осей корабля в оси детали.</summary>
        public void Unrotate(ref float x, ref float y, ref float z)
        {
            Rot(ref z, ref x, -RY); Rot(ref y, ref z, -RX); Rot(ref x, ref y, -RZ);
        }

        // поворот пары осей (a, b) на угол deg: x→y вокруг z, y→z вокруг x, z→x вокруг y
        static void Rot(ref float a, ref float b, float deg)
        {
            if (deg == 0f) return;
            double r = deg * Math.PI / 180.0, c = Math.Cos(r), s = Math.Sin(r);
            float na = (float)(a * c - b * s), nb = (float)(a * s + b * c);
            a = na; b = nb;
        }

        /// <summary>Точка в осях корабля → в единичных осях формы.</summary>
        public void ToLocal(float x, float y, float z, out float lx, out float ly, out float lz)
        {
            lx = x - X; ly = y - Y; lz = z - Z;
            Unrotate(ref lx, ref ly, ref lz);
            lx /= SX; ly /= SY; lz /= SZ;
        }

        /// <summary>Внутри ли точка (оси корабля) сплошной детали. Для пластины, трубы и ротора — всегда нет.</summary>
        public bool Contains(float x, float y, float z)
        {
            ToLocal(x, y, z, out float lx, out float ly, out float lz);
            float rr = lx * lx + lz * lz;
            switch (Form)
            {
                case Form.Cube: return Math.Abs(lx) <= 0.5f && Math.Abs(ly) <= 0.5f && Math.Abs(lz) <= 0.5f;
                case Form.Sphere: return rr + ly * ly <= 0.25f;
                case Form.Dome: return ly >= -0.25f && rr + ly * ly <= 0.25f;
                case Form.Cylinder: return Math.Abs(ly) <= 1f && rr <= 0.25f;
                case Form.Capsule:
                {
                    float cy = Math.Max(-0.5f, Math.Min(0.5f, ly)), dy = ly - cy;
                    return rr + dy * dy <= 0.25f;
                }
                case Form.Frustum:
                {
                    if (Math.Abs(ly) > 1f) return false;
                    float r = FrustumRadius(ly);
                    return rr <= r * r;
                }
                default: return false;
            }
        }

        /// <summary>Радиус усечённого конуса на высоте ly: 0,5 внизу (y = −1), 0,25 вверху (y = 1).</summary>
        public static float FrustumRadius(float ly) => 0.5f - 0.125f * (ly + 1f);
    }

    /// <summary>
    /// «Ковчег-7» снаружи по принятому концепту ART-001: 28 простых частей. Нос в −Z, пол отсеков y = 0.
    /// Корпус подогнан к отсекам ShipLayout (мостик шириной 10 м, всё внутри от z = −6 до 24),
    /// поэтому он длиннее и шире, чем в заметках художника: 37 м с двигателями, 13,6 м в носовой оболочке.
    /// Изнутри корпус не виден: замкнутые выпуклые детали показывают в отсеки только изнанку,
    /// а её шейдер отбрасывает. Детали симметричны по x, кроме манипулятора на оси.
    /// </summary>
    public static class HullPlan
    {
        public const float CenterY = 1.6f;

        // основной объём: сплюснутая капсула 12,8 × 6 м от z = −7 до 27
        public const float BodyW = 12.8f, BodyH = 6f, BodyZ = 10f, BodyHalf = 17f;
        // носовая оболочка: эллипсоид 13,6 × 6,4 × 16 м, срезан спереди плоскостью z = −7,2 под стекло
        public const float NoseW = 13.6f, NoseH = 6.4f, NoseL = 8f, NoseZ = -3.2f, NoseCut = -7.2f;
        // двигатели: центры кожухов
        public const float EngineX = 5.4f, EngineY = 1.0f, EngineZ = 26.5f, ShroudD = 2.6f, ShroudL = 2.2f;
        public const float SearchlightY = -0.1f, LensZ = -7.75f;

        public static readonly HullPart[] Parts = Build();

        static HullPart[] Build()
        {
            var p = new List<HullPart>();
            void Add(string name, HullNode node, Form f, HullPaint paint,
                float x, float y, float z, float sx, float sy, float sz, float rx = 0, float ry = 0, float rz = 0)
                => p.Add(new HullPart
                {
                    Name = name, Node = node, Form = f, Paint = paint,
                    X = x, Y = y, Z = z, SX = sx, SY = sy, SZ = sz, RX = rx, RY = ry, RZ = rz
                });

            // поворот (90, 0, 0) кладёт ось y формы вдоль +Z корабля; масштаб: (ширина, длина/2 или длина, высота)
            Add("Корпус", HullNode.Body, Form.Capsule, HullPaint.Metal, 0, CenterY, BodyZ, BodyW, BodyHalf, BodyH, 90);
            // купол срезан у −y формы, после поворота срез смотрит в нос; срез на NoseZ − NoseL/2
            Add("Носовая оболочка", HullNode.Nose, Form.Dome, HullPaint.Metal, 0, CenterY, NoseZ, NoseW, NoseL * 2, NoseH, 90);
            // кормовой переход: широкий конец у −y формы → к носу; z от 21 до 29
            Add("Кормовой переход", HullNode.Stern, Form.Frustum, HullPaint.Metal, 0, CenterY, 25f, 11f, 4f, 5.2f, 90);
            // панорамное стекло на срезе носа, лицом вперёд (пластина смотрит в +z, поворот на 180°)
            Add("Панорамное стекло", HullNode.Glass, Form.Plate, HullPaint.Window, 0, 1.75f, NoseCut - 0.02f, 9f, 2f, 1f, 0, 180);

            // окна лаборатории: по два на борт, на цилиндрической части корпуса у отсека z 8…14
            foreach (float sx in new[] { -1f, 1f })
                foreach (float z in new[] { 10.1f, 11.9f })
                    Add("Окно лаборатории", HullNode.LabWindow, Form.Plate, HullPaint.Window,
                        sx * (BodyW / 2 + 0.02f), CenterY, z, 1.7f, 1f, 1f, 0, sx * 90f);

            foreach (float sx in new[] { -1f, 1f })
            {
                float ex = sx * EngineX;
                Add("Кожух двигателя", HullNode.Shroud, Form.Tube, HullPaint.Metal, ex, EngineY, EngineZ, ShroudD, ShroudL / 2, ShroudD, 90);
                Add("Ротор", HullNode.Rotor, Form.Rotor, HullPaint.Seam, ex, EngineY, EngineZ, ShroudD * 0.8f, 0.15f, ShroudD * 0.8f, 90);
                Add("Крепление двигателя", HullNode.Mount, Form.Cube, HullPaint.Seam, sx * 3.45f, EngineY, EngineZ - 0.2f, 1.5f, 0.45f, 1.4f);
                // клин: размах по x, толщина по y, хорда по z; острый край к корме
                Add("Руль глубины", HullNode.Rudder, Form.Wedge, HullPaint.Metal, sx * 5.2f, 2.6f, 24.6f, 2.6f, 0.22f, 1.8f);
            }
            // поворот (0, 0, 90) ставит клин вертикально: размах по y
            Add("Верхний стабилизатор", HullNode.Stabilizer, Form.Wedge, HullPaint.Metal, 0, 4.2f, 25f, 1.6f, 0.26f, 2.6f, 0, 0, 90);
            Add("Нижний стабилизатор", HullNode.Stabilizer, Form.Wedge, HullPaint.Metal, 0, -0.8f, 25f, 1.2f, 0.22f, 2.2f, 0, 0, 90);

            // прожектор под стеклом: корпус-цилиндр вдоль Z и светящаяся линза
            Add("Корпус прожектора", HullNode.Searchlight, Form.Cylinder, HullPaint.Seam, 0, SearchlightY, -7.4f, 0.9f, 0.3f, 0.9f, 90);
            Add("Линза прожектора", HullNode.Searchlight, Form.Sphere, HullPaint.Lens, 0, SearchlightY, LensZ, 0.75f, 0.75f, 0.12f);

            // манипулятор под носом: основание, два звена (капсулы), две губки захвата
            Add("Основание манипулятора", HullNode.Manipulator, Form.Cylinder, HullPaint.Seam, 0, -1.5f, -5.5f, 1f, 0.15f, 1f);
            Add("Звено манипулятора", HullNode.Manipulator, Form.Capsule, HullPaint.Metal, 0, -1.9f, -6.4f, 0.35f, 0.95f, 0.35f, -108.4f);
            Add("Звено манипулятора", HullNode.Manipulator, Form.Capsule, HullPaint.Metal, 0, -1.95f, -7.95f, 0.3f, 0.7f, 0.3f, -69f);
            foreach (float sx in new[] { -1f, 1f })
                Add("Губка захвата", HullNode.Manipulator, Form.Wedge, HullPaint.Seam, sx * 0.17f, -1.65f, -8.85f, 0.12f, 0.25f, 0.6f, 0, 180);

            // нижний шлюз трюма и сервисные крышки на спине
            Add("Нижний шлюз", HullNode.Airlock, Form.Cylinder, HullPaint.Lock, 0, Airlock.Y, Airlock.Z, Airlock.Radius * 2, Airlock.Thick, Airlock.Radius * 2);
            Add("Сервисная крышка", HullNode.Cover, Form.Cube, HullPaint.Seam, 0, 4.62f, 15.5f, 2.4f, 0.25f, 1.8f);
            Add("Сервисная крышка", HullNode.Cover, Form.Cube, HullPaint.Seam, 0, 4.62f, 19f, 2.4f, 0.25f, 1.8f);
            return p.ToArray();
        }

        /// <summary>Внутри ли точка (оси корабля) основного объёма: корпус, носовая оболочка или кормовой переход.</summary>
        public static bool Inside(float x, float y, float z)
        {
            foreach (var part in Parts)
                if ((part.Node == HullNode.Body || part.Node == HullNode.Nose || part.Node == HullNode.Stern)
                    && part.Contains(x, y, z)) return true;
            return false;
        }

        /// <summary>Самая передняя и самая задняя точки по оси корабля.</summary>
        public static float NoseTip => NoseCut;
        public static float SternEnd => 25f + 4f;

        /// <summary>Цвета по палитре ART-001.</summary>
        public static void Color(HullPaint p, out float r, out float g, out float b)
        {
            int c;
            switch (p)
            {
                case HullPaint.Metal: c = 0x8C806B; break;
                case HullPaint.Seam: c = 0x514D43; break;
                case HullPaint.Window: c = 0xF6DEAC; break;
                case HullPaint.Lens: c = 0xE9F7FF; break;
                default: c = 0x9AD9E8; break;
            }
            r = ((c >> 16) & 255) / 255f; g = ((c >> 8) & 255) / 255f; b = (c & 255) / 255f;
        }

        /// <summary>Светится ли краска сама: окна, линза, метка шлюза. Металл эмиссией не красим.</summary>
        public static bool Glows(HullPaint p) => p == HullPaint.Window || p == HullPaint.Lens || p == HullPaint.Lock;
    }
}
