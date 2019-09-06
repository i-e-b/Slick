using System;
using System.Collections.Generic;
using System.Linq;
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

        public double X;
        public double Y;

        /// <summary>
        /// Start rendering tiles into a display container. Always starts at 0,0
        /// </summary>
        public TileCanvas([NotNull]Grid displayContainer, [NotNull]IStorageContainer tileStore)
        {
            _displayContainer = displayContainer;
            _tileStore = tileStore;

            _tileCache = new Dictionary<PositionKey, CachedTile>();

            X = 0.0;
            Y = 0.0;

            Invalidate();
        }

        /// <summary>
        /// Use this if the tile data is changed
        /// </summary>
        public void ClearCache() {
            _tileCache.Clear();
            Invalidate();
        }

        /// <summary>
        /// Use this if the window size changes or the viewport is scrolled
        /// </summary>
        public void Invalidate()
        {
            // TODO:
            // 1) figure out what tiles should be showing
            // 2) add any not in the cache
            // 3) remove anything in the cache that should not be visible
            // 4) ensure the tiles are in the correct offset position

            var required = VisibleTiles((int) _displayContainer.ActualWidth, (int) _displayContainer.ActualHeight);
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
                var container = _tileCache[key];
                if (container == null) continue;

                container.Detach();
                _displayContainer.Children?.Remove(container.UiCanvas);
                _tileCache.Remove(key);
            }

            // re-align what's left
            foreach (var kvp in _tileCache)
            {
                var pos = VisualRectangle(kvp.Key);
                kvp.Value?.MoveTo(pos.X, pos.Y);
            }
        }

        /// <summary>
        /// Load a tile from the store, and add it to the display container and dictionary
        /// If no tile is available in the store at this position, we put an empty proxy in place.
        /// </summary>
        private void AddToCache(PositionKey key)
        {
            if (key == null) return;

            var ct = new CachedTile();
            try {
                _tileCache.Add(key, ct);
            }
            catch {
                return;
            }

            var name = key.ToString();
            var res = _tileStore.Exists(name);
            if (res.IsFailure) {
                ct.SetState(TileState.Empty);
                return;
            }

            var version = res.ResultData?.CurrentVersion ?? 1;
            var img = _tileStore.Read(name, "img", version);

            if (img.IsFailure || img.ResultData == null) {
                ct.SetState(TileState.Empty);
                return;
            }



            // TODO: threading
            // THE REST OF THIS SHOULD BE IN ANOTHER THREAD

            var fileData = InterleavedFile.ReadFromStream(img.ResultData);
            
            var Red = new byte[65536];
            var Green = new byte[65536];
            var Blue = new byte[65536];
            if (fileData != null) WaveletCompress.Decompress(fileData, Red, Green, Blue, 1);

            var packed = new byte[65536 * 4];

            for (int i = 0; i < Red.Length; i++)
            {
                // could do a EPX 2x here
                packed[4*i+0] = Blue[i];
                packed[4*i+1] = Green[i];
                packed[4*i+2] = Red[i];

                if (Blue[i] >= 254 && Green[i] >= 254 && Red[i] >= 254){
                    packed[4*i+3] = 0;
                } else {
                    packed[4*i+3] = 255;
                }
            }
            ct.RawImageData = packed;
            ct.SetState(TileState.Ready);

        }

        [NotNull]private static IEnumerable<PositionKey> NoKeys() { yield break; }


        [NotNull]
        public List<PositionKey> VisibleTiles(int width, int height)
        {
            var scale = 1;// 1 << (_drawScale - 1);

            var tlLoc = ScreenToCanvas(0,0);
            var brLoc = ScreenToCanvas(width * scale, height * scale);

            var result = new List<PositionKey>();
            var tlPos = tlLoc.TilePosition ?? throw new Exception("TL tile position lookup failed");
            var brPos = brLoc.TilePosition ?? throw new Exception("BR tile position lookup failed");
            for (int y = tlPos.Y; y <= brPos.Y + 2; y++)
            {
                for (int x = tlPos.X; x <= brPos.X + 3; x++)
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
        public void ImportBytes(RawImageInterleaved_UInt8 img, int xOffset, int yOffset)
        {
            //TODO:IMPLEMENT_ME();
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

        // TEMP! TODO: delete me!
        public void SetPixel(int color, int x, int y)
        {
            var loca = ScreenToCanvas(x,y);
            var pk = loca.TilePosition ?? throw new Exception("Screen index failed");

            //if (!_canvasTiles.ContainsKey(pk)) _canvasTiles.Add(pk, new TileImage(pk));
            //var img = _canvasTiles[pk];

            //img?.DrawOnTile(loca.X, loca.Y, 1, color, InkType.Import, _drawScale);
            //_changedTiles.Add(pk);
        }
    }
}