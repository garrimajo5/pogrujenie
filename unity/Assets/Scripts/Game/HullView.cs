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
            foreach (var p in HullPlan.Parts)
            {
                var go = Shapes.Make(ShapeOf(p.Form), p.Name, transform, MaterialOf(p.Paint));
                var t = go.transform;
                t.localPosition = new Vector3(p.X, p.Y, p.Z);
                t.localRotation = Quaternion.Euler(p.RX, p.RY, p.RZ);
                t.localScale = new Vector3(p.SX, p.SY, p.SZ);
                Parts.Add(t);
                if (p.Node == HullNode.Body) Body = go.GetComponent<Renderer>();
                if (p.Node == HullNode.Rotor) rotors.Add(t);
            }
            Rotors = rotors.ToArray();
        }

        void OnDestroy()
        {
            foreach (var m in mats.Values) Destroy(m);
        }
    }
}
