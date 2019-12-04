using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using JetBrains.Annotations;
using SlickCommon.Canvas;
using SlickCommon.ImageFormats;
using SlickCommon.Storage;

namespace SlickUWP.Canvas
{
    /// <summary>
    /// Handles scrolling and scaling inputs, manages a set of tile-image controls for display
    /// </summary>
    public class TileCanvas
    {
        public const int TileImageSize = 256;
        public const int GridSize = 32; // must be a factor of TileImageSize

        // tiles, cache, display
        [NotNull] private readonly Grid _displayContainer;
        [NotNull] private IStorageContainer _tileStore;
        [NotNull] private readonly Dictionary<PositionKey, CachedTile> _tileCache;
        [NotNull] private readonly HashSet<PositionKey> _selectedTiles;
        
        // History
        [NotNull] private readonly HashSet<PositionKey> _lastChangedTiles;

        // Working buffers (they *MUST* be ThreadStatic)
        [ThreadStatic]private static byte[] Red;
        [ThreadStatic]private static byte[] Green;
        [ThreadStatic]private static byte[] Blue;

        /// <summary>Offset of canvas in real pixels</summary>
        public double X;
        /// <summary>Offset of canvas in real pixels</summary>
        public double Y;
        /// <summary>Centre of screen in real pixels</summary>
        private double _cx;
        /// <summary>Centre of screen in real pixels</summary>
        private double _cy;

        private double _viewScale = 1.0;

        private volatile bool _inReflow = false;
        

        /// <summary>
        /// Start rendering tiles into a display container. Always starts at 0,0
        /// </summary>
        public TileCanvas([NotNull]Grid displayContainer, [NotNull]IStorageContainer tileStore)
        {
            _tileCache = new Dictionary<PositionKey, CachedTile>();
            _lastChangedTiles = new HashSet<PositionKey>();
            _selectedTiles = new HashSet<PositionKey>();

            _tileStore = tileStore;

            _displayContainer = displayContainer;
            _displayContainer.SizeChanged += _displayContainer_SizeChanged;
            
            _cx = _displayContainer.ActualWidth / 2;
            _cy = _displayContainer.ActualHeight / 2;

            X = 0.0;
            Y = 0.0;

            ThreadPool.SetMinThreads(4, 1);
            ThreadPool.SetMaxThreads(4, 1);
            Invalidate();
        }

        private void _displayContainer_SizeChanged(object sender, Windows.UI.Xaml.SizeChangedEventArgs e)
        {
            _cx = _displayContainer.ActualWidth / 2;
            _cy = _displayContainer.ActualHeight / 2;
            Invalidate();
        }

        /// <summary>
        /// Use this if the tile data is changed
        /// </summary>
        public void ChangeStorage([NotNull]IStorageContainer newTileStore) {
            _tileStore = newTileStore;

            // recentre and reset zoom
            X = 0.0;
            Y = 0.0;
            CentreAndZoom(_displayContainer.ActualWidth / 2, _displayContainer.ActualHeight / 2);

            // clear caches and invalidate
            ResetCache();
        }

        /// <summary>
        /// Called when the base store changes (changed page, or undo)
        /// </summary>
        private void ResetCache() {
            var toDetach = _tileCache.Values?.ToArray() ?? new CachedTile[0];
            foreach (var tile in toDetach) { tile.Detach(); }
            Win2dCanvasPool.Sanitise();
            _tileCache.Clear();
            _lastChangedTiles.Clear();

            Invalidate();
        }


        /// <summary>
        /// Use this if the window size changes or the viewport is scrolled
        /// </summary>
        public void Invalidate()
        {
            // 1) figure out what tiles should be showing
            // 2) add any not in the cache
            // 3) remove anything in the cache that should not be visible
            // 4) ensure the tiles are in the correct offset position

            if (_inReflow) return;
            _inReflow = true;

            var width = (int)_displayContainer.ActualWidth;
            var height = (int)_displayContainer.ActualHeight;

            var required = VisibleTiles(0, 0, width, height);
            var toRemove = new HashSet<PositionKey>(_tileCache.Keys ?? NoKeys());
            var toAdd = new HashSet<PositionKey>();

            // add any missing
            foreach (var key in required)
            {
                toRemove.Remove(key);
                if (!_tileCache.ContainsKey(key)) toAdd.Add(key);
            }

            // remove any extra
            foreach (var key in toRemove)
            {
                if (!_tileCache.TryGetValue(key, out var container))
                {
                    continue;
                    //throw new Exception("Lost container!");
                }

                container.Detach();
                _tileCache.Remove(key);
            }

            // add any missing (we do this after remove to use the canvas pool effectively)
            foreach (var key in toAdd)
            {
                if (!_tileCache.ContainsKey(key)) AddToCache(key);
            }

            // re-align what's left
            foreach (var kvp in _tileCache)
            {
                var pos = VisualRectangleNative(kvp.Key);
                if (kvp.Value == null) continue;
                kvp.Value.SetSelected(_selectedTiles.Contains(kvp.Key));
                kvp.Value.MoveTo(pos.X, pos.Y);
            }

            Win2dCanvasPool.Sanitise();
            _inReflow = false;
        }

        /// <summary>
        /// Load a tile from the store, and add it to the display container and dictionary
        /// If no tile is available in the store at this position, we put an empty proxy in place.
        /// </summary>
        private void AddToCache(PositionKey key)
        {
            if (key == null) return;

            var tile = new CachedTile(_displayContainer);
            try {
                _tileCache.Add(key, tile);
                tile.SetState(TileState.Locked);
            }
            catch {
                return;
            }

            // Read the db and set tile state in the background
            var xkey = key;
            //ThreadPool.QueueUserWorkItem(x => { LoadTileDataSync(xkey, tile); });
            ThreadPool.UnsafeQueueUserWorkItem(x => { LoadTileDataSync((PositionKey)x, tile); }, xkey);
        }

        [NotNull]private static IEnumerable<PositionKey> NoKeys() { yield break; }


        [NotNull]
        public List<PositionKey> VisibleTiles(int dx, int dy, int width, int height)
        {
            var tlLoc = ScreenToCanvas(dx, dy);
            var brLoc = ScreenToCanvas(dx + width, dy + height);

            if (double.IsNaN(tlLoc.X) || double.IsNaN(brLoc.X))
                throw new Exception("ScreenToCanvas gave an invalid result");

            var result = new List<PositionKey>();
            var tlPos = tlLoc.TilePosition ?? throw new Exception("TL tile position lookup failed");
            var brPos = brLoc.TilePosition ?? throw new Exception("BR tile position lookup failed");
            for (int y = tlPos.Y; y <= brPos.Y; y++)
            {
                for (int x = tlPos.X; x <= brPos.X; x++)
                {
                    result.Add(new PositionKey(x, y));
                }
            }

            return result;
        }

        /// <summary>
        /// Relative move of the canvas (e.g. from touch scrolling)
        /// </summary>
        public void Scroll(double dx, double dy){
            X += dx / _viewScale;
            Y += dy / _viewScale;

            Invalidate();
        }

        /// <summary>
        /// Set an absolute scroll position
        /// </summary>
        public void ScrollTo(double x, double y){
            X = x;
            Y = y;

            Invalidate();
        }

        /// <summary>
        /// Rotate through scaling options
        /// </summary>
        public int SwitchScale() {
            _viewScale /= 2;
            //_viewScale -= 0.1;
            if (_viewScale < 0.249) _viewScale = 1.0;

            UpdateViewScale();

            Invalidate();

            return (int)(1 / _viewScale);
        }

        private void UpdateViewScale()
        {
            _displayContainer.RenderTransform = new ScaleTransform
            {
                CenterX = (int) (_displayContainer.ActualWidth / 2),
                CenterY = (int) (_displayContainer.ActualHeight / 2),
                ScaleX = _viewScale,
                ScaleY = _viewScale
            };
        }

        /// <summary>
        /// Change scale by a fractional amount.
        /// This is restricted in range
        /// </summary>
        public void DeltaScale(double deltaScale)
        {
            if (Math.Abs(1 - deltaScale) < 0.001) return;
            var target = _viewScale;

            target += deltaScale - 1.0f;
            if (target > 2.0) target = 2.0;
            if (target < 0.25) target = 0.25;

            _viewScale = target;
            UpdateViewScale();
        }

        /// <summary>
        /// Centre on the given point, and set scale as 1:1
        /// </summary>
        public void CentreAndZoom(double wX, double wY){
            // w[XY] are in screen co-ords at 100%
            // centre at 100%:
            var cx = _displayContainer.ActualWidth / 2;
            var cy = _displayContainer.ActualHeight / 2;

            // scroll at 100%:
            var dx = wX - cx;
            var dy = wY - cy;

            // scroll to centre at current zoom
            X += dx / _viewScale;
            Y += dy / _viewScale;

            _viewScale = 1.0;
            _displayContainer.RenderTransform = new ScaleTransform
            {
                CenterX = (int)cx,
                CenterY = (int)cy,
                ScaleX = _viewScale,
                ScaleY = _viewScale
            };

            Invalidate();
        }

        /// <summary>
        /// Remove all tiles from the display container.
        /// <para></para>
        /// Call when page is being switched.
        /// </summary>
        public void Close() {
            _displayContainer.Dispatcher?.RunAsync(CoreDispatcherPriority.High, () =>
            {
                _displayContainer.Children?.Clear();
            });
        }

        /// <summary>
        /// Draw an entire image onto the canvas, scaled to fit in the given rectangle.
        /// Expects screen-space co-ordinates.
        /// <para></para>
        /// Returns false if any of the tiles couldn't be written to
        /// </summary>
        public bool ImportBytesScaled(RawImageInterleaved_UInt8 img, int left, int top, int right, int bottom) {
            if (img == null) return false;
            if (left >= right || top >= bottom) return true;

            var tl = ScreenToCanvas(left, top);
            var br = ScreenToCanvas(right, bottom);

            var tileLeft = tl.TilePosition?.X ?? throw new Exception("Invalid tile calculation");
            var tileTop = tl.TilePosition.Y;
            var tileRight = br.TilePosition?.X ?? throw new Exception("Invalid tile calculation");
            var tileBottom = br.TilePosition.Y;

            double targetNativeTop = ((double)tileTop * TileImageSize) + tl.Y;
            double targetNativeBottom = ((double)tileBottom * TileImageSize) + br.Y;
            double targetNativeLeft = ((double)tileLeft * TileImageSize) + tl.X;
            double targetNativeRight = ((double)tileRight * TileImageSize) + br.X;

            double scale_x = img.Width / (targetNativeRight - targetNativeLeft);
            double scale_y = img.Height / (targetNativeBottom - targetNativeTop);

            // scan through the tiles we cover, and scale segments to fit.
            for (int ty = tileTop; ty <= tileBottom; ty++)
            {
                // see if we're at the top or bottom of the tiles covered
                var localNativeTop = (double)ty*TileImageSize;
                var localNativeBottom = ((double)ty + 1) * TileImageSize;
                var tgtTop = (localNativeTop < targetNativeTop) ? tl.Y : 0;
                var tgtHeight = (localNativeBottom > targetNativeBottom) ? br.Y : TileImageSize;
                tgtHeight -= tgtTop;

                for (int tx = tileLeft; tx <= tileRight; tx++)
                {
                    // see if we're at the left or right of the tiles covered
                    var localNativeLeft = (double)tx * TileImageSize;
                    var localNativeRight = ((double)tx + 1) * TileImageSize;
                    var tgtLeft = (localNativeLeft < targetNativeLeft) ? tl.X : 0;
                    var tgtWidth = (localNativeRight > targetNativeRight) ? br.X : TileImageSize;
                    tgtWidth -= tgtLeft;

                    // calculate what area of the image to use
                    var imgLeft = localNativeLeft - targetNativeLeft;
                    var imgTop = localNativeTop - targetNativeTop;
                    if (imgLeft < 0) imgLeft = 0;
                    if (imgTop < 0) imgTop = 0;

                    // Ensure the tile is ready to be drawn on:
                    var key = new PositionKey(tx, ty);
                    var ok = PrepareTileForDraw(key, out var tile);
                    if (!ok) return false;

                    var changed = AlphaMapImageToTileScaled(img, tile,
                            imageArea: new Quad(imgLeft, imgTop, tgtWidth * scale_x, tgtHeight * scale_y),
                            tileArea: new Quad(tgtLeft, tgtTop, tgtWidth, tgtHeight)
                        );

                    if (changed) {
                        _lastChangedTiles.Add(key);
                        tile.Invalidate();
                        tile.SetState(TileState.Ready);
                        ThreadPool.UnsafeQueueUserWorkItem(x => { WriteTileToBackingStoreSync((PositionKey) x, tile); }, key);
                    }
                }
            }
            return true;
        }

        private bool PrepareTileForDraw([NotNull]PositionKey key, out CachedTile tile)
        {
            if (!_tileCache.ContainsKey(key))
            {
                var newTile = new CachedTile(_displayContainer);
                newTile.AllocateEmptyImage();
                _tileCache.Add(key, newTile);
            }

            tile = _tileCache[key];
            if (tile == null) return false; // should never happen
            if (tile.State == TileState.Locked) return false;

            var dst = tile.GetTileData();
            if (dst == null)
            {
                tile.AllocateEmptyImage();
                dst = tile.GetTileData();
            }

            if (dst == null) throw new Exception("Tile data is missing, even after allocation");
            return true;
        }

        /// <summary>
        /// Write an image onto a tile, with transparency.
        /// Returns true if any pixels were changed.
        /// `tileArea` is in tile space. `imageArea` is in image space.
        /// </summary>
        /// <remarks>The scaling here is only intended for small variations (between 0.6x and 1.4x). The
        /// Source image should be scaled if you are outside of this range</remarks>
        private bool AlphaMapImageToTileScaled(RawImageInterleaved_UInt8 img, ICachedTile tile, Quad imageArea, Quad tileArea)
        {
            // This needs to be improved
            var dst = tile?.GetTileData();
            var src = img?.Data;
            if (src == null || dst == null || imageArea == null || tileArea == null) return false;
            if (img.Width < 1 || img.Height < 1) return false;

            bool changed = false;

            // start and end limits on tile
            int x0 = (int)Math.Max(tileArea.X, 0);
            int x1 = (int)Math.Min(tileArea.X + tileArea.Width, TileImageSize);
            int y0 = (int)Math.Max(tileArea.Y, 0);
            int y1 = (int)Math.Min(tileArea.Y + tileArea.Height, TileImageSize);

            int dst_width = x1 - x0;
            int dst_height = y1 - y0;
            if (dst_width < 1 || dst_height < 1) return false;

            double scale_x = imageArea.Width / dst_width;
            double scale_y = imageArea.Height / dst_height;

            var imgyo = imageArea.Y;
            var imgxo = imageArea.X;

            // AA caches
            int[] src_aa = new int[(img.Width + 1) * 4];

            for (int y = y0; y < y1; y++)
            {
                var img_yi = y - y0;

                // prepare a scaled row here
                AntiAliasRow(img, img_yi, scale_y, imgyo, src, src_aa);

                // copy image row
                for (int x = x0; x < x1; x++)
                {
                    var img_xi = x - x0;

                    // Measure AA
                    var xoff_f = Range(0.0, (img_xi * scale_x) + imgxo, img.Width);
                    var xoff = (int)xoff_f;
                    double x_frac = xoff_f - xoff;
                    var imul = (int)(255 * x_frac);
                    var mul = 255 - imul;

                    var src_xi = xoff * 4;

                    var src_i = Range(0, src_xi, src_aa.Length - 4); // offset into source raw image
                    var src_i2 = Range(0, src_xi + 4, src_aa.Length - 4); // offset into source raw image
                    var dst_i = y * (TileImageSize * 4) + (x * 4); // offset into tile data
                    if (dst_i < 0) continue;
                    if (dst_i >= dst.Length) continue;

                    // Threshold alpha
                    if (src_aa[src_i + 3] < 2 && src_aa[src_i2 + 3] < 2) continue;

                    // Take source samples and do AA
                    var srcB = ((src_aa[src_i + 0] * mul) + (src_aa[src_i2 + 0] * imul)) >> 8;
                    var srcG = ((src_aa[src_i + 1] * mul) + (src_aa[src_i2 + 1] * imul)) >> 8;
                    var srcR = ((src_aa[src_i + 2] * mul) + (src_aa[src_i2 + 2] * imul)) >> 8;
                    var srcA = ((src_aa[src_i + 3] * mul) + (src_aa[src_i2 + 3] * imul)) >> 8;

                    var newAlpha = srcA / 255.0f;
                    var oldAlpha = 1.0f - newAlpha;

                    // Alpha blend over existing color
                    // This for plain alpha:
                    //dst[dst_i + 0] = Clip((dst[dst_i + 0] * oldAlpha) + (src[src_i + 0] * newAlpha));
                    //dst[dst_i + 1] = Clip((dst[dst_i + 1] * oldAlpha) + (src[src_i + 1] * newAlpha));
                    //dst[dst_i + 2] = Clip((dst[dst_i + 2] * oldAlpha) + (src[src_i + 2] * newAlpha));

                    // This for pre-multiplied alpha
                    dst[dst_i + 0] = Clip((dst[dst_i + 0] * oldAlpha) + srcB);
                    dst[dst_i + 1] = Clip((dst[dst_i + 1] * oldAlpha) + srcG);
                    dst[dst_i + 2] = Clip((dst[dst_i + 2] * oldAlpha) + srcR);
                    dst[dst_i + 3] = 255; // tile alpha is always 100%

                    changed = true;
                }
            }
            return changed;
        }

        private void AntiAliasRow([NotNull]RawImageInterleaved_UInt8 img, int img_yi, double scale_y, double imgyo, [NotNull]byte[] src, [NotNull]int[] src_aa)
        {
            // Measure AA
            var rowbytes = img.Width * 4;
            var yoff_f = Range(0.0, (img_yi * scale_y) + imgyo, img.Height - 1);
            var yoff = (int) yoff_f;
            double y_frac = yoff_f - yoff;
            var imul = (int) (255 * y_frac);
            var mul = 255 - imul;

            var end = img.Width * 4;
            var src_yi = Range(0, yoff * rowbytes, src.Length - (rowbytes + 4 + end));

            // pre-calc AA row data from two source lines 
            for (int x = 0; x < end; x += 4)
            {
                src_aa[x + 0] = ((src[src_yi + 0 + x] * mul) + (src[src_yi + 0 + x + rowbytes] * imul)) >> 8;
                src_aa[x + 1] = ((src[src_yi + 1 + x] * mul) + (src[src_yi + 1 + x + rowbytes] * imul)) >> 8;
                src_aa[x + 2] = ((src[src_yi + 2 + x] * mul) + (src[src_yi + 2 + x + rowbytes] * imul)) >> 8;
                src_aa[x + 3] = ((src[src_yi + 3 + x] * mul) + (src[src_yi + 3 + x + rowbytes] * imul)) >> 8;
            }
        }

        private int Range(int low, int value, int high)
        {
            if (value > high) return high;
            if (value < low) return low;
            return value;
        }
        private double Range(double low, double value, double high)
        {
            if (value > high) return high;
            if (value < low) return low;
            return value;
        }

        /// <summary>
        /// Read the canvas (including backing store where required) to generate a single image.
        /// The tiles selected do not need to be contiguous.
        /// </summary>
        /// <param name="tiles">Tiles to render</param>
        public RawImageInterleaved_UInt8 ExportBytes(List<PositionKey> tiles)
        {
            if (tiles == null) return null;

            // Find boundaries of the tiles
            int top = int.MaxValue, left = int.MaxValue,
                right = int.MinValue, bottom = int.MinValue;

            foreach (var key in tiles)
            {
                if (key == null) continue;
                top = Math.Min(top, key.Y * TileImageSize);
                left = Math.Min(left, key.X * TileImageSize);
                bottom = Math.Max(bottom, (key.Y + 1) * TileImageSize);
                right = Math.Max(right, (key.X + 1) * TileImageSize);
            }
            if (top >= bottom || left >= right) return null;
            
            // allocate bytes
            var width = right - left;
            var height = bottom - top;
            var raw = new byte[width * height * 4];

            // clear the raw array to White
            for (int i = 0; i < raw.Length; i++) { raw[i] = 255; }

            // render tiles
            ICachedTile temp = new OffscreenTile();
            foreach (var key in tiles)
            {
                if (key == null) continue;
                byte[] tileData;
                if (_tileCache.TryGetValue(key, out var cached))
                {
                    tileData = cached.GetTileData();
                }
                else
                {
                    LoadTileDataSync(key, temp);
                    tileData = temp.GetTileData();
                }
                var tileLeft = (key.X * TileImageSize) - left;
                var tileTop = (key.Y * TileImageSize) - top;
                CopyTileToImage(tileData, raw, tileLeft, tileTop, width, height);
            }

            // return image
            return new RawImageInterleaved_UInt8{
                Height = height,
                Width = width,
                Data = raw
            };
        }

        private void CopyTileToImage(byte[] src, byte[] dst, int left, int top, int dstWidth, int dstHeight)
        {
            if (src == null || dst == null) return;
            var xb = left * 4;

            // scan through src, place it in dst
            for (int y = 0; y < TileImageSize; y++)
            {
                if (top + y > dstHeight) break;
                var dyo = (top + y) * (dstWidth * 4);
                var syo = y * TileImageSize * 4;

                for (int x = 0; x < TileImageSize; x++)
                {
                    if (x + left > dstWidth) break;
                    var xo = x*4;
                    dst[dyo + xb + xo + 0] = src[syo + xo + 0];
                    dst[dyo + xb + xo + 1] = src[syo + xo + 1];
                    dst[dyo + xb + xo + 2] = src[syo + xo + 2];
                    dst[dyo + xb + xo + 3] = src[syo + xo + 3];
                }
            }
        }

        private void LoadTileDataSync(PositionKey key, ICachedTile tile)
        {
            if (Red == null) Red = new byte[65536];
            if (Green == null) Green = new byte[65536];
            if (Blue == null) Blue = new byte[65536];
            try
            {
                if (key == null || tile == null) return;

                var name = key.ToString();
                var res = _tileStore.Exists(name);
                if (res.IsFailure)
                {
                    tile.SetState(TileState.Empty);
                    return;
                }

                var version = res.ResultData?.CurrentVersion ?? 1;
                var img = _tileStore.Read(name, "img", version);

                if (img.IsFailure || img.ResultData == null)
                {
                    tile.SetState(TileState.Empty);
                    return;
                }

                var fileData = InterleavedFile.ReadFromStream(img.ResultData);
                if (fileData != null) WaveletCompress.Decompress(fileData, Red, Green, Blue, 1);

                var packed = new byte[CachedTile.ByteSize];

                for (int i = 0; i < Red.Length; i++)
                {
                    packed[4 * i + 0] = Blue[i];
                    packed[4 * i + 1] = Green[i];
                    packed[4 * i + 2] = Red[i];

                    if (Blue[i] >= 254 && Green[i] >= 254 && Red[i] >= 254)
                    {
                        packed[4 * i + 3] = 0;
                    }
                    else
                    {
                        packed[4 * i + 3] = 255;
                    }
                }

                tile.SetTileData(packed);
                tile.SetState(TileState.Ready);
            }
            catch
            {
                tile?.MarkCorrupted();
            }
        }

        private void WriteTileToBackingStoreSync(PositionKey key, CachedTile tile)
        {
            if (Red == null) Red = new byte[65536];
            if (Green == null) Green = new byte[65536];
            if (Blue == null) Blue = new byte[65536];

            if (key == null) return;

            if (tile == null) return;
            if (tile.State == TileState.Locked) return;

            var name = key.ToString();

            if (tile.ImageIsBlank())
            {
                _tileStore.Delete(name, "img");
                tile.SetState(TileState.Empty);
            }
            else
            {
                var packed = tile.GetTileData();
                if (packed == null) return;

                for (int i = 0; i < Red.Length; i++)
                {
                    Blue[i] = packed[4 * i + 0];
                    Green[i] = packed[4 * i + 1];
                    Red[i] = packed[4 * i + 2];
                }

                using (var ms = new MemoryStream())
                {
                    WaveletCompress.Compress(Red, Green, Blue, tile.Width, tile.Height).WriteToStream(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    var ok = _tileStore.Store(name, "img", ms);

                    if (ok.IsFailure)
                    {
                        throw new Exception("Storage error: DB might be corrupt.", ok.FailureCause);
                    }
                }
            }
        }
        
        private static byte Clip(float value)
        {
            if (value >= 255) return 255;
            if (value <= 0) return 0;
            return (byte)Math.Round(value);
        }

        /// <summary>
        /// Convert a screen position to a canvas pixel
        /// </summary>
        [NotNull]public CanvasPixelPosition ScreenToCanvas(double x, double y) {
            if (_viewScale < 0.01)
                throw new Exception("Invalid view scale!");

            // distance from top-left at 100% zoom
            var dx = x / _viewScale;
            var dy = y / _viewScale;

            // extra offset due to zoom level
            var ox = (_cx / _viewScale) - _cx;
            var oy = (_cy / _viewScale) - _cy;

            // offset by current X,Y
            var qx = X + dx - ox;
            var qy = Y + dy - oy;

            // quantise to tile size
            var tx = Math.Floor(qx / TileImageSize);
            var ty = Math.Floor(qy / TileImageSize);

            // Position inside the tile
            var ax = qx - (tx * TileImageSize);
            var ay = qy - (ty * TileImageSize);
            if (ax < 0) ax += TileImageSize;
            if (ay < 0) ay += TileImageSize;

            return new CanvasPixelPosition{
                TilePosition = new PositionKey(tx,ty),
                X = ax, Y = ay
            };
        }
        
        /// <summary>
        /// Canvas to screen position. DOES NOT calculate for zoom
        /// </summary>
        public void CanvasToScreen(PositionKey tilePos, out float x, out float y)
        {
            if (tilePos == null) {
                x = 0; y = 0;
                return;
            }

            x = (tilePos.X * TileImageSize) - (int)X;
            y = (tilePos.Y * TileImageSize) - (int)Y;
        }

        /// <summary>
        /// Calculate the visual rectange at native scale
        /// </summary>
        [NotNull]
        private Quad VisualRectangleNative(PositionKey tile)
        {
            CanvasToScreen(tile, out var x, out var y);
            return new Quad((int)x, (int)y, TileImageSize, TileImageSize);
        }

        public double CurrentZoom()
        {
            return 1.0 / _viewScale;
        }

        /// <summary>
        /// Roll back the versions of the last changed tiles
        /// </summary>
        public void Undo()
        {            
            // for multi-undo, we could store prev. changed tiles set in the metadata store, or stack up change sets in ram.

            var changed = _lastChangedTiles.ToArray();
            var changesMade = false;
            foreach (var tile in changed)
            {
                // if tiles exist for version-1:
                // - roll the version back in meta
                // - reload tile (purge from cache and flip the reload trigger)
                var path = tile.ToString();
                var node = _tileStore.Exists(path);
                if (node.IsFailure) continue;
                if (node.ResultData == null) continue;

                var prevVersion = node.ResultData.CurrentVersion - 1;

                if (prevVersion < 1)
                {
                    // undoing the first stroke. Mark it deleted.
                    var deadNode = new StorageNode { CurrentVersion = node.ResultData.CurrentVersion, Id = node.ResultData.Id, IsDeleted = true };
                    _tileStore.UpdateNode(path, deadNode);
                    changesMade = true;
                }
                else
                {
                    var ok = _tileStore.Read(path, "img", prevVersion);
                    if (ok.IsFailure) continue;

                    var newNode = new StorageNode { CurrentVersion = prevVersion, Id = node.ResultData.Id, IsDeleted = false };
                    _tileStore.UpdateNode(path, newNode);
                    changesMade = true;
                }
            }

            if (changesMade) {
                ResetCache();
            }

            // Reset the last update set
            // For multi-undo, we should roll back to a previous undo set.
            _lastChangedTiles.Clear();
        }

        public void CentreOn(PositionKey pos)
        {
            if (pos == null) return;

            X = (pos.X * TileImageSize);
            Y = (pos.Y * TileImageSize);
            
            X -= _displayContainer.ActualWidth / 2;
            Y -= _displayContainer.ActualHeight / 2;

            Invalidate();
        }

        [NotNull]
        public PositionKey PositionOfCurrentCentre()
        {            
            var cx = X + _displayContainer.ActualWidth / 2;
            var cy = Y + _displayContainer.ActualHeight / 2;

            return new PositionKey(cx / TileImageSize, cy / TileImageSize);
        }

        public void AddSelection(double x, double y)
        {
            var pos = ScreenToCanvas(x,y);
            _selectedTiles.Add(pos.TilePosition);
            Invalidate();
        }

        public void ClearSelection()
        {
            _selectedTiles.Clear();
            Invalidate();
        }

        public List<PositionKey> SelectedTiles()
        {
            return _selectedTiles.ToList();
        }
    }
}