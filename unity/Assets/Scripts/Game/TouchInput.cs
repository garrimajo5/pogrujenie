using System;
using Pogrujenie.Core;
using UnityEngine;

namespace Pogrujenie.Game
{
    /// <summary>
    /// Экранное управление для телефона: крестовина слева, кнопка действия справа, свайп по остальному
    /// экрану — обзор. За штурвалом добавляются «Всплыть», «Вниз» и «Свет».
    /// В редакторе работают мышь (как один палец), WASD/стрелки, E/Enter, R/F (вверх-вниз) и L (свет).
    /// Тесты подменяют ввод через PadOverride, VertOverride, InjectLook, PressAction и PressLight —
    /// как G.Ctl.padMove и G.Ctl.btnVert в браузере.
    /// </summary>
    public sealed class TouchInput : MonoBehaviour
    {
        public readonly TouchRouter Router = new TouchRouter();

        /// <summary>Подмена крестовины в тестах (x, y из −1, 0, 1); null — читать экран.</summary>
        public Vector2Int? PadOverride;
        /// <summary>Подмена кнопок «Всплыть»/«Вниз» в тестах (1, 0, −1); null — читать экран.</summary>
        public int? VertOverride;

        /// <summary>Показывать кнопки пилота (игрок за штурвалом).</summary>
        public bool PilotButtons;
        /// <summary>Вертикаль пилота: 1 — всплыть, −1 — вниз.</summary>
        public int Vert { get; private set; }
        /// <summary>Нажали «Свет» в этом кадре.</summary>
        public bool LightPressed { get; private set; }

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; } // в «точках» экрана (1/160 дюйма)
        public bool ActionDown { get; private set; }
        public bool ActionPressed { get; private set; }
        public string ActionLabel = "";
        /// <summary>Рядом есть пост — кнопка горит ярче и подписана.</summary>
        public bool ActionEnabled;

        public Circle PadCircle => Router.Pad;
        public Circle ActionCircle => Router.Action;

        bool injectedPress, injectedDown, injectedLight;
        Vector2 injectedLook;
        bool legacyInput = true;
        int lastW, lastH;
        Rect lastSafe;
        Texture2D ring, disc, arrow;

        void Awake()
        {
            Relayout(true);
            try { Input.simulateMouseWithTouches = false; } catch (InvalidOperationException) { legacyInput = false; }
            ring = MakeCircle(128, true);
            disc = MakeCircle(128, false);
            arrow = MakeArrow(64);
        }

        void OnDestroy()
        {
            Destroy(ring); Destroy(disc); Destroy(arrow);
        }

        public void PressAction() { injectedPress = true; }
        public void HoldAction(bool down) { injectedDown = down; if (down) injectedPress = true; }
        public void InjectLook(Vector2 points) { injectedLook += points; }
        public void PressLight() { injectedLight = true; }

        void Relayout(bool force = false)
        {
            var safe = Screen.safeArea;
            if (!force && Screen.width == lastW && Screen.height == lastH && safe == lastSafe) return;
            lastW = Screen.width; lastH = Screen.height; lastSafe = safe;
            TouchLayout.Compute(safe.x, safe.y, safe.width, safe.height, out var pad, out var act);
            Router.SetLayout(pad, act);
            TouchLayout.ComputePilot(safe.x, safe.y, safe.width, safe.height, out var up, out var down, out var light);
            Router.SetPilotLayout(up, down, light);
        }

        /// <summary>Читает экран и клавиатуру. Вызывается игроком один раз в кадр, до движения.</summary>
        public void Poll()
        {
            Relayout();
            Router.EndFrame();
            Router.PilotButtons = PilotButtons;
            if (legacyInput)
            {
                try { ReadScreen(); }
                catch (InvalidOperationException)
                {
                    // в настройках проекта включена только новая Input System — экран читать нечем
                    legacyInput = false;
                    Debug.LogWarning("Старый Input недоступен: включите Active Input Handling = Both");
                }
            }
            float scale = Screen.dpi > 1f ? 160f / Screen.dpi : 1f;
            int mx = Router.MoveX, my = Router.MoveY;
            if (legacyInput)
            {
                if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) my = 1;
                if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) my = -1;
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) mx = -1;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) mx = 1;
            }
            if (PadOverride.HasValue) { mx = PadOverride.Value.x; my = PadOverride.Value.y; }
            Move = new Vector2(mx, my);
            Look = new Vector2(Router.LookDX, Router.LookDY) * scale + injectedLook;
            bool keyPress = legacyInput && (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Return));
            bool keyDown = legacyInput && (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Return));
            ActionPressed = Router.ActionPressed || injectedPress || keyPress;
            ActionDown = Router.ActionDown || injectedDown || keyDown;
            int v = Router.Vert;
            if (legacyInput && PilotButtons)
            {
                if (Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.Space)) v = 1;
                if (Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.C)) v = -1;
            }
            if (VertOverride.HasValue) v = VertOverride.Value;
            Vert = PilotButtons ? v : 0;
            bool keyLight = legacyInput && PilotButtons && Input.GetKeyDown(KeyCode.L);
            LightPressed = PilotButtons && (Router.LightPressed || injectedLight || keyLight);
            injectedLight = false;
            injectedPress = false;
            injectedLook = Vector2.zero;
        }

        void ReadScreen()
        {
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    var p = new P2(t.position.x, t.position.y);
                    switch (t.phase)
                    {
                        case TouchPhase.Began: Router.Begin(t.fingerId, p); break;
                        case TouchPhase.Moved:
                        case TouchPhase.Stationary: Router.Move(t.fingerId, p); break;
                        default: Router.End(t.fingerId); break;
                    }
                }
                return;
            }
            // мышь в редакторе и на ПК — как палец с номером −1
            const int mouse = -1;
            var m = new P2(Input.mousePosition.x, Input.mousePosition.y);
            if (Input.GetMouseButtonDown(0)) Router.Begin(mouse, m);
            else if (Input.GetMouseButton(0)) Router.Move(mouse, m);
            else if (Input.GetMouseButtonUp(0)) Router.End(mouse);
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var pad = Router.Pad; var act = Router.Action;
            var dim = new Color(0.75f, 0.95f, 1f, 0.28f);
            var lit = new Color(0.75f, 0.95f, 1f, 0.75f);
            DrawTex(ring, pad.C, pad.R, dim);
            // четыре стрелки; горит та, куда ведёт палец
            for (int i = 0; i < 4; i++)
            {
                int dx = i == 0 ? 0 : i == 1 ? 1 : i == 2 ? 0 : -1;
                int dy = i == 0 ? 1 : i == 1 ? 0 : i == 2 ? -1 : 0;
                bool on = (dx != 0 && Move.x == dx) || (dy != 0 && Move.y == dy);
                var c = new P2(pad.C.X + dx * pad.R * 0.62f, pad.C.Y + dy * pad.R * 0.62f);
                var r = GuiRect(c, pad.R * 0.28f);
                var old = GUI.matrix;
                GUIUtility.RotateAroundPivot(90f * i, r.center); // по часовой: вверх, вправо, вниз, влево
                GUI.color = on ? lit : dim;
                GUI.DrawTexture(r, arrow);
                GUI.matrix = old;
            }
            var idle = new Color(0.75f, 0.95f, 1f, 0.12f);
            DrawTex(disc, act.C, act.R, !ActionEnabled ? idle : ActionDown ? lit : new Color(0.75f, 0.95f, 1f, 0.5f));
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(act.R * 0.32f),
                wordWrap = true,
            };
            style.normal.textColor = new Color(0.02f, 0.1f, 0.15f);
            GUI.Label(GuiRect(act.C, act.R), ActionLabel, style);
            if (PilotButtons)
            {
                var small = new GUIStyle(style) { fontSize = Mathf.RoundToInt(Router.Up.R * 0.36f) };
                PilotButton(Router.Up, "Всплыть", Vert > 0, small);
                PilotButton(Router.Down, "Вниз", Vert < 0, small);
                LightButton(Router.Light, small);
            }
        }

        /// <summary>Прожектор горит — кнопка «Свет» янтарная, «Свет вкл»; погашен — тусклое кольцо, «Свет выкл».</summary>
        public bool LampLit;

        /// <summary>Подпись кнопки «Свет» сейчас (Core/Searchlight.cs).</summary>
        public string LightLabel => Searchlight.ButtonLabel(LampLit);

        /// <summary>Цвет кнопки «Свет» сейчас.</summary>
        public Color LightColor
        {
            get
            {
                Searchlight.ButtonColor(LampLit, out float r, out float g, out float b, out float a);
                return new Color(r, g, b, a);
            }
        }

        void LightButton(Circle c, GUIStyle style)
        {
            DrawTex(Searchlight.ButtonRingOnly(LampLit) ? ring : disc, c.C, c.R, LightColor);
            GUI.color = Color.white;
            var st = new GUIStyle(style);
            st.normal.textColor = LampLit ? new Color(0.02f, 0.1f, 0.15f) : new Color(0.75f, 0.95f, 1f, 0.85f);
            GUI.Label(GuiRect(c.C, c.R), LightLabel, st);
        }

        void PilotButton(Circle c, string label, bool on, GUIStyle style)
        {
            DrawTex(disc, c.C, c.R, on ? new Color(0.75f, 0.95f, 1f, 0.75f) : new Color(0.75f, 0.95f, 1f, 0.4f));
            GUI.color = Color.white;
            GUI.Label(GuiRect(c.C, c.R), label, style);
        }

        static Rect GuiRect(P2 c, float r) => new Rect(c.X - r, Screen.height - c.Y - r, r * 2, r * 2);

        static void DrawTex(Texture2D tex, P2 c, float r, Color col)
        {
            GUI.color = col;
            GUI.DrawTexture(GuiRect(c, r), tex);
        }

        static Texture2D MakeCircle(int size, bool ringOnly)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (size - 1) / 2f, R = size / 2f - 1;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(R - d); // мягкий край
                    if (ringOnly) a *= Mathf.Clamp01(d - (R - 6f));
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        /// <summary>Треугольник остриём вверх (в координатах GUI).</summary>
        static Texture2D MakeArrow(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // в текстуре y растёт вверх, а GUI рисует её так же — остриё у верхнего края
                    float v = y / (float)(size - 1);
                    float half = (1f - v) * 0.5f;
                    float u = Mathf.Abs(x / (float)(size - 1) - 0.5f);
                    byte a = (byte)(u <= half && v > 0.1f ? 255 : 0);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            t.SetPixels32(px);
            t.Apply();
            return t;
        }
    }
}
