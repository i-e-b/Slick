using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using JetBrains.Annotations;

namespace SlickWindows.Canvas
{
    /// <summary>
    /// Image storage for an unbounded canvas
    /// </summary>
    public class EndlessCanvas : IEndlessCanvas
    {
        private double _xOffset, _yOffset;
        private InkSettings _lastPen;
        private readonly string _basePath;

        [NotNull]private readonly HashSet<PositionKey> _changedTiles;
        [NotNull]private readonly Dictionary<PositionKey, TileImage> _canvasTiles;
        
        [NotNull]private readonly Dictionary<int, InkSettings> _inkSettings;

        public double X { get { return _xOffset; } }
        public double Y { get { return _yOffset; } }
        
        public int DpiY;
        public int DpiX;

        /// <summary>
        /// Load and run a canvas from a storage folder
        /// </summary>
        /// <param name="deviceDpi">Screen resolution</param>
        /// <param name="basePath">Storage folder</param>
        public EndlessCanvas(int deviceDpi, string basePath)
        {
            _inkSettings = new Dictionary<int, InkSettings>();
            _canvasTiles = new Dictionary<PositionKey, TileImage>();
            _changedTiles = new HashSet<PositionKey>();

            DpiX = deviceDpi; // TODO: derive from the context
            _basePath = basePath;
            DpiY = deviceDpi;
            _xOffset = 0.0;
            _yOffset = 0.0;

            if (!string.IsNullOrWhiteSpace(basePath)) {
                Directory.CreateDirectory(basePath);
                var saved = Directory.GetFiles(basePath);
                foreach (var path in saved)
                {
                    var key = PositionKey.Parse(Path.GetFileNameWithoutExtension(path));
                    _canvasTiles.Add(key, TileImage.Load(path));
                }
            }

            _lastPen = new InkSettings
            {
                PenColor = Color.BlueViolet,
                PenSize = 5.0,
                PenType = InkType.Overwrite
            };
        }

        /// <summary>
        /// Display from the current offset into a graphics output
        /// </summary>
        public void RenderToGraphics(Graphics g, int width, int height){
            // TODO: generalise graphics

            // work out the indexes we need, find in dictionary, draw
            int ox = (int)(_xOffset / TileImage.Size);
            int oy = (int)(_yOffset / TileImage.Size);
            int mx = width / TileImage.Size;
            int my = height / TileImage.Size;

            for (int y = -1; y <= my; y++)
            {
                var yIdx = oy + y;

                for (int x = -1; x <= mx; x++)
                {
                    var xIdx = ox + x;
                    var pk = new PositionKey(xIdx, yIdx);
                    if (!_canvasTiles.ContainsKey(pk)) continue;

                    var tile = _canvasTiles[pk];
                    tile.Render(g, (xIdx * TileImage.Size) - _xOffset, (yIdx * TileImage.Size) - _yOffset);
                }
            }
        }

        /// <summary>
        /// move the offset
        /// </summary>
        public void Scroll(double dx, double dy){
            _xOffset += dx;
            _yOffset += dy;
        }

        /// <summary>
        /// Draw curve in the current inking colour
        /// </summary>
        public void Ink(int stylusId, DPoint start, DPoint end)
        {
            var pt = start;
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var dp = end.Pressure - start.Pressure;
            
            var penSet = _inkSettings.ContainsKey(stylusId) ? _inkSettings[stylusId] : _lastPen;

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

        /// <summary>
        /// Save any tiles changed since last save
        /// </summary>
        public void SaveChanges() {
            if (string.IsNullOrWhiteSpace(_basePath)) return;
            foreach (var key in _changedTiles)
            {
                if (!_canvasTiles.ContainsKey(key)) continue;
                _canvasTiles[key]?.Save(_basePath, key);
            }
            _changedTiles.Clear();
        }

        private PositionKey InkPoint(InkSettings ink, DPoint pt)
        {
            var xIdx = Math.Floor((pt.X + _xOffset) / TileImage.Size);
            var yIdx = Math.Floor((pt.Y + _yOffset) / TileImage.Size);
            var pk = new PositionKey((int) xIdx, (int) yIdx);

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
        public void SetPen(int stylusId, Color color, double size, InkType type) {
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