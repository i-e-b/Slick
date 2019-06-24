using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using SlickWindows.ImageFormats;
using SlickWindows.Storage;

namespace SlickWindows.Canvas
{
    /// <summary>
    /// Image storage for an unbounded canvas
    /// </summary>
    public class EndlessCanvas : IEndlessCanvas
    {
        // For tile cache
        [NotNull] private static readonly object _storageLock = new object();
        [NotNull] private readonly HashSet<PositionKey> _changedTiles;
        [NotNull] private readonly Dictionary<PositionKey, TileImage> _canvasTiles;
        [NotNull] private readonly Queue<TileSource> _imageQueue = new Queue<TileSource>();
        [NotNull] private readonly ManualResetEventSlim _updateTileCache;
        public volatile bool IsRunning;
        [CanBeNull] private volatile IStorageContainer _storage;

        // For drawing/inking
        [NotNull] private readonly Dictionary<int, InkSettings> _inkSettings;
        private InkSettings _lastPen;
        private readonly Action _invalidateAction;

        // History
        [NotNull] private readonly HashSet<PositionKey> _lastChangedTiles;

        // Selection / Export:
        [NotNull] private readonly HashSet<PositionKey> _selectedTiles;
        private bool _inSelectMode;

        // Rendering properties
        public int Width { get; set; }
        public int Height { get; set; }
        private double _xOffset, _yOffset;

        public double X { get { return _xOffset; } }
        public double Y { get { return _yOffset; } }

        public int DpiY;
        public int DpiX;
        private byte _drawScale = 1;

        public const int MaxScale = 3;


        /// <summary>
        /// Load and run a canvas from a storage path
        /// </summary>
        /// <param name="deviceDpi">Screen resolution</param>
        /// <param name="pageFilePath">Storage path</param>
        /// <param name="invalidateAction">Optional action to call when drawing area becomes invalid</param>
        /// <param name="width">Initial display width (can be oversize if not known)</param>
        /// <param name="height">Initial display height (can be oversize if not known)</param>
        public EndlessCanvas(int width, int height, int deviceDpi, string pageFilePath, Action invalidateAction)
        {
            if (string.IsNullOrWhiteSpace(pageFilePath)) throw new ArgumentNullException(nameof(pageFilePath));
            Directory.CreateDirectory(Path.GetDirectoryName(pageFilePath) ?? "");
            _storage = new LiteDbStorageContainer(pageFilePath);

            _updateTileCache = new ManualResetEventSlim(true);
            IsRunning = true;

            Width = width;
            Height = height;
            _inkSettings = new Dictionary<int, InkSettings>();
            _canvasTiles = new Dictionary<PositionKey, TileImage>();
            _changedTiles = new HashSet<PositionKey>();
            _lastChangedTiles = new HashSet<PositionKey>();
            _selectedTiles = new HashSet<PositionKey>();
            _inSelectMode=false;

            DpiX = deviceDpi;
            _invalidateAction = invalidateAction;
            DpiY = deviceDpi;
            _xOffset = 0.0;
            _yOffset = 0.0;

            // Load on a different thread so the screen comes up fast
            LoadImagesAsync();

            _lastPen = new InkSettings
            {
                PenColor = Color.BlueViolet,
                PenSize = 5.0,
                PenType = InkType.Overwrite
            };
        }

        private void LoadImagesAsync()
        {
            // Try to load images for any visible tiles,
            // and remove any loaded tiles that are far
            // offscreen.
            // Trigger a re-draw as images come in.
            //
            // Another thread reads the image data and completes the tiles
            // (this allows us to put a fast proxy in place)
            new Thread(() =>
                {
                    while (IsRunning)
                    {
                        _updateTileCache.Wait();
                        _updateTileCache.Reset();

                        // load any new tiles
                        var visibleTiles = VisibleTiles(Width, Height);

                        lock (_storageLock) // prevent conflict with changing page
                        {
                            foreach (var tile in visibleTiles)
                            {
                                if (_canvasTiles.ContainsKey(tile)) continue; // already in memory
                                if (_storage == null) break; // volatile

                                var name = tile.ToString();

                                var thing = _storage.Exists(name);
                                if (thing.IsFailure) continue;
                                var version = thing.ResultData?.CurrentVersion ?? 1;

                                var data = _storage.Read(name, "img", version);
                                if (!data.IsSuccess) continue; // bad node data

                                var image = new TileImage(Color.DarkGray, _drawScale);
                                image.Locked = true;
                                _canvasTiles.Add(tile, image);

                                _imageQueue.Enqueue(new TileSource(image, data));
                            }

                            // unload any old tiles
                            PositionKey[] loadedTiles;
                            lock (_canvasTiles)
                            {
                                loadedTiles = _canvasTiles.Keys.ToArray();
                            }
                            foreach (var old in loadedTiles)
                            {
                                if (visibleTiles.Contains(old)) continue; // still visible
                                if (_changedTiles.Contains(old)) continue; // awaiting storage
                                _canvasTiles.Remove(old);
                            }

                            _invalidateAction?.Invoke(); // redraw with what we have
                        }
                    }
                })
            { IsBackground = true, Name = "Tileset worker" }.Start();

            // load the actual images.
            // Using the thread pool kills app performance :-(
            new Thread(() =>
            {
                while (IsRunning)
                {
                    while (_imageQueue.Count > 0)
                    {
                        var info = _imageQueue.Dequeue();
                        if (info == null) continue;
                        var fileData = InterleavedFile.ReadFromStream(info.Data);
                        if (fileData != null) WaveletCompress.Decompress(fileData, info.Image, _drawScale);
                        info.Image.Locked = false;
                        _invalidateAction?.Invoke(); // redraw with final image
                    }
                    Thread.Sleep(250);
                }
            })
            { IsBackground = true, Name = "Image loading worker" }.Start();
        }


        [NotNull]
        private List<PositionKey> VisibleTiles(int width, int height)
        {
            Width = width;
            Height = height;
            
            // change render locations based on scale
            var displaySize = TileImage.Size >> (_drawScale - 1);
            var offset_x = (int)_xOffset >> (_drawScale - 1);
            var offset_y = (int)_yOffset >> (_drawScale - 1);

            // work out the indexes we need, find in dictionary, draw
            int ox = offset_x / displaySize;
            int oy = offset_y / displaySize;
            int mx = (int)Math.Round((double)width / displaySize);
            int my = (int)Math.Round((double)height / displaySize);

            var result = new List<PositionKey>();
            for (int y = -1; y <= my; y++)
            {
                var yIdx = oy + y;

                for (int x = -1; x <= mx; x++)
                {
                    var xIdx = ox + x;
                    result.Add(new PositionKey(xIdx, yIdx));
                }
            }

            return result;
        }

        /// <summary>
        /// Display from the current offset into a graphics output
        /// </summary>
        public void RenderToGraphics(Graphics g, int width, int height)
        {
            Width = width;
            Height = height;

            PositionKey[] toDraw;
            try
            {
                toDraw = _canvasTiles.Keys.ToArray(); // assuming the background loop is keeping everything minimal?
            }
            catch
            {
                return; // race condition can trigger this
            }

            foreach (var index in toDraw)
            {
                if (index == null) continue;
                if (!_canvasTiles.ContainsKey(index)) continue;
                var ti = _canvasTiles[index];

                // change render locations based on scale
                var displaySize = TileImage.Size >> (_drawScale - 1);
                var offset_x = (int)_xOffset >> (_drawScale - 1);
                var offset_y = (int)_yOffset >> (_drawScale - 1);

                var dx = (index.X * displaySize) - offset_x;
                var dy = (index.Y * displaySize) - offset_y;

                // if offscreen, don't bother
                if (dx > width || dx < -displaySize || dy > height || dy < -displaySize) continue;

                // draw tile
                var hilite = _selectedTiles.Contains(index);
                ti?.Render(g, dx, dy, hilite, _drawScale);
            }
        }

        private TileImage LoadTileSync(PositionKey tile) {
            if (tile == null || _storage == null) return null;

            var name = tile.ToString();

            var thing = _storage.Exists(name);
            if (thing.IsFailure) return null;
            var version = thing.ResultData?.CurrentVersion ?? 1;

            var data = _storage.Read(name, "img", version);
            if (!data.IsSuccess) return null; // bad node data

            var image = new TileImage();
            var fileData = InterleavedFile.ReadFromStream(data);
            if (fileData != null) WaveletCompress.Decompress(fileData, image, 1);
            return image;
        }
        

        private volatile bool isDrawing = false;
        /// <summary>
        /// Draw the selected tiles, from a given offset, into a bitmap image.
        /// </summary>
        public void RenderToImage(Bitmap bmp, int topIdx, int leftIdx, List<PositionKey> selectedTiles)
        {
            if (isDrawing) {
                Console.WriteLine("bump");
                return;

            }
            if (bmp == null || selectedTiles == null) return;

            isDrawing = true;
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                foreach (var tile in selectedTiles)
                {
                    if (tile == null) continue;
                    var ti = LoadTileSync(tile);
                    if (ti == null) continue;

                    var dx = (tile.X - leftIdx) * TileImage.Size;
                    var dy = (tile.Y - topIdx) * TileImage.Size;

                    // if offscreen, don't bother
                    if (dx > bmp.Width || dx < -TileImage.Size || dy > bmp.Height || dy < -TileImage.Size) continue;

                    // draw tile
                    ti.Render(g, dx, dy, false, 1);
                }
            }
            isDrawing = false;
        }

        public void SetSizeHint(int width, int height)
        {
            Width = width;
            Height = height;
            _updateTileCache.Set();
        }

        /// <summary>
        /// move the offset
        /// </summary>
        public void Scroll(double dx, double dy)
        {
            _xOffset += dx * (1 << (_drawScale - 1));
            _yOffset += dy * (1 << (_drawScale - 1));
            _updateTileCache.Set();
        }

        /// <summary>
        /// Set an absolute scroll position
        /// </summary>
        public void ScrollTo(double x, double y)
        {
            _xOffset = x;
            _yOffset = y;
            _updateTileCache.Set();
        }

        /// <summary>
        /// Signal that a drawing curve is starting.
        /// This is used to delimit undo/redo actions
        /// </summary>
        public void StartStroke() {
            _lastChangedTiles.Clear();
        }

        private PositionKey ScreenToTile(double x, double y)
        {
            var displaySize = TileImage.Size >> (_drawScale - 1);

            var xIdx = Math.Floor((x / displaySize) + (_xOffset / TileImage.Size));
            var yIdx = Math.Floor((y / displaySize) + (_yOffset / TileImage.Size));

            return new PositionKey((int)xIdx, (int)yIdx);
        }

        /// <summary>
        /// Draw curve in the current inking colour
        /// </summary>
        public void Ink(int stylusId, bool isErase, DPoint start, DPoint end)
        {
            if (_inSelectMode) {
                // use pens to toggle selection
                _selectedTiles.Add(ScreenToTile(start.X, start.Y));
                return;
            }
            
            if (_drawScale != 1) {
                // If we're scaled out, use pens to scroll
                Scroll(start.X - end.X, start.Y - end.Y);
                return;
            }

            var pt = start;
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var dp = end.Pressure - start.Pressure;

            var penSet = _inkSettings.ContainsKey(stylusId) ? _inkSettings[stylusId] : GuessPen(isErase);

            var dd = Math.Floor(Math.Max(Math.Abs(dx), Math.Abs(dy)));

            var tile = InkPoint(penSet, pt);
            _changedTiles.Add(tile);
            _lastChangedTiles.Add(tile);
            if (dd < 1) { return; }

            dx /= dd;
            dy /= dd;
            dp /= dd;
            for (int i = 0; i < dd; i++)
            {
                var ctile = InkPoint(penSet, pt);
                _changedTiles.Add(ctile);
                _lastChangedTiles.Add(ctile);
                pt.X += dx;
                pt.Y += dy;
                pt.Pressure += dp;
            }
        }

        private InkSettings GuessPen(bool isErase)
        {
            if (isErase) return new InkSettings { PenColor = Color.White, PenSize = 15, PenType = InkType.Overwrite };
            return _lastPen;
        }

        /// <summary>
        /// Save any tiles changed since last save
        /// </summary>
        public void SaveChanges()
        {
            if (_storage == null) return;
            lock (_storageLock)
            {
                lock (_canvasTiles)
                {
                    foreach (var key in _changedTiles)
                    {
                        if (!_canvasTiles.ContainsKey(key)) continue;
                        var tile = _canvasTiles[key];
                        if (tile == null) continue;
                        if (tile.Locked) continue;

                        if (_storage == null) return;
                        var name = key.ToString();

                        if (tile.ImageIsBlank())
                        {
                            _storage.Delete(name, "img");
                            _canvasTiles.Remove(key);
                        }
                        else
                        {
                            var ms = new MemoryStream();
                            WaveletCompress.Compress(tile).WriteToStream(ms);
                            ms.Seek(0, SeekOrigin.Begin);
                            _storage.Store(name, "img", ms);
                        }
                    }
                }
                _changedTiles.Clear();
            }
        }

        private PositionKey InkPoint(InkSettings ink, DPoint pt)
        {
            lock (_storageLock)
            {
                var xIdx = Math.Floor((pt.X + _xOffset) / TileImage.Size);
                var yIdx = Math.Floor((pt.Y + _yOffset) / TileImage.Size);
                var pk = new PositionKey((int)xIdx, (int)yIdx);

                if (!_canvasTiles.ContainsKey(pk)) _canvasTiles.Add(pk, new TileImage());
                var img = _canvasTiles[pk];

                var ax = (pt.X + _xOffset) % TileImage.Size;
                var ay = (pt.Y + _yOffset) % TileImage.Size;

                if (ax < 0) ax += TileImage.Size;
                if (ay < 0) ay += TileImage.Size;

                if (ink.PenType == InkType.Overwrite)
                {
                    img?.Overwrite(ax, ay, pt.Pressure * ink.PenSize, ink.PenColor);
                }
                else
                {
                    img?.Highlight(ax, ay, pt.Pressure * ink.PenSize, ink.PenColor);
                }
                return pk;
            }
        }

        /// <summary>
        /// Set current inking color, size, etc
        /// </summary>
        public void SetPen(int stylusId, Color color, double size, InkType type)
        {
            _lastPen = new InkSettings
            {
                PenColor = color,
                PenSize = size,
                PenType = type
            };
            if (!_inkSettings.ContainsKey(stylusId)) _inkSettings.Add(stylusId, _lastPen);
            else _inkSettings[stylusId] = _lastPen;
        }

        public void ChangeBasePath(string newPath)
        {
            lock (_storageLock)
            {
                lock (_canvasTiles)
                {
                    // wipe state
                    _changedTiles.Clear();
                    _canvasTiles.Clear();
                    _selectedTiles.Clear();

                    // change storage
                    Directory.CreateDirectory(Path.GetDirectoryName(newPath) ?? "");
                    _storage = new LiteDbStorageContainer(newPath);

                    // re-centre
                    _xOffset = 0;
                    _yOffset = 0;
                }
            }
            _updateTileCache.Set();
        }

        /// <summary>
        /// Rotate through scaling options
        /// </summary>
        public int SwitchScale()
        {
            // Keep the centre of the view in the same visual place, switching scale
            if (_drawScale >= MaxScale)
            { // reset to 1:1
                _xOffset += (Width << (_drawScale - 1)) / 2.0;
                _yOffset += (Height << (_drawScale - 1)) / 2.0;
                _xOffset -= Width / 2.0;
                _yOffset -= Height / 2.0;
                _drawScale = 1;
            }
            else
            {   // zoom out a step
                _xOffset -= (Width << (_drawScale - 1)) / 2.0;
                _yOffset -= (Height << (_drawScale - 1)) / 2.0;
                _drawScale++;
            }
            ResetTileCache();
            return _drawScale;
        }

        /// <summary>
        /// Clear all tiles and start reloading. This is mostly used for changing display scale
        /// </summary>
        public void ResetTileCache() {

            lock (_storageLock)
            {
                lock (_canvasTiles)
                {

                    // clear the caches and start processing
                    _changedTiles.Clear();
                    _canvasTiles.Clear();
                }
            }

            _updateTileCache.Set();
            _invalidateAction?.Invoke();
        }

        /// <summary>
        /// Set a single pixel on the canvas.
        /// This is very slow, you should use the `InkPoint` method unless you're doing something special.
        /// This is used for importing.
        /// </summary>
        public void SetPixel(Color color, int x, int y)
        {
            var xIdx = Math.Floor((x + _xOffset) / TileImage.Size);
            var yIdx = Math.Floor((y + _yOffset) / TileImage.Size);

            var pk = new PositionKey((int)xIdx, (int)yIdx);

            if (!_canvasTiles.ContainsKey(pk)) _canvasTiles.Add(pk, new TileImage());
            var img = _canvasTiles[pk];

            var ax = (x + _xOffset) % TileImage.Size;
            var ay = (y + _yOffset) % TileImage.Size;

            if (ax < 0) ax += TileImage.Size;
            if (ay < 0) ay += TileImage.Size;

            img?.Overwrite(ax, ay, 1, color);
            _changedTiles.Add(pk);
        }
        

        private class TileSource {
            [NotNull]public readonly TileImage Image;
            [NotNull]public readonly Stream Data;

            public TileSource([NotNull] TileImage image, [NotNull] Stream data)
            {
                Image = image;
                Data = data;
            }
        }

        /// <summary>
        /// Toggle the version of the last changed tiles.
        /// </summary>
        public void Undo()
        {
            if (_storage == null) return;
            // multi-undo: Could store prev. changed tiles set in the metadata store.

            foreach (var tile in _lastChangedTiles)
            {
                // if tiles exist for version-1:
                // - roll the version back in meta
                // - reload tile (purge from cache and flip the reload trigger)
                var path = tile.ToString();
                var node = _storage.Exists(path);
                if (node.IsFailure) continue;
                if (node.ResultData == null) continue;

                var prevVersion = node.ResultData.CurrentVersion - 1;

                if (prevVersion < 1) {
                    // undoing the first stroke. Mark it deleted.
                    var deadNode = new StorageNode { CurrentVersion = node.ResultData.CurrentVersion, Id = node.ResultData.Id, IsDeleted = true };
                    _storage.UpdateNode(path, deadNode);
                    _canvasTiles.Remove(tile);
                    _updateTileCache.Set();
                }
                else
                {
                    var ok = _storage.Read(path, "img", prevVersion);
                    if (ok.IsFailure) continue;

                    var newNode = new StorageNode { CurrentVersion = prevVersion, Id = node.ResultData.Id, IsDeleted = false };
                    _storage.UpdateNode(path, newNode);
                    _canvasTiles.Remove(tile);
                    _updateTileCache.Set();
                }
            }

            // Reset the last update set
            // For multi-undo, we should roll back to a previous undo set.
            _lastChangedTiles.Clear();
        }

        /// <summary>
        /// Centre on the given point, and set scale as 1:1
        /// </summary>
        public void CentreAndZoom(int wX, int wY)
        {
            if (_drawScale <= 1) return; // already zoomed in
            // wX,Y are in screen scale, window co-ordinates

            // convert window coords to offsets
            wX -= Width / 2;
            wY -= Height / 2;
            wX <<= (_drawScale - 1);
            wY <<= (_drawScale - 1);

            // centre on the clicked point
            _xOffset += wX;
            _yOffset += wY;
            
            // restore scale, position and start re-draw.
            _xOffset += (Width << (_drawScale - 1)) / 2.0;
            _yOffset += (Height << (_drawScale - 1)) / 2.0;
            _xOffset -= Width / 2.0;
            _yOffset -= Height / 2.0;
            _drawScale = 1;

            ResetTileCache();
        }

        [NotNull]public InfoPin[] AllPins()
        {
            if (_storage==null) return new InfoPin[0];

            lock (_storageLock) {
                var result = _storage.ReadAllPins();
                if (result.IsFailure || result.ResultData == null) return new InfoPin[0];

                return result.ResultData;
            }
        }

        public void WritePinAtCurrentOffset(string text)
        {
            if (_storage==null) return;

            lock (_storageLock) {
                // tile location for middle of screen
                
                // offset is scale independent position of top-left window corner
                var x = _xOffset;
                var y = _yOffset;

                // tiles from window corner to window centre
                x += (Width / 2) << (_drawScale - 1);
                y += (Height / 2) << (_drawScale - 1);

                // tile of centre of screen
                x /= TileImage.Size;
                y /= TileImage.Size;

                var pos = new PositionKey((int)x, (int)y);

                _storage.SetPin(pos.ToString(), text);
            }
        }

        public void CentreOnPin(InfoPin pin)
        {
            if (pin == null) return;
            var pos = PositionKey.Parse(pin.Id);
            if (pos == null) return;

            
            // tiles from window corner to window centre
            var wX = Width / 2;
            var wY = Height / 2;
            wX <<= (_drawScale - 1);
            wY <<= (_drawScale - 1);

            // we have a tile index. Set the offset based on this
            _xOffset = pos.X * TileImage.Size - wX;
            _yOffset = pos.Y * TileImage.Size - wY;

            _updateTileCache.Set();
            _invalidateAction?.Invoke();
        }

        public void DeletePin(InfoPin pin)
        {
            if (_storage == null || pin == null) return;

            lock (_storageLock) {
                _storage.RemovePin(pin.Id);
            }
        }

        public bool ToggleSelectMode()
        {
            _inSelectMode = !_inSelectMode;
            if (_inSelectMode == false) {
                _selectedTiles.Clear();
                _invalidateAction?.Invoke();
            }
            return _inSelectMode;
        }

        [NotNull]public List<PositionKey> SelectedTiles()
        {
            return _selectedTiles.ToList();
        }
    }
}