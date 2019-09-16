using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
        // tiles, cache, display
        [NotNull] private readonly Grid _displayContainer;
        [NotNull] private IStorageContainer _tileStore;
        [NotNull] private readonly Dictionary<PositionKey, CachedTile> _tileCache;
        [NotNull] private readonly HashSet<PositionKey> _selectedTiles;
        
        // History
        [NotNull] private readonly HashSet<PositionKey> _lastChangedTiles;

        public double X;
        public double Y;
        private double _cx;
        private double _cy;

        private double _viewScale = 1.0;

        public const int TileImageSize = 256;
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

            ThreadPool.SetMinThreads(2, 2);
            ThreadPool.SetMaxThreads(10, 10);
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

            var required = VisibleTiles(0, 0, (int)_displayContainer.ActualWidth, (int)_displayContainer.ActualHeight);
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
                if (!_tileCache.TryGetValue(key, out var container)) {
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
                var pos = VisualRectangle(kvp.Key);
                kvp.Value?.MoveTo(pos.X, pos.Y);
                kvp.Value?.SetSelected(_selectedTiles.Contains(kvp.Key));
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
            ThreadPool.QueueUserWorkItem(x => { LoadTileDataSync(xkey, tile); });
        }

        [NotNull]private static IEnumerable<PositionKey> NoKeys() { yield break; }


        [NotNull]
        public List<PositionKey> VisibleTiles(int dx, int dy, int width, int height)
        {
            var tlLoc = ScreenToCanvas(dx, dy);
            var brLoc = ScreenToCanvas(dx + width, dy + height);

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
            if (_viewScale < 0.249) _viewScale = 1.0;

            _displayContainer.RenderTransform = new ScaleTransform
            {
                CenterX = (int)(_displayContainer.ActualWidth / 2),
                CenterY = (int)(_displayContainer.ActualHeight / 2),
                ScaleX = _viewScale,
                ScaleY = _viewScale
            };

            Invalidate();

            return (int)(1 / _viewScale);
        }

        /// <summary>
        /// Centre on the given point, and set scale as 1:1
        /// </summary>
        public void CentreAndZoom(double wX, double wY){
            // TODO: centre at this zoom level

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
            _displayContainer.Children?.Clear();
        }

        /// <summary>
        /// Draw an image into the tile canvas (committing to storage).
        /// The base position is the visible area of the Display Container.
        /// </summary>
        public void ImportBytes(RawImageInterleaved_UInt8 img, int targetLeft, int targetTop, int targetWidth, int targetHeight, int sourceLeft, int sourceTop)
        {
            if (img == null) return;

            // Cases:
            // 1) tile is loaded and empty -- create storage, write, commit to backing
            // 2) tile is locked -- abort
            // 3) tile is ready -- merge onto array, commit to backing

            var positionKeys = VisibleTiles(targetLeft, targetTop, targetWidth, targetHeight);
            _lastChangedTiles.Clear(); // last set of tiles are now forever. For multi undo, we could push a stack here

            foreach (var key in positionKeys)
            {
                var rect = VisualRectangle(key);
                if (!_tileCache.ContainsKey(key)) {
                    // TODO: pull out to function
                    var newTile = new CachedTile(_displayContainer);
                    _tileCache.Add(key, newTile);
                }

                var tile = _tileCache[key];
                if (tile == null) continue; // should never happen

                switch (tile.State) {
                    case TileState.Empty:
                        // allocate and write
                        tile.AllocateEmptyImage();
                        var really = AlphaMapImageToTile(img, rect, tile, sourceLeft, sourceTop);
                        if (!really) {
                            tile.Deallocate();
                        }
                        else {
                            tile.SetState(TileState.Ready);
                            
                            _lastChangedTiles.Add(key);
                            ThreadPool.QueueUserWorkItem(x => { WriteTileToBackingStoreSync(key, tile); });
                        }
                        continue;

                    case TileState.Locked:
                        // Can't safely write at the moment.
                        // TODO: can we push back and keep the ink wet?
                        continue;

                    case TileState.Ready:
                        var changed = AlphaMapImageToTile(img, rect, tile, sourceLeft, sourceTop);
                        tile.Invalidate();
                        if (changed) { 
                            _lastChangedTiles.Add(key);
                            ThreadPool.QueueUserWorkItem(x => { WriteTileToBackingStoreSync(key, tile); });
                        }
                        break;
                }
            }
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

            InterleavedFile fileData;
            try
            {
                fileData = InterleavedFile.ReadFromStream(img.ResultData);
            }
            catch
            {
                tile.MarkCorrupted();
                return;
            }

            var Red = new byte[65536];
            var Green = new byte[65536];
            var Blue = new byte[65536];
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

        private void WriteTileToBackingStoreSync(PositionKey key, CachedTile tile)
        {
            if (key == null) return;

            //if (!_tileCache.TryGetValue(key, out var tile)) return;
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

                var Red = new byte[packed.Length / 4];
                var Green = new byte[packed.Length / 4];
                var Blue = new byte[packed.Length / 4];

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
        
        /// <summary>
        /// Returns true if any pixels were changed
        /// </summary>
        private static bool AlphaMapImageToTile(RawImageInterleaved_UInt8 img, Quad rect, CachedTile tile, int imgDx, int imgDy)
        {
            var dst = tile?.GetTileData();
            var src = img?.Data;
            if (src == null || dst == null || rect == null) return false;

            bool changed = false;

            int left = rect.X - imgDx;
            int top = rect.Y - imgDy;

            for (int y = 0; y < 256; y++)
            {
                for (int x = 0; x < 256; x++)
                {
                    var src_i = ((y + top) * img.Width * 4) + ((x + left) * 4);
                    var dst_i = y * (256 * 4) + (x * 4);

                    if (dst_i < 0) break;
                    if (src_i < 0) break;
                    if (dst_i >= dst.Length) return changed;
                    if (src_i >= src.Length) return changed;

                    var srcAlpha = src[src_i + 3];
                    if (srcAlpha < 5) continue;

                    var newAlpha = srcAlpha / 255.0f;
                    var oldAlpha = 1.0f - newAlpha;

                    // Alpha blend over existing color
                    // This for plain alpha:
                    //dst[dst_i + 0] = Clip((dst[dst_i + 0] * oldAlpha) + (src[src_i + 0] * newAlpha));
                    //dst[dst_i + 1] = Clip ((dst[dst_i + 1] * oldAlpha) + (src[src_i + 1] * newAlpha));
                    //dst[dst_i + 2] = Clip ((dst[dst_i + 2] * oldAlpha) + (src[src_i + 2] * newAlpha));

                    // This for pre-multiplied alpha
                    dst[dst_i + 0] = Clip((dst[dst_i + 0] * oldAlpha) + (src[src_i + 0]));
                    dst[dst_i + 1] = Clip ((dst[dst_i + 1] * oldAlpha) + (src[src_i + 1]));
                    dst[dst_i + 2] = Clip ((dst[dst_i + 2] * oldAlpha) + (src[src_i + 2]));
                    dst[dst_i + 3] = 255;

                    changed = true;
                }
            }
            return changed;
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
            var cx = _cx;
            var cy = _cy;
            
            // distance from top-left at 100% zoom
            var dx = x / _viewScale;
            var dy = y / _viewScale;

            // extra offset due to zoom level
            var ox = (cx / _viewScale) - cx;
            var oy = (cy / _viewScale) - cy;

            // offset by current X,Y
            var qx = X + dx - ox;
            var qy = Y + dy - oy;

            // quantise to tile size
            var tx = Math.Floor(qx / TileImageSize);
            var ty = Math.Floor(qy / TileImageSize);

            var ax = qx - (tx * TileImageSize);
            var ay = qy - (ty * TileImageSize);
            if (ax < 0) ax += TileImageSize;
            if (ay < 0) ay += TileImageSize;

            return new CanvasPixelPosition{
                TilePosition = new PositionKey(tx,ty),
                X = ax, Y = ay
            };
        }
        
        public void CanvasToScreen(PositionKey tilePos, out float x, out float y)
        {
            if (tilePos == null) {
                x = 0; y = 0;
                return;
            }

            var displaySize = TileImageSize;
            var offset_x = (int)X;// >> (_drawScale - 1);
            var offset_y = (int)Y;// >> (_drawScale - 1);

            x = (tilePos.X * displaySize) - offset_x;
            y = (tilePos.Y * displaySize) - offset_y;
        }

        [NotNull]
        private Quad VisualRectangle(PositionKey tile)
        {
            CanvasToScreen(tile, out var x, out var y);
            return new Quad((int)x, (int)y, TileImageSize, TileImageSize);
        }

        public int CurrentZoom()
        {
            return (int)Math.Round(1.0 / _viewScale);
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