using System;
using System.Collections.Generic;
using System.Drawing;
using JetBrains.Annotations;

namespace SlickWindows.Canvas
{
    /// <summary>
    /// Image storage for an unbounded canvas
    /// </summary>
    public class EndlessCanvas {
        private double _xOffset, _yOffset;
        [NotNull]private readonly Dictionary<PositionKey, TileImage> _canvasTiles;
        private Color _penColor;
        private double _penSize;
        private InkType _penType;

        public double X { get { return _xOffset; } }
        public double Y { get { return _yOffset; } }
        
        public int DpiY;
        public int DpiX;


        public EndlessCanvas(int deviceDpi)
        {
            DpiX = deviceDpi; // TODO: derive from the context
            DpiY = deviceDpi;
           _xOffset = 100.0; 
           _yOffset = 100.0;
            
           _penColor = Color.BlueViolet;
           _penSize = 5.0;
           _penType = InkType.Overwrite;

           _canvasTiles = new Dictionary<PositionKey, TileImage>();
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
        public void Ink(DPoint start, DPoint end) {
            var xIdx = Math.Floor((start.X + _xOffset) / TileImage.Size);
            var yIdx = Math.Floor((start.Y + _yOffset) / TileImage.Size);
            var pk = new PositionKey((int)xIdx, (int)yIdx);

            if (!_canvasTiles.ContainsKey(pk)) _canvasTiles.Add(pk, new TileImage());
            var img = _canvasTiles[pk];

            var bx = (start.X + _xOffset) % TileImage.Size;
            var by = (start.Y + _yOffset) % TileImage.Size;

            if (bx < 0) bx += TileImage.Size;
            if (by < 0) by += TileImage.Size;       

            if (_penType == InkType.Overwrite) {
                img?.Overwrite(bx, by, start.Pressure * _penSize, _penColor);
            }
            else {
                img?.Highlight(bx, by, start.Pressure * _penSize, _penColor);
            }
        }

        /// <summary>
        /// Set current inking color, size, etc
        /// </summary>
        public void SetPen(Color color, double size, InkType type) {
            _penColor = color;
            _penSize = size;
            _penType = type;
        }
    }
}