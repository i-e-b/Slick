using System;
using System.Globalization;
using Containers;

namespace SlickWindows.Canvas
{
    /// <summary>
    /// position on plane
    /// </summary>
    internal class PositionKey: PartiallyOrdered
    {
        public readonly int X;
        public readonly int Y;

        public PositionKey(int x, int y)
        {
            X = x;
            Y = y;
        }

        /// <inheritdoc />
        public override int CompareTo(object obj)
        {
            if (obj is PositionKey key) {
                if (key.X == X) return Y - key.Y;
                return X - key.X;
            }
            return 0;
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

            return new PositionKey(
                int.Parse(bits[0], NumberStyles.HexNumber),
                int.Parse(bits[1], NumberStyles.HexNumber)
            );
        }
    }
}