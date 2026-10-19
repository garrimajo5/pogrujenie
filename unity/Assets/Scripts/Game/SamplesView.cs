using System.Collections.Generic;
using Pogrujenie.Core;
using UnityEngine;

namespace Pogrujenie.Game
{
    /// <summary>
    /// Образцы на дне одним мешем (октаэдры с цветом вершин, шейдер дна, светятся сами — как
    /// MeshBasicMaterial в js/manip.js) и поднимаемый образец: идёт от дна к шлюзу корабля.
    /// Два вызова отрисовки на все образцы.
    /// </summary>
    public sealed class SamplesView : MonoBehaviour
    {
        public const float Size = 0.22f, Stretch = 1.3f;

        public Samples Model { get; private set; }
        public Mesh Mesh { get; private set; }
        /// <summary>Сколько образцов сейчас нарисовано на дне.</summary>
        public int Shown { get; private set; }
        public Transform Carried { get; private set; }

        Material seabedMat, carryMat;
        int version = -1;

        public static Color ColorOf(SampleKind k)
        {
            int c = Samples.Color(k);
            return new Color(((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f);
        }

        public void Build(Samples model)
        {
            Model = model;
            Mesh = new Mesh { name = "Образцы на дне" };
            gameObject.AddComponent<MeshFilter>().sharedMesh = Mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            seabedMat = new Material(Resources.Load<Shader>("Seabed")) { name = "Образцы" };
            seabedMat.SetFloat("_Lit", 0.4f);
            seabedMat.SetColor("_Glow", new Color(0.8f, 0.8f, 0.8f));
            mr.sharedMaterial = seabedMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            carryMat = Shapes.Paint(Color.white, "Поднимаемый образец");
            var c = Shapes.Make(Shape.Sphere, "Поднимаемый образец", transform, carryMat);
            c.transform.localScale = new Vector3(Size * 2f, Size * 2f * Stretch, Size * 2f);
            c.SetActive(false);
            Carried = c.transform;
            Sync(null);
        }

        /// <summary>Перерисовать дно, если образцы менялись; поднимаемый образец — между дном и шлюзом.</summary>
        public void Sync(ShipPhysics ship)
        {
            if (Model == null) return;
            if (version != Model.Version) Rebuild();
            bool carry = Model.Carrying && Model.Target != null && ship != null;
            Carried.gameObject.SetActive(carry);
            if (!carry) return;
            var s = Model.Target;
            Samples.LockPoint(ship, out float lx, out float ly, out float lz);
            Carried.position = Vector3.Lerp(new Vector3(s.X, s.Y, s.Z), new Vector3(lx, ly, lz), Model.Lift);
            var col = ColorOf(s.Kind);
            if (carryMat.color != col) { carryMat.color = col; carryMat.SetColor("_Glow", col * 0.8f); }
        }

        void Rebuild()
        {
            version = Model.Version;
            var v = new List<Vector3>(); var cols = new List<Color>(); var tris = new List<int>();
            Shown = 0;
            foreach (var s in Model.All)
            {
                if (s.Taken) continue;
                Shown++;
                var c = ColorOf(s.Kind);
                var o = new Vector3(s.X, s.Y, s.Z);
                var rot = Quaternion.Euler(0f, s.I * Mathf.Rad2Deg, 23f); // как Euler(0, i, 0.4) в браузере
                Vector3 P(float x, float y, float z) => o + rot * new Vector3(x * Size, y * Size * Stretch, z * Size);
                int b = v.Count;
                v.Add(P(1, 0, 0)); v.Add(P(-1, 0, 0)); v.Add(P(0, 1, 0)); v.Add(P(0, -1, 0)); v.Add(P(0, 0, 1)); v.Add(P(0, 0, -1));
                for (int k = 0; k < 6; k++) cols.Add(c);
                // восемь граней октаэдра, нормаль cross(b − a, c − a) наружу — лицевая сторона в Unity
                int[] f = { 0, 2, 4, 4, 2, 1, 1, 2, 5, 5, 2, 0, 4, 3, 0, 1, 3, 4, 5, 3, 1, 0, 3, 5 };
                foreach (int i in f) tris.Add(b + i);
            }
            Mesh.Clear();
            Mesh.SetVertices(v);
            Mesh.SetColors(cols);
            Mesh.SetTriangles(tris, 0);
            Mesh.RecalculateNormals();
            Mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (seabedMat != null) Destroy(seabedMat);
            if (carryMat != null) Destroy(carryMat);
            if (Mesh != null) Destroy(Mesh);
        }
    }
}
