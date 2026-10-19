using System.Collections.Generic;
using Pogrujenie.Core;
using UnityEngine;

namespace Pogrujenie.Game
{
    /// <summary>
    /// «Ковчег-7» снаружи по принятому концепту ART-001 (docs/concept-art/kovcheg-7-rakursy.png):
    /// 28 деталей из Core/HullPlan.Parts, один материал на краску — пять вызовов отрисовки на материалы.
    /// Окна, линза прожектора и метка шлюза светятся сами, металл — нет.
    /// </summary>
    public sealed class HullView : MonoBehaviour
    {
        public const float WindowGlow = 0.85f, LensGlow = 1f, LockGlow = 0.45f;

        public readonly List<Transform> Parts = new List<Transform>();
        public Renderer Body { get; private set; }
        public Transform[] Rotors { get; private set; }
        /// <summary>Рули глубины, по борту на каждый.</summary>
        public Transform[] Rudders { get; private set; }
        /// <summary>Крышка нижнего шлюза: откидывается на петле (Core/Airlock).</summary>
        public Transform Cover { get; private set; }

        readonly List<Quaternion> rotorBase = new List<Quaternion>(), rudderBase = new List<Quaternion>();
        readonly List<float> rotorSide = new List<float>();

        readonly Dictionary<HullPaint, Material> mats = new Dictionary<HullPaint, Material>();

        public static Shape ShapeOf(Form f)
        {
            switch (f)
            {
                case Form.Cube: return Shape.Cube;
                case Form.Sphere: return Shape.Sphere;
                case Form.Cylinder: return Shape.Cylinder;
                case Form.Capsule: return Shape.Capsule;
                case Form.Dome: return Shape.Dome;
                case Form.Frustum: return Shape.Frustum;
                case Form.Tube: return Shape.Tube;
                case Form.Rotor: return Shape.Rotor;
                case Form.Wedge: return Shape.Wedge;
                default: return Shape.Plate;
            }
        }

        public Material MaterialOf(HullPaint p)
        {
            if (mats.TryGetValue(p, out var m)) return m;
            HullPlan.Color(p, out float r, out float g, out float b);
            var c = new Color(r, g, b);
            m = Shapes.Paint(c, "Корпус: " + p);
            if (HullPlan.Glows(p))
            {
                float k = p == HullPaint.Window ? WindowGlow : p == HullPaint.Lens ? LensGlow : LockGlow;
                m.SetColor("_Glow", c * k);
            }
            mats[p] = m;
            return m;
        }

        public void Build()
        {
            var rotors = new List<Transform>();
            var rudders = new List<Transform>();
            foreach (var p in HullPlan.Parts)
            {
                var go = Shapes.Make(ShapeOf(p.Form), p.Name, transform, MaterialOf(p.Paint));
                var t = go.transform;
                t.localPosition = new Vector3(p.X, p.Y, p.Z);
                t.localRotation = Quaternion.Euler(p.RX, p.RY, p.RZ);
                t.localScale = new Vector3(p.SX, p.SY, p.SZ);
                Parts.Add(t);
                if (p.Node == HullNode.Body) Body = go.GetComponent<Renderer>();
                if (p.Node == HullNode.Rotor) { rotors.Add(t); rotorBase.Add(t.localRotation); rotorSide.Add(Mathf.Sign(p.X)); }
                if (p.Node == HullNode.Rudder) { rudders.Add(t); rudderBase.Add(t.localRotation); }
                if (p.Node == HullNode.Airlock) Cover = t;
            }
            Rotors = rotors.ToArray();
            Rudders = rudders.ToArray();
        }

        /// <summary>
        /// Анимация по заметкам ART-001 (Core/HullMotion): ротор крутится вокруг своей оси (ось y формы
        /// вдоль корабля), руль глубины поворачивается вокруг поперечной оси x корабля.
        /// </summary>
        public void Animate(HullMotion m)
        {
            for (int i = 0; i < Rotors.Length; i++)
                Rotors[i].localRotation = rotorBase[i] * Quaternion.Euler(0f, m.RotorAngleOf(rotorSide[i]), 0f);
            for (int i = 0; i < Rudders.Length; i++)
                Rudders[i].localRotation = Quaternion.Euler(m.Pitch, 0f, 0f) * rudderBase[i];
        }

        /// <summary>Крышка шлюза поворачивается вокруг петли у носовой кромки (Airlock.CoverPose).</summary>
        public void AnimateLock(Airlock a)
        {
            if (Cover == null) return;
            a.CoverPose(out float y, out float z, out float rx);
            Cover.localPosition = new Vector3(0f, y, z);
            Cover.localRotation = Quaternion.Euler(rx, 0f, 0f);
        }

        void OnDestroy()
        {
            foreach (var m in mats.Values) Destroy(m);
        }
    }
}
