using System;
using System.Globalization;
using Containers;

namespace SlickCommon.Storage
{
    /// <summary>
    /// position on plane
    /// </summary>
    public class PositionKey: PartiallyOrdered
    {
        public readonly int X;
        public readonly int Y;

        public PositionKey(int x, int y)
        {
            X = x;
            Y = y;
        }

        public PositionKey(double x, double y)
        {
            X = (int)x;
            Y = (int)y;
        }

        /// <inheritdoc />
        public override int CompareTo(object obj)
        {
            if (obj is PositionKey key) {
                if (key.X == X) return Y - key.Y;
                return X - key.X;
            }
            return -1;
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (X * 255) ^ Y;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return X.ToString("X") + "_" + Y.ToString("X");
        }

        public static PositionKey Parse(string s) {
            var bits = s?.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (bits == null || bits.Length < 2) return null;

            var ok = int.TryParse(bits[0], NumberStyles.HexNumber, null, out var x);
            ok &= int.TryParse(bits[1], NumberStyles.HexNumber, null, out var y);
            if (!ok) return null;

            return new PositionKey(x, y);
        }
    }
}