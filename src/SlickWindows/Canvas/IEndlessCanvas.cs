using System;
using System.Collections.Generic;
using System.Drawing;

namespace SlickWindows.Canvas
{
    public interface IEndlessCanvas : IDisposable
    {
        double X { get; }
        double Y { get; }

        /// <summary>
        /// move the offset
        /// </summary>
        void Scroll(double dx, double dy);

        /// <summary>
        /// Save any tiles changed since last save
        /// </summary>
        void SaveChanges();

        /// <summary>
        /// Set current inking color, size, etc
        /// </summary>
        void SetPen(int stylusId, Color color, double size, InkType type);

        /// <summary>
        /// Convert a screen position to a canvas pixel
        /// </summary>
        CanvasPixelPosition ScreenToCanvas(double x, double y);

        /// <summary>
        /// Display from the current offset into a graphics output
        /// </summary>
        void RenderToGraphics(Graphics g, int width, int height, Rectangle clipRect);

        /// <summary>
        /// Draw the selected tiles, from a given offset, into a bitmap image.
        /// This does NOT change the canvas position or size hint.
        /// </summary>
        void RenderToImage(Bitmap bmp, int topIdx, int leftIdx, List<PositionKey> selectedTiles);

        /// <summary>
        /// Draw curve in the current inking colour
        /// </summary>
        void Ink(DPoint start, DPoint end);

        /// <summary>
        /// Set a single pixel on the canvas.
        /// This is very slow, you should use the `InkPoint` method unless you're doing something special.
        /// This is used for importing.
        /// </summary>
        void SetPixel(Color color, int x, int y);
    }
}