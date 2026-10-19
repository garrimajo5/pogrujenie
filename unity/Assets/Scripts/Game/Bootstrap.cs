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
        /// <summary>Колесо штурвала (модель MOD-001), дочернее Wheel.</summary>
        public MeshRenderer WheelModel { get; private set; }
        public SeabedView Seafloor { get; private set; }
        public HullView Hull { get; private set; }
        /// <summary>Роторы и рули глубины по ART-001: следуют за тягой и вертикалью пилота.</summary>
        public HullMotion HullAnim { get; private set; }
        /// <summary>Образцы на дне и груз трюма; подбор — лебёдкой через открытый нижний шлюз.</summary>
        public Samples Samples { get; private set; }
        public SamplesView SampleView { get; private set; }
        /// <summary>Пост лебёдки шлюза в трюме (кнопку действия держат).</summary>
        public Interactable Winch { get; private set; }
        /// <summary>Свет сквозь лёд: слабеет с глубиной.</summary>
        public Light Sun { get; private set; }

        /// <summary>Прожектор включён (кнопкой действия). Светит, только если у него есть энергия.</summary>
        public bool LampOn = true;

        /// <summary>Сообщение на приборах в конце строки состояния, гаснет через 4,2 с (Core/StatusMessage.cs).</summary>
        public StatusMessage Status { get; } = new StatusMessage();

        /// <summary>Ключ сохранения в PlayerPrefs (как localStorage в браузере).</summary>
        public const string SaveKeyMain = "pogrujenie.save.v1";
        /// <summary>В тестовых сценах сохранение выключено, пока тест не задаст свой ключ.</summary>
        public static string TestSaveKey;
        /// <summary>Куда пишется сохранение; null — не сохраняем.</summary>
        public static string SaveKey => SceneManager.GetActiveScene().name.StartsWith("InitTestScene") ? TestSaveKey : SaveKeyMain;
        /// <summary>Есть несохранённые изменения; запись через SaveDelay после первого из них.</summary>
        public bool SaveDirty { get; private set; }
        public const float SaveDelay = 1f;
        float saveTimer;

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
            Reactor.Message += (title, text) => Say(title);
            Motion = new ShipPhysics { Ground = Seabed.Height };
            Current = new SeaCurrent();
            HullAnim = new HullMotion();
            Current.Changed += i => Say("Течение сменилось");
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Build();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var m in ownMats) Destroy(m);
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
            // корпус снаружи по ART-001: 28 деталей, изнутри не виден (Core/HullPlan.cs)
            Hull = new GameObject("Корпус снаружи").AddComponent<HullView>();
            Hull.transform.SetParent(ship, false);
            Hull.Build();

            // отсеки: полы, стены, переборки и люки
            Layout = new ShipLayout();
            Layout.Inter.Add(new Interactable(0, Player.SeatZ, 1.7f, () => "Сесть за штурвал", () => Player.SitAtHelm()));
            View = new GameObject("Отсеки").AddComponent<ShipView>();
            View.transform.SetParent(ship, false);
            View.Build(Layout);
            Layout.Lock.Message += Say;

            // образцы на дне (как js/manip.js) и лебёдка у кормовой кромки шлюза
            Samples = new Samples();
            Samples.Message += Say;
            Load();
            Samples.Changed += () => SaveDirty = true;
            Winch = Samples.MakePost(Layout.Lock, Motion);
            Layout.Inter.Add(Winch);
            SampleView = new GameObject("Образцы на дне").AddComponent<SamplesView>();
            SampleView.transform.SetParent(transform, false);
            SampleView.Build(Samples);

            // штурвал на стойке: колесо MOD-001 поворачивается вместе с кораблём, стойка — в ShipView
            var wheel = new GameObject("Штурвал").transform;
            wheel.SetParent(ship, false);
            wheel.localPosition = new Vector3(0, BridgeProps.WheelY, BridgeProps.WheelZ);
            wheel.localRotation = Quaternion.Euler(BridgeProps.WheelTilt, 0, 0);
            Wheel = wheel;
            var wheelMat = Models.Paint(BridgeProps.Wheel);
            ownMats.Add(wheelMat);
            WheelModel = Models.Make(BridgeProps.Wheel, "Колесо штурвала", wheel, wheelMat).GetComponent<MeshRenderer>();

            var lampGo = new GameObject("Прожектор");
            lampGo.transform.SetParent(ship, false);
            lampGo.transform.localPosition = new Vector3(0, HullPlan.SearchlightY, HullPlan.LensZ - 0.1f); // перед линзой
            lampGo.transform.localRotation = Quaternion.Euler(15, 180, 0);
            Lamp = lampGo.AddComponent<Light>();
            Lamp.type = LightType.Spot;
            Lamp.spotAngle = 40f;
            Lamp.intensity = 3f;
            Lamp.color = new Color(0.85f, 0.95f, 1f);

            // светляки: стайка кружит перед носом
            Fireflies = new Transform[24];
            var fireflyMat = Mat(new Color(0.5f, 1f, 0.8f), "Светляк");
            fireflyMat.SetColor("_Glow", new Color(0.25f, 0.5f, 0.4f)); // светятся сами
            for (int i = 0; i < Fireflies.Length; i++)
            {
                var f = Shapes.Make(Shape.Sphere, "Светляк", transform, fireflyMat);
                f.transform.localScale = Vector3.one * 0.3f;
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

        readonly System.Collections.Generic.List<Material> ownMats = new System.Collections.Generic.List<Material>();

        Material Mat(Color c, string name)
        {
            var m = Shapes.Paint(c, name);
            ownMats.Add(m);
            return m;
        }

        /// <summary>Деталь корабля своим материалом (без CreatePrimitive: ни коллайдеров, ни Standard).</summary>

        /// <summary>Прожектор берёт силу из реактора: доля 0 или аварийный сброс — света нет.</summary>
        public void ApplyPower()
        {
            float k = Reactor.K(ShipSystem.Lamp);
            Lamp.enabled = LampOn && k > 0f;
            Lamp.range = LampRange * k;
        }

        /// <summary>Строка-сообщение на приборах.</summary>
        public void Say(string msg) { Status.Say(msg); }

        /// <summary>Кнопка «Свет» за штурвалом: включить или погасить прожектор.</summary>
        public void ToggleLamp()
        {
            LampOn = !LampOn;
            if (LampOn && Reactor.K(ShipSystem.Lamp) <= 0f) Say("Прожектор обесточен");
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
            Status.Update(dt);
            float k = Reactor.K(ShipSystem.Engines);
            Motion.Update(dt, k, Current.X, Current.Z);
            HullAnim.Update(dt, Motion.InThrust, Motion.InVert, k);
            Hull.Animate(HullAnim);
            Hull.AnimateLock(Layout.Lock);
            Samples.Update(dt);
            Ship.SetPositionAndRotation(new Vector3(Motion.X, Motion.Y, Motion.Z),
                Quaternion.Euler(0f, Motion.Yaw * Mathf.Rad2Deg, 0f));
            Wheel.localRotation = Quaternion.Euler(BridgeProps.WheelTilt, 0f, -Motion.Turn * 80f);
            SampleView.Sync(Motion, Layout.Lock.T);
            if (SaveDirty)
            {
                saveTimer += dt;
                if (saveTimer >= SaveDelay) Save();
            }
        }

        /// <summary>Записать груз трюма и поднятые образцы (Core/SaveGame.cs) в PlayerPrefs.</summary>
        public bool Save()
        {
            SaveDirty = false; saveTimer = 0f;
            string key = SaveKey;
            if (string.IsNullOrEmpty(key)) return false;
            PlayerPrefs.SetString(key, SaveGame.Write(Samples));
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Прочитать сохранение при запуске; нет его или оно битое — начинаем с чистого трюма.</summary>
        public bool Load()
        {
            string key = SaveKey;
            if (string.IsNullOrEmpty(key) || !PlayerPrefs.HasKey(key)) return false;
            return SaveGame.Read(PlayerPrefs.GetString(key), Samples);
        }

        // телефон сворачивает игру без выхода — пишем сразу
        void OnApplicationPause(bool paused) { if (paused && SaveDirty) Save(); }
        void OnApplicationQuit() { if (SaveDirty) Save(); }

        /// <summary>Подсказка у открытого шлюза: что можно достать и как далеко.</summary>
        public string SampleHint()
        {
            Samples.LockPoint(Motion, out float x, out float y, out float z);
            var s = Samples.Find(x, y, z);
            if (s == null) return "образцов рядом нет";
            return $"внизу {s.Name.ToLowerInvariant()}, {Samples.DistanceTo(s, x, y, z):0} м";
        }

        void OnGUI()
        {
            float s = Mathf.Max(1f, Screen.height / 720f);
            var style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * s) };
            style.normal.textColor = new Color(0.75f, 0.95f, 1f);
            GUI.Label(new Rect(16 * s, 16 * s, 900 * s, 40 * s), "Погружение · Unity 0.1", style);
            style.fontSize = Mathf.RoundToInt(16 * s);
            GUI.Label(new Rect(16 * s, 52 * s, 900 * s, 30 * s), StatusLine(), style);
        }

        /// <summary>Строка состояния на приборах: где игрок, глубина, ход, реактор, шлюз, груз и сообщение.</summary>
        public string StatusLine()
        {
            string place = Player.Piloting ? "За штурвалом" : ShipLayout.RoomName(Player.Room);
            string line = $"{place} · глубина {Motion.Depth:0} м · ход {Motion.Speed:0.0} м/с · реактор {Reactor.Used}/{Reactor.Limit} · прожектор {Reactor[ShipSystem.Lamp]}";
            if (Layout.Lock.FullyOpen) line += " · шлюз открыт · " + SampleHint();
            if (Samples.Cargo.Count > 0) line += $" · образцов в трюме {Samples.Cargo.Count}";
            if (Status.Visible) line += " · " + Status.Text;
            return line;
        }
    }
}
