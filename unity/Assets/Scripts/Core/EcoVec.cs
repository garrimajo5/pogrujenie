using System;

namespace Pogrujenie.Core
{
    /// <summary>Точка или скорость в осях Unity для экосистемы (Core без UnityEngine).</summary>
    public struct EcoVec : IEquatable<EcoVec>
    {
        public float X, Y, Z;

        public EcoVec(float x, float y, float z) { X = x; Y = y; Z = z; }

        public static readonly EcoVec Zero = new EcoVec(0, 0, 0);

        public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public float LengthSq => X * X + Y * Y + Z * Z;
        /// <summary>Длина в плоскости дна, без высоты.</summary>
        public float Flat => (float)Math.Sqrt(X * X + Z * Z);

        public EcoVec Normalized
        {
            get { float l = Length; return l > 1e-6f ? new EcoVec(X / l, Y / l, Z / l) : Zero; }
        }

        public static EcoVec operator +(EcoVec a, EcoVec b) => new EcoVec(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static EcoVec operator -(EcoVec a, EcoVec b) => new EcoVec(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static EcoVec operator *(EcoVec a, float k) => new EcoVec(a.X * k, a.Y * k, a.Z * k);

        public static float Distance(EcoVec a, EcoVec b) => (a - b).Length;
        public static float FlatDistance(EcoVec a, EcoVec b) => (a - b).Flat;

        /// <summary>Шаг от a к b на долю t (THREE.Vector3.lerp).</summary>
        public static EcoVec Lerp(EcoVec a, EcoVec b, float t) => a + (b - a) * t;

        public bool Equals(EcoVec o) => X == o.X && Y == o.Y && Z == o.Z;
        public override bool Equals(object o) => o is EcoVec v && Equals(v);
        public override int GetHashCode() => X.GetHashCode() ^ (Y.GetHashCode() << 2) ^ (Z.GetHashCode() >> 2);
        public override string ToString() => $"({X:0.##}, {Y:0.##}, {Z:0.##})";
    }
}
