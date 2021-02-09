using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace SlickCommon.Canvas
{
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class ColorEncoding {

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

        private static int Clip(int v)
        {
            if (v > 255) return 255;
            if (v < 0) return 0;
            return v;
        }

        public static int ToRGB32(Color c)
        {
            return c.ToArgb();
        }
    }
}