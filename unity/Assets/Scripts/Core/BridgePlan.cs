using System;
using System.Collections.Generic;

namespace Pogrujenie.Core
{
    /// <summary>Краски мостика по палитре ART-002 (docs/concept-art/mostik.md).</summary>
    public enum BridgePaint { Metal, Seam, Shadow, Deck, Plafond }

    /// <summary>Деталь оболочки мостика: брусок (размеры и центр, м) с наклоном RX вокруг оси x (градусы, как Quaternion.Euler).</summary>
    public struct BridgePart
    {
        public string Name; public BridgePaint Paint;
        public float W, H, D, X, Y, Z, RX;

        public BridgePart(string name, BridgePaint paint, float w, float h, float d, float x, float y, float z, float rx = 0f)
        { Name = name; Paint = paint; W = w; H = h; D = d; X = x; Y = y; Z = z; RX = rx; }

        /// <summary>Восемь углов бруска в осях корабля с учётом наклона.</summary>
        public IEnumerable<(float x, float y, float z)> Corners()
        {
            double r = RX * Math.PI / 180.0, c = Math.Cos(r), s = Math.Sin(r);
            foreach (float sx in new[] { -0.5f, 0.5f })
                foreach (float sy in new[] { -0.5f, 0.5f })
                    foreach (float sz in new[] { -0.5f, 0.5f })
                    {
                        float lx = sx * W, ly = sy * H, lz = sz * D;
                        yield return (X + lx, Y + (float)(ly * c - lz * s), Z + (float)(ly * s + lz * c));
                    }
        }

        public float MinY { get { float m = float.MaxValue; foreach (var p in Corners()) m = Math.Min(m, p.y); return m; } }
        public float MaxY { get { float m = float.MinValue; foreach (var p in Corners()) m = Math.Max(m, p.y); return m; } }
    }

    /// <summary>
    /// Оболочка мостика по ART-002 на игровых размерах отсека (10 × 8 × 3,2 м, носовой проём 7,6 × 1,9 м, y 0,8…2,7):
    /// носовое окно из трёх секций с переплётами и рамой, скос потолка от окна, рёбра бортов, 10 плафонов
    /// (по пять на борт), решётки пола вдоль бортов и у люка, палитра концепта. Пульт, кресло, голостол
    /// и бортовые посты — следующие части задачи.
    /// </summary>
    public static class BridgePlan
    {
        public const float WindowHalf = 3.8f, WindowBottom = 0.8f, WindowTop = 2.7f, WallInner = -5.9f;
        public const float Ceiling = 3.2f, SideInner = 4.9f;
        /// <summary>Переплёты носового окна (центры по x): средняя секция шире, перед пилотом.</summary>
        public static readonly float[] Mullions = { -1.3f, 1.3f };
        public const float MullionW = 0.16f;
        /// <summary>Скос потолка: от верха окна до потолка, глубина по z.</summary>
        public const float SlopeDepth = 1.0f;
        public static readonly float SlopeAngle = (float)(Math.Atan2(Ceiling - WindowTop, SlopeDepth) * 180.0 / Math.PI);
        /// <summary>Плафоны по бортам: z центров.</summary>
        public static readonly float[] LampZ = { -5.0f, -3.5f, -2.0f, -0.5f, 1.0f };
        public const float LampY = 2.35f;
        /// <summary>Рёбра бортов между плафонами.</summary>
        public static readonly float[] RibZ = { -4.25f, -2.75f, -1.25f, 0.25f };
        public const float GrateStep = 0.12f;

        public static readonly BridgePart[] Parts = Build();

        static BridgePart[] Build()
        {
            var p = new List<BridgePart>();
            void Add(string n, BridgePaint k, float w, float h, float d, float x, float y, float z, float rx = 0f) =>
                p.Add(new BridgePart(n, k, w, h, d, x, y, z, rx));

            // рама носового окна и два переплёта — три секции стекла
            Add("Рама окна: низ", BridgePaint.Metal, 2 * WindowHalf + 0.2f, 0.1f, 0.3f, 0, WindowBottom, -5.92f);
            Add("Рама окна: верх", BridgePaint.Metal, 2 * WindowHalf + 0.2f, 0.1f, 0.3f, 0, WindowTop, -5.92f);
            foreach (float s in new[] { -1f, 1f })
                Add("Рама окна: бок", BridgePaint.Metal, 0.14f, WindowTop - WindowBottom, 0.3f, s * (WindowHalf + 0.03f), (WindowTop + WindowBottom) / 2, -5.92f);
            foreach (float x in Mullions)
                Add("Переплёт окна", BridgePaint.Metal, MullionW, WindowTop - WindowBottom, 0.3f, x, (WindowTop + WindowBottom) / 2, -5.95f);

            // скос потолка: от верхней кромки окна (y 2,7, z −5,9) вверх и назад до потолка
            float len = (float)Math.Sqrt(SlopeDepth * SlopeDepth + (Ceiling - WindowTop) * (Ceiling - WindowTop));
            Add("Скос потолка", BridgePaint.Metal, 2 * SideInner, 0.06f, len, 0, (WindowTop + Ceiling) / 2, WallInner + SlopeDepth / 2, -SlopeAngle);

            // рёбра бортов и плафоны с тёмной подложкой
            foreach (float s in new[] { -1f, 1f })
            {
                foreach (float z in RibZ)
                    Add("Ребро борта", BridgePaint.Metal, 0.08f, Ceiling, 0.18f, s * (SideInner - 0.04f), Ceiling / 2, z);
                foreach (float z in LampZ)
                {
                    Add("Подложка плафона", BridgePaint.Seam, 0.04f, 0.38f, 0.5f, s * (SideInner - 0.02f), LampY, z);
                    Add("Плафон", BridgePaint.Plafond, 0.06f, 0.28f, 0.4f, s * (SideInner - 0.05f), LampY, z);
                }
            }

            // решётки пола: вдоль бортов по пять секций и одна перед люком в коридор
            foreach (float s in new[] { -1f, 1f })
                for (int i = 0; i < 5; i++)
                    Grate(p, s * 4.35f, -4.7f + 1.4f * i, 0.9f, 1.3f);
            Grate(p, 0, 1.45f, 1.6f, 0.7f);
            return p.ToArray();
        }

        /// <summary>Решётка: тёмная подложка вровень с полом и прутья вдоль z.</summary>
        static void Grate(List<BridgePart> p, float x, float z, float w, float d)
        {
            p.Add(new BridgePart("Решётка пола", BridgePaint.Shadow, w, 0.01f, d, x, 0.005f, z));
            int n = (int)Math.Floor(w / GrateStep);
            for (int i = 0; i <= n; i++)
                p.Add(new BridgePart("Прут решётки", BridgePaint.Metal, 0.03f, 0.015f, d, x - n * GrateStep / 2 + i * GrateStep, 0.0175f, z));
        }

        /// <summary>Брусок отсека на мостике (не переборка к коридору).</summary>
        public static bool InBridge(Box b) => b.Z < 1.95f && b.Z > -6.5f;

        /// <summary>Краска бруска отсека на мостике по палитре концепта; для других отсеков — null.</summary>
        public static BridgePaint? PaintOf(Box b)
        {
            if (!InBridge(b)) return null;
            switch (b.Kind)
            {
                case BoxKind.Floor: return BridgePaint.Deck;
                case BoxKind.Ceil: return BridgePaint.Seam;
                case BoxKind.Dark: return BridgePaint.Shadow;
                case BoxKind.Lamp: return BridgePaint.Plafond;
                default: return BridgePaint.Metal;
            }
        }

        /// <summary>Цвет краски, 0xRRGGBB по палитре ART-002.</summary>
        public static int Hex(BridgePaint p)
        {
            switch (p)
            {
                case BridgePaint.Metal: return 0x8C806B;
                case BridgePaint.Seam: return 0x514D43;
                case BridgePaint.Shadow: return 0x0B1C24;
                case BridgePaint.Deck: return 0x34484C;
                default: return 0xF6DEAC;
            }
        }

        public static void Color(BridgePaint p, out float r, out float g, out float b)
        {
            int c = Hex(p);
            r = ((c >> 16) & 255) / 255f; g = ((c >> 8) & 255) / 255f; b = (c & 255) / 255f;
        }
    }
}
