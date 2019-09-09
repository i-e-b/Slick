﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Windows.UI.Xaml.Controls;
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
        [NotNull] private readonly Grid _displayContainer;
        [NotNull] private readonly IStorageContainer _tileStore;
        [NotNull] private readonly Dictionary<PositionKey, CachedTile> _tileCache;

        [NotNull] private static readonly object _storageLock = new object();

        public double X;
        public double Y;

        /// <summary>
        /// Start rendering tiles into a display container. Always starts at 0,0
        /// </summary>
        public TileCanvas([NotNull]Grid displayContainer, [NotNull]IStorageContainer tileStore)
        {
            _tileCache = new Dictionary<PositionKey, CachedTile>();

            _tileStore = tileStore;

            _displayContainer = displayContainer;
            _displayContainer.SizeChanged += _displayContainer_SizeChanged;

            X = 0.0;
            Y = 0.0;

            ThreadPool.SetMinThreads(2, 2);
            ThreadPool.SetMaxThreads(40, 40);
            Invalidate();
        }

        private void _displayContainer_SizeChanged(object sender, Windows.UI.Xaml.SizeChangedEventArgs e)
        {
            Invalidate();
        }

        /// <summary>
        /// Use this if the tile data is changed
        /// </summary>
        public void ClearCache() {

            var toDetach = _tileCache.Values?.ToArray() ?? new CachedTile[0];
            foreach (var tile in toDetach) { tile.Detach(); }
            _tileCache.Clear();

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

            var required = VisibleTiles(0, 0, (int)_displayContainer.ActualWidth, (int)_displayContainer.ActualHeight);
            var toRemove = new HashSet<PositionKey>(_tileCache.Keys ?? NoKeys());

            // add any missing
            foreach (var key in required)
            {
                toRemove.Remove(key);
                if (!_tileCache.ContainsKey(key)) AddToCache(key);
            }

            // remove any extra
            foreach (var key in toRemove)
            {
                if (!_tileCache.TryGetValue(key, out var container)) continue;

                // todo: pull out to function
                container.Detach();
                _tileCache.Remove(key);

            }

            // re-align what's left
            foreach (var kvp in _tileCache)
            {
                var pos = VisualRectangle(kvp.Key);
                kvp.Value?.MoveTo(pos.X, pos.Y);
            }

            _displayContainer.InvalidateArrange();
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
            ThreadPool.QueueUserWorkItem(x => { LoadTileDataSync(key, tile); });
        }

        [NotNull]private static IEnumerable<PositionKey> NoKeys() { yield break; }


        [NotNull]
        public List<PositionKey> VisibleTiles(int dx, int dy, int width, int height)
        {
            var scale = 1;// 1 << (_drawScale - 1);

            var tlLoc = ScreenToCanvas(dx, dy);
            var brLoc = ScreenToCanvas(dx + width * scale, dy + height * scale);

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
            X += dx;
            Y += dy;

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
        public int SwitchScale() { return 0; }

        /// <summary>
        /// Centre on the given point, and set scale as 1:1
        /// </summary>
        public void CentreAndZoom(int wX, int wY){ }

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
            var changedTiles = new Queue<PositionKey>();

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
                            tile.Invalidate(); // cause a redraw -- TODO: should not be needed
                            changedTiles.Enqueue(key);
                        }
                        continue;

                    case TileState.Locked:
                        // Can't safely write at the moment.
                        // TODO: can we push back and keep the ink wet?
                        continue;

                    case TileState.Ready:
                        var changed = AlphaMapImageToTile(img, rect, tile, sourceLeft, sourceTop);
                        tile.Invalidate(); // cause a redraw -- TODO: should not be needed
                        if (changed) { changedTiles.Enqueue(key); }
                        break;
                }
            }

            while (changedTiles.TryDequeue(out var key))
            {
                // write back to database
                var xkey = key;
                //ThreadPool.QueueUserWorkItem(x => { WriteTileToBackingStoreSync(xkey); });
                WriteTileToBackingStoreSync(xkey);
            }

        }

        
        private void LoadTileDataSync(PositionKey key, CachedTile tile)
        {
            if (key == null || tile == null) return;

            //lock (_storageLock)
            {
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

                var Red = new byte[65536];
                var Green = new byte[65536];
                var Blue = new byte[65536];
                if (fileData != null) WaveletCompress.Decompress(fileData, Red, Green, Blue, 1);

                var packed = new byte[CachedTile.ByteSize];

                for (int i = 0; i < Red.Length; i++)
                {
                    // could do a EPX 2x here
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

                tile.RawImageData = packed;
                tile.SetState(TileState.Ready);
            }
        }


        private void WriteTileToBackingStoreSync(PositionKey key)
        {
            if (key == null) return;

            if (!_tileCache.TryGetValue(key, out var tile)) return;
            if (tile == null) return;
            if (tile.State == TileState.Locked) return;

            var name = key.ToString();

            if (tile.ImageIsBlank())
            {
                //lock (_storageLock)
                {
                    _tileStore.Delete(name, "img");

                    tile.SetState(TileState.Empty);
                }
            }
            else
            {
                var packed = tile.RawImageData;
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
                    //lock (_storageLock)
                    {
                        var ok = _tileStore.Store(name, "img", ms);

                        if (ok.IsFailure)
                        {
                            throw new Exception("Storage error: DB might be corrupt.", ok.FailureCause);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Returns true if any pixels were changed
        /// </summary>
        private static bool AlphaMapImageToTile(RawImageInterleaved_UInt8 img, Quad rect, CachedTile tile, int imgDx, int imgDy)
        {
            var dst = tile?.RawImageData;
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

                    int newAlpha = src[src_i + 3]; // alpha
                    int oldAlpha = 255 - newAlpha;
                    if (newAlpha < 10) continue;

                    // Alpha blend over existing color
                    dst[dst_i + 0] = (byte) (((dst[dst_i + 0] * oldAlpha) >> 8) + ((src[src_i + 0] * newAlpha) >> 8));
                    dst[dst_i + 1] = (byte) (((dst[dst_i + 1] * oldAlpha) >> 8) + ((src[src_i + 1] * newAlpha) >> 8));
                    dst[dst_i + 2] = (byte) (((dst[dst_i + 2] * oldAlpha) >> 8) + ((src[src_i + 2] * newAlpha) >> 8));
                    dst[dst_i + 3] = 255;

                    changed = true;
                }
            }
            return changed;
        }

        public const int TileImageSize = 256;

        /// <summary>
        /// Convert a screen position to a canvas pixel
        /// </summary>
        [NotNull]public CanvasPixelPosition ScreenToCanvas(double x, double y) {
            //var dss = 1.0 / (1 << (_drawScale - 1));
            //var invScale = 1.0 / VisualScale;

            var sx = x;// * invScale;
            var sy = y;// * invScale;

            var ox = X;// * dss;
            var oy = Y;// * dss;

            var tx = Math.Floor((sx + ox) / TileImageSize);
            var ty = Math.Floor((sy + oy) / TileImageSize);

            var ax = (ox - (tx * TileImageSize) + sx);// * invScale;
            var ay = (oy - (ty * TileImageSize) + sy);// * invScale;
            
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
    }
}