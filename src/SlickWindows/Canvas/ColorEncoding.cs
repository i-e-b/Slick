using System.Drawing;
using JetBrains.Annotations;

namespace SlickWindows.Canvas
{
    public static class ColorEncoding {

        public static short To16Bit(Color color) {
            int bits = ((color.R & 0xF8) << 8)
                       | ((color.G & 0xFC) << 3)
                       | ((color.B & 0xF8) >> 3);
            return (short) bits;
        }

        public static Color ColorFrom16Bit(short color) {
            int r = (color >> 8) & 0xF8;
            int g = (color >> 3) & 0xFC;
            int b = (color << 3) & 0xF8;
            return Color.FromArgb(r, g, b);
        }

        [NotNull]public static Brush BrushFrom16Bit(short color) {
            return new SolidBrush(ColorFrom16Bit(color));
        }
    }
}