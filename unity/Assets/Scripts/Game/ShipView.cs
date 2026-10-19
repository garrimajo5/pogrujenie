using System.Collections.Generic;
using Pogrujenie.Core;
using UnityEngine;

namespace Pogrujenie.Game
{
    /// <summary>
    /// Отсеки «Ковчега-7» из ShipLayout.Boxes: полы, стены, переборки, люки и свет внутри.
    /// Один материал на вид бруска, чтобы не плодить вызовы отрисовки. Пол трюма вокруг проёма и мелкие
    /// детали люка шлюза (кромка, решётка, шахта) слиты в один меш на группу (MergeGroup).
    /// Мостик в палитре ART-002 (BridgePlan.PaintOf), его оболочка из BridgePlan.Parts — по одному мешу на краску.
    /// </summary>
    public sealed class ShipView : MonoBehaviour
    {
        public ShipLayout Layout { get; private set; }
        public Transform[] DoorMeshes { get; private set; }
        public int BoxCount { get; private set; }
        /// <summary>Слитые меши по группам: «Пол трюма», «Кромка люка шлюза», «Решётка люка», «Шахта шлюза»,
        /// «Носовая стена», «Борт мостика».</summary>
        public readonly Dictionary<string, MeshFilter> Merged = new Dictionary<string, MeshFilter>();
        /// <summary>Оболочка мостика (BridgePlan.Parts): один меш на краску.</summary>
        public readonly Dictionary<BridgePaint, MeshFilter> Bridge = new Dictionary<BridgePaint, MeshFilter>();
        readonly Dictionary<BridgePaint, Material> bridgeMats = new Dictionary<BridgePaint, Material>();
        /// <summary>Модели художника на мостике (BridgeProps.Fixed) по имени детали.</summary>
        public readonly Dictionary<string, MeshRenderer> Props = new Dictionary<string, MeshRenderer>();
        readonly List<Material> propMats = new List<Material>();
        readonly List<Mesh> ownMeshes = new List<Mesh>();

        /// <summary>В какой слитый меш идёт брусок; null — рисуется отдельно.</summary>
        public static string MergeGroup(string name)
        {
            if (name.StartsWith("Пол трюма", System.StringComparison.Ordinal)) return "Пол трюма";
            if (name == "Кромка люка шлюза" || name == "Решётка люка" || name == "Шахта шлюза") return name;
            if (name.StartsWith("Нос:", System.StringComparison.Ordinal)) return "Носовая стена";
            if (name == "Борт мостика") return name;
            return null;
        }

        readonly Dictionary<BoxKind, Material> mats = new Dictionary<BoxKind, Material>();

        public void Build(ShipLayout layout)
        {
            Layout = layout;
            Material M(BoxKind k)
            {
                if (mats.TryGetValue(k, out var m)) return m;
                m = Shapes.Paint(KindColor(k), k.ToString());
                mats[k] = m;
                return m;
            }

            Material Of(Box b)
            {
                var bp = BridgePlan.PaintOf(b);
                return bp.HasValue ? BridgeMat(bp.Value) : M(b.Kind);
            }

            var groups = new Dictionary<string, List<CombineInstance>>();
            var groupMat = new Dictionary<string, Material>();
            foreach (var b in ShipLayout.Boxes)
            {
                BoxCount++;
                var pos = new Vector3(b.X, b.Y, b.Z);
                var size = new Vector3(b.W, b.H, b.D);
                string g = MergeGroup(b.Name);
                if (g != null)
                {
                    if (!groups.TryGetValue(g, out var list)) { list = new List<CombineInstance>(); groups[g] = list; groupMat[g] = Of(b); }
                    list.Add(new CombineInstance { mesh = Shapes.MeshOf(Shape.Cube), transform = Matrix4x4.TRS(pos, Quaternion.identity, size) });
                    continue;
                }
                var go = Shapes.Make(Shape.Cube, b.Name, transform, Of(b));
                go.transform.localPosition = pos;
                go.transform.localScale = size;
            }
            foreach (var kv in groups)
            {
                var mesh = new Mesh { name = kv.Key };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                ownMeshes.Add(mesh);
                Merged[kv.Key] = MergedObject(kv.Key, mesh, groupMat[kv.Key]);
            }

            // оболочка мостика по ART-002: рама и переплёты окна, скос потолка, рёбра, плафоны, решётки пола
            var parts = new Dictionary<BridgePaint, List<CombineInstance>>();
            foreach (var p in BridgePlan.Parts)
            {
                if (!parts.TryGetValue(p.Paint, out var list)) parts[p.Paint] = list = new List<CombineInstance>();
                list.Add(new CombineInstance
                {
                    mesh = Shapes.MeshOf(Shape.Cube),
                    transform = Matrix4x4.TRS(new Vector3(p.X, p.Y, p.Z), Quaternion.Euler(p.RX, 0, 0), new Vector3(p.W, p.H, p.D)),
                });
            }
            foreach (var kv in parts)
            {
                var mesh = new Mesh { name = "Мостик: " + kv.Key };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                ownMeshes.Add(mesh);
                Bridge[kv.Key] = MergedObject(mesh.name, mesh, BridgeMat(kv.Key));
            }

            DoorMeshes = new Transform[layout.Doors.Length];
            for (int i = 0; i < layout.Doors.Length; i++)
            {
                var d = Shapes.Make(Shape.Cube, "Люк " + (i + 1), transform, M(BoxKind.Trim));
                d.transform.localScale = new Vector3(1.6f, 2.3f, 0.08f);
                DoorMeshes[i] = d.transform;
            }

            // кресло пилота, корпус голостола и стойка штурвала — модели MOD-001…003 по ART-002
            foreach (var p in BridgeProps.Fixed)
            {
                var mat = Models.Paint(p.Model);
                propMats.Add(mat);
                var go = Models.Make(p.Model, p.Name, transform, mat);
                go.transform.localPosition = new Vector3(p.X, p.Y, p.Z);
                go.transform.localScale = new Vector3(p.SX, p.SY, p.SZ);
                Props[p.Name] = go.GetComponent<MeshRenderer>();
            }
            // реактор — пока как образ, без поста
            Cylinder("Реактор", new Vector3(0, 1.5f, 17.2f), new Vector3(1.8f, 1.5f, 1.8f), M(BoxKind.Lamp));

            Lamp(new Vector3(0, 2.9f, -2), 12f, new Color(1f, 0.92f, 0.8f), 1.1f);
            Lamp(new Vector3(0, 2.4f, 5), 7f, new Color(1f, 0.9f, 0.75f), 0.8f);
            Lamp(new Vector3(0, 3.1f, 11), 12f, new Color(0.62f, 0.9f, 1f), 1.0f);
            Lamp(new Vector3(0, 2.4f, 17.2f), 9f, new Color(0.44f, 0.9f, 1f), 0.9f);
            Lamp(new Vector3(0, 2.7f, 21.75f), 7f, new Color(1f, 0.9f, 0.75f), 0.7f);
            Sync();
        }

        MeshFilter MergedObject(string name, Mesh mesh, Material m)
        {
            var go = Shapes.Make(Shape.Cube, name, transform, m);
            var mf = go.GetComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            return mf;
        }

        /// <summary>Материал краски мостика; плафоны светятся тёплым #F6DEAC.</summary>
        public Material BridgeMat(BridgePaint p)
        {
            if (bridgeMats.TryGetValue(p, out var m)) return m;
            BridgePlan.Color(p, out float r, out float g, out float b);
            var c = new Color(r, g, b);
            m = Shapes.Paint(c, "Мостик: " + p);
            if (p == BridgePaint.Plafond) m.SetColor("_Glow", c * 0.85f);
            bridgeMats[p] = m;
            return m;
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
            var c = Shapes.Make(Shape.Cylinder, name, transform, m);
            c.transform.localPosition = pos;
            c.transform.localScale = scale;
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
            foreach (var m in bridgeMats.Values) Destroy(m);
            foreach (var m in propMats) Destroy(m);
            foreach (var m in ownMeshes) Destroy(m);
        }
    }
}
