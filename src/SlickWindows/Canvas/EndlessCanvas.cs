using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using SlickWindows.ImageFormats;

namespace SlickWindows.Canvas
{
    /// <summary>
    /// Image storage for an unbounded canvas
    /// </summary>
    public class EndlessCanvas : IEndlessCanvas
    {
        public int Width { get; set; }
        public int Height { get; set; }
        private double _xOffset, _yOffset;
        private InkSettings _lastPen;
        private readonly string _basePath;
        private readonly Action _invalidateAction;

        [NotNull] private readonly HashSet<PositionKey> _changedTiles;
        [NotNull] private readonly Dictionary<PositionKey, TileImage> _canvasTiles;

        [NotNull] private readonly Dictionary<int, InkSettings> _inkSettings;

        public double X { get { return _xOffset; } }
        public double Y { get { return _yOffset; } }

        public int DpiY;
        public int DpiX;

        public volatile bool IsRunning;

        /// <summary>
        /// Load and run a canvas from a storage folder
        /// </summary>
        /// <param name="deviceDpi">Screen resolution</param>
        /// <param name="basePath">Storage folder</param>
        /// <param name="invalidateAction">Optional action to call when drawing area becomes invalid</param>
        /// <param name="width">Initial display width (can be oversize if not known)</param>
        /// <param name="height">Initial display height (can be oversize if not known)</param>
        public EndlessCanvas(int width, int height, int deviceDpi, string basePath, Action invalidateAction)
        {
            _basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));
            Directory.CreateDirectory(basePath);

            IsRunning = true;

            Width = width;
            Height = height;
            _inkSettings = new Dictionary<int, InkSettings>();
            _canvasTiles = new Dictionary<PositionKey, TileImage>();
            _changedTiles = new HashSet<PositionKey>();

            DpiX = deviceDpi;
            _invalidateAction = invalidateAction;
            DpiY = deviceDpi;
            _xOffset = 0.0;
            _yOffset = 0.0;

            // Load on a different thread so the screen comes up fast
            LoadImagesAsync(basePath);

            _lastPen = new InkSettings
            {
                PenColor = Color.BlueViolet,
                PenSize = 5.0,
                PenType = InkType.Overwrite
            };
        }

        private void LoadImagesAsync([NotNull]string basePath)
        {
            // Try to load images for any visible tiles,
            // and remove any loaded tiles that are far
            // offscreen.
            // Trigger a re-draw as images come in.
            new Thread(() =>
                {
                    while (IsRunning)
                    {
                        // load any new tiles
                        var visibleTiles = VisibleTiles(Width, Height, Width / 2, Height / 2); // load extra tiles outside the viewport
                        var loadedTiles = _canvasTiles.Keys.ToArray();

                        bool changed = false;

                        foreach (var tile in visibleTiles)
                        {
                            if (_canvasTiles.ContainsKey(tile)) continue; // already in memory

                            var path = Path.Combine(basePath, tile.ToString());
                            if (!File.Exists(path)) continue; // no such tile written

                            //_canvasTiles.Add(tile, TileImage.Load(path));
                            
                            using (var fs = File.Open(path, FileMode.Open, FileAccess.Read))
                            {
                                var fileData = InterleavedFile.ReadFromStream(fs);
                                _canvasTiles.Add(tile, WaveletCompress.Decompress(fileData));
                            }

                            changed = true;
                        }
                        if (changed) _invalidateAction?.Invoke(); // redraw with what we have

                        // unload any old tiles
                        foreach (var old in loadedTiles)
                        {
                            if (visibleTiles.Contains(old)) continue; // still visible
                            _canvasTiles.Remove(old);
                        }
                        Thread.Sleep(250); // TODO: put a wait/reset in here rather than just pausing
                    }
                })
            { IsBackground = true }.Start();
        }


        [NotNull]
        private List<PositionKey> VisibleTiles(int width, int height, int extraX = 0, int extraY = 0)
        {
            Width = width;
            Height = height;

            // work out the indexes we need, find in dictionary, draw
            int ox = (int)((_xOffset - extraX) / TileImage.Size);
            int oy = (int)((_yOffset - extraY) / TileImage.Size);
            int mx = (int)Math.Round((double)(width + (extraX * 2)) / TileImage.Size);
            int my = (int)Math.Round((double)(height + (extraY * 2)) / TileImage.Size);

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

            var toDraw = VisibleTiles(width, height);
            foreach (var index in toDraw)
            {
                if (!_canvasTiles.ContainsKey(index)) continue;
                _canvasTiles[index]?.Render(g, (index.X * TileImage.Size) - _xOffset, (index.Y * TileImage.Size) - _yOffset);
            }
        }

        /// <summary>
        /// move the offset
        /// </summary>
        public void Scroll(double dx, double dy)
        {
            _xOffset += dx;
            _yOffset += dy;
        }

        /// <summary>
        /// Set an absolute scroll position
        /// </summary>
        public void ScrollTo(double x, double y)
        {
            _xOffset = x;
            _yOffset = y;
        }

        /// <summary>
        /// Draw curve in the current inking colour
        /// </summary>
        public void Ink(int stylusId, bool isErase, DPoint start, DPoint end)
        {
            var pt = start;
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var dp = end.Pressure - start.Pressure;

            var penSet = _inkSettings.ContainsKey(stylusId) ? _inkSettings[stylusId] : GuessPen(isErase);

            var dd = Math.Floor(Math.Max(Math.Abs(dx), Math.Abs(dy)));

            _changedTiles.Add(InkPoint(penSet, pt));
            if (dd < 1) { return; }

            dx /= dd;
            dy /= dd;
            dp /= dd;
            for (int i = 0; i < dd; i++)
            {
                _changedTiles.Add(InkPoint(penSet, pt));
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
            if (string.IsNullOrWhiteSpace(_basePath)) return;
            foreach (var key in _changedTiles)
            {
                if (!_canvasTiles.ContainsKey(key)) continue;
                var tile = _canvasTiles[key];
                if (tile == null) continue;

                var filePath = Path.Combine(_basePath, key.ToString());

                if (tile.ImageIsBlank())
                {
                    DeleteFile(filePath);
                    _canvasTiles.Remove(key);
                }
                else
                {
                    var storage = WaveletCompress.Compress(tile);
                    using (var fs = File.Open(filePath, FileMode.Create, FileAccess.Write))
                    {
                        storage.WriteToStream(fs);
                    }
                }
            }
            _changedTiles.Clear();
        }

        private void DeleteFile([NotNull]string filePath)
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }

        private PositionKey InkPoint(InkSettings ink, DPoint pt)
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
    }
}