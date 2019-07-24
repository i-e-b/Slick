using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
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

        // caching
        [CanBeNull]private TextureBrush _renderCache;
        [NotNull] private readonly object _cacheLock = new object();
        private bool _lastSelectState;
        private volatile bool _cachable;

        /// <summary>
        /// If 'locked' is set, commands to draw will be ignored
        /// </summary>
        public volatile bool Locked;

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
                if (Hilight[i] != 0) return false;
            }
            return true;
        }

        /// <summary>
        /// Clear internal caches. Call this if you change the image data
        /// </summary>
        public void Invalidate()
        {
            _renderCache = null;
        }

        public void Render(Graphics g, double dx, double dy, bool selected, byte drawScale, float visualScale)
        {
            if (g==null) return;
            var size = Size >> (drawScale - 1);
            if (Locked) {
                g.FillRectangle(Brushes.Gray, (int)dx, (int)dy, size, size);
                return;
            }

            var cache = _renderCache;
            if (cache == null || selected != _lastSelectState) {
                lock (_cacheLock)
                {
                    cache?.Dispose();
                    cache = CopyDataToTexture(selected, drawScale);
                }
            }


// TODO: need to do visual (dpi) scaling
            cache.ResetTransform();
            cache.TranslateTransform((int)dx, (int)dy);
            g.FillRectangle(cache, (int)dx, (int)dy, size, size);


            _lastSelectState = selected;
        }

        private static Rectangle GetTargetRectangle(double dx, double dy, byte drawScale, float visualScale)
        {
            var size = Size >> (drawScale - 1);
            var rect = new Rectangle((int) Math.Floor(dx), (int) Math.Floor(dy),
                (int) Math.Floor(size * visualScale + 1), (int) Math.Floor(size * visualScale+1));
            return rect;
        }


        public void CommitCache(byte drawScale, float visualScale)
        {
              lock(_cacheLock){
                _cachable = true;
                _renderCache?.Dispose();
                _renderCache = CopyDataToTexture(false, drawScale);
            }
        }

        /// <summary>
        /// Draw an ink point on this tile. Returns true if the tile contents were changed, false otherwise
        /// </summary>
        public bool DrawOnTile(double px, double py, double radius, Color penColor, InkType inkPenType, int drawScale)
        {
            if (Locked) return false;
            PreparePen(px, py, radius, out var top, out var left, out var right, out var bottom);

            if (bottom < 0 || right < 0 || top > Size || left > Size) return false;

            
            var size = Size >> (drawScale - 1);
            int r = penColor.R;
            int g = penColor.G;
            int b = penColor.B;

            var rsq = (int)(radius / 2);
            rsq *= rsq;

            var blurEdge = (rsq - 2) * 0.5;
            var blurFact = 255 / (rsq - blurEdge);
            if (blurFact > 127) blurFact = 127;

            if (inkPenType == InkType.Import)
            {
                var idx = (top * size) + left;

                Red[idx] = (byte)r;
                Green[idx] = (byte)g;
                Blue[idx] = (byte)b;
            }
            else
            {
                for (int y = top; y < bottom; y++)
                {
                    var ysq = (int)((y - py) * (y - py));
                    var yo = y * size;

                    for (int x = left; x < right; x++)
                    {
                        var idx = yo + x;

                        // circular pen
                        var xsq = (int)((x - px) * (x - px));
                        var posSum = xsq + ysq;

                        if (posSum > rsq) continue;

                        var blend = Pin255(blurFact * (posSum - blurEdge));
                        int dnelb = 256 - blend;

                        Red[idx] = (byte)((Red[idx] * blend + r * dnelb) >> 8);
                        Green[idx] = (byte)((Green[idx] * blend + g * dnelb) >> 8);
                        Blue[idx] = (byte)((Blue[idx] * blend + b * dnelb) >> 8);
                    }
                }
            }
            Invalidate();

            return true;
        }

        private int Pin255(double rsq)
        {
            if (rsq > 255) return 255;
            if (rsq < 0) return 0;
            return (int)rsq;
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

        [NotNull]private TextureBrush CopyDataToTexture(bool selected, byte drawScale, Rectangle rect)
        {
            var width = rect.Width;
            var height = rect.Height;

            var size = Size >> (drawScale - 1); // size of source that is usable
            var sampleCount = Math.Min(size * size, Red.Length);
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppPArgb);

            var bmpData = bmp.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly, bmp.PixelFormat);

            var dx = size / (double)width;
            var dy = size / (double)height;

            try
            {
                // We scan through the data and copy bytes over
                // This is a point to do scaling

                for (int y = 0; y < height; y++)
                {
                    var oy = (int)(y * dy) * size;

                    for (int x = 0; x < width; x++)
                    {
                        var j = x + y * width;
                        var i = (int)(x * dx) + oy; // NN scaling. Should do better 

                        if (i >= sampleCount) continue;

                        var r = Red[i];
                        var g = Green[i];
                        var b = Blue[i];
                        
                        if (selected) { r >>= 1; g >>= 1; b >>= 1; } // TODO: move this from cache to draw

                        Marshal.WriteInt32(bmpData.Scan0, j * sizeof(Int32), Alpha | (r << 16) | (g << 8) | (b));
                    }
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

            var texture = new TextureBrush(bmp);
            bmp.Dispose();
            return texture;
        }
    }
}