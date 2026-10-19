using Pogrujenie.Core;
using UnityEngine;

namespace Pogrujenie.Game
{
    /// <summary>
    /// Игрок от первого лица в отсеках: крестовина ведёт, свайп поворачивает взгляд, кнопка действия
    /// работает с ближайшим постом перед игроком (люки, штурвал, шлюз); у лебёдки шлюза кнопку держат. Камера — дочерняя корабля.
    /// За штурвалом крестовина ведёт уже корабль: вверх-вниз — ход, влево-вправо — поворот;
    /// «Всплыть»/«Вниз» меняют глубину, «Свет» — прожектор, кнопка действия — «Встать».
    /// </summary>
    public sealed class Player : MonoBehaviour
    {
        public const float SeatZ = CameraView.SeatZ, SeatEye = CameraView.SeatEye, StandZ = -3.2f;

        public readonly Walker Walker = new Walker();
        Bootstrap boot;

        public Room Room => Piloting ? Room.Bridge : ShipLayout.RoomAt(Walker.Z);
        public Interactable Target { get; private set; }
        public bool Piloting { get; private set; }

        public void Init(Bootstrap b)
        {
            boot = b;
            Walker.CanStand = b.Layout.CanStand;
            Walker.Reset();
            Apply();
        }

        /// <summary>Поставить игрока в точку корабля (тесты, телепорт).</summary>
        public void Place(float x, float z, float yaw)
        {
            if (Piloting) Stand();
            Walker.X = x; Walker.Z = z; Walker.Yaw = yaw;
            Apply();
        }

        /// <summary>Сесть за штурвал: взгляд прямо на нос (в кадре рама окна и штурвал), крестовина ведёт корабль.</summary>
        public void SitAtHelm()
        {
            if (Piloting) return;
            Piloting = true;
            Walker.Yaw = Mathf.PI; Walker.Pitch = CameraView.SeatPitch;
            boot.Controls.PilotButtons = true;
            boot.Say("Вы за штурвалом");
            Apply();
        }

        /// <summary>Встать из-за штурвала за креслом пилота; корабль глушит тягу и дрейфует.</summary>
        public void Stand()
        {
            if (!Piloting) return;
            Piloting = false;
            var s = boot.Motion;
            s.InThrust = s.InTurn = s.InVert = 0;
            Walker.X = 0; Walker.Z = StandZ; Walker.Yaw = Mathf.PI; Walker.Pitch = CameraView.StandPitch;
            boot.Controls.PilotButtons = false;
            Apply();
        }

        void Update()
        {
            if (boot == null) return;
            var input = boot.Controls;
            input.Poll();
            Walker.Look(input.Look.x, input.Look.y);
            bool stood = false;
            if (Piloting)
            {
                var s = boot.Motion;
                s.InThrust = input.Move.y; s.InTurn = input.Move.x; s.InVert = input.Vert;
                if (input.LightPressed) boot.ToggleLamp();
                Target = null;
                if (input.ActionPressed) { Stand(); stood = true; }
                else { input.ActionLabel = "Встать"; input.ActionEnabled = true; }
            }
            if (!Piloting)
            {
                Walker.Move(input.Move.x, input.Move.y, Time.deltaTime);
                Target = boot.Layout.Nearest(Walker.X, Walker.Z, Walker.Yaw);
                // кнопку, которой только что встали, не отдаём посту в том же кадре
                if (input.ActionPressed && Target != null && !stood) Target.Do();
                // посты удержания (лебёдка шлюза) работают, пока кнопка зажата
                if (input.ActionDown && Target != null && Target.Hold != null && !stood) Target.Hold(Time.deltaTime);
                input.ActionLabel = Target != null ? Target.Label() : "";
                input.ActionEnabled = Target != null;
            }
            input.LampLit = boot.Lamp.enabled;
            Apply();
        }

        void Apply()
        {
            boot.Cam.fieldOfView = CameraView.Fov(boot.Cam.aspect);
            var cam = boot.Cam.transform;
            cam.localPosition = Piloting ? new Vector3(0f, SeatEye, SeatZ) : new Vector3(Walker.X, Walker.Eye, Walker.Z);
            cam.localRotation = Quaternion.Euler(-Walker.Pitch * Mathf.Rad2Deg, Walker.Yaw * Mathf.Rad2Deg, 0f);
        }
    }
}
