﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Input.Inking;
using Windows.UI.Input.Inking.Core;
using JetBrains.Annotations;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using SlickCommon.Canvas;
using SlickCommon.ImageFormats;
using SlickUWP.CrossCutting;

namespace SlickUWP.Canvas
{
    /// <summary>
    /// Handles inking input that is currently in progress.
    /// </summary>
    public class WetInkCanvas
    {
        [NotNull] private readonly CanvasControl _renderTarget;
        [NotNull] private readonly List<DPoint> _stroke;
        [NotNull] private readonly Queue<DPoint[]> _dryingInk; // strokes we are currently drying

        [NotNull] private readonly Dictionary<int, Color> _penColors; 
        [NotNull] private readonly Dictionary<int, double> _penSizes; 

        public WetInkCanvas([NotNull]CanvasControl renderTarget)
        {
            _renderTarget = renderTarget;
            _renderTarget.Draw += _renderTarget_Draw;
            _stroke = new List<DPoint>();
            _dryingInk = new Queue<DPoint[]>();
            _penColors = new Dictionary<int, Color>();
            _penSizes = new Dictionary<int, double>();
        }

        
        /// <summary>
        /// Try to get a stable ID for input devices
        /// </summary>
        public static int GuessPointerId(PointerPoint point)
        {
            // NOTE: `Windows.Devices.Input.PenDevice` should do this, but is not available in Win 10 1803 -- which I am stuck with.
            if (point?.PointerDevice == null || point.Properties == null) return 0;

            var id = 0;
            id += (int)point.PointerDevice.PointerDeviceType;
            id += point.PointerDevice.IsIntegrated ? 10 : 20;
            id += point.Properties.IsEraser ? 5 : 8;
            id += (int)point.PointerDevice.MaxContacts * 40;
            id += ((int)point.PointerDevice.MaxPointersWithZDistance + 1) * 80;
            return id;
        }

        /// <summary>
        /// Finish a pen stoke. It should be rendered into the tile store.
        /// </summary>
        public void CommitTo(TileCanvas tileCanvas)
        {
            if (tileCanvas == null) return;

            try
            {
                int width = (int)_renderTarget.ActualWidth;
                int height = (int)_renderTarget.ActualHeight;

                _dryingInk.Enqueue(_stroke.ToArray());
                _stroke.Clear(); // ready for next

                // render and copy on a separate thread
                ThreadPool.QueueUserWorkItem(DryWaitingStroke(tileCanvas, width, height));

            }
            catch (Exception ex)
            {
                Logging.WriteLogMessage(ex.ToString());
            }
        }

        private WaitCallback DryWaitingStroke([NotNull]TileCanvas tileCanvas, int width, int height)
        {
            return x => {
                byte[] bytes;
                Quad coverage;
                
                // Try to get a waiting stroke (peek, so we can draw the waiting stroke)
                if (!_dryingInk.TryPeek(out var strokeToRender)) return;
                if (strokeToRender.Length < 1) {
                    _dryingInk.TryDequeue(out _);
                    return;
                }

                var clipRegion = MeasureDrawing(strokeToRender, tileCanvas.CurrentZoom());
                var pixelWidth = (int)clipRegion.Width;
                var pixelHeight = (int)clipRegion.Height;

                using (var offscreen = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), pixelWidth, pixelHeight, 96,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized, CanvasAlphaMode.Premultiplied))
                {
                    using (var ds = offscreen.CreateDrawingSession())
                    {
                        ds?.Clear(Colors.Transparent);
                        coverage = DrawToSession(ds, strokeToRender, clipRegion, tileCanvas.CurrentZoom());
                    }
                    bytes = offscreen.GetPixelBytes();
                }

                // render into tile cache
                // TEST: cropping and using the scaled write
                var uncropped = new RawImageInterleaved_UInt8{
                    Data = bytes, Height = (int)Math.Ceiling(clipRegion.Height), Width = (int)Math.Ceiling(clipRegion.Width)
                };
                var visualWidth = (int)(pixelWidth / tileCanvas.CurrentZoom());
                var visualHeight = (int)(pixelHeight / tileCanvas.CurrentZoom());
                var success = tileCanvas.ImportBytesScaled(uncropped, (int)clipRegion.X, (int)clipRegion.Y, (int) (clipRegion.X + visualWidth), (int) (clipRegion.Y + visualHeight));

                
                _renderTarget.Invalidate();
                if (success) _dryingInk.TryDequeue(out _); // pull it off the queue (don't do this if a tile was locked)

                // safety check -- if there are still strokes waiting, spawn more threads
                if (_dryingInk.Count > 0) {
                    ThreadPool.QueueUserWorkItem(DryWaitingStroke(tileCanvas, width, height));
                }
            };
        }

        /// <summary>
        /// Prepare for the start of a pen stroke
        /// </summary>
        public void StartStroke(InkUnprocessedInput input, PointerEventArgs penEvent)
        {
            _stroke.Clear();
        }

        /// <summary>
        /// Continue a pen stroke
        /// </summary>
        public void Stroke(PointerEventArgs penEvent, CanvasPixelPosition canvasPos)
        {
            try
            {
                if (penEvent?.CurrentPoint?.Properties == null) return;

                var x = penEvent.CurrentPoint.Position.X;
                var y = penEvent.CurrentPoint.Position.Y;
                var isEraser = penEvent.CurrentPoint.Properties.IsEraser || penEvent.CurrentPoint.Properties.IsRightButtonPressed; // treat right-click as erase

                if (penEvent.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)) {
                    // hacky straight line tool:
                    if (_stroke.Count > 1) _stroke.RemoveAt(_stroke.Count - 1); // replace last point instead of adding a new one
                }

                if (penEvent.KeyModifiers.HasFlag(VirtualKeyModifiers.Menu))// this is actually *ALT*
                {
                    LockToGrid(canvasPos, ref x, ref y);
                }

                _stroke.Add(new DPoint
                {
                    X = x,
                    Y = y,
                    StylusId = GuessPointerId(penEvent.CurrentPoint),
                    Pressure = penEvent.CurrentPoint.Properties.Pressure,
                    IsErase = isEraser
                });

                _renderTarget.Invalidate();
            }
            catch (Exception ex)
            {
                Logging.WriteLogMessage(ex.ToString());
            }
        }

        private static void LockToGrid(CanvasPixelPosition canvasPos, ref double x, ref double y)
        {
            if (canvasPos == null) return;

            double grid = TileCanvas.GridSize;

            var x1 = Math.Round(canvasPos.X / grid) * grid;
            x += x1 - canvasPos.X;

            var y1 = Math.Round(canvasPos.Y / grid) * grid;
            y += y1 - canvasPos.Y;
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
                Quad screenQuad = new Quad(0, 0, sender?.ActualWidth ?? 0, sender?.ActualHeight ?? 0);

                g.Clear(Colors.Transparent);
                DrawToSession(g, _stroke.ToArray(), screenQuad, 1.0);

                // Overdraw any ink that is still drying...
                var drying = _dryingInk.ToArray();
                foreach (var stroke in drying)
                {
                    DrawToSession(g, stroke, screenQuad, 1.0);
                }
            }
            catch (Exception ex)
            {
                Logging.WriteLogMessage(ex.ToString());
            }
        }
        

        [NotNull]
        private Quad MeasureDrawing(DPoint[] strokeToRender, double zoom)
        {
            var clipRegion = new Quad(0,0,0,0);
            try
            {
                // TODO: merge this with render to reduce duplication
                var pts = strokeToRender;
                if (pts == null || pts.Length < 1) return clipRegion;

                // get size and color from dictionary, or set default
                if (!_penColors.TryGetValue(pts[0].StylusId, out var color)) { color = pts[0].IsErase ? Colors.White : Colors.BlueViolet; }
                if (!_penSizes.TryGetValue(pts[0].StylusId, out var size)) { size = pts[0].IsErase ? 6.5 : 2.5; }

                var attr = new InkDrawingAttributes{
                    Size = new Size(size * zoom, size * zoom),
                    Color = color,
                    PenTip = PenTipShape.Circle
                };
                var s = new CoreIncrementalInkStroke(attr, Matrix3x2.Identity);

                var minX = pts.Min(p=>p.X);
                var minY = pts.Min(p=>p.Y);

                for (int i = 0; i < pts.Length; i++)
                {
                    var current = pts[i];
                    var zx = ((current.X - minX) * zoom) + minX;
                    var zy = ((current.Y - minY) * zoom) + minY;
                    s.AppendInkPoints(new[]{
                        new InkPoint(new Point(zx, zy), (float) current.Pressure)
                    });
                }

                var stroke = s.CreateInkStroke();
                if (stroke == null) throw new Exception("Stroke creation failed");

                var bounds = stroke.BoundingRect;
                clipRegion.X = (int) minX;
                clipRegion.Y = (int) minY;
                clipRegion.Width = (int)(bounds.Width + (2 * size));
                clipRegion.Height = (int)(bounds.Height + (2 * size));
            }
            catch (Exception ex)
            {
                Logging.WriteLogMessage(ex.ToString());
            }

            return clipRegion;
        }


        [NotNull]
        private Quad DrawToSession(CanvasDrawingSession g, DPoint[] strokeToRender, [NotNull]Quad clipRegion, double zoom)
        {
            var coverage = new Quad(0,0,0,0);
            if (g == null) return coverage;
            try
            {
                var pts = strokeToRender;
                if (pts == null || pts.Length < 1) return coverage;

                // using the ink infrastructure for drawing...
                var strokes = new List<InkStroke>();

                // get size and color from dictionary, or set default
                if (!_penColors.TryGetValue(pts[0].StylusId, out var color)) { color = pts[0].IsErase ? Colors.White : Colors.BlueViolet; }
                if (!_penSizes.TryGetValue(pts[0].StylusId, out var size)) { size = pts[0].IsErase ? 6.5 : 2.5; }

                var attr = new InkDrawingAttributes{
                    Size = new Size(size * zoom, size * zoom),
                    Color = color,
                    PenTip = PenTipShape.Circle
                };
                var s = new CoreIncrementalInkStroke(attr, Matrix3x2.Identity);

                for (int i = 0; i < pts.Length; i++)
                {
                    var current = pts[i];
                    s.AppendInkPoints(new[]{
                        new InkPoint(new Point((current.X - clipRegion.X) * zoom, (current.Y - clipRegion.Y) * zoom), (float) current.Pressure)
                    });
                }

                var stroke = s.CreateInkStroke();
                if (stroke == null) throw new Exception("Stroke creation failed");
                strokes.Add(stroke);

                var bounds = stroke.BoundingRect;
                coverage.X = (int)bounds.X - 1;
                coverage.Y = (int)bounds.Y - 1;
                coverage.Width = (int)bounds.Width + 2;
                coverage.Height = (int)bounds.Height + 2;

                g.DrawInk(strokes);
            }
            catch (Exception ex)
            {
                Logging.WriteLogMessage(ex.ToString());
            }

            return coverage;
        }

        public void SetPenSize(PointerPoint pointer, double size)
        {
            var id = GuessPointerId(pointer);
            if (!_penSizes.TryAdd(id, size))
                _penSizes[id] = size;
        }

        public void SetPenColor(PointerPoint pointer, Color color)
        {
            var id = GuessPointerId(pointer);
            if (!_penColors.TryAdd(id, color))
                _penColors[id] = color;
        }
    }
}