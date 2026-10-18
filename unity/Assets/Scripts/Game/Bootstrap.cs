using Pogrujenie.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pogrujenie.Game
{
    /// <summary>
    /// Заготовка Unity-версии: собирает сцену из кода — тёмная вода, дно, «Ковчег-7» с прожектором
    /// и стайка светляков. Прожектор питается от реактора: его дальность — доля «Прожектор».
    /// Сцена Main создаётся меню «Погружение → Создать сцену Main» или сборкой; в пустой сцене
    /// заготовка тоже поднимается сама при нажатии Play.
    /// </summary>
    public sealed class Bootstrap : MonoBehaviour
    {
        public const float LampRange = 32f; // как в браузерной версии: 32 м при базовой доле

        public static Bootstrap Instance { get; private set; }

        public Reactor Reactor { get; private set; }
        public Camera Cam { get; private set; }
        public Transform Ship { get; private set; }
        public Light Lamp { get; private set; }
        public Transform[] Fireflies { get; private set; }
        public TouchInput Controls { get; private set; }
        public Player Player { get; private set; }
        public ShipLayout Layout { get; private set; }
        public ShipView View { get; private set; }

        /// <summary>Прожектор включён (кнопкой действия). Светит, только если у него есть энергия.</summary>
        public bool LampOn = true;

        string lastMsg = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (Instance != null || FindAnyObjectByType<Bootstrap>() != null) return;
            var scene = SceneManager.GetActiveScene();
            // в тестовых сценах и чужих сценах заготовку не поднимаем
            if (scene.name.StartsWith("InitTestScene")) return;
            if (!string.IsNullOrEmpty(scene.path) && scene.name != "Main") return;
            new GameObject("Погружение").AddComponent<Bootstrap>();
        }

        void Awake()
        {
            Instance = this;
            Reactor = new Reactor();
            Reactor.Message += (title, text) => lastMsg = title;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Build();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Build()
        {
            var water = new Color(0.02f, 0.07f, 0.12f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = water;
            RenderSettings.fogDensity = 0.035f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.14f, 0.2f);

            Cam = Camera.main;
            if (Cam == null)
            {
                var camGo = new GameObject("Камера");
                camGo.tag = "MainCamera";
                Cam = camGo.AddComponent<Camera>();
            }
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = water;
            Cam.farClipPlane = 200f;

            // дно отмели
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Дно";
            floor.transform.SetParent(transform);
            floor.transform.position = new Vector3(0, -12f, 0);
            floor.transform.localScale = new Vector3(20, 1, 20);
            Paint(floor, new Color(0.18f, 0.2f, 0.17f));

            // «Ковчег-7»: нос в −Z, как в браузерной версии
            var ship = new GameObject("Ковчег-7").transform;
            ship.SetParent(transform);
            Ship = ship;
            var hull = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            hull.name = "Корпус";
            hull.transform.SetParent(ship, false);
            hull.transform.localPosition = new Vector3(0, 1.5f, 9f);
            hull.transform.localRotation = Quaternion.Euler(90, 0, 0);
            hull.transform.localScale = new Vector3(11f, 16f, 5f); // 32 м: от носа z=−7 до кормы; изнутри не виден
            Paint(hull, new Color(0.55f, 0.5f, 0.42f));
            Destroy(hull.GetComponent<Collider>());

            // отсеки: полы, стены, переборки и люки
            Layout = new ShipLayout();
            Layout.Inter.Add(new Interactable(0, -4.3f, 1.7f,
                () => LampOn ? "Погасить прожектор" : "Включить прожектор", ToggleLamp));
            View = new GameObject("Отсеки").AddComponent<ShipView>();
            View.transform.SetParent(ship, false);
            View.Build(Layout);

            var lampGo = new GameObject("Прожектор");
            lampGo.transform.SetParent(ship, false);
            lampGo.transform.localPosition = new Vector3(0, -0.5f, -7.2f);
            lampGo.transform.localRotation = Quaternion.Euler(15, 180, 0);
            Lamp = lampGo.AddComponent<Light>();
            Lamp.type = LightType.Spot;
            Lamp.spotAngle = 40f;
            Lamp.intensity = 3f;
            Lamp.color = new Color(0.85f, 0.95f, 1f);

            // светляки: стайка кружит перед носом
            Fireflies = new Transform[24];
            for (int i = 0; i < Fireflies.Length; i++)
            {
                var f = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                f.name = "Светляк";
                f.transform.SetParent(transform);
                f.transform.localScale = Vector3.one * 0.3f;
                Paint(f, new Color(0.5f, 1f, 0.8f));
                Destroy(f.GetComponent<Collider>());
                Fireflies[i] = f.transform;
            }

            Cam.transform.SetParent(ship, false);
            Cam.nearClipPlane = 0.05f;
            Controls = gameObject.AddComponent<TouchInput>();
            Player = gameObject.AddComponent<Player>();
            Player.Init(this);
            ApplyPower();
        }

        static void Paint(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r != null) r.material.color = c;
        }

        /// <summary>Прожектор берёт силу из реактора: доля 0 или аварийный сброс — света нет.</summary>
        public void ApplyPower()
        {
            float k = Reactor.K(ShipSystem.Lamp);
            Lamp.enabled = LampOn && k > 0f;
            Lamp.range = LampRange * k;
        }

        /// <summary>Кнопка действия на мостике: включить или погасить прожектор.</summary>
        public void ToggleLamp()
        {
            LampOn = !LampOn;
            if (LampOn && Reactor.K(ShipSystem.Lamp) <= 0f) lastMsg = "Прожектор обесточен";
            ApplyPower();
        }

        void Update()
        {
            // перегрев реактора (Reactor.Update) включится вместе с машинным отделением
            ApplyPower();
            float t = Time.time;
            for (int i = 0; i < Fireflies.Length; i++)
            {
                float a = t * 0.4f + i * 0.26f;
                float r = 6f + Mathf.Sin(i * 1.7f) * 2f;
                Fireflies[i].position = Ship.position + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(t + i) * 1.5f - 2f, -20f + Mathf.Sin(a) * r);
            }
        }

        void OnGUI()
        {
            float s = Mathf.Max(1f, Screen.height / 720f);
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * s) };
            style.normal.textColor = new Color(0.75f, 0.95f, 1f);
            GUI.Label(new Rect(16 * s, 16 * s, 900 * s, 40 * s), "Погружение · Unity 0.1", style);
            style.fontSize = Mathf.RoundToInt(16 * s);
            string line = $"{ShipLayout.RoomName(Player.Room)} · реактор {Reactor.Used}/{Reactor.Limit} · прожектор {Reactor[ShipSystem.Lamp]}";
            if (lastMsg.Length > 0) line += " · " + lastMsg;
            GUI.Label(new Rect(16 * s, 52 * s, 900 * s, 30 * s), line, style);
        }
    }
}
