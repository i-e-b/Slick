using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace SlickWindows.Canvas
{
    /// <summary>
    /// small image fragment
    /// </summary>
    internal class TileImage
    {
        // TODO: ability to merge (darkest pixel wins?)
        public const int Size = 64;
        public const int Pixels = Size * Size;

        [NotNull]private readonly short[] data;

        public TileImage()
        {
            data = new short[Pixels];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = -1;
            }
        }

        // TODO: move this to an separate archiver
        public bool UpdateStorage([NotNull]string basePath, [NotNull]PositionKey pos) {
            var actual = Path.Combine(basePath, pos.ToString());

            if (ImageIsBlank())
            {
                if (File.Exists(actual)) File.Delete(actual); // no longer needed
                return false;
            }

            var img = CopyDataToBitmap(data);
            img.Save(actual, ImageFormat.Gif); // TODO: better storage
            return true;
        }

        private bool ImageIsBlank()
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] != -1) return false; // -1 is same as white
            }
            return true;
        }

        public static TileImage Load(string path)
        {
            var img = new Bitmap(Image.FromFile(path));
            var tile = new TileImage();

            // TODO: this is crazy slow. Fix it.
            for (int y = 0; y < Size>>2; y++)
            {
                for (int x = 0; x < Size>>2; x++)
                {
                    tile.data[(y*Size)+x] = ColorEncoding.To16Bit(img.GetPixel(x,y));
                }
            }
            return tile;
        }

        public void Render(Graphics g, double dx, double dy) {
            var img = CopyDataToBitmap(data);
            g.DrawImageUnscaled(img, (int)dx, (int)dy);
        }

        public void Overwrite(double px, double py, double radius, Color penColor)
        {
            var cdata = PreparePen(px, py, radius, penColor, out var top, out var left, out var right, out var bottom);

            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    data[(y*Size)+x] = cdata;
                }
            }
        }

        public void Highlight(double px, double py, double radius, Color penColor) {
            var cdata = PreparePen(px, py, radius, penColor, out var top, out var left, out var right, out var bottom);

            for (int y = top; y < bottom; y++)
            {
                for (int x = left; x < right; x++)
                {
                    // TODO: do highlighter mode
                    data[(y*Size)+x] = cdata;
                }
            }
        }

        private static short PreparePen(double px, double py, double radius, Color penColor, out int top, out int left, out int right, out int bottom)
        {
            var cdata = ColorEncoding.To16Bit(penColor);

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
        
        private Bitmap CopyDataToBitmap([NotNull]short[] imgData)
        {
            var bmp = new Bitmap(Size, Size, PixelFormat.Format16bppRgb565);

            var bmpData = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                ImageLockMode.WriteOnly, bmp.PixelFormat);

            try
            {
                Marshal.Copy(imgData, 0, bmpData.Scan0, imgData.Length);
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            return bmp;
        }
    }
}