using System.Collections.Generic;
using Pogrujenie.Core;
using UnityEngine;

namespace Pogrujenie.Game
{
    /// <summary>
    /// Живой мир отмели в сцене: держит <see cref="EcoWorld"/> (Core) и рисует его существ.
    /// Светляки-полосники по ART-011 — все шесть стай одним мешем (EcoFishLook: тёмное тело, четыре
    /// полосы цвета стаи, ярче в тревоге) на шейдере дна с цветом вершин, светятся сами, как MeshBasicMaterial в браузере:
    /// один вызов отрисовки на 132 рыбки. Светящийся мох — 22 пятна одним мешем на шейдере EcoGlow (свет
    /// прибавляется к дну), яркость пятна по EcoMoss.Glow. Скребуны по концепту ART-006 — десять одним
    /// мешем на шейдере дна (EcoGrazerLook: панцирь, шесть лап, скребок), без своего света. Корабль передаёт сюда место, ход и прожектор
    /// (<see cref="SetShip"/>), события ближе 80 м забирает <see cref="TakeMessages"/>.
    /// </summary>
    public sealed class EcoView : MonoBehaviour
    {
        public EcoWorld World { get; private set; }
        public EcoFish Fish { get; private set; }
        /// <summary>Меш всех светляков (перестраивается каждый кадр).</summary>
        public Mesh FishMesh { get; private set; }
        public MeshRenderer FishRenderer { get; private set; }
        public EcoMoss Moss { get; private set; }
        public EcoGrazer Grazers { get; private set; }
        /// <summary>Меш всех пятен мха (цвета меняются, когда пятно тускнеет или разгорается).</summary>
        public Mesh MossMesh { get; private set; }
        public MeshRenderer MossRenderer { get; private set; }
        /// <summary>Меш всех скребунов (перестраивается каждый кадр).</summary>
        public Mesh GrazerMesh { get; private set; }
        public MeshRenderer GrazerRenderer { get; private set; }

        Material fishMat, mossMat, grazerMat;
        EcoMossLook.Mesh moss;
        Color[] mossCols;
        float[] mossShown;
        Vector3[] grazerVerts;
        EcoVec[] grazerPlace;
        Vector3[] verts;
        Color[] cols;
        EcoVec[] place;

        public void Build(uint seed = EcoRandom.WorldSeed)
        {
            World = new EcoWorld(seed) { Ground = Seabed.Height };
            World.Ship.Pos = new EcoVec(ShipPhysics.StartX, ShipPhysics.StartY, ShipPhysics.StartZ);
            Fish = World.Add(new EcoFish());
            Moss = World.Add(new EcoMoss());           // мох раньше скребунов: они его едят
            Grazers = World.Add(new EcoGrazer(Moss));

            int per = EcoFishLook.VertsPerFish, n = Fish.All.Count;
            verts = new Vector3[n * per];
            cols = new Color[n * per];
            place = new EcoVec[n * per];
            var tris = new int[n * EcoFishLook.Tris.Length];
            for (int i = 0; i < n; i++)
                for (int k = 0; k < EcoFishLook.Tris.Length; k++)
                    tris[i * EcoFishLook.Tris.Length + k] = i * per + EcoFishLook.Tris[k];
            var normals = new Vector3[n * per];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;

            FishMesh = new Mesh { name = "Светляки-полосники" };
            FishMesh.MarkDynamic();
            var go = new GameObject("Светляки-полосники");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = FishMesh;
            FishRenderer = go.AddComponent<MeshRenderer>();
            fishMat = new Material(Resources.Load<Shader>("Seabed")) { name = "Светляки" };
            fishMat.SetFloat("_Lit", 0f);                 // свет сцены не нужен
            fishMat.SetColor("_Glow", Color.white);       // цвет вершин и есть свечение
            FishRenderer.sharedMaterial = fishMat;
            FishRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            FishRenderer.receiveShadows = false;

            Sync();
            FishMesh.normals = normals;
            FishMesh.triangles = tris;
            FishMesh.RecalculateBounds();

            BuildMoss();
            BuildGrazers();
        }

        MeshRenderer Renderer(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        void BuildMoss()
        {
            moss = EcoMossLook.Build(Moss.Patches, Seabed.Height);
            var verts = new Vector3[moss.Verts.Length];
            for (int i = 0; i < verts.Length; i++) verts[i] = new Vector3(moss.Verts[i].X, moss.Verts[i].Y, moss.Verts[i].Z);
            mossCols = new Color[verts.Length];
            mossShown = new float[Moss.Patches.Count];
            for (int i = 0; i < mossShown.Length; i++) mossShown[i] = -1f;
            MossMesh = new Mesh { name = "Светящийся мох" };
            MossMesh.vertices = verts;
            MossMesh.triangles = moss.Tris;
            MossMesh.RecalculateBounds();
            mossMat = new Material(Resources.Load<Shader>("EcoGlow")) { name = "Светящийся мох" };
            MossRenderer = Renderer("Светящийся мох", MossMesh, mossMat);
            SyncMoss();
        }

        void BuildGrazers()
        {
            int per = EcoGrazerLook.VertsPerGrazer, n = Grazers.All.Count;
            grazerVerts = new Vector3[n * per];
            grazerPlace = new EcoVec[n * per];
            var cols = new Color[n * per];
            var tris = new int[n * EcoGrazerLook.Tris.Length];
            for (int i = 0; i < n; i++)
            {
                for (int k = 0; k < per; k++) cols[i * per + k] = ColorOf(EcoGrazerLook.Paint[k]);
                for (int k = 0; k < EcoGrazerLook.Tris.Length; k++)
                    tris[i * EcoGrazerLook.Tris.Length + k] = i * per + EcoGrazerLook.Tris[k];
            }
            GrazerMesh = new Mesh { name = "Скребуны" };
            GrazerMesh.MarkDynamic();
            grazerMat = new Material(Resources.Load<Shader>("Seabed")) { name = "Скребуны" };
            grazerMat.SetFloat("_Lit", 1f);
            grazerMat.SetColor("_Glow", new Color(0.12f, 0.12f, 0.12f)); // своего света нет, но в глубине не чёрные
            GrazerRenderer = Renderer("Скребуны", GrazerMesh, grazerMat);
            SyncGrazers();
            GrazerMesh.colors = cols;
            GrazerMesh.triangles = tris;
            GrazerMesh.RecalculateNormals();
            GrazerMesh.RecalculateBounds();
        }

        public static Color ColorOf(uint c) =>
            new Color(((c >> 16) & 255) / 255f, ((c >> 8) & 255) / 255f, (c & 255) / 255f);

        /// <summary>Где корабль, как быстро идёт (м/с, задний ход — минус) и горит ли прожектор.</summary>
        public void SetShip(Vector3 pos, float speed, bool light)
        {
            World.Ship.Pos = new EcoVec(pos.x, pos.y, pos.z);
            World.Ship.Speed = speed;
            World.Ship.Light = light;
        }

        /// <summary>Шаг мира на dt и перерисовка существ.</summary>
        public void Step(float dt)
        {
            World.Step(dt);
            Sync();
            SyncMoss();
            SyncGrazers();
        }

        /// <summary>Тексты событий, которые слышны с корабля (ближе 80 м); очередь очищается.</summary>
        public List<string> TakeMessages()
        {
            var list = new List<string>();
            foreach (var ev in World.Bus.Take(World.Ship.Pos))
            {
                string text = ev.Text;
                if (!string.IsNullOrEmpty(text)) list.Add(text);
            }
            return list;
        }

        /// <summary>Вершины и цвета всех рыбок по миру сейчас.</summary>
        public void Sync()
        {
            int per = EcoFishLook.VertsPerFish;
            float t = World.Time;
            foreach (var f in Fish.All)
            {
                int at = f.Index * per;
                EcoFishLook.Place(f, t, place, at);
                EcoFishLook.VertexColor(f, EcoFishLook.Paint.Body, out float r, out float g, out float b);
                var body = new Color(r, g, b);
                EcoFishLook.VertexColor(f, EcoFishLook.Paint.Stripe, out r, out g, out b);
                var stripe = new Color(r, g, b);
                EcoFishLook.VertexColor(f, EcoFishLook.Paint.Fin, out r, out g, out b);
                var fin = new Color(r, g, b);
                for (int k = 0; k < per; k++)
                {
                    var p = place[at + k];
                    verts[at + k] = new Vector3(p.X, p.Y, p.Z);
                    var paint = EcoFishLook.Paints[k];
                    cols[at + k] = paint == EcoFishLook.Paint.Stripe ? stripe : paint == EcoFishLook.Paint.Fin ? fin : body;
                }
            }
            FishMesh.vertices = verts;
            FishMesh.colors = cols;
            FishMesh.RecalculateBounds();
        }

        /// <summary>Цвета мха по яркости пятен; пятно, чья яркость почти не менялась, не трогаем.</summary>
        public void SyncMoss()
        {
            var c = EcoMossLook.Color;
            bool dirty = false;
            foreach (var p in Moss.Patches)
            {
                float glow = p.Glow;
                if (Mathf.Abs(glow - mossShown[p.Index]) < 0.002f) continue;
                mossShown[p.Index] = glow;
                dirty = true;
                int v0 = p.Index * EcoMossLook.VertsPerPatch;
                for (int k = 0; k < EcoMossLook.VertsPerPatch; k++)
                {
                    float a = moss.Base[v0 + k] * glow;
                    mossCols[v0 + k] = new Color(c[0] * a, c[1] * a, c[2] * a, 1f);
                }
            }
            if (dirty) MossMesh.colors = mossCols;
        }

        /// <summary>Вершины скребунов по миру сейчас: место, курс, шаг лап, скребок.</summary>
        public void SyncGrazers()
        {
            int per = EcoGrazerLook.VertsPerGrazer;
            float t = World.Time;
            foreach (var g in Grazers.All)
            {
                int at = g.Index * per;
                EcoGrazerLook.Place(g, t, grazerPlace, at);
                for (int k = 0; k < per; k++)
                {
                    var p = grazerPlace[at + k];
                    grazerVerts[at + k] = new Vector3(p.X, p.Y, p.Z);
                }
            }
            GrazerMesh.vertices = grazerVerts;
            if (GrazerMesh.subMeshCount > 0 && GrazerMesh.GetIndexCount(0) > 0) GrazerMesh.RecalculateNormals();
            GrazerMesh.RecalculateBounds();
        }

        /// <summary>Яркость мха в меше у вершины v (зелёный канал, для тестов).</summary>
        public float MossShown(int v) => mossCols[v].g;

        /// <summary>Середина вершин скребуна (для тестов): где он нарисован.</summary>
        public Vector3 GrazerDrawnAt(int grazer)
        {
            int per = EcoGrazerLook.VertsPerGrazer;
            var s = Vector3.zero;
            for (int k = 0; k < per; k++) s += grazerVerts[grazer * per + k];
            return s / per;
        }

        /// <summary>Середина вершин рыбки (для тестов): где она нарисована.</summary>
        public Vector3 DrawnAt(int fish)
        {
            int per = EcoFishLook.VertsPerFish;
            var s = Vector3.zero;
            for (int k = 0; k < per; k++) s += verts[fish * per + k];
            return s / per;
        }

        /// <summary>Цвет тела рыбки в меше (для тестов).</summary>
        public Color DrawnColor(int fish) => cols[fish * EcoFishLook.VertsPerFish + EcoFishLook.StripeVertex];

        /// <summary>Цвет тела рыбки в меше (нос, для тестов).</summary>
        public Color DrawnBody(int fish) => cols[fish * EcoFishLook.VertsPerFish];

        void OnDestroy()
        {
            if (fishMat != null) Destroy(fishMat);
            if (FishMesh != null) Destroy(FishMesh);
            if (mossMat != null) Destroy(mossMat);
            if (MossMesh != null) Destroy(MossMesh);
            if (grazerMat != null) Destroy(grazerMat);
            if (GrazerMesh != null) Destroy(GrazerMesh);
        }
    }
}
