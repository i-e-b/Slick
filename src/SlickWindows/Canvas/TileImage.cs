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
        // TODO: ability to merge (darkest pixel wins?)
        public const int Size = 256;
        public const int Pixels = Size * Size;

        [NotNull]public readonly Int32[] Data;

        /// <summary>
        /// If 'locked' is set, commands to draw will be ignored
        /// </summary>
        public bool Locked { get; set; }

        public TileImage()
        {
            Data = new Int32[Pixels];
            for (int i = 0; i < Data.Length; i++)
            {
                Data[i] = -1;
            }
        }

        /// <summary>
        /// Tile image where we expect data to be loaded later
        /// </summary>
        public TileImage(Color background, byte scale)
        {
            var samples = Pixels >> (scale - 1);
            Data = new Int32[samples];
            var c = ColorEncoding.ToRGB32(background);
            for (int i = 0; i < samples; i++)
            {
                Data[i] = c;
            }
        }

        public TileImage(Int32[] data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public int Width { get { return Size; } }
        public int Height { get { return Size; } }

        public bool ImageIsBlank()
        {
            for (int i = 0; i < Data.Length; i++)
            {
                if (Data[i] != -1) return false; // -1 is same as white
            }
            return true;
        }

        public void Render(Graphics g, double dx, double dy, byte drawScale) {
            var img = CopyDataToBitmap(Data, drawScale);
            g?.DrawImageUnscaled(img, (int)dx, (int)dy);
        }

        public void Overwrite(double px, double py, double radius, Color penColor)
        {
            if (Locked) return;
            var cdata = PreparePen(px, py, radius, penColor, out var top, out var left, out var right, out var bottom);

            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    Data[(y*Size)+x] = cdata;
                }
            }
        }

        public void Highlight(double px, double py, double radius, Color penColor)
        {
            if (Locked) return;
            var cdata = PreparePen(px, py, radius, penColor, out var top, out var left, out var right, out var bottom);

            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    // TODO: do highlighter mode
                    Data[(y*Size)+x] = cdata;
                }
            }
        }

        private static int PreparePen(double px, double py, double radius, Color penColor, out int top, out int left, out int right, out int bottom)
        {
            var cdata = ColorEncoding.ToRGB32(penColor);

            // simple square for now...
            if (radius < 1) radius = 1;
            var ol = (int) (radius / 2);
            var or = (int) (radius - ol);

            top = Math.Max(0, (int) (py - ol));
            left = Math.Max(0, (int) (px - ol));
            right = Math.Min(Size, (int) (px + or));
            bottom = Math.Min(Size, (int) (py + or));
            return cdata;
        }
        
        [NotNull]private Bitmap CopyDataToBitmap([NotNull] int[] imgData, byte drawScale)
        {
            var size = Size >> (drawScale - 1);
            var sampleCount = Math.Min(size * size, imgData.Length);
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);

            var bmpData = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.WriteOnly, bmp.PixelFormat);

            try
            {
                Marshal.Copy(imgData, 0, bmpData.Scan0, sampleCount);
            }
            catch
            {
                //ignore draw races
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            return bmp;
        }
    }
}