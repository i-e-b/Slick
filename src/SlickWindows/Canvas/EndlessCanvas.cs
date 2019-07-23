﻿using System;
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
        [NotNull] private static readonly object _dataQueueLock = new object();
        [NotNull] private readonly HashSet<PositionKey> _changedTiles;
        [NotNull] private readonly Dictionary<PositionKey, TileImage> _canvasTiles;
        [NotNull] private readonly Queue<TileSource> _imageQueue = new Queue<TileSource>();
        [CanBeNull] private volatile IStorageContainer _storage;
        private string _storagePath;

        [NotNull] private readonly ManualResetEventSlim _updateTileCache;
        [NotNull] private readonly ManualResetEventSlim _updateTileData;
        [NotNull] private readonly ManualResetEventSlim _saveTileData;
        private volatile bool _okToDraw;
        private volatile bool _isRunning;

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

        /// <summary>
        /// DPI of container (updated through AutoScaleForm triggers)
        /// </summary>
        public int Dpi;
        private byte _drawScale = 1;

        public float VisualScale => Dpi / 96.0f;
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
        public EndlessCanvas(int width, int height, int deviceDpi, string pageFilePath, Action invalidateAction)
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
            _inSelectMode = false;

            Dpi = deviceDpi;
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

        // TODO:
        // These two functions should be the core of all interactions with the canvas.
        // The existing functionality has got way too messy.

        /// <summary>
        /// Convert a screen position to a canvas pixel
        /// </summary>
        [NotNull]public CanvasPixelPosition ScreenToCanvas(int x, int y) {
            // Compensate for canvas offset; find tile and sub-offset.

            var dss = (double)(1 << (_drawScale - 1));

            var sx = x / VisualScale;
            var sy = y / VisualScale;

            var ox = _xOffset / dss;// / VisualScale;
            var oy = _yOffset / dss;// / VisualScale;

            var tx = Math.Floor((sx + ox) / TileSize);
            var ty = Math.Floor((sy + oy) / TileSize);

            var ax = (ox - (tx * TileSize) + sx) / VisualScale;
            var ay = (oy - (ty * TileSize) + sy) / VisualScale;
            
            if (ax < 0) ax += TileImage.Size;
            if (ay < 0) ay += TileImage.Size;

            return new CanvasPixelPosition{
                TilePosition = new PositionKey(tx,ty),
                X = (int)ax,
                Y = (int)ay
            };
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

                                var image = new TileImage(Color.DarkGray, _drawScale) { Locked = true };
                                _canvasTiles.Add(tile, image);

                                lock (_dataQueueLock)
                                {
                                    _imageQueue.Enqueue(new TileSource(image, data));
                                    _updateTileData.Set();
                                }
                            }

                            _okToDraw = true;

                            // unload any old tiles
                            PositionKey[] loadedTiles = null;
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

                            _invalidateAction?.Invoke(); // redraw with what we have
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
                        TileSource info;
                        lock (_dataQueueLock)
                        {
                            if (_imageQueue.Count < 1) break;
                            info = _imageQueue.Dequeue();
                        }
                        if (info == null) continue;
                        var fileData = InterleavedFile.ReadFromStream(info.Data);
                        if (fileData != null) WaveletCompress.Decompress(fileData, info.Image, _drawScale);
                        info.Image.Invalidate();
                        info.Image.Locked = false;
                        info.Image.CommitCache(_drawScale);
                        _invalidateAction?.Invoke(); // redraw with final image
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
                        TileImage tile;
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
                            lock (_storageLock)
                            {
                                _storage.Delete(name, "img");
                                _canvasTiles.Remove(key);
                            }
                        }
                        else
                        {
                            using (var ms = new MemoryStream())
                            {
                                WaveletCompress.Compress(tile).WriteToStream(ms);
                                ms.Seek(0, SeekOrigin.Begin);
                                lock (_storageLock)
                                {
                                    _storage.Store(name, "img", ms);
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

        [NotNull]
        private List<PositionKey> VisibleTiles(int width, int height)
        {
            Width = width;
            Height = height;

            // change render locations based on scale
            var displaySize = TileSize;
            var offset_x = (int)_xOffset >> (_drawScale - 1);
            var offset_y = (int)_yOffset >> (_drawScale - 1);

            // work out the indexes we need, find in dictionary, draw
            int ox = (int)(offset_x / displaySize);
            int oy = (int)(offset_y / displaySize);
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
            if (g == null) return;
            Width = width;
            Height = height;

            PositionKey[] toDraw;
            try
            {
                toDraw = _canvasTiles.Keys.ToArray(); // assuming the background loop is keeping everything minimal
            }
            catch
            {
                return; // race condition can trigger this
            }

            foreach (var index in toDraw)
            {
                if (index == null) continue;
                TileImage ti;
                try { ti = _canvasTiles[index]; } catch { continue; } // happens in race conditions

                // change render locations based on scale
                var displaySize = TileSize;
                var offset_x = (int)_xOffset >> (_drawScale - 1);
                var offset_y = (int)_yOffset >> (_drawScale - 1);

                var dx = (index.X * displaySize) - offset_x;
                var dy = (index.Y * displaySize) - offset_y;

                // if offscreen, don't bother
                if (dx > width || dx < -displaySize || dy > height || dy < -displaySize) continue;

                // draw tile
                var hilite = _selectedTiles.Contains(index);
                ti?.Render(g, dx, dy, hilite, _drawScale, VisualScale);
            }
        }

        /// <summary>
        /// Load a tile image from storage, pausing the current thread until it is ready
        /// </summary>
        private TileImage LoadTileSync(PositionKey tile)
        {
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


        /// <summary>
        /// Draw the selected tiles, from a given offset, into a bitmap image.
        /// This does NOT change the canvas position or size hint.
        /// </summary>
        public void RenderToImage(Bitmap bmp, int topIdx, int leftIdx, List<PositionKey> selectedTiles)
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
        }

        /// <summary>
        /// Draw curve in the current inking colour
        /// </summary>
        public void Ink(int stylusId, bool isErase, DPoint start, DPoint end)
        {
            /*
            // TEMP:
            var testPt = ScreenToCanvas((int)start.X, (int)start.Y);

            var needsAdd = !_canvasTiles.ContainsKey(testPt.TilePosition);
            var img = needsAdd ? new TileImage() : _canvasTiles[testPt.TilePosition];
            if (needsAdd) _canvasTiles.Add(testPt.TilePosition, img);

            img?.DrawOnTile(testPt.X, testPt.Y, 10, Color.Black, InkType.Overwrite, _drawScale);
            return;
            */

            
            var startPoint = ScreenToCanvas((int)start.X, (int)start.Y);

            if (_inSelectMode)
            {
                // use pens to toggle selection
                _selectedTiles.Add(startPoint.TilePosition);
                return;
            }

            if (_drawScale != 1)
            {
                // If we're scaled out, use pens to scroll
                Scroll(start.X - end.X, start.Y - end.Y);
                return;
            }

            if (!_okToDraw) return;

            var pt = start;
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var dp = end.Pressure - start.Pressure;

            var penSet = _inkSettings.ContainsKey(stylusId) ? _inkSettings[stylusId] : GuessPen(isErase);

            var dd = Math.Floor(Math.Max(Math.Abs(dx), Math.Abs(dy)));

            if (dd > 0)
            {
                dx /= dd;
                dy /= dd;
                dp /= dd;
            }
            double radius;

            for (double i = 0; i <= dd; i += radius)
            {
                var tiles = InkPoint(penSet, pt, out radius);
                radius /= 4;
                foreach (var tile in tiles)
                {
                    _changedTiles.Add(tile);
                    _lastChangedTiles.Add(tile);
                }
                pt.X += dx * radius;
                pt.Y += dy * radius;
                pt.Pressure += dp * radius;
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
            // trigger the async worker
            _saveTileData.Set();
        }

        [NotNull]
        private List<PositionKey> InkPoint(InkSettings ink, DPoint pt, out double radius)
        {
            lock (_storageLock)
            {
                radius = pt.Pressure * ink.PenSize;
                var changed = new List<PositionKey>(4);

                // primary tile
                var loca = ScreenToCanvas((int)pt.X, (int)pt.Y);
                var tx = loca.TilePosition?.X ?? 0;
                var ty = loca.TilePosition?.Y ?? 0;
                var possibleTiles = new[]{
                    new PositionKey(tx - 1, ty - 1), new PositionKey(tx    , ty - 1), new PositionKey(tx + 1, ty - 1),
                    new PositionKey(tx - 1, ty    ), new PositionKey(tx    , ty    ), new PositionKey(tx + 1, ty    ),
                    new PositionKey(tx - 1, ty + 1), new PositionKey(tx    , ty + 1), new PositionKey(tx + 1, ty + 1)
                };

                foreach (var pk in possibleTiles)
                {
                    var needsAdd = !_canvasTiles.ContainsKey(pk);
                    var img = needsAdd ? new TileImage() : _canvasTiles[pk];
                    if (needsAdd) _canvasTiles.Add(pk, img);

                    var ax = loca.X + (tx - pk.X) * TileImage.Size;
                    var ay = loca.Y + (ty - pk.Y) * TileImage.Size;

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


        public void ChangeBasePath(string newPath)
        {
            _okToDraw = false;
            lock (_storageLock) lock (_dataQueueLock) lock (_canvasTiles)
            {
                // wipe state
                _imageQueue.Clear();
                _changedTiles.Clear();
                _canvasTiles.Clear();
                _selectedTiles.Clear();

                // change storage
                _storage?.Dispose();
                Directory.CreateDirectory(Path.GetDirectoryName(newPath) ?? "");
                _storage = new LiteDbStorageContainer(newPath);
                _storagePath = newPath;

                // re-centre
                _xOffset = 0;
                _yOffset = 0;
            }
            _updateTileCache.Set();
            _invalidateAction?.Invoke();
        }
        

        public string FileName()
        {
            return Path.GetFileNameWithoutExtension(_storagePath);
        }

        /// <summary>
        /// Clear all tiles and start reloading. This is mostly used for changing display scale
        /// </summary>
        public void ResetTileCache()
        {
            _okToDraw = false;
            lock (_storageLock) lock (_dataQueueLock) lock (_canvasTiles)
                    {
                        // clear the caches and start processing
                        _imageQueue.Clear();
                        _changedTiles.Clear();
                        _canvasTiles.Clear();
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
            var loca = ScreenToCanvas(x,y);
            var pk = loca.TilePosition ?? throw new Exception("Screen index failed");

            if (!_canvasTiles.ContainsKey(pk)) _canvasTiles.Add(pk, new TileImage());
            var img = _canvasTiles[pk];

            img?.DrawOnTile(loca.X, loca.Y, 1, color, InkType.Import, _drawScale);
            _changedTiles.Add(pk);
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

            // convert window coords to offsets
            var loca = ScreenToCanvas(wX, wY);

            // centre on the clicked point
            loca.GetAbsolute(out _xOffset, out _yOffset);
            
            _xOffset = (loca.TilePosition.X * TileImage.Size * VisualScale) + loca.X;
            _yOffset = (loca.TilePosition.Y * TileImage.Size * VisualScale) + loca.Y;

            // centre on screen
            //_xOffset -= Width / (2.0*VisualScale);
            //_yOffset -= Height / (2.0*VisualScale);

            // set zoom level
            _drawScale = 1;

            ResetTileCache();
        }

        [NotNull]
        public InfoPin[] AllPins()
        {
            if (_storage == null) return new InfoPin[0];

            lock (_storageLock)
            {
                var result = _storage.ReadAllPins();
                if (result.IsFailure || result.ResultData == null) return new InfoPin[0];

                return result.ResultData;
            }
        }

        public void WritePinAtCurrentOffset(string text)
        {
            if (_storage == null) return;

            lock (_storageLock)
            {
                // tile location for middle of screen
                var x = (Width / 2);
                var y = (Height / 2);

                var loca = ScreenToCanvas(x,y);

                _storage.SetPin(loca.TilePosition.ToString(), text);
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

            lock (_storageLock)
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
                _invalidateAction?.Invoke();
            }
            return _inSelectMode;
        }

        [NotNull]
        public List<PositionKey> SelectedTiles()
        {
            return _selectedTiles.ToList();
        }

        /// <summary>
        /// Try to trigger a refresh of the canvas.
        /// </summary>
        public void Invalidate()
        {
            _invalidateAction?.Invoke();
        }

        /// <summary>
        /// Write an external image into this canvas
        /// </summary>
        public void CrossLoadImage([NotNull] Image img, int px, int py, Size size)
        {
            using (var bmp = new Bitmap(img, size))
            {
                // TODO: scaling when we're in 'map' mode in the canvas.

                var width = bmp.Width;
                var height = bmp.Height;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var color = bmp.GetPixel(x, y);
                        if (color.R == 255 && color.G == 255 && color.B == 255) continue; // skip white pixels
                        SetPixel(color, x + px, y + py);
                    }
                }
            }
            SaveChanges();
            Invalidate();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _isRunning = false;
            _saveTileData.Set();
        }
    }
}