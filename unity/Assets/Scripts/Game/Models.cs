using System.Collections.Generic;
using Pogrujenie.Core;
using UnityEngine;

namespace Pogrujenie.Game
{
    /// <summary>
    /// Модели художника из Resources/Models: меш PGM1 (`<имя>.bytes`, Core/MeshPack) и текстура `<имя>-tex.bytes` (PNG).
    /// Материал — наш Pogrujenie/Lit с текстурой (Standard в APK вырезается). Меш и текстура грузятся один раз.
    /// </summary>
    public static class Models
    {
        public const string Folder = "Models/";
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        public static Mesh MeshOf(string model)
        {
            if (meshes.TryGetValue(model, out var m) && m != null) return m;
            var asset = Resources.Load<TextAsset>(Folder + model);
            if (asset == null) throw new System.IO.FileNotFoundException("нет модели Resources/" + Folder + model + ".bytes");
            var p = MeshPack.Read(asset.bytes);
            int n = p.Vertices;
            var v = new Vector3[n]; var nr = new Vector3[n]; var uv = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                v[i] = new Vector3(p.Pos[i * 3], p.Pos[i * 3 + 1], p.Pos[i * 3 + 2]);
                nr[i] = new Vector3(p.Nrm[i * 3], p.Nrm[i * 3 + 1], p.Nrm[i * 3 + 2]);
                uv[i] = new Vector2(p.Uv[i * 2], p.Uv[i * 2 + 1]);
            }
            m = new Mesh { name = model, vertices = v, normals = nr, uv = uv, triangles = p.Tri };
            m.RecalculateBounds();
            meshes[model] = m;
            return m;
        }

        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        /// <summary>Текстура модели: PNG из TextAsset, с mipmaps, после загрузки только для GPU.</summary>
        public static Texture2D TextureOf(string model)
        {
            if (textures.TryGetValue(model, out var t) && t != null) return t;
            var asset = Resources.Load<TextAsset>(Folder + model + "-tex");
            if (asset == null) throw new System.IO.FileNotFoundException("нет текстуры Resources/" + Folder + model + "-tex.bytes");
            t = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = model };
            if (!t.LoadImage(asset.bytes, true)) throw new System.IO.InvalidDataException("текстура " + model + " не PNG");
            t.wrapMode = TextureWrapMode.Clamp;
            textures[model] = t;
            return t;
        }

        /// <summary>Материал модели: белый цвет, её текстура; у голостола светятся голубые огни.</summary>
        public static Material Paint(string model)
        {
            var mat = Shapes.Paint(Color.white, model);
            mat.mainTexture = TextureOf(model);
            if (BridgeProps.GlowingLights(model)) mat.SetFloat("_TexGlow", 1f);
            return mat;
        }

        /// <summary>Деталь с мешем модели, без коллайдера и теней. Материал создаёт вызывающий и отвечает за него.</summary>
        public static GameObject Make(string model, string name, Transform parent, Material mat)
        {
            var go = Shapes.Make(Shape.Cube, name, parent, mat);
            go.GetComponent<MeshFilter>().sharedMesh = MeshOf(model);
            return go;
        }
    }
}
