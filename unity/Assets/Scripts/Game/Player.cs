using Pogrujenie.Core;
using UnityEngine;

namespace Pogrujenie.Game
{
    /// <summary>
    /// Игрок от первого лица на мостике: крестовина ведёт, свайп поворачивает взгляд,
    /// кнопка действия пока включает и гасит прожектор. Камера — дочерняя корабля, координаты корабельные.
    /// </summary>
    public sealed class Player : MonoBehaviour
    {
        public readonly Walker Walker = new Walker();
        Bootstrap boot;

        public void Init(Bootstrap b)
        {
            boot = b;
            Walker.Reset();
            Apply();
        }

        void Update()
        {
            if (boot == null) return;
            var input = boot.Controls;
            input.Poll();
            Walker.Look(input.Look.x, input.Look.y);
            Walker.Move(input.Move.x, input.Move.y, Time.deltaTime);
            if (input.ActionPressed) boot.ToggleLamp();
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
