using Pogrujenie.Core;
using UnityEngine;

namespace Pogrujenie.Game
{
    /// <summary>
    /// Видимый луч прожектора (S.beam в js/ship.js): открытый конус вдоль +Z прожектора от линзы
    /// (радиус 0,3 м) до 55 м (13 м), светлее у линзы, цвета складываются с водой (шейдер Pogrujenie/Beam).
    /// Один меш и один вызов отрисовки; виден, только пока прожектор светит.
    /// </summary>
    public static class SearchlightView
    {
        public const string ShaderResource = "Beam";
        public const int Segments = 24, Rings = 6;

        public static MeshRenderer MakeBeam(Transform lamp)
        {
            var go = new GameObject("Луч прожектора");
            go.transform.SetParent(lamp, false);
            go.AddComponent<MeshFilter>().sharedMesh = BuildMesh();
            var r = go.AddComponent<MeshRenderer>();
            var mat = new Material(Resources.Load<Shader>(ShaderResource)) { name = "Луч прожектора" };
            mat.color = new Color(0.81f, 0.965f, 1f, Searchlight.BeamOpacity); // 0xcff6ff
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        static Mesh BuildMesh()
        {
            int n = Segments + 1; // шов: первая вершина кольца повторяется
            var pos = new Vector3[n * (Rings + 1)];
            var col = new Color[pos.Length];
            for (int j = 0; j <= Rings; j++)
            {
                float t = j / (float)Rings;
                float rad = Mathf.Lerp(Searchlight.BeamTip, Searchlight.BeamEnd, t);
                float v = Searchlight.BeamShade(t);
                for (int i = 0; i < n; i++)
                {
                    float a = i / (float)Segments * Mathf.PI * 2f;
                    pos[j * n + i] = new Vector3(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad, t * Searchlight.BeamLength);
                    col[j * n + i] = new Color(v, v, v, 1f);
                }
            }
            var tri = new int[Segments * Rings * 6];
            int k = 0;
            for (int j = 0; j < Rings; j++)
                for (int i = 0; i < Segments; i++)
                {
                    int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                    tri[k++] = a; tri[k++] = c; tri[k++] = b;
                    tri[k++] = b; tri[k++] = c; tri[k++] = d;
                }
            var m = new Mesh { name = "Луч прожектора" };
            m.vertices = pos;
            m.colors = col;
            m.triangles = tri;
            m.RecalculateBounds();
            return m;
        }
    }
}
