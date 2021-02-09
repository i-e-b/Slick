using System;
using System.Collections.Generic;
using System.Drawing;
using JetBrains.Annotations;
using SlickCommon.ImageFormats;
using SlickCommon.Storage;

namespace SlickCommon.Canvas
{
    public interface IEndlessCanvas : IDisposable
    {
        double X { get; }
        double Y { get; }
        
        /// <summary>
        /// DPI of container (updated through AutoScaleForm triggers)
        /// </summary>
        int Dpi { get; set; }

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
        /// Draw curve in the current inking colour
        /// </summary>
        void Ink(DPoint start, DPoint end);

        /// <summary>
        /// Set a single pixel on the canvas.
        /// This is very slow, you should use the `InkPoint` method unless you're doing something special.
        /// This is used for importing.
        /// </summary>
        void SetPixel(Color color, int x, int y);

        /// <summary>
        /// Write an image into the canvas, with position and scaling
        /// </summary>
        void CrossLoadImage(RawImagePlanar image, int left, int top, Size size);

        /// <summary>
        /// Return a list of tile indexes that have been selected for export
        /// </summary>
        /// <returns></returns>
        List<PositionKey> SelectedTiles();

        /// <summary>
        /// Start a pen stroke.
        /// </summary>
        void StartStroke();

        /// <summary>
        /// End a pen stroke, committing it to the canvas
        /// </summary>
        void EndStroke();

        /// <summary>
        /// List all pinned locations on the canvas
        /// </summary>
        [NotNull] InfoPin[] AllPins();

        void WritePinAtCurrentOffset(string text);
        void CentreOnPin(InfoPin pin);
        void DeletePin(InfoPin pin);
    }
}