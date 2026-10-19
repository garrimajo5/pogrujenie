using System.Collections.Generic;
using UnityEngine;

namespace Pogrujenie.Game
{
    /// <summary>
    /// Формы деталей. Первые четыре — как примитивы Unity; остальные для корпуса по ART-001 (Core/HullPlan.cs):
    /// купол (шар Ø1, срезанный плоскостью y = −0,25), усечённый конус (Ø1 внизу, Ø0,5 вверху, высота 2),
    /// труба (Ø1 снаружи, Ø0,84 внутри, высота 2), ротор (ступица и пять лопастей в диске Ø1 толщиной 1),
    /// клин (брусок 1 м: толстый у −z, острый у +z), пластина (квадрат 1 м в плоскости xy лицом в +z, без изнанки).
    /// </summary>
    public enum Shape { Cube, Sphere, Cylinder, Capsule, Dome, Frustum, Tube, Rotor, Wedge, Plate }

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
                case Shape.Dome: m = Dome(12, 24); break;
                case Shape.Frustum: m = Frustum(24); break;
                case Shape.Tube: m = Tube(24); break;
                case Shape.Rotor: m = Rotor(5); break;
                case Shape.Wedge: m = Wedge(); break;
                case Shape.Plate: m = Plate(); break;
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

        /// <summary>Купол: шар радиуса 0.5 от верхнего полюса до широты 120° (y = −0,25) и плоский срез.</summary>
        static Mesh Dome(int rings, int seg)
        {
            var b = new Builder();
            const float cut = Mathf.PI * 2f / 3f;
            var rows = new int[rings + 1][];
            for (int i = 0; i <= rings; i++)
            {
                float lat = cut * i / rings;
                rows[i] = new int[seg + 1];
                for (int j = 0; j <= seg; j++)
                {
                    float lon = 2f * Mathf.PI * j / seg;
                    var n = new Vector3(Mathf.Sin(lat) * Mathf.Cos(lon), Mathf.Cos(lat), Mathf.Sin(lat) * Mathf.Sin(lon));
                    rows[i][j] = b.Add(n * 0.5f, n);
                }
            }
            for (int r = 0; r < rings; r++)
                for (int j = 0; j < seg; j++)
                    b.Quad(rows[r][j], rows[r][j + 1], rows[r + 1][j + 1], rows[r + 1][j]);
            Disc(b, -0.25f, 0.5f * Mathf.Sin(cut), Vector3.down, seg);
            return b.Build();
        }

        /// <summary>Плоский круг на высоте y лицом в n.</summary>
        static void Disc(Builder b, float y, float r, Vector3 n, int seg)
        {
            int c = b.Add(new Vector3(0f, y, 0f), n);
            var ring = new int[seg + 1];
            for (int j = 0; j <= seg; j++)
            {
                float lon = 2f * Mathf.PI * j / seg;
                ring[j] = b.Add(new Vector3(Mathf.Cos(lon) * r, y, Mathf.Sin(lon) * r), n);
            }
            for (int j = 0; j < seg; j++) b.Tri(c, ring[j], ring[j + 1]);
        }

        /// <summary>Боковая поверхность вращения от (r0, y0) до (r1, y1); outward = false — лицом к оси.</summary>
        static void Side(Builder b, float r0, float y0, float r1, float y1, bool outward, int seg)
        {
            float slope = (r0 - r1) / (y1 - y0); // наклон нормали к оси y
            int[] lo = new int[seg + 1], hi = new int[seg + 1];
            for (int j = 0; j <= seg; j++)
            {
                float lon = 2f * Mathf.PI * j / seg, cx = Mathf.Cos(lon), cz = Mathf.Sin(lon);
                var n = new Vector3(cx, slope, cz).normalized;
                if (!outward) n = -n;
                lo[j] = b.Add(new Vector3(cx * r0, y0, cz * r0), n);
                hi[j] = b.Add(new Vector3(cx * r1, y1, cz * r1), n);
            }
            for (int j = 0; j < seg; j++) b.Quad(hi[j], hi[j + 1], lo[j + 1], lo[j]);
        }

        /// <summary>Усечённый конус высотой 2: радиус 0.5 внизу (y = −1), 0.25 вверху (y = 1).</summary>
        static Mesh Frustum(int seg)
        {
            var b = new Builder();
            Side(b, 0.5f, -1f, 0.25f, 1f, true, seg);
            Disc(b, 1f, 0.25f, Vector3.up, seg);
            Disc(b, -1f, 0.5f, Vector3.down, seg);
            return b.Build();
        }

        /// <summary>Полый короткий цилиндр: стенка от радиуса 0.42 до 0.5, высота 2, торцы-кольца.</summary>
        static Mesh Tube(int seg)
        {
            const float ro = 0.5f, ri = 0.42f;
            var b = new Builder();
            Side(b, ro, -1f, ro, 1f, true, seg);
            Side(b, ri, -1f, ri, 1f, false, seg);
            foreach (float y in new[] { 1f, -1f })
            {
                var n = new Vector3(0f, y, 0f);
                int[] o = new int[seg + 1], i = new int[seg + 1];
                for (int j = 0; j <= seg; j++)
                {
                    float lon = 2f * Mathf.PI * j / seg, cx = Mathf.Cos(lon), cz = Mathf.Sin(lon);
                    o[j] = b.Add(new Vector3(cx * ro, y, cz * ro), n);
                    i[j] = b.Add(new Vector3(cx * ri, y, cz * ri), n);
                }
                for (int j = 0; j < seg; j++) b.Quad(o[j], o[j + 1], i[j + 1], i[j]);
            }
            return b.Build();
        }

        /// <summary>Брусок с центром c и полуосями u, v, w (взаимно перпендикулярны).</summary>
        static void Block(Builder b, Vector3 c, Vector3 u, Vector3 v, Vector3 w)
        {
            var axes = new[] { u, v, w };
            for (int k = 0; k < 3; k++)
            {
                Vector3 a = axes[k], p = axes[(k + 1) % 3], q = axes[(k + 2) % 3];
                foreach (float sgn in new[] { 1f, -1f })
                {
                    var n = (a * sgn).normalized;
                    var f = c + a * sgn;
                    b.Quad(b.Add(f - p - q, n), b.Add(f + p - q, n), b.Add(f + p + q, n), b.Add(f - p + q, n));
                }
            }
        }

        /// <summary>Ротор в диске радиуса 0.5 толщиной 1 по y: ступица и лопасти, чуть повёрнутые к потоку.</summary>
        static Mesh Rotor(int blades)
        {
            var b = new Builder();
            Side(b, 0.12f, -0.5f, 0.12f, 0.5f, true, 12);
            Disc(b, 0.5f, 0.12f, Vector3.up, 12);
            Disc(b, -0.5f, 0.12f, Vector3.down, 12);
            for (int k = 0; k < blades; k++)
            {
                float a = 2f * Mathf.PI * k / blades;
                var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var tangent = new Vector3(-radial.z, 0f, radial.x);
                // лопасть повёрнута на 25° вокруг своего луча
                var chord = (tangent * Mathf.Cos(0.44f) + Vector3.up * Mathf.Sin(0.44f)) * 0.08f;
                var thick = Vector3.Cross(radial, chord).normalized * 0.02f;
                Block(b, radial * 0.3f, radial * 0.19f, chord, thick);
            }
            return b.Build();
        }

        /// <summary>Клин: сечение в плоскости yz — треугольник (−z: y от −0,5 до 0,5; +z: острый край на y = 0).</summary>
        static Mesh Wedge()
        {
            var b = new Builder();
            Vector3 P(float px, float py, float pz) => new Vector3(px, py, pz);
            foreach (float x in new[] { -0.5f, 0.5f })
            {
                var n = new Vector3(x * 2f, 0f, 0f);
                b.Tri(b.Add(P(x, 0.5f, -0.5f), n), b.Add(P(x, -0.5f, -0.5f), n), b.Add(P(x, 0f, 0.5f), n));
            }
            var back = Vector3.back;
            b.Quad(b.Add(P(-0.5f, 0.5f, -0.5f), back), b.Add(P(0.5f, 0.5f, -0.5f), back),
                b.Add(P(0.5f, -0.5f, -0.5f), back), b.Add(P(-0.5f, -0.5f, -0.5f), back));
            foreach (float y in new[] { 0.5f, -0.5f })
            {
                var n = new Vector3(0f, y * 2f, 0.5f).normalized;
                b.Quad(b.Add(P(-0.5f, y, -0.5f), n), b.Add(P(0.5f, y, -0.5f), n),
                    b.Add(P(0.5f, 0f, 0.5f), n), b.Add(P(-0.5f, 0f, 0.5f), n));
            }
            return b.Build();
        }

        /// <summary>Пластина 1 × 1 м в плоскости xy лицом в +z. Сзади не видна: из отсека сквозь окно видно воду.</summary>
        static Mesh Plate()
        {
            var b = new Builder();
            var n = Vector3.forward;
            b.Quad(b.Add(new Vector3(-0.5f, -0.5f, 0f), n), b.Add(new Vector3(0.5f, -0.5f, 0f), n),
                b.Add(new Vector3(0.5f, 0.5f, 0f), n), b.Add(new Vector3(-0.5f, 0.5f, 0f), n));
            return b.Build();
        }
    }
}
