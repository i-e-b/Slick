using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
// ReSharper disable BuiltInTypeReferenceStyle

namespace SlickWindows.Canvas
{
    /// <summary>
    /// small image fragment
    /// </summary>
    public class TileImage
    {
        public const int Size = 256;
        public const int Pixels = Size * Size;

        // Image planes:
        // The RGB planes are as you'd expect. Hilight is a special indexed plane: 0 is transparent.
        [NotNull] public readonly byte[] Red, Green, Blue, Hilight;

        [CanBeNull]private Bitmap renderCache;

        /// <summary>
        /// If 'locked' is set, commands to draw will be ignored
        /// </summary>
        public bool Locked { get; set; }

        /// <summary>
        /// Create a default blank tile
        /// </summary>
        public TileImage() : this(Color.White, 1) { }

        /// <summary>
        /// Tile image where we expect data to be loaded later
        /// </summary>
        public TileImage(Color background, byte scale)
        {
            var samples = Pixels >> (scale - 1);
            Red = new byte[Pixels];
            Green = new byte[Pixels];
            Blue = new byte[Pixels];
            Hilight = new byte[Pixels];
            var r = background.R;
            var g = background.G;
            var b = background.B;

            for (int i = 0; i < samples; i++)
            {
                Red[i] = r;
                Green[i] = g;
                Blue[i] = b;
                Hilight[i] = 0;
            }
        }

        public int Width { get { return Size; } }
        public int Height { get { return Size; } }

        public bool ImageIsBlank()
        {
            for (int i = 0; i < Red.Length; i++)
            {
                if (Red[i] < 255) return false;
                if (Green[i] < 255) return false;
                if (Blue[i] < 255) return false;
                if (Hilight[i] < 255) return false;
            }
            return true;
        }

        /// <summary>
        /// Clear internal caches. Call this if you change the image data
        /// </summary>
        public void Invalidate()
        {
            renderCache = null;
        }

        public void Render(Graphics g, double dx, double dy, bool hilite, byte drawScale)
        {
            if (renderCache == null) {
                renderCache = CopyDataToBitmap(hilite, drawScale);
            }
            g?.DrawImageUnscaled(renderCache, (int)dx, (int)dy);
        }

        public void Overwrite(double px, double py, double radius, Color penColor)
        {
            if (Locked) return;
            PreparePen(px, py, radius, out var top, out var left, out var right, out var bottom);

            var r = penColor.R;
            var g = penColor.G;
            var b = penColor.B;

            var rsq = (int)(radius / 2);
            rsq *= rsq;

            for (int y = top; y < bottom; y++)
            {
                var ysq = (int)((y - py) * (y - py));
                var yo = y * Size;

                for (int x = left; x < right; x++)
                {
                    var idx = yo + x;

                    // circular pen
                    var xsq = (int)((x - px) * (x - px));
                    if (xsq + ysq > rsq) continue;

                    Red[idx] = r;
                    Green[idx] = g;
                    Blue[idx] = b;
                }
            }
            Invalidate();
        }

        public void Highlight(double px, double py, double radius, Color penColor)
        {
            if (Locked) return;
            PreparePen(px, py, radius, out var top, out var left, out var right, out var bottom);

            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    // TODO: do highlighter mode
                    Hilight[(y*Size)+x] = 1;
                }
            }
            Invalidate();
        }

        private static void PreparePen(double px, double py, double radius, out int top, out int left, out int right, out int bottom)
        {
            // simple square for now...
            if (radius < 1) radius = 1;
            var ol = (int) (radius / 2);
            var or = (int) (radius - ol);

            top = Math.Max(0, (int) (py - ol));
            left = Math.Max(0, (int) (px - ol));
            right = Math.Min(Size, (int) (px + or));
            bottom = Math.Min(Size, (int) (py + or));
        }

        private const int Alpha = unchecked((int)0xff000000);

        [NotNull]private Bitmap CopyDataToBitmap(bool selected, byte drawScale)
        {
            var size = Size >> (drawScale - 1);
            var sampleCount = Math.Min(size * size, Red.Length);
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);

            var bmpData = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.WriteOnly, bmp.PixelFormat);

            try
            {
                // each plane, we scan through the data and copy bytes over

                for (int i = 0; i < sampleCount; i++)
                {
                    var r = Red[i];
                    var g = Green[i];
                    var b = Blue[i];

                    // TODO: draw hilight pen plane

                    if (selected) { r >>= 1; g >>= 1; b >>= 1; }

                    Marshal.WriteInt32(bmpData.Scan0, i * sizeof(Int32), Alpha | (r << 16) | (g << 8) | (b));
                }
            }
            catch
            {
                // ignore draw races
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            return bmp;
        }
    }
}