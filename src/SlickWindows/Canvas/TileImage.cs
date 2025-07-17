using System.Diagnostics.CodeAnalysis;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SlickCommon.Canvas;
using SlickCommon.ImageFormats;
using SlickCommon.Storage;

// ReSharper disable BuiltInTypeReferenceStyle

namespace SlickWindows.Canvas
{

    /// <summary>
    /// small image fragment
    /// </summary>
    public class TileImage : ITileImage
    {
        public const int Size = 256;
        public const int Pixels = Size * Size;

        // Image planes:
        // The RGB planes are as you'd expect. Highlight is a special indexed plane: 0 is transparent.
        public readonly byte[] Red, Green, Blue, Highlight;
        private readonly int[] _raw;

        // caching
        private TextureBrush? _renderCache;
        private readonly object _cacheLock = new();
        private volatile bool _canCache;

        /// <summary>
        /// If 'locked' is set, commands to draw will be ignored
        /// </summary>
        public volatile bool Locked;

        public int Width => Size;
        public int Height => Size;
        public PositionKey Position { get; set; }


        /// <summary>
        /// Create a default blank tile
        /// </summary>
        public TileImage(PositionKey pos) : this(pos, Color.White, 1) { }

        /// <summary>
        /// Tile image where we expect data to be loaded later
        /// </summary>
        public TileImage(PositionKey pos, Color background, byte scale)
        {
            Position = pos;
            var samples = Pixels >> (scale - 1);
            Red = new byte[Pixels];
            Green = new byte[Pixels];
            Blue = new byte[Pixels];
            Highlight = new byte[Pixels];
            _raw = new int[Pixels* 8];
            var r = background.R;
            var g = background.G;
            var b = background.B;

            for (int i = 0; i < samples; i++)
            {
                Red[i] = r;
                Green[i] = g;
                Blue[i] = b;
                Highlight[i] = 0;
            }
        }


        public bool ImageIsBlank()
        {
            for (int i = 0; i < Red.Length; i++)
            {
                if (Red[i] < 255) return false;
                if (Green[i] < 255) return false;
                if (Blue[i] < 255) return false;
                if (Highlight[i] != 0) return false;
            }
            return true;
        }

        /// <summary>
        /// Clear internal caches. Call this if you change the image data
        /// </summary>
        public void Invalidate()
        {
            lock (_cacheLock)
            {
                _renderCache?.Dispose();
                _renderCache = null;
            }
        }

        public void Render(Graphics? g, double dx, double dy, bool selected, byte drawScale, float visualScale)
        {
            if (g == null) return;
            var rect = GetTargetRectangle(dx,dy, drawScale, visualScale);
            if (Locked) {
                g.FillRectangle(Brushes.Gray, rect);
                return;
            }

            var cache = _renderCache;

            if (cache == null)
            {
                lock (_cacheLock)
                {
                    UpdateRawByteCache(drawScale, rect);
                    cache = RawToTextureBrush(rect.Width, rect.Height);
                }
            }

            try
            {
                cache.ResetTransform();
                cache.TranslateTransform((int)dx, (int)dy);
            }
            catch { return; }

            g.FillRectangle(cache, rect);

            // overdraw if selected
            if (selected)
            {
                var hatch = new HatchBrush(HatchStyle.SmallCheckerBoard, Color.Transparent);
                g.FillRectangle(hatch, rect);
            }

            if (_canCache) { _renderCache = cache; }
        }

        public RawImageInterleaved_Int32 GetRawImage(byte drawScale, float visualScale) {
            var rect = GetTargetRectangle(0, 0, drawScale, visualScale);
            UpdateRawByteCache(drawScale, rect);

            return new RawImageInterleaved_Int32
            {
                Data = _raw,
                Height = rect.Height,
                Width = rect.Width
            };
        }

        private static Rectangle GetTargetRectangle(double dx, double dy, byte drawScale, float visualScale)
        {
            var size = Size >> (drawScale - 1);
            var rect = new Rectangle((int) Math.Floor(dx), (int) Math.Floor(dy),
                (int)Math.Floor(size * visualScale + 0.5), (int)Math.Floor(size * visualScale + 0.5));
            return rect;
        }


        public void CommitCache(byte drawScale, float visualScale)
        {
            _canCache = true;
            var rect = GetTargetRectangle(0, 0, drawScale, visualScale);
            lock (_cacheLock){
                _renderCache?.Dispose();
                UpdateRawByteCache(drawScale, rect);
                _renderCache = RawToTextureBrush(rect.Width, rect.Height);
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

            var rsq = radius / 2;
            rsq *= rsq;

            var blurEdge = (rsq - 2) * 0.5;
            var blurFact = 256 / (rsq - blurEdge);
            if (blurFact > 127) blurFact = 127;

            if (inkPenType == InkType.Import)
            {
                SetPixel(top, size, left, r, g, b);
            }
            else
            {
                if (px < 0) px -= 0.0; else px += 0.5;
                if (py < 0) py -= 0.5; else py += 0.5;
                DrawPenPoint(px-0.5, py-0.5, top, bottom, size, left, right, rsq, blurFact, blurEdge, r, g, b);
            }
            Invalidate();

            return true;
        }

        /// <summary>
        /// Blend pen with existing tile ink
        /// </summary>
        private void DrawPenPoint(double px, double py, int top, int bottom, int size, int left, int right, double rsq, double blurFact, double blurEdge, int r, int g, int b)
        {
            for (int y = top; y < bottom; y++)
            {
                var ysq = (y - py) * (y - py);
                var yo  = y * size;

                for (int x = left; x < right; x++)
                {
                    var idx = yo + x;

                    // circular pen
                    var xsq = (x - px) * (x - px);
                    var posSum = xsq + ysq;

                    if (posSum > rsq) continue; // outside of circle

                    var blend    = Pin255(blurFact * (posSum - blurEdge)); // 0 is full ink, 255 is no ink
                    int invBlend = 255 - blend;

                    Red[idx] = (byte) ((Red[idx] * blend + r * invBlend) >> 8);
                    Green[idx] = (byte) ((Green[idx] * blend + g * invBlend) >> 8);
                    Blue[idx] = (byte) ((Blue[idx] * blend + b * invBlend) >> 8);
                }
            }
        }

        private void SetPixel(int top, int size, int left, int r, int g, int b)
        {
            var idx = (top * size) + left;

            Red[idx] = (byte) r;
            Green[idx] = (byte) g;
            Blue[idx] = (byte) b;
        }

        private int Pin255(double rsq)
        {
            if (rsq > 255) return 255;
            if (rsq < 0) return 0;
            return (int)rsq;
        }

        /// <summary>
        /// Get the smallest square that encloses the pen point
        /// </summary>
        private static void PreparePen(double px, double py, double radius, out int top, out int left, out int right, out int bottom)
        {
            if (radius < 1) radius = 1;
            var ol = radius / 2;
            var or = radius - ol;

            top = Math.Max(0, (int) (py - ol - 1));
            left = Math.Max(0, (int) (px - ol - 1));
            right = Math.Min(Size, (int) (px + or + 1));
            bottom = Math.Min(Size, (int) (py + or + 1));
        }

        private const int Alpha = unchecked((int)0xff000000);

        private void UpdateRawByteCache(byte drawScale, Rectangle rect)
        {
            var width = rect.Width;
            var height = rect.Height;

            var size = Size >> (drawScale - 1); // size of source that is usable
            var sampleCount = Math.Min(size * size, Red.Length);

            var scale = width / size;

            // scale / copy into the managed 'raw' array
            if (scale == 1)
            {
                Scale_1to1(size);
            }
            else if (scale == 2)
            {
                EPXT_2x(size, width, height, 5);
            }
            else
            {
                // weird scale. Handle with a dumb nearest-neighbor for now.
                ScaleNearestNeighbour(size, width, height, sampleCount);
            }
        }

        
        private void Scale_1to1(int size)
        {
            // We scan through the data and copy bytes over
            // This is a point to do scaling

            for (int y = 0; y < size; y++)
            {
                var oy = y * size;

                for (int x = 0; x < size; x++)
                {
                    var i = x + oy;

                    var r = Red[i];
                    var g = Green[i];
                    var b = Blue[i];

                    _raw[i] = Alpha | (r << 16) | (g << 8) | (b);
                }
            }
        }

        /// <summary>
        /// Pixel art scaler for exactly 2x
        /// </summary>
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        // ReSharper disable once IdentifierTypo
        public void EPXT_2x(int size, int width, int height, int sigBits)
        {
            var srcWidth = size;
            var srcHeight = size;
            var dstWidth = width;

            for (int i = 0; i < width*height; i++)
            {
                _raw[i] = Alpha;
            }

            var dy = dstWidth;
            for (int plane = 0; plane < 3; plane++)
            {
                var small = Pick(plane, Blue, Green, Red);
                if (small == null) continue;
                int shift = plane * 8;

                for (int y = 0; y < srcHeight; y++)
                {
                    var dyo = 2 * y * dstWidth;
                    var syo = y * srcWidth;
                    var row = (y == 0 || y == srcHeight - 1) ? 0 : srcWidth;

                    for (int x = 0; x < srcWidth; x++)
                    {
                        var dx = 2 * x;
                        var col = (x == 0 || x == srcWidth - 1) ? 0 : 1;

                        var _1 = dyo + dx;
                        var _2 = dyo + dx + 1;
                        var _3 = dyo + dy + dx;
                        var _4 = dyo + dy + dx + 1;

                        var P = (int)small[syo + x];
                        var A = (small[syo + x - row] >> sigBits);
                        var C = (small[syo + x - col] >> sigBits);
                        var B = (small[syo + x + col] >> sigBits);
                        var D = (small[syo + x + row] >> sigBits);
                        
                        var v1 = P;
                        var v2 = P;
                        var v3 = P;
                        var v4 = P;

                        if (C == A && C != D && A != B) { v1 = small[syo + x - row]; }
                        if (A == B && A != C && B != D) { v2 = small[syo + x + col]; }
                        if (D == C && D != B && C != A) { v3 = small[syo + x - col]; }
                        if (B == D && B != A && D != C) { v4 = small[syo + x + row]; }

                        _raw[_1] |= v1 << shift;
                        _raw[_2] |= v2 << shift;
                        _raw[_3] |= v3 << shift;
                        _raw[_4] |= v4 << shift;
                    }
                }
            }
        }

        private void ScaleNearestNeighbour(int size, int width, int height, int sampleCount)
        {
            var dx = size / (double) width;
            var dy = size / (double) height;

            // We scan through the data and copy bytes over
            // This is a point to do scaling

            for (int y = 0; y < height; y++)
            {
                var oy = (int) (y * dy) * size;

                for (int x = 0; x < width; x++)
                {
                    var j = x + y * width;
                    var i = (int) (x * dx) + oy; // NN scaling. Should do better 

                    if (i >= sampleCount) continue;

                    var r = Red[i];
                    var g = Green[i];
                    var b = Blue[i];

                    _raw[j] = Alpha | (r << 16) | (g << 8) | (b);
                }
            }
        }

        private TextureBrush RawToTextureBrush(int width, int height)
        {
            using var bmp = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
            BitmapData? bmpData = null;
            try
            {
                bmpData = bmp.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly, bmp.PixelFormat);

                Marshal.Copy(_raw, 0, bmpData.Scan0, width * height);
            }
            catch
            {
                /* ignore draw races */
            }
            finally
            {
                if (bmpData != null) bmp.UnlockBits(bmpData);
            }

            var texture = new TextureBrush(bmp);
            return texture;
        }

        
        private static T? Pick<T>(int i, params T[] stuff)
        {
            if (i >= stuff.Length) return default;
            return stuff[i];
        }
    }
}