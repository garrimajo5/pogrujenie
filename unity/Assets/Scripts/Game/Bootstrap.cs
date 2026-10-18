using Pogrujenie.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pogrujenie.Game
{
    /// <summary>
    /// Заготовка Unity-версии: собирает сцену из кода — отмель с рельефом и льдом, вода с туманом
    /// по глубине, «Ковчег-7» с прожектором
    /// и стайка светляков. Прожектор питается от реактора: его дальность — доля «Прожектор».
    /// Корабль ведут за штурвалом (ShipPhysics): тяга зависит от доли «Двигатели», течение сносит всегда.
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
        public ShipPhysics Motion { get; private set; }
        public SeaCurrent Current { get; private set; }
        public Transform Wheel { get; private set; }
        public SeabedView Seafloor { get; private set; }
        /// <summary>Свет сквозь лёд: слабеет с глубиной.</summary>
        public Light Sun { get; private set; }

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
            Motion = new ShipPhysics { Ground = Seabed.Height };
            Current = new SeaCurrent();
            Current.Changed += i => Say("Течение сменилось");
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
            // вода как в браузерной версии: туман FogExp2, рассеянный свет сверху и свет сквозь лёд
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            Cam = Camera.main;
            if (Cam == null)
            {
                var camGo = new GameObject("Камера");
                camGo.tag = "MainCamera";
                Cam = camGo.AddComponent<Camera>();
            }
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.farClipPlane = 200f; // дальше туман всё равно непрозрачен

            var sunGo = new GameObject("Свет сквозь лёд");
            sunGo.transform.SetParent(transform);
            // в браузере свет идёт из (0.3, 1, 0.25); x в Unity меняет знак
            sunGo.transform.rotation = Quaternion.LookRotation(-new Vector3(-0.3f, 1f, 0.25f));
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.color = new Color(0xc8 / 255f, 0xf2 / 255f, 1f);
            Sun.shadows = LightShadows.None;

            // отмель: рельеф из шума как js/world.js и лёд над головой
            Seafloor = new GameObject("Отмель").AddComponent<SeabedView>();
            Seafloor.transform.SetParent(transform);
            Seafloor.Build();

            // «Ковчег-7»: нос в −Z, как в браузерной версии
            var ship = new GameObject("Ковчег-7").transform;
            ship.SetParent(transform);
            Ship = ship;
            ship.position = new Vector3(Motion.X, Motion.Y, Motion.Z);
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
            Layout.Inter.Add(new Interactable(0, Player.SeatZ, 1.7f, () => "Сесть за штурвал", () => Player.SitAtHelm()));
            View = new GameObject("Отсеки").AddComponent<ShipView>();
            View.transform.SetParent(ship, false);
            View.Build(Layout);

            // штурвал на пульте: поворачивается вместе с кораблём
            var wheel = new GameObject("Штурвал").transform;
            wheel.SetParent(ship, false);
            wheel.localPosition = new Vector3(0, 0.95f, -5.25f);
            wheel.localRotation = Quaternion.Euler(-20, 0, 0);
            Wheel = wheel;
            var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rim.name = "Обод штурвала";
            Destroy(rim.GetComponent<Collider>());
            rim.transform.SetParent(wheel, false);
            rim.transform.localRotation = Quaternion.Euler(90, 0, 0);
            rim.transform.localScale = new Vector3(0.6f, 0.02f, 0.6f);
            Paint(rim, new Color(0.12f, 0.17f, 0.19f));
            var spoke = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spoke.name = "Спица штурвала";
            Destroy(spoke.GetComponent<Collider>());
            spoke.transform.SetParent(wheel, false);
            spoke.transform.localPosition = new Vector3(0, 0, 0.03f); // со стороны пилота (+Z)
            spoke.transform.localScale = new Vector3(0.62f, 0.06f, 0.03f);
            Paint(spoke, new Color(0.75f, 0.6f, 0.3f));

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
            ApplyWater();
        }

        static readonly Color HemiSky = new Color(0x9a / 255f, 0xd9 / 255f, 0xe8 / 255f);    // 0x9ad9e8
        static readonly Color HemiGround = new Color(0x0b / 255f, 0x1c / 255f, 0x24 / 255f); // 0x0b1c24

        /// <summary>Вода по глубине корабля: цвет и плотность тумана, рассеянный свет, свет сквозь лёд.</summary>
        public void ApplyWater()
        {
            float depth = Motion.Depth;
            Water.FogColor(depth, out float r, out float g, out float b);
            var fog = new Color(r, g, b);
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = Water.FogDensity(depth);
            Cam.backgroundColor = fog;
            float a = Water.Ambient(depth);
            RenderSettings.ambientSkyColor = HemiSky * a;
            RenderSettings.ambientEquatorColor = Color.Lerp(HemiSky, HemiGround, 0.5f) * a;
            RenderSettings.ambientGroundColor = HemiGround * a;
            Sun.intensity = Water.Sun(depth);
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

        /// <summary>Строка-сообщение на приборах.</summary>
        public void Say(string msg) { lastMsg = msg; }

        /// <summary>Кнопка «Свет» за штурвалом: включить или погасить прожектор.</summary>
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
            Step(Time.deltaTime);
            ApplyWater();
            float t = Time.time;
            for (int i = 0; i < Fireflies.Length; i++)
            {
                // стайка живёт у места старта, перед носом; корабль может от неё уплыть
                float a = t * 0.4f + i * 0.26f;
                float r = 6f + Mathf.Sin(i * 1.7f) * 2f;
                Fireflies[i].position = new Vector3(ShipPhysics.StartX + Mathf.Cos(a) * r,
                    ShipPhysics.StartY + Mathf.Sin(t + i) * 1.5f - 2f, ShipPhysics.StartZ - 20f + Mathf.Sin(a) * r);
            }
        }

        /// <summary>Шаг хода корабля: течение, тяга от доли двигателей, положение и курс в сцене.</summary>
        public void Step(float dt)
        {
            if (dt <= 0f) return;
            dt = Mathf.Min(dt, 0.25f); // после паузы не прыгаем
            Current.Update(dt);
            Motion.Update(dt, Reactor.K(ShipSystem.Engines), Current.X, Current.Z);
            Ship.SetPositionAndRotation(new Vector3(Motion.X, Motion.Y, Motion.Z),
                Quaternion.Euler(0f, Motion.Yaw * Mathf.Rad2Deg, 0f));
            Wheel.localRotation = Quaternion.Euler(-20f, 0f, -Motion.Turn * 80f);
        }

        void OnGUI()
        {
            float s = Mathf.Max(1f, Screen.height / 720f);
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * s) };
            style.normal.textColor = new Color(0.75f, 0.95f, 1f);
            GUI.Label(new Rect(16 * s, 16 * s, 900 * s, 40 * s), "Погружение · Unity 0.1", style);
            style.fontSize = Mathf.RoundToInt(16 * s);
            string place = Player.Piloting ? "За штурвалом" : ShipLayout.RoomName(Player.Room);
            string line = $"{place} · глубина {Motion.Depth:0} м · ход {Motion.Speed:0.0} м/с · реактор {Reactor.Used}/{Reactor.Limit} · прожектор {Reactor[ShipSystem.Lamp]}";
            if (lastMsg.Length > 0) line += " · " + lastMsg;
            GUI.Label(new Rect(16 * s, 52 * s, 900 * s, 30 * s), line, style);
        }
    }
}
