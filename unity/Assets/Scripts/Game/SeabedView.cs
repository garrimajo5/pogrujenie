using Pogrujenie.Core;
using UnityEngine;

namespace Pogrujenie.Game
{
    /// <summary>
    /// Отмель в сцене: одна сетка дна из Seabed.BuildGrid (цвет в вершинах, 64 800 треугольников)
    /// и лёд над головой на y = 0. Всего два вызова отрисовки.
    /// </summary>
    public sealed class SeabedView : MonoBehaviour
    {
        public MeshFilter Floor { get; private set; }
        public MeshFilter Ice { get; private set; }

        static readonly Color IceColor = new Color(0x86 / 255f, 0xbc / 255f, 0xcb / 255f); // 0x86bccb

        public void Build()
        {
            var shader = Shader.Find("Pogrujenie/Seabed");
            if (shader == null) shader = Shader.Find("Standard");

            // дно
            Seabed.BuildGrid(out var p, out var n, out var c, out var tris);
            int count = p.Length / 3;
            var verts = new Vector3[count];
            var norms = new Vector3[count];
            var cols = new Color[count];
            for (int i = 0; i < count; i++)
            {
                verts[i] = new Vector3(p[i * 3], p[i * 3 + 1], p[i * 3 + 2]);
                norms[i] = new Vector3(n[i * 3], n[i * 3 + 1], n[i * 3 + 2]);
                cols[i] = new Color(c[i * 3], c[i * 3 + 1], c[i * 3 + 2]);
            }
            var mesh = new Mesh { name = "Дно отмели" };
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.colors = cols;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            var floorMat = new Material(shader) { name = "Дно" };
            floorMat.SetFloat("_Lit", 1f);
            floorMat.SetColor("_Glow", new Color(0.05f, 0.12f, 0.13f)); // бирюзовые блики (каустика)
            Floor = Part("Дно", mesh, floorMat);

            // лёд: квадрат 900 м, лицом вниз
            var ice = new Mesh { name = "Лёд" };
            const float h = 450f;
            ice.vertices = new[]
            {
                new Vector3(-h, Seabed.IceY, -h), new Vector3(h, Seabed.IceY, -h),
                new Vector3(-h, Seabed.IceY, h), new Vector3(h, Seabed.IceY, h),
            };
            ice.colors = new[] { IceColor, IceColor, IceColor, IceColor };
            ice.triangles = new[] { 0, 1, 2, 1, 3, 2 }; // обход обратный дну: лицевая сторона вниз
            ice.RecalculateNormals();
            ice.RecalculateBounds();
            var iceMat = new Material(shader) { name = "Лёд" };
            iceMat.SetFloat("_Lit", 0f);
            iceMat.SetColor("_Glow", Color.white); // светится сам, как MeshBasicMaterial в браузере
            Ice = Part("Лёд", ice, iceMat);
        }

        MeshFilter Part(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return mf;
        }
    }
}
