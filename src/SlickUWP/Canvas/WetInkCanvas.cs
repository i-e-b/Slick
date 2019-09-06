using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using Windows.Graphics.DirectX;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml.Controls;
using JetBrains.Annotations;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using SlickCommon.Canvas;
using SlickCommon.ImageFormats;
using SlickCommon.Storage;
using SlickUWP.Adaptors;
using System.Runtime.InteropServices.WindowsRuntime;

namespace SlickUWP.Canvas
{
    /// <summary>
    /// Handles inking input that is currently in progress.
    /// </summary>
    public class WetInkCanvas
    {
        [NotNull] private readonly CanvasControl _renderTarget;
        [NotNull] private readonly List<DPoint> _stroke;

        public WetInkCanvas([NotNull]CanvasControl renderTarget)
        {
            _renderTarget = renderTarget;
            _renderTarget.Draw += _renderTarget_Draw;
            _stroke = new List<DPoint>();
        }

        /// <summary>
        /// Finish a pen stoke. It should be rendered into the tile store.
        /// </summary>
        public void CommitTo(TileCanvas tileCanvas)
        {
            if (tileCanvas == null) return;

            try
            {
                byte[] bytes = null;
                int width = (int)_renderTarget.ActualWidth;
                int height = (int)_renderTarget.ActualHeight;

                // Get an offscreen target
                var device = CanvasDevice.GetSharedDevice(); // NEVER dispose of 'GetSharedDevice' or put it in a `using`. You will crash your program.
                using (var offscreen = new CanvasRenderTarget(device, width, height, 96, DirectXPixelFormat.B8G8R8A8UIntNormalized, CanvasAlphaMode.Premultiplied))
                {
                    using (var ds = offscreen.CreateDrawingSession())
                    {
                        ds.Clear(Colors.Transparent);
                        DrawToSession(ds);
                    }

                    bytes = offscreen.GetPixelBytes();
                }

                // render into tile cache
                tileCanvas.ImportBytes(new RawImageInterleaved_UInt8{
                    Data = bytes,
                    Width = width,
                    Height = height
                }, 0, 0);;

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            // Stroke has been committed. Remove from wet ink and redraw.
            _stroke.Clear();
            _renderTarget.Invalidate();
        }

        /// <summary>
        /// Prepare for the start of a pen stroke
        /// </summary>
        public void StartStroke(InkUnprocessedInput input, PointerEventArgs penEvent)
        {
            // TODO: set the color/size/etc; make sure the old wet ink is cleared.
            _stroke.Clear();
        }

        /// <summary>
        /// Continue a pen stroke
        /// </summary>
        public void Stroke(PointerEventArgs penEvent)
        {
            try
            {
                if (penEvent?.CurrentPoint?.Properties == null) return;

                _stroke.Add(new DPoint
                {
                    X = penEvent.CurrentPoint.Position.X,
                    Y = penEvent.CurrentPoint.Position.Y,
                    StylusId = (int)penEvent.CurrentPoint.PointerId,
                    Pressure = penEvent.CurrentPoint.Properties.Pressure,
                    IsErase = penEvent.CurrentPoint.Properties.IsEraser
                });

                _renderTarget.Invalidate();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }


        /// <summary>
        /// Draw any waiting wet ink strokes
        /// </summary>
        private void _renderTarget_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            try
            {
                var g = args?.DrawingSession;
                if (g == null) return;

                g.Clear(Colors.Transparent);
                DrawToSession(g);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private void DrawToSession(CanvasDrawingSession g)
        {
            try
            {
                var pts = _stroke.ToArray(); // get a static copy
                if (pts == null || pts.Length < 1) return;
                var prev = pts[0];
                for (int i = 0; i < pts.Length; i++)
                {
                    var current = pts[i];
                    g.DrawLine((float)prev.X, (float)prev.Y, (float)current.X, (float)current.Y, Color.FromArgb(255, 0, 0, 0), (float)(2.0 * current.Pressure) /*width*/);
                    prev = current;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

    }
}