using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using SlickCommon.ImageFormats;

namespace SlickWindows.ImageFormats
{
    public class SystemImage
    {
        [NotNull]
        public static RawImagePlanar ToRaw([NotNull]Bitmap src)
        {
            var ri = new Rectangle(Point.Empty, src.Size);
            var srcData = src.LockBits(ri, ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb); // this is actually BGRA
            var len = srcData.Height * srcData.Width;
            var R = new byte[len];
            var G = new byte[len];
            var B = new byte[len];

            var raw = new byte[len * 4];
            try
            {
                Marshal.Copy(srcData.Scan0, raw, 0, len * 4);
            }
            finally
            {
                src.UnlockBits(srcData);
            }

            for (int i = 0; i < len; i++)
            {
                var j = i*4;
                R[i] = raw[j+2];
                G[i] = raw[j+1];
                B[i] = raw[j+0];
            }

            return new RawImagePlanar
            {
                Red = R,
                Green = G,
                Blue = B,
                Height = srcData.Height,
                Width = srcData.Width
            };
        }
    }
}