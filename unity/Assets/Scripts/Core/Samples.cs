using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    public enum SampleKind { Moss, Shell, Coral, Spine }

    /// <summary>Образец на дне отмели: вид, место в мире (оси Unity), поднят ли.</summary>
    public sealed class Sample
    {
        public readonly int I;
        public readonly SampleKind Kind;
        public readonly float X, Y, Z;
        public bool Taken;
        public Sample(int i, SampleKind kind, float x, float y, float z) { I = i; Kind = kind; X = x; Y = y; Z = z; }
        public string Name => Samples.Name(Kind);
    }

    /// <summary>Груз трюма: вид и номер образца со дна (−1 — не со дна).</summary>
    public struct CargoItem
    {
        public SampleKind Kind; public int I;
        public CargoItem(SampleKind kind, int i) { Kind = kind; I = i; }
        public string Name => Samples.Name(Kind);
    }

    public enum GrabState { Idle, Reach, Back }

    /// <summary>
    /// Образцы на дне и подбор через открытый нижний шлюз трюма (перенос js/manip.js).
    /// 14 образцов лежат там же, где в браузерной версии (M.build: у пятен мха, у папоротников,
    /// у станций огнецвета, у норы кольчужника), x меняет знак. Пост — лебёдка у кормовой кромки
    /// шлюза: удержание кнопки действия 1,4 с — захват ближайшего образца ниже шлюза в 14 м,
    /// ещё 1,2 с — подъём, образец ложится в трюм. Отпустил раньше — захват сбрасывается.
    /// </summary>
    public sealed class Samples
    {
        public const float Reach = 14f, GrabTime = 1.4f, BackTime = 1.2f;
        /// <summary>Пост лебёдки в трюме: у кормовой кромки крышки, чтобы не перекрывать пост шлюза.</summary>
        public const float PostZ = Airlock.Z + 1.2f, PostR = 1.0f;

        /// <summary>Места из браузерной версии: вид, x, z в осях браузера и подъём над дном.</summary>
        static readonly (SampleKind k, float x, float z, float lift)[] Places =
        {
            (SampleKind.Moss, 31.63f, 94.44f, 0.15f),
            (SampleKind.Moss, -24.62f, -79.71f, 0.15f),
            (SampleKind.Moss, -111.67f, 74.62f, 0.15f),
            (SampleKind.Moss, -42.46f, 34.77f, 0.15f),
            (SampleKind.Moss, -24.08f, 173.59f, 0.15f),
            (SampleKind.Moss, 110.62f, -20.3f, 0.15f),
            (SampleKind.Moss, 19.51f, 22.68f, 0.15f),
            (SampleKind.Moss, 117.75f, -47.64f, 0.15f),
            (SampleKind.Shell, 13.33f, 76.45f, 0.1f),
            (SampleKind.Shell, -67.48f, 83.59f, 0.1f),
            (SampleKind.Coral, -55.35f, 23.24f, 0.15f),
            (SampleKind.Coral, 34.9f, 57.2f, 0.15f),
            (SampleKind.Coral, 56.48f, -68.29f, 0.15f),
            (SampleKind.Spine, -52.04f, 84.95f, 0.1f),
        };

        public static string Name(SampleKind k)
        {
            switch (k)
            {
                case SampleKind.Moss: return "Светящийся мох";
                case SampleKind.Shell: return "Ракушка моллюска";
                case SampleKind.Coral: return "Ветка огнецвета";
                default: return "Игла шипоежа";
            }
        }

        /// <summary>Цвет образца, 0xRRGGBB, как в js/manip.js.</summary>
        public static int Color(SampleKind k)
        {
            switch (k)
            {
                case SampleKind.Moss: return 0x9dffd0;
                case SampleKind.Shell: return 0xe8c9a8;
                case SampleKind.Coral: return 0xff8a3c;
                default: return 0x8cdcff;
            }
        }

        public readonly List<Sample> All = new List<Sample>();
        public readonly List<CargoItem> Cargo = new List<CargoItem>();
        /// <summary>Номера образцов, поднятых со дна (даже если груз потом потрачен).</summary>
        public readonly HashSet<int> Got = new HashSet<int>();

        public GrabState State { get; private set; }
        public Sample Target { get; private set; }
        /// <summary>Захват: 0..1 за GrabTime удержания.</summary>
        public float Progress { get; private set; }
        /// <summary>Растёт при каждом изменении того, что лежит на дне (для перерисовки).</summary>
        public int Version { get; private set; }

        /// <summary>Сообщение на приборы.</summary>
        public event Action<string> Message;
        /// <summary>Изменилось то, что сохраняется: груз трюма или поднятые образцы (для записи сохранения).</summary>
        public event Action Changed;

        float back;
        bool holding;

        public Samples()
        {
            for (int i = 0; i < Places.Length; i++)
            {
                var p = Places[i];
                float x = -p.x; // браузер → Unity
                All.Add(new Sample(i, p.k, x, Seabed.Height(x, p.z) + p.lift, p.z));
            }
        }

        /// <summary>Центр крышки шлюза в мире: корабль в (X, Y, Z), поворот Yaw вокруг y (как Quaternion.Euler).</summary>
        public static void LockPoint(ShipPhysics s, out float x, out float y, out float z)
        {
            x = s.X + (float)Math.Sin(s.Yaw) * Airlock.Z;
            y = s.Y + Airlock.Y;
            z = s.Z + (float)Math.Cos(s.Yaw) * Airlock.Z;
        }

        /// <summary>Точка в осях корабля → в мир: корабль в (X, Y, Z), поворот Yaw вокруг y (как Quaternion.Euler).</summary>
        public static void ToWorld(ShipPhysics s, float lx, float ly, float lz, out float x, out float y, out float z)
        {
            float sin = (float)Math.Sin(s.Yaw), cos = (float)Math.Cos(s.Yaw);
            x = s.X + cos * lx + sin * lz;
            y = s.Y + ly;
            z = s.Z - sin * lx + cos * lz;
        }

        /// <summary>
        /// Шкив лебёдки — верх троса, в осях корабля: под решёткой люка над шахтой шлюза.
        /// Захват в покое: в шахте над закрытой крышкой (RestY) или под днищем у открытого шлюза (RestOpenY).
        /// </summary>
        public const float PulleyY = -0.1f, RestY = -0.6f, RestOpenY = Airlock.Y - 0.75f;

        /// <summary>
        /// Где висит захват в покое, в осях корабля по y: опускается из шахты под днище во второй
        /// половине открытия крышки (lockT от 0,5 до 1), чтобы не задеть откидывающуюся крышку.
        /// </summary>
        public static float RestHeight(float lockT)
        {
            float k = Math.Max(0f, Math.Min(1f, (lockT - 0.5f) * 2f));
            return RestY + (RestOpenY - RestY) * k;
        }

        /// <summary>Шкив лебёдки в мире.</summary>
        public static void PulleyPoint(ShipPhysics s, out float x, out float y, out float z)
            => ToWorld(s, 0f, PulleyY, Airlock.Z, out x, out y, out z);

        /// <summary>
        /// Захват на конце троса в мире: в покое под шахтой; при захвате тянется к образцу на дне
        /// (доля Progress), при подъёме несёт образец обратно к шлюзу (доля Lift).
        /// </summary>
        public void HookPoint(ShipPhysics s, float lockT, out float x, out float y, out float z)
        {
            ToWorld(s, 0f, RestHeight(lockT), Airlock.Z, out float rx, out float ry, out float rz);
            float k = 0f;
            if (Target != null && State == GrabState.Reach) k = Progress;
            else if (Target != null && State == GrabState.Back) k = 1f - Lift;
            float tx = Target?.X ?? rx, ty = Target?.Y ?? ry, tz = Target?.Z ?? rz;
            x = rx + (tx - rx) * k; y = ry + (ty - ry) * k; z = rz + (tz - rz) * k;
        }

        /// <summary>Ближайший образец на дне ниже шлюза и не дальше Reach; null — дотянуться не до чего.</summary>
        public Sample Find(float lx, float ly, float lz)
        {
            Sample best = null; float bd = Reach;
            foreach (var s in All)
            {
                if (s.Taken || s.Y >= ly) continue;
                float dx = s.X - lx, dy = s.Y - ly, dz = s.Z - lz;
                float d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (d <= bd) { bd = d; best = s; }
            }
            return best;
        }

        public float DistanceTo(Sample s, float lx, float ly, float lz)
        {
            float dx = s.X - lx, dy = s.Y - ly, dz = s.Z - lz;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>Удержание кнопки на лебёдке: захват тянется к образцу; на полном захвате образец поднимается.</summary>
        public void Hold(Sample s, float dt)
        {
            if (s == null || (State != GrabState.Idle && State != GrabState.Reach)) return;
            if (Target != s) { Target = s; Progress = 0f; }
            State = GrabState.Reach;
            holding = true;
            Progress = Math.Min(1f, Progress + dt / GrabTime);
            if (Progress >= 1f)
            {
                State = GrabState.Back; back = BackTime;
                s.Taken = true; Version++;
                Message?.Invoke("Образец захвачен");
            }
        }

        public void Update(float dt)
        {
            if (State == GrabState.Reach && !holding)
            {
                Progress = Math.Max(0f, Progress - dt * 2f);
                if (Progress <= 0f) { State = GrabState.Idle; Target = null; }
            }
            else if (State == GrabState.Back)
            {
                back -= dt;
                if (back <= 0f) Stow();
            }
            holding = false;
        }

        void Stow()
        {
            var s = Target;
            State = GrabState.Idle; Target = null; Progress = 0f; back = 0f;
            Cargo.Add(new CargoItem(s.Kind, s.I));
            Got.Add(s.I);
            Changed?.Invoke();
            Message?.Invoke($"В трюме: {s.Name.ToLowerInvariant()} (образцов {Cargo.Count})");
        }

        /// <summary>Поднимается ли сейчас образец, и где он: доля пути от дна (0) до шлюза (1).</summary>
        public bool Carrying => State == GrabState.Back;
        public float Lift => State == GrabState.Back ? 1f - Math.Max(0f, back) / BackTime : 0f;

        /// <summary>Все образцы снова на дне, трюм пуст.</summary>
        public void Reset()
        {
            foreach (var s in All) s.Taken = false;
            Cargo.Clear(); Got.Clear();
            State = GrabState.Idle; Target = null; Progress = 0f; back = 0f; holding = false;
            Version++;
            Changed?.Invoke();
        }

        /// <summary>
        /// Восстановить из сохранения (как M.restore в js/manip.js): поднятые образцы убраны со дна;
        /// груз трюма — по порядку, образец со дна принимается, только если номер есть, вид совпадает
        /// и он ещё не лежит в трюме. Неверные номера и записи пропускаются.
        /// </summary>
        public void Restore(IEnumerable<int> got, IEnumerable<CargoItem> cargo)
        {
            foreach (var s in All) s.Taken = false;
            Cargo.Clear(); Got.Clear();
            State = GrabState.Idle; Target = null; Progress = 0f; back = 0f; holding = false;
            if (got != null)
                foreach (int i in got)
                    if (i >= 0 && i < All.Count) { All[i].Taken = true; Got.Add(i); }
            var used = new HashSet<int>();
            if (cargo != null)
                foreach (var c in cargo)
                {
                    if (c.I >= 0)
                    {
                        if (c.I >= All.Count || All[c.I].Kind != c.Kind || !used.Add(c.I)) continue;
                        All[c.I].Taken = true; Got.Add(c.I);
                    }
                    Cargo.Add(new CargoItem(c.Kind, c.I < 0 ? -1 : c.I));
                }
            Version++;
        }

        /// <summary>
        /// Пост лебёдки для ShipLayout.Inter: доступен при полностью открытом шлюзе, если есть что поднять
        /// или подъём уже идёт. Работает удержанием кнопки действия.
        /// </summary>
        public Interactable MakePost(Airlock lockState, ShipPhysics ship)
        {
            Sample Near()
            {
                LockPoint(ship, out float x, out float y, out float z);
                return Find(x, y, z);
            }
            return new Interactable(0f, PostZ, PostR,
                () => State == GrabState.Back ? "Поднимаю…" : State == GrabState.Reach ? $"Захват {Progress * 100f:0}%" : "Достать образец",
                () => { })
            {
                On = () => lockState.FullyOpen && (State != GrabState.Idle || Near() != null),
                Hold = dt => { if (State == GrabState.Reach && Target != null) Hold(Target, dt); else if (State == GrabState.Idle) Hold(Near(), dt); },
            };
        }
    }
}
