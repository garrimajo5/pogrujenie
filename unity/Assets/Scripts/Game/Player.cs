using Pogrujenie.Core;
using UnityEngine;

namespace Pogrujenie.Game
{
    /// <summary>
    /// Игрок от первого лица в отсеках: крестовина ведёт, свайп поворачивает взгляд, кнопка действия
    /// работает с ближайшим постом перед игроком (люки, прожектор). Камера — дочерняя корабля.
    /// </summary>
    public sealed class Player : MonoBehaviour
    {
        public readonly Walker Walker = new Walker();
        Bootstrap boot;

        public Room Room => ShipLayout.RoomAt(Walker.Z);
        public Interactable Target { get; private set; }

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
            Walker.X = x; Walker.Z = z; Walker.Yaw = yaw;
            Apply();
        }

        void Update()
        {
            if (boot == null) return;
            var input = boot.Controls;
            input.Poll();
            Walker.Look(input.Look.x, input.Look.y);
            Walker.Move(input.Move.x, input.Move.y, Time.deltaTime);
            Target = boot.Layout.Nearest(Walker.X, Walker.Z, Walker.Yaw);
            if (input.ActionPressed && Target != null) Target.Do();
            input.ActionLabel = Target != null ? Target.Label() : "";
            input.ActionEnabled = Target != null;
            Apply();
        }

        void Apply()
        {
            var cam = boot.Cam.transform;
            cam.localPosition = new Vector3(Walker.X, Walker.Eye, Walker.Z);
            cam.localRotation = Quaternion.Euler(-Walker.Pitch * Mathf.Rad2Deg, Walker.Yaw * Mathf.Rad2Deg, 0f);
        }
    }
}
