using System;
using System.IO;
using System.Text;

namespace Pogrujenie.Core
{
    /// <summary>
    /// Меш модели художника в формате PGM1 (делает tools/glb_to_unity.py из GLB в models/):
    /// "PGM1", int32 вершин, int32 индексов, позиции (3 float), нормали (3 float), UV (2 float), индексы (uint16).
    /// Оси уже игровые: x со знаком минус относительно glTF, обход треугольников развёрнут, v перевёрнут.
    /// </summary>
    public sealed class MeshPack
    {
        public float[] Pos, Nrm, Uv;
        public int[] Tri;
        public int Vertices => Pos.Length / 3;
        public int Triangles => Tri.Length / 3;

        public static MeshPack Read(byte[] data)
        {
            using (var r = new BinaryReader(new MemoryStream(data)))
            {
                if (Encoding.ASCII.GetString(r.ReadBytes(4)) != "PGM1") throw new InvalidDataException("не PGM1");
                int nv = r.ReadInt32(), ni = r.ReadInt32();
                if (nv <= 0 || ni <= 0 || ni % 3 != 0) throw new InvalidDataException("пустой меш");
                var m = new MeshPack { Pos = new float[nv * 3], Nrm = new float[nv * 3], Uv = new float[nv * 2], Tri = new int[ni] };
                for (int i = 0; i < m.Pos.Length; i++) m.Pos[i] = r.ReadSingle();
                for (int i = 0; i < m.Nrm.Length; i++) m.Nrm[i] = r.ReadSingle();
                for (int i = 0; i < m.Uv.Length; i++) m.Uv[i] = r.ReadSingle();
                for (int i = 0; i < ni; i++)
                {
                    int k = r.ReadUInt16();
                    if (k >= nv) throw new InvalidDataException("индекс вне меша");
                    m.Tri[i] = k;
                }
                return m;
            }
        }

        /// <summary>Габариты: минимум и максимум по осям (x, y, z).</summary>
        public void Bounds(out float[] min, out float[] max)
        {
            min = new[] { float.MaxValue, float.MaxValue, float.MaxValue };
            max = new[] { float.MinValue, float.MinValue, float.MinValue };
            for (int i = 0; i < Pos.Length; i++)
            {
                int a = i % 3;
                min[a] = Math.Min(min[a], Pos[i]);
                max[a] = Math.Max(max[a], Pos[i]);
            }
        }
    }
}
