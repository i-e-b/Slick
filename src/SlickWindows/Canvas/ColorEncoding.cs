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

        public static Color YcocgToColor(int Y, int Co, int Cg)
        {
            Co = (Co - 127) << 1;
            Cg = (Cg - 127) << 1;

            var tmp = Y - (Cg >> 1);
            var G = Cg + tmp;
            var B = tmp - (Co >> 1);
            var R = B + Co;

            return Color.FromArgb(Clip(R), Clip(G), Clip(B));
        }
        
        public static Color YcbcrToColor(double Y, double Cb, double Cr)
        {
            var R = 1.164 * (Y - 16) + 0.0 * (Cb - 128) + 1.596 * (Cr - 128);
            var G = 1.164 * (Y - 16) + -0.392 * (Cb - 128) + -0.813 * (Cr - 128);
            var B = 1.164 * (Y - 16) + 2.017 * (Cb - 128) + 0.0 * (Cr - 128);

            return Color.FromArgb(Clip((int)R), Clip((int)G), Clip((int)B));
        }
        
        public static Color ExpPaletteColor(int Y, int p1, int p2)
        {
            p1 = (p1 - 127) << 1;
            p2 = (p2 - 127) << 1;

            var tmp = Y - (p2 >> 1);
            var G = p2 + tmp;
            var B = tmp - (p1 >> 1);
            var R = B + p1;

            return Color.FromArgb(Clip(R), Clip(G), Clip(B));
        }


        private static int Clip(int v)
        {
            if (v > 255) return 255;
            if (v < 0) return 0;
            return v;
        }

        [NotNull]public static Brush BrushFrom16Bit(short color) {
            return new SolidBrush(ColorFrom16Bit(color));
        }
    }
}