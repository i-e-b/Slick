using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using SlickCommon.Canvas;
using SlickCommon.ImageFormats;
using SlickCommon.Storage;


namespace SlickWindows.Canvas
{
    /// <summary>
    /// Image storage for an unbounded canvas
    /// </summary>
    public class EndlessCanvas : IEndlessCanvas
    {
        // For tile cache
        [NotNull] private static readonly object _dataQueueLock = new object();
        [NotNull] private readonly HashSet<PositionKey> _changedTiles;
        [NotNull] private readonly Dictionary<PositionKey, TileImage> _canvasTiles;
        [NotNull] private readonly Queue<TileSource> _imageQueue = new Queue<TileSource>();
        private volatile IStorageContainer? _storage;
        private string? _storagePath;

        [NotNull] private readonly ManualResetEventSlim _updateTileCache;
        [NotNull] private readonly ManualResetEventSlim _updateTileData;
        [NotNull] private readonly ManualResetEventSlim _saveTileData;
        private volatile bool _okToDraw;
        private volatile bool _isRunning;

        // For drawing/inking
        [NotNull] private readonly Dictionary<int, InkSettings> _inkSettings;
        private InkSettings _lastPen;
        private readonly Action<Rectangle>? _invalidateAction;
        [NotNull] private readonly List<DPoint> _wetInkCurve;

        // History
        [NotNull] private readonly HashSet<PositionKey> _lastChangedTiles;

        // Selection / Export:
        [NotNull] private readonly HashSet<PositionKey> _selectedTiles;
        private bool _inSelectMode;

        // Rendering properties
        public int Width { get; set; }
        public int Height { get; set; }
        private double _xOffset, _yOffset;

        public double X => _xOffset;
        public double Y => _yOffset;

        /// <summary>
        /// DPI of container (updated through AutoScaleForm triggers)
        /// </summary>
        private int _dpi;
        public int Dpi
        {
            get => _dpi;
            set => _dpi = value < 100 ? 96 : 192;
        }

        private byte _drawScale = 1;

        public float VisualScale
        {
            get {
                // we can do some nice stuff if we gate to round numbers
                var exact = Dpi / 96.0f;
                if (exact < 1.5) return 1.0f;
                return 2.0f;
            }
        }

        public float TileSize => (TileImage.Size >> (_drawScale - 1)) * VisualScale;

        public const int MaxScale = 3;


        /// <summary>
        /// Load and run a canvas from a storage path
        /// </summary>
        /// <param name="deviceDpi">Screen resolution</param>
        /// <param name="pageFilePath">Storage path</param>
        /// <param name="invalidateAction">Optional action to call when drawing area becomes invalid</param>
        /// <param name="width">Initial display width (can be oversize if not known)</param>
        /// <param name="height">Initial display height (can be oversize if not known)</param>
        public EndlessCanvas(int width, int height, int deviceDpi, string pageFilePath, Action<Rectangle> invalidateAction)
        {
            _okToDraw = false;
            if (string.IsNullOrWhiteSpace(pageFilePath)) throw new ArgumentNullException(nameof(pageFilePath));

            _updateTileCache = new ManualResetEventSlim(true);
            _updateTileData = new ManualResetEventSlim(false);
            _saveTileData = new ManualResetEventSlim(false);
            _isRunning = true;

            Width = width;
            Height = height;
            _inkSettings = new Dictionary<int, InkSettings>();
            _canvasTiles = new Dictionary<PositionKey, TileImage>();
            _changedTiles = new HashSet<PositionKey>();
            _lastChangedTiles = new HashSet<PositionKey>();
            _selectedTiles = new HashSet<PositionKey>();
            _wetInkCurve = new List<DPoint>();
            _inSelectMode = false;

            Dpi = deviceDpi; // 96 is 1:1
            _invalidateAction = invalidateAction;
            _xOffset = 0.0;
            _yOffset = 0.0;
            ChangeBasePath(pageFilePath);

            // Load on a different thread so the screen comes up fast
            RunAsyncWorkers();

            _lastPen = new InkSettings
            {
                PenColor = Color.BlueViolet,
                PenSize = (int)PenSizes.Default,
                PenType = InkType.Overwrite
            };
        }


        /// <summary>
        /// Convert a screen position to a canvas pixel
        /// </summary>
        public CanvasPixelPosition ScreenToCanvas(double x, double y) {
            var dss = 1.0 / (1 << (_drawScale - 1));
            var invScale = 1.0 / VisualScale;

            var sx = x * invScale;
            var sy = y * invScale;

            var ox = _xOffset * dss;
            var oy = _yOffset * dss;

            var tx = Math.Floor((sx + ox) / TileSize);
            var ty = Math.Floor((sy + oy) / TileSize);

            var ax = (ox - (tx * TileSize) + sx) * invScale;
            var ay = (oy - (ty * TileSize) + sy) * invScale;
            
            if (ax < 0) ax += TileImage.Size;
            if (ay < 0) ay += TileImage.Size;

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

            var displaySize = TileSize;
            var offsetX = (int)_xOffset >> (_drawScale - 1);
            var offsetY = (int)_yOffset >> (_drawScale - 1);

            x = (tilePos.X * displaySize) - offsetX;
            y = (tilePos.Y * displaySize) - offsetY;
        }

        private void RunAsyncWorkers()
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
                    while (_isRunning)
                    {
                        _updateTileCache.Wait();
                        _updateTileCache.Reset();

                        // load any new tiles
                        var visibleTiles = VisibleTiles(Width, Height);

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

                                var image = new TileImage(tile, Color.DarkGray, _drawScale) { Locked = true };
                                _canvasTiles.Add(tile, image);

                                lock (_dataQueueLock)
                                {
                                    _imageQueue.Enqueue(new TileSource(image, data));
                                    _updateTileData.Set();
                                }
                            }

                            _okToDraw = true;
                            _invalidateAction?.Invoke(new Rectangle(0,0,0,0)); // redraw with what we have

                            // unload any old tiles
                            PositionKey[]? loadedTiles = null;
                            try
                            {
                                loadedTiles = _canvasTiles.Keys.ToArray();
                            }
                            catch
                            {
                                // ignore
                            }
                            if (loadedTiles != null)
                            {
                                foreach (var old in loadedTiles)
                                {
                                    if (visibleTiles.Contains(old)) continue; // still visible
                                    if (_changedTiles.Contains(old)) continue; // awaiting storage
                                    _canvasTiles.Remove(old);
                                }
                            }
                        }
                    }
                })
            { IsBackground = true, Name = "Tileset worker" }.Start();

            // load the actual images.
            // Using the thread pool kills app performance :-(
            new Thread(() =>
            {
                while (_isRunning)
                {
                    _updateTileData.Wait();
                    _updateTileData.Reset();

                    // run through queued images
                    while (true)
                    {
                        TileSource? info;
                        lock (_dataQueueLock)
                        {
                            if (_imageQueue.Count < 1) break;
                            info = _imageQueue.Dequeue();
                        }
                        if (info == null) continue;
                        var fileData = InterleavedFile.ReadFromStream(info.Data);
                        WaveletCompress.Decompress(fileData, info.Image.Red, info.Image.Green, info.Image.Blue, _drawScale);
                        info.Image.Invalidate();
                        info.Image.Locked = false;
                        info.Image.CommitCache(_drawScale, VisualScale);
                        _invalidateAction?.Invoke(VisualRectangle(info.Image.Position)); // redraw with final image
                    }
                }
            })
            { IsBackground = true, Name = "Image loading worker" }.Start();

            // Save any changed images
            new Thread(() =>
            {
                while (_isRunning)
                {
                    _saveTileData.Wait();
                    _saveTileData.Reset();
                    if (_storage == null) continue;
                    PositionKey[] waiting;
                    lock (_canvasTiles)
                    {
                        try
                        {
                            waiting = _changedTiles.ToArray();
                            _changedTiles.Clear();
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    foreach (var key in waiting)
                    {
                        TileImage? tile;
                        try {
                            tile = _canvasTiles[key];
                        } catch {
                            continue;
                        }
                        if (tile == null) continue;
                        if (tile.Locked) continue;

                        if (_storage == null) break;
                        var name = key.ToString();

                        if (tile.ImageIsBlank())
                        {
                            {
                                _storage.Delete(name, "img");
                                _canvasTiles.Remove(key);
                            }
                        }
                        else
                        {
                            using (var ms = new MemoryStream())
                            {
                                WaveletCompress.Compress(tile.Red, tile.Green, tile.Blue, tile.Width, tile.Height).WriteToStream(ms);
                                ms.Seek(0, SeekOrigin.Begin);
                                {
                                    var ok = _storage.Store(name, "img", ms);
                                    if (ok.IsFailure) {
                                        throw new Exception("Storage error: DB might be corrupt.", ok.FailureCause);
                                    }
                                }
                            }
                        }
                    }
                }
            })
            { /* Important: must not be a background thread */
                IsBackground = false,
                Name = "Image saving worker"
            }.Start();
        }

        private Rectangle VisualRectangle(PositionKey tile)
        {
            CanvasToScreen(tile, out var x, out var y);
            return new Rectangle((int)x, (int)y, (int)TileSize, (int)TileSize);
        }

        [NotNull]
        public List<PositionKey> VisibleTiles(int width, int height)
        {
            var scale = 1 << (_drawScale - 1);

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
        /// Display from the current offset into a graphics output
        /// </summary>
        public void RenderToGraphics(Graphics? g, int width, int height, Rectangle clipRect)
        {
            if (g == null) return;

            RenderTiles(g, width, height, clipRect);
            RenderWetInk(g);
        }

        /// <summary>
        /// Draw the selected tiles, from a given offset, into a bitmap image.
        /// This does NOT change the canvas position or size hint.
        /// </summary>
        public void RenderToImage(Bitmap? bmp, int topIdx, int leftIdx, List<PositionKey>? selectedTiles)
        {
            if (bmp == null || selectedTiles == null) return;

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
                    ti.Render(g, dx, dy, false, 1, 1.0f);
                }
            }
        }

        private void RenderWetInk([NotNull]Graphics g)
        {
            if (_wetInkCurve.Count < 2) return;
            var prev = _wetInkCurve[0];
            var scale = 1.0f / VisualScale;
            for (int i = 1; i < _wetInkCurve.Count; i++)
            {
                var next = _wetInkCurve[i];
                var pen = GuessPen(next.IsErase);
                var size = next.Pressure * pen.PenSize * VisualScale * 0.6;

                g.InterpolationMode = InterpolationMode.High;
                g.SmoothingMode = SmoothingMode.HighQuality;
                using (var gPen = new Pen(pen.PenColor, (float)size))
                {
                    g.DrawLine(gPen,
                        (float)prev.X * scale,
                        (float)prev.Y * scale,
                        (float)next.X * scale,
                        (float)next.Y * scale
                    );
                }
                prev = next;
            }
        }

        private void RenderTiles(Graphics g, int width, int height, Rectangle clipRect)
        {
            Width = width;
            Height = height;

            if (clipRect.Width < 1 || clipRect.Height < 1)
            {
                clipRect = new Rectangle(0, 0, width, height);
            }

            PositionKey[]? toDraw = null;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    toDraw = _canvasTiles.Keys.ToArray(); // assuming the background loop is keeping everything minimal
                    break;
                }
                catch { /* race condition can trigger this */ }
            }

            if (toDraw == null) return;

            foreach (var index in toDraw)
            {
                if (index == null) continue;
                TileImage? ti;
                try
                {
                    ti = _canvasTiles[index];
                }
                catch
                {
                    continue;
                } // happens in race conditions

                // if offscreen, don't bother
                var rect = VisualRectangle(index);

                if (rect.Left > clipRect.Right || rect.Right < clipRect.Left
                                               || rect.Top > clipRect.Bottom || rect.Bottom < clipRect.Top) continue;

                // draw tile
                var hilite = _selectedTiles.Contains(index);
                ti?.Render(g, rect.Left, rect.Top, hilite, _drawScale, VisualScale);
            }
        }

        
        /// <summary>
        /// Load a tile image from storage, pausing the current thread until it is ready
        /// </summary>
        private TileImage? LoadTileSync(PositionKey tile)
        {
            if (tile == null || _storage == null) return null;

            var name = tile.ToString();

            var thing = _storage.Exists(name);
            if (thing.IsFailure) return null;
            var version = thing.ResultData?.CurrentVersion ?? 1;

            var data = _storage.Read(name, "img", version);
            if (!data.IsSuccess) return null; // bad node data

            var image = new TileImage(tile);
            var fileData = InterleavedFile.ReadFromStream(data);
            WaveletCompress.Decompress(fileData, image.Red, image.Green, image.Blue, 1);
            return image;
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
            _xOffset += dx * (1 << (_drawScale - 1)) / VisualScale;
            _yOffset += dy * (1 << (_drawScale - 1)) / VisualScale;
            _updateTileCache.Set();
            _updateTileData.Set();
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
        public void StartStroke()
        {
            if (!_okToDraw) return;
            _lastChangedTiles.Clear();
            _wetInkCurve.Clear();
        }

        public void EndStroke()
        {
            // Render the wet ink curve into the canvas image

            if (!_okToDraw) return;
            _okToDraw = false;

            for (int p = 1; p < _wetInkCurve.Count; p++)
            {
                var start = _wetInkCurve[p-1];
                var end = _wetInkCurve[p];

                var pt = start;
                var dx = end.X - start.X;
                var dy = end.Y - start.Y;
                var dp = end.Pressure - start.Pressure;

                var penSet = _inkSettings.ContainsKey(start.StylusId) ? _inkSettings[start.StylusId] : GuessPen(start.IsErase);

                var dd = Math.Floor(Math.Max(Math.Abs(dx), Math.Abs(dy)));

                if (dd > 0)
                {
                    dx /= dd;
                    dy /= dd;
                    dp /= dd;
                }
                double radius;

                // TODO: render the wet ink to a temp bitmap and import it
                // Instead of drawing dots, get every covered tile, and draw the whole line to each
                for (double i = 0; i <= dd; i += radius)
                {
                    // Draw
                    var tiles = InkPoint(penSet, pt, out radius);

                    // add changed tiles to update lists
                    foreach (var tile in tiles)
                    {
                        _changedTiles.Add(tile);
                        _lastChangedTiles.Add(tile);
                    }

                    // move dot forward
                    radius *= VisualScale / 4;
                    pt.X += dx * radius;
                    pt.Y += dy * radius;
                    pt.Pressure += dp * radius;
                }
            }
            _okToDraw = true;

            _wetInkCurve.Clear();
            SaveChanges();
            Invalidate();
        }

        /// <summary>
        /// Draw curve in the current inking colour
        /// </summary>
        public void Ink(DPoint start, DPoint end)
        {
            if (_inSelectMode)
            {
                // use pens to toggle selection
                var startPoint = ScreenToCanvas((int)start.X, (int)start.Y);
                if (startPoint.TilePosition != null) _selectedTiles.Add(startPoint.TilePosition);
                Invalidate();
                return;
            }

            if (_drawScale != 1)
            {
                // If we're scaled out, use pens to scroll
                Scroll(start.X - end.X, start.Y - end.Y);
                return;
            }


            // add points to wet ink
            if (_wetInkCurve.Count < 1) _wetInkCurve.Add(start);
            _wetInkCurve.Add(end);

            // invalidate rectangle covering the new ink
            
            var scale = 1.0f / VisualScale;
            var left = (int)(Math.Min(start.X, end.X) * scale);
            var top = (int)(Math.Min(start.Y, end.Y) * scale);
            var width = (int)(Math.Max(start.X, end.X) * scale) - left;
            var height = (int)(Math.Max(start.Y, end.Y) * scale) - top;
            _invalidateAction?.Invoke(new Rectangle(left - 10, top - 10, width + 20, height + 20));
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
            // trigger the async worker
            _saveTileData.Set();
        }

        [NotNull]
        private List<PositionKey> InkPoint(InkSettings ink, DPoint pt, out double radius)
        {
            {
                radius = pt.Pressure * ink.PenSize;
                var changed = new List<PositionKey>(4);

                // primary tile
                var local = ScreenToCanvas(pt.X, pt.Y);
                var tx = local.TilePosition?.X ?? 0;
                var ty = local.TilePosition?.Y ?? 0;
                var possibleTiles = new[]{
                    new PositionKey(tx - 1, ty - 1), new PositionKey(tx    , ty - 1), new PositionKey(tx + 1, ty - 1),
                    new PositionKey(tx - 1, ty    ), new PositionKey(tx    , ty    ), new PositionKey(tx + 1, ty    ),
                    new PositionKey(tx - 1, ty + 1), new PositionKey(tx    , ty + 1), new PositionKey(tx + 1, ty + 1)
                };

                foreach (var pk in possibleTiles)
                {
                    var needsAdd = !_canvasTiles.ContainsKey(pk);
                    var img = needsAdd ? new TileImage(pk) : _canvasTiles[pk];
                    if (needsAdd) _canvasTiles.Add(pk, img);

                    var ax = local.X + (tx - pk.X) * TileImage.Size;
                    var ay = local.Y + (ty - pk.Y) * TileImage.Size;

                    if (img?.DrawOnTile(ax, ay, radius, ink.PenColor, ink.PenType, _drawScale) == true)
                    {
                        changed.Add(pk);
                    }
                }
                return changed;
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


        public void ChangeBasePath(string? newPath)
        {
            if (string.IsNullOrWhiteSpace(newPath!)) return;
            _okToDraw = false;
            lock (_dataQueueLock) lock (_canvasTiles)
            {
                // wipe state
                _imageQueue.Clear();
                _changedTiles.Clear();
                _canvasTiles.Clear();
                _selectedTiles.Clear();

                // change storage
                _storage?.Dispose();
                Directory.CreateDirectory(Path.GetDirectoryName(newPath!) ?? "");

                _storage = LoadStorageFile(new SystemIoFile(newPath!));
                _storagePath = newPath;

                // re-centre
                _xOffset = 0;
                _yOffset = 0;
            }
            _updateTileCache.Set();
            _invalidateAction?.Invoke(new Rectangle(0, 0, 0, 0));
        }
        
        [NotNull]private static IStorageContainer LoadStorageFile(IStreamProvider wrapper)
        {
            if (wrapper == null) throw new Exception("Could not read file -- failed to access file system");
            try
            {
                return new StreamDbStorageContainer(wrapper);
            }
            catch (Exception streamReason)
            {
                try
                {
                    return new LiteDbStorageContainer(wrapper);
                }
                catch (Exception liteReason)
                {
                    throw new Exception("Could not read file -- it's either damaged or not a Slick file.\r\n" + liteReason + "\r\n" + streamReason);
                }
            }
        }


        public string FileName()
        {
            if (string.IsNullOrWhiteSpace(_storagePath!)) return "untitled";
            return Path.GetFileNameWithoutExtension(_storagePath!) ?? "untitled";
        }

        /// <summary>
        /// Clear all tiles and start reloading. This is mostly used for changing display scale
        /// </summary>
        public void ResetTileCache()
        {
            _okToDraw = false;
            lock (_dataQueueLock)
            lock (_canvasTiles)
            {
                // clear the caches and start processing
                _imageQueue.Clear();
                _changedTiles.Clear();
                _canvasTiles.Clear();
            }
            _updateTileCache.Set();
            _invalidateAction?.Invoke(new Rectangle(0,0,0,0));
        }


        private class TileSource
        {
            [NotNull] public readonly TileImage Image;
            [NotNull] public readonly Stream Data;

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

                if (prevVersion < 1)
                {
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

            // Centre on clicked area
            _xOffset += (wX - (Width / 2)) << (_drawScale - 1);
            _yOffset += (wY - (Height / 2)) << (_drawScale - 1);

            // zoom in on centre
            _xOffset += (Width << (_drawScale - 1)) / 2.0;
            _yOffset += (Height << (_drawScale - 1)) / 2.0;
            _xOffset -= Width / 2.0;
            _yOffset -= Height / 2.0;
            _drawScale = 1;

            ResetTileCache();
        }

        public InfoPin[] AllPins()
        {
            if (_storage == null) return new InfoPin[0];

            {
                var result = _storage.ReadAllPins();
                if (result.IsFailure || result.ResultData == null) return new InfoPin[0];

                return result.ResultData;
            }
        }

        public void WritePinAtCurrentOffset(string text)
        {
            {
                // tile location for middle of screen
                var x = (Width / 2);
                var y = (Height / 2);

                var local = ScreenToCanvas(x,y);

                if (_storage == null || local.TilePosition == null) return;
                _storage.SetPin(local.TilePosition.ToString(), text);
            }
        }

        public void CentreOnPin(InfoPin? pin)
        {
            if (pin?.Id == null) return;
            var pos = PositionKey.Parse(pin.Id);
            if (pos == null) return;

            _xOffset = pos.X * TileSize;
            _yOffset = pos.Y * TileSize;

            _updateTileCache.Set();
            _invalidateAction?.Invoke(new Rectangle(0,0,0,0));
        }

        public void DeletePin(InfoPin? pin)
        {
            if (_storage == null || pin?.Id == null) return;

            {
                _storage.RemovePin(pin.Id);
            }
        }

        public bool ToggleSelectMode()
        {
            _inSelectMode = !_inSelectMode;
            if (_inSelectMode == false)
            {
                _selectedTiles.Clear();
                _invalidateAction?.Invoke(new Rectangle(0,0,0,0));
            }
            return _inSelectMode;
        }

        public List<PositionKey> SelectedTiles()
        {
            return _selectedTiles.ToList();
        }

        /// <summary>
        /// Try to trigger a refresh of the canvas.
        /// </summary>
        public void Invalidate()
        {
            _invalidateAction?.Invoke(new Rectangle(0,0,0,0));
        }

        /// <summary>
        /// Write an external image into this canvas.
        /// `px` & `py` are in screen points
        /// </summary>
        public void CrossLoadImage(RawImagePlanar img, int px, int py, Size size)
        {
            if (img.Red == null || img.Green == null || img.Blue == null) return;

            var width = (int)(size.Width * VisualScale);
            var height = (int)(size.Height * VisualScale);
            var offsetX = (int)(px * VisualScale);
            var offsetY = (int)(py * VisualScale);

            var xScale = (float)img.Width / width;
            var yScale = (float)img.Height / height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    var sy = (int)(y * yScale) * img.Width;
                    var sx = (int)(x * xScale);

                    var idx = (sy + sx) % img.Red.Length;

                    var color = Color.FromArgb(255, img.Red[idx], img.Green[idx], img.Blue[idx]);
                    if (color.R == 255 && color.G == 255 && color.B == 255) continue; // skip white pixels
                    SetPixel(color, x + offsetX, y + offsetY);
                }
            }
            SaveChanges();
            Invalidate();
        }
        

        /// <summary>
        /// Set a single pixel on the canvas.
        /// This is very slow, you should use the `InkPoint` method unless you're doing something special.
        /// This is used for importing.
        /// </summary>
        public void SetPixel(Color color, int x, int y)
        {
            var local = ScreenToCanvas(x,y);
            var pk = local.TilePosition ?? throw new Exception("Screen index failed");

            if (!_canvasTiles.ContainsKey(pk)) _canvasTiles.Add(pk, new TileImage(pk));
            var img = _canvasTiles[pk];

            img?.DrawOnTile(local.X, local.Y, 1, color, InkType.Import, _drawScale);
            _changedTiles.Add(pk);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _isRunning = false;
            _saveTileData.Set();
        }

    }
}