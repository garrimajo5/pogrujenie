using System.Collections.Generic;
using Pogrujenie.Core;
using UnityEngine;

namespace Pogrujenie.Game
{
    /// <summary>
    /// Отсеки «Ковчега-7» из ShipLayout.Boxes: полы, стены, переборки, люки и свет внутри.
    /// Один материал на вид бруска, чтобы не плодить вызовы отрисовки.
    /// </summary>
    public sealed class ShipView : MonoBehaviour
    {
        public ShipLayout Layout { get; private set; }
        public Transform[] DoorMeshes { get; private set; }
        public int BoxCount { get; private set; }

        readonly Dictionary<BoxKind, Material> mats = new Dictionary<BoxKind, Material>();

        public void Build(ShipLayout layout)
        {
            Layout = layout;
            var shared = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var baseMat = shared.GetComponent<Renderer>().sharedMaterial;
            Destroy(shared);

            Material M(BoxKind k)
            {
                if (mats.TryGetValue(k, out var m)) return m;
                m = new Material(baseMat) { name = k.ToString(), color = KindColor(k) };
                mats[k] = m;
                return m;
            }

            foreach (var b in ShipLayout.Boxes)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = b.Name;
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(b.X, b.Y, b.Z);
                go.transform.localScale = new Vector3(b.W, b.H, b.D);
                go.GetComponent<Renderer>().sharedMaterial = M(b.Kind);
                BoxCount++;
            }

            DoorMeshes = new Transform[layout.Doors.Length];
            for (int i = 0; i < layout.Doors.Length; i++)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Cube);
                d.name = "Люк " + (i + 1);
                Destroy(d.GetComponent<Collider>());
                d.transform.SetParent(transform, false);
                d.transform.localScale = new Vector3(1.6f, 2.3f, 0.08f);
                d.GetComponent<Renderer>().sharedMaterial = M(BoxKind.Trim);
                DoorMeshes[i] = d.transform;
            }

            // голостол сонара и реактор — пока как образы, без постов
            Cylinder("Голостол сонара", new Vector3(0, 0.475f, -1.2f), new Vector3(2.2f, 0.475f, 2.2f), M(BoxKind.Dark));
            Cylinder("Реактор", new Vector3(0, 1.5f, 17.2f), new Vector3(1.8f, 1.5f, 1.8f), M(BoxKind.Lamp));

            Lamp(new Vector3(0, 2.9f, -2), 12f, new Color(1f, 0.92f, 0.8f), 1.1f);
            Lamp(new Vector3(0, 2.4f, 5), 7f, new Color(1f, 0.9f, 0.75f), 0.8f);
            Lamp(new Vector3(0, 3.1f, 11), 12f, new Color(0.62f, 0.9f, 1f), 1.0f);
            Lamp(new Vector3(0, 2.4f, 17.2f), 9f, new Color(0.44f, 0.9f, 1f), 0.9f);
            Lamp(new Vector3(0, 2.7f, 21.75f), 7f, new Color(1f, 0.9f, 0.75f), 0.7f);
            Sync();
        }

        static Color KindColor(BoxKind k)
        {
            switch (k)
            {
                case BoxKind.Floor: return new Color(0.2f, 0.23f, 0.25f);
                case BoxKind.Ceil: return new Color(0.16f, 0.22f, 0.25f);
                case BoxKind.Wall: return new Color(0.42f, 0.47f, 0.48f);
                case BoxKind.Wall2: return new Color(0.5f, 0.55f, 0.52f);
                case BoxKind.Dark: return new Color(0.12f, 0.17f, 0.19f);
                case BoxKind.Lamp: return new Color(1f, 0.89f, 0.72f);
                case BoxKind.Trim: return new Color(0.5f, 0.58f, 0.61f);
                default: return new Color(0.4f, 0.4f, 0.4f);
            }
        }

        void Cylinder(string name, Vector3 pos, Vector3 scale, Material m)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            c.name = name;
            Destroy(c.GetComponent<Collider>());
            c.transform.SetParent(transform, false);
            c.transform.localPosition = pos;
            c.transform.localScale = scale;
            c.GetComponent<Renderer>().sharedMaterial = m;
        }

        void Lamp(Vector3 pos, float range, Color c, float intensity)
        {
            var go = new GameObject("Свет отсека");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = range;
            l.color = c;
            l.intensity = intensity;
        }

        /// <summary>Створки люков едут вбок по мере открытия.</summary>
        public void Sync()
        {
            for (int i = 0; i < DoorMeshes.Length; i++)
            {
                var d = Layout.Doors[i];
                DoorMeshes[i].localPosition = new Vector3(d.Offset, 1.15f, d.Z);
            }
        }

        void Update()
        {
            if (Layout == null) return;
            Layout.Update(Time.deltaTime);
            Sync();
        }

        void OnDestroy()
        {
            foreach (var m in mats.Values) Destroy(m);
        }
    }
}
