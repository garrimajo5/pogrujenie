using System.Collections.Generic;
using UnityEngine;

namespace Pogrujenie.Game
{
    public enum Shape { Cube, Sphere, Cylinder, Capsule }

    /// <summary>
    /// Детали сцены без GameObject.CreatePrimitive: свои меши тех же размеров, что у примитивов Unity
    /// (куб 1 м, шар Ø1, цилиндр и капсула Ø1 высотой 2), и материал на шейдере Pogrujenie/Lit из Resources.
    /// CreatePrimitive в сборке добавляет коллайдеры, а физика вырезана, — в лог сыпались ошибки;
    /// его материал на Standard в APK не попадает, и детали были розовыми (отчёт qa/2026-10-18-unity-pervyy-zapusk).
    /// </summary>
    public static class Shapes
    {
        public const string ShaderName = "Pogrujenie/Lit";
        public const string ShaderResource = "Lit";

        static readonly Dictionary<Shape, Mesh> meshes = new Dictionary<Shape, Mesh>();
        static Shader shader;

        /// <summary>Шейдер деталей. Берётся из Resources, поэтому есть и в редакторе, и в APK.</summary>
        public static Shader LitShader
        {
            get
            {
                if (shader == null) shader = Resources.Load<Shader>(ShaderResource);
                return shader;
            }
        }

        public static Material Paint(Color c, string name = "Деталь")
        {
            var m = new Material(LitShader) { name = name };
            m.color = c;
            return m;
        }

        /// <summary>Деталь с мешем и материалом, без коллайдера и теней.</summary>
        public static GameObject Make(Shape s, string name, Transform parent, Material mat)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = MeshOf(s);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        public static Mesh MeshOf(Shape s)
        {
            if (meshes.TryGetValue(s, out var m) && m != null) return m;
            switch (s)
            {
                case Shape.Cube: m = Cube(); break;
                case Shape.Sphere: m = Round(16, 24, 0f); break;
                case Shape.Capsule: m = Round(16, 24, 0.5f); break;
                default: m = Cylinder(24); break;
            }
            m.name = s.ToString();
            meshes[s] = m;
            return m;
        }

        sealed class Builder
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<int> T = new List<int>();

            public int Add(Vector3 v, Vector3 n) { V.Add(v); N.Add(n); return V.Count - 1; }

            /// <summary>Треугольник лицом наружу: обход сверяется с нормалями вершин.</summary>
            public void Tri(int a, int b, int c)
            {
                var face = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
                if (face.sqrMagnitude < 1e-12f) return; // вырожденный у полюса
                if (Vector3.Dot(face, N[a] + N[b] + N[c]) < 0f) { T.Add(a); T.Add(c); T.Add(b); }
                else { T.Add(a); T.Add(b); T.Add(c); }
            }

            public void Quad(int a, int b, int c, int d) { Tri(a, b, c); Tri(a, c, d); }

            public Mesh Build()
            {
                var m = new Mesh();
                m.SetVertices(V);
                m.SetNormals(N);
                m.SetTriangles(T, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        static Mesh Cube()
        {
            var b = new Builder();
            var dirs = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var n in dirs)
            {
                var u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                var w = Vector3.Cross(n, u);
                var c = n * 0.5f;
                int i0 = b.Add(c + (-u - w) * 0.5f, n), i1 = b.Add(c + (u - w) * 0.5f, n);
                int i2 = b.Add(c + (u + w) * 0.5f, n), i3 = b.Add(c + (-u + w) * 0.5f, n);
                b.Quad(i0, i1, i2, i3);
            }
            return b.Build();
        }

        /// <summary>Шар радиуса 0.5; при stretch > 0 полушария раздвигаются на ±stretch — капсула.</summary>
        static Mesh Round(int rings, int seg, float stretch)
        {
            var b = new Builder();
            var rows = new List<int[]>();
            for (int i = 0; i <= rings; i++)
            {
                float lat = Mathf.PI * i / rings; // 0 — верхний полюс
                // экватор капсулы дублируется: верхний и нижний края цилиндрической части
                int copies = stretch > 0f && i == rings / 2 ? 2 : 1;
                for (int k = 0; k < copies; k++)
                {
                    float shift = i < rings / 2 || (copies == 2 && k == 0) ? stretch : -stretch;
                    var row = new int[seg + 1];
                    for (int j = 0; j <= seg; j++)
                    {
                        float lon = 2f * Mathf.PI * j / seg;
                        var n = new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));
                        row[j] = b.Add(n * 0.5f + Vector3.up * shift, n);
                    }
                    rows.Add(row);
                }
            }
            for (int r = 0; r + 1 < rows.Count; r++)
                for (int j = 0; j < seg; j++)
                    b.Quad(rows[r][j], rows[r][j + 1], rows[r + 1][j + 1], rows[r + 1][j]);
            return b.Build();
        }

        static Mesh Cylinder(int seg)
        {
            var b = new Builder();
            int[] top = new int[seg + 1], bot = new int[seg + 1];
            for (int j = 0; j <= seg; j++)
            {
                float lon = 2f * Mathf.PI * j / seg;
                var n = new Vector3(Mathf.Cos(lon), 0f, Mathf.Sin(lon));
                top[j] = b.Add(n * 0.5f + Vector3.up, n);
                bot[j] = b.Add(n * 0.5f + Vector3.down, n);
            }
            for (int j = 0; j < seg; j++) b.Quad(top[j], top[j + 1], bot[j + 1], bot[j]);
            foreach (float y in new[] { 1f, -1f })
            {
                var n = new Vector3(0f, y, 0f);
                int c = b.Add(n, n);
                var ring = new int[seg + 1];
                for (int j = 0; j <= seg; j++)
                {
                    float lon = 2f * Mathf.PI * j / seg;
                    ring[j] = b.Add(new Vector3(Mathf.Cos(lon) * 0.5f, y, Mathf.Sin(lon) * 0.5f), n);
                }
                for (int j = 0; j < seg; j++) b.Tri(c, ring[j], ring[j + 1]);
            }
            return b.Build();
        }
    }
}
