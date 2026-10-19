using System.Collections.Generic;
using Pogrujenie.Core;
using UnityEngine;

namespace Pogrujenie.Game
{
    /// <summary>
    /// Живой мир отмели в сцене: держит <see cref="EcoWorld"/> (Core) и рисует его существ.
    /// Светляки-полосники — все шесть стай одним мешем (EcoFishLook: тело, хвост, цвет стаи, ярче
    /// в тревоге) на шейдере дна с цветом вершин, светятся сами, как MeshBasicMaterial в браузере:
    /// один вызов отрисовки на 132 рыбки. Корабль передаёт сюда место, ход и прожектор
    /// (<see cref="SetShip"/>), события ближе 80 м забирает <see cref="TakeMessages"/>.
    /// </summary>
    public sealed class EcoView : MonoBehaviour
    {
        public EcoWorld World { get; private set; }
        public EcoFish Fish { get; private set; }
        /// <summary>Меш всех светляков (перестраивается каждый кадр).</summary>
        public Mesh FishMesh { get; private set; }
        public MeshRenderer FishRenderer { get; private set; }

        Material fishMat;
        Vector3[] verts;
        Color[] cols;
        EcoVec[] place;

        public void Build(uint seed = EcoRandom.WorldSeed)
        {
            World = new EcoWorld(seed) { Ground = Seabed.Height };
            World.Ship.Pos = new EcoVec(ShipPhysics.StartX, ShipPhysics.StartY, ShipPhysics.StartZ);
            Fish = World.Add(new EcoFish());

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
        }

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
                EcoFishLook.Color(f, out float r, out float g, out float b);
                var body = new Color(r, g, b);
                var tail = body * EcoFishLook.TailShade;
                for (int k = 0; k < per; k++)
                {
                    var p = place[at + k];
                    verts[at + k] = new Vector3(p.X, p.Y, p.Z);
                    cols[at + k] = k >= 6 ? tail : body;
                }
            }
            FishMesh.vertices = verts;
            FishMesh.colors = cols;
            FishMesh.RecalculateBounds();
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
        public Color DrawnColor(int fish) => cols[fish * EcoFishLook.VertsPerFish];

        void OnDestroy()
        {
            if (fishMat != null) Destroy(fishMat);
            if (FishMesh != null) Destroy(FishMesh);
        }
    }
}
