using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    /// <summary>Точка на экране в пикселях: начало внизу слева, y вверх (как у Unity Input).</summary>
    public struct P2
    {
        public float X, Y;
        public P2(float x, float y) { X = x; Y = y; }
        public static P2 operator -(P2 a, P2 b) => new P2(a.X - b.X, a.Y - b.Y);
        public float Length => (float)Math.Sqrt(X * X + Y * Y);
        public override string ToString() => $"({X:0.##}, {Y:0.##})";
    }

    /// <summary>Круг на экране: центр и радиус в пикселях.</summary>
    public struct Circle
    {
        public P2 C; public float R;
        public Circle(P2 c, float r) { C = c; R = r; }
        public bool Contains(P2 p) => (p - C).Length <= R;
        public float Left => C.X - R;
        public float Right => C.X + R;
        public float Bottom => C.Y - R;
        public float Top => C.Y + R;
    }

    /// <summary>
    /// Раскладка экранных органов управления: крестовина внизу слева, кнопка действия внизу справа.
    /// Считается от безопасной области экрана (вырезы и скругления телефона), размеры от её короткой стороны.
    /// </summary>
    public static class TouchLayout
    {
        public const float PadShare = 0.19f, ActionShare = 0.11f, MarginShare = 0.06f;

        public static void Compute(float safeX, float safeY, float safeW, float safeH, out Circle pad, out Circle action)
        {
            float s = Math.Min(safeW, safeH);
            float m = s * MarginShare;
            float pr = s * PadShare, ar = s * ActionShare;
            pad = new Circle(new P2(safeX + m + pr, safeY + m + pr), pr);
            // кнопка действия чуть выше нижнего края — там её удобнее достать большим пальцем
            action = new Circle(new P2(safeX + safeW - m - ar * 1.3f, safeY + m + pr), ar);
        }

        public const float PilotShare = 0.075f;

        /// <summary>
        /// Кнопки пилота (видны только за штурвалом): «Всплыть» и «Вниз» столбиком слева от кнопки действия,
        /// «Свет» над ней. Все внутри безопасной области и не задевают крестовину и кнопку действия.
        /// </summary>
        public static void ComputePilot(float safeX, float safeY, float safeW, float safeH,
            out Circle up, out Circle down, out Circle light)
        {
            Compute(safeX, safeY, safeW, safeH, out _, out var action);
            float s = Math.Min(safeW, safeH);
            float br = s * PilotShare, gap = s * 0.035f;
            float cx = action.C.X - action.R - gap - br * 1.4f;
            up = new Circle(new P2(cx, action.C.Y + br + gap * 0.5f), br);
            down = new Circle(new P2(cx, action.C.Y - br - gap * 0.5f), br);
            if (down.Bottom < safeY + gap) // на низком экране столбик поднимаем
            {
                float lift = safeY + gap - down.Bottom;
                up.C.Y += lift; down.C.Y += lift;
            }
            light = new Circle(new P2(action.C.X, action.Top + gap + br), br);
        }
    }

    /// <summary>Крестовина на 8 направлений, как в браузерной версии (js/controls.js).</summary>
    public static class DPad
    {
        public const float DeadZone = 0.2f; // доля радиуса: в центре крестовины — стоп

        /// <summary>Направление по смещению пальца от центра (y вверх). Компоненты −1, 0 или 1.</summary>
        public static void Direction(float dx, float dy, float radius, out int x, out int y)
        {
            x = 0; y = 0;
            if (Math.Sqrt(dx * dx + dy * dy) <= radius * DeadZone) return;
            double step = Math.PI / 4;
            double ang = Math.Round(Math.Atan2(dy, dx) / step) * step;
            x = (int)Math.Round(Math.Cos(ang));
            y = (int)Math.Round(Math.Sin(ang));
        }
    }

    /// <summary>
    /// Разбирает касания: палец, коснувшийся крестовины, ведёт игрока (его можно вести по кругу и за край),
    /// палец на кнопке действия держит её, остальные пальцы — обзор свайпом. Пальцы работают одновременно.
    /// </summary>
    public sealed class TouchRouter
    {
        enum Role { Pad, Action, Look, Up, Down, Light }

        sealed class Finger { public Role Role; public P2 Last; }

        readonly Dictionary<int, Finger> fingers = new Dictionary<int, Finger>();

        public Circle Pad, Action, Up, Down, Light;

        /// <summary>За штурвалом: кнопки «Всплыть», «Вниз» и «Свет» работают. Иначе там обычный свайп.</summary>
        public bool PilotButtons;

        public int MoveX { get; private set; }
        public int MoveY { get; private set; }
        public bool ActionDown { get; private set; }
        /// <summary>Кнопку нажали в этом кадре; сбрасывается в EndFrame.</summary>
        public bool ActionPressed { get; private set; }
        /// <summary>Нажали «Свет» в этом кадре; сбрасывается в EndFrame.</summary>
        public bool LightPressed { get; private set; }
        /// <summary>Вертикаль: 1 — держат «Всплыть», −1 — «Вниз», 0 — ничего (или кнопки скрыты).</summary>
        public int Vert => !PilotButtons ? 0 : HasRole(Role.Up) ? 1 : HasRole(Role.Down) ? -1 : 0;
        public float LookDX { get; private set; }
        public float LookDY { get; private set; }

        public void SetLayout(Circle pad, Circle action) { Pad = pad; Action = action; }

        public void SetPilotLayout(Circle up, Circle down, Circle light) { Up = up; Down = down; Light = light; }

        public void Begin(int id, P2 p)
        {
            var f = new Finger { Last = p };
            if (Pad.Contains(p)) f.Role = Role.Pad;
            else if (Action.Contains(p)) f.Role = Role.Action;
            else if (PilotButtons && Up.Contains(p)) f.Role = Role.Up;
            else if (PilotButtons && Down.Contains(p)) f.Role = Role.Down;
            else if (PilotButtons && Light.Contains(p)) f.Role = Role.Light;
            else f.Role = Role.Look;
            fingers[id] = f;
            if (f.Role == Role.Action) { ActionDown = true; ActionPressed = true; }
            if (f.Role == Role.Light) LightPressed = true;
            Apply(f, p);
        }

        public void Move(int id, P2 p)
        {
            if (fingers.TryGetValue(id, out var f)) Apply(f, p);
        }

        public void End(int id)
        {
            if (!fingers.TryGetValue(id, out var f)) return;
            fingers.Remove(id);
            if (f.Role == Role.Pad) { MoveX = 0; MoveY = 0; }
            if (f.Role == Role.Action) ActionDown = HasRole(Role.Action);
        }

        bool HasRole(Role r)
        {
            foreach (var f in fingers.Values) if (f.Role == r) return true;
            return false;
        }

        void Apply(Finger f, P2 p)
        {
            switch (f.Role)
            {
                case Role.Pad:
                    var d = p - Pad.C;
                    DPad.Direction(d.X, d.Y, Pad.R, out int x, out int y);
                    MoveX = x; MoveY = y;
                    break;
                case Role.Look:
                    LookDX += p.X - f.Last.X;
                    LookDY += p.Y - f.Last.Y;
                    break;
            }
            f.Last = p;
        }

        /// <summary>Конец кадра: обзор уже применён, «нажатие» прошло.</summary>
        public void EndFrame()
        {
            LookDX = 0; LookDY = 0; ActionPressed = false; LightPressed = false;
        }

        public void Clear()
        {
            fingers.Clear(); MoveX = MoveY = 0; ActionDown = false; EndFrame();
        }
    }

    /// <summary>
    /// Игрок на палубе, координаты корабля: нос в −Z, пол y = 0. Оси как в Unity (левая система):
    /// при взгляде на нос правый борт — это −X (в браузерной версии +X). Курс yaw в радианах:
    /// вперёд = (sin yaw, cos yaw), поэтому yaw = π — лицом к носу. Взгляд вверх — pitch > 0.
    /// </summary>
    public sealed class Walker
    {
        public const float Speed = 2.4f, Radius = 0.35f, Eye = 1.65f, MaxPitch = 1.45f;
        public const float LookSens = 0.0055f; // радиан на «точку» экрана (1/160 дюйма), как в браузере

        public float X, Z, Yaw = (float)Math.PI, Pitch = -0.08f;

        /// <summary>Где можно стоять. Пока только мостик: x∈[−5,5], z∈[−6,2]; остальные отсеки — следующая задача.</summary>
        public Func<float, float, bool> CanStand;

        public Walker()
        {
            CanStand = (x, z) => x > -5 + Radius && x < 5 - Radius && z > -6 + Radius && z < 2 - Radius;
            Reset();
        }

        public void Reset() { X = 0; Z = -3f; Yaw = (float)Math.PI; Pitch = -0.08f; }

        public float ForwardX => (float)Math.Sin(Yaw);
        public float ForwardZ => (float)Math.Cos(Yaw);

        /// <summary>Крестовина: my — вперёд/назад, mx — вправо/влево. Вдоль стены скользит по одной оси.</summary>
        public void Move(float mx, float my, float dt)
        {
            float len = (float)Math.Sqrt(mx * mx + my * my);
            if (len <= 0) return;
            mx /= len; my /= len;
            float sp = Speed * dt;
            float fx = ForwardX, fz = ForwardZ, rx = (float)Math.Cos(Yaw), rz = -(float)Math.Sin(Yaw);
            float dx = (fx * my + rx * mx) * sp, dz = (fz * my + rz * mx) * sp;
            if (CanStand(X + dx, Z)) X += dx;
            if (CanStand(X, Z + dz)) Z += dz;
        }

        /// <summary>Свайп: палец вправо — поворот вправо, палец вверх — взгляд вверх. Смещение в «точках» экрана.</summary>
        public void Look(float dx, float dy, float sensX = 1f, float sensY = 1f)
        {
            Yaw += dx * LookSens * sensX;
            Pitch = Math.Max(-MaxPitch, Math.Min(MaxPitch, Pitch + dy * LookSens * sensY));
        }
    }
}
