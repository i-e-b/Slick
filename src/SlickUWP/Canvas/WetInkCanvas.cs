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
        [NotNull] private readonly List<DPoint> _stroke; // stroke being drawn
        
        [NotNull]private static readonly object _dryLock = new object();
        [NotNull] private readonly List<DPoint[]> _dryingInk; // strokes we are currently drying
        [NotNull] private readonly HashSet<DPoint[]> _renderingInk; // strokes we are currently rendering

        [NotNull] private readonly Dictionary<int, Color> _penColors; 
        [NotNull] private readonly Dictionary<int, double> _penSizes; 

        public WetInkCanvas([NotNull]CanvasControl renderTarget)
        {
            _renderTarget = renderTarget;
            _renderTarget.Draw += _renderTarget_Draw;
            _stroke = new List<DPoint>();
            _dryingInk = new List<DPoint[]>();
            _renderingInk = new HashSet<DPoint[]>();
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
                lock(_dryLock){
                    _dryingInk.Add(_stroke.ToArray());
                    _stroke.Clear(); // ready for next
                }

                // render and copy on a separate thread
                DryWaitingStroke(tileCanvas);
            }
            catch (Exception ex)
            {
                Logging.WriteLogMessage(ex.ToString());
            }
        }

        private void DryWaitingStroke([NotNull]TileCanvas tileCanvas)
        {
            // Try to get a waiting stroke (peek, so we can draw the waiting stroke)
            DPoint[][] waitingStrokes;
            lock (_dryLock)
            {
                waitingStrokes = _dryingInk.ToArray();
                _dryingInk.Clear();
                if (waitingStrokes != null) _renderingInk.UnionWith(waitingStrokes);
            }

            tileCanvas.Invalidate(); // show progress if the render is slow.
            _renderTarget.Invalidate();

            if (waitingStrokes == null) return;
            foreach (var strokeToRender in waitingStrokes)
            {
                if (strokeToRender.Length < 1)
                {
                    continue;
                }

                // Figure out what part of the screen is covered
                var clipRegion = MeasureDrawing(strokeToRender, tileCanvas.CurrentZoom());
                var pixelWidth = (int) clipRegion.Width;
                var pixelHeight = (int) clipRegion.Height;

                // draw to an image
                byte[] bytes;
                using (var offscreen = new CanvasRenderTarget(CanvasDevice.GetSharedDevice(), pixelWidth, pixelHeight, 96,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized, CanvasAlphaMode.Premultiplied))
                {
                    using (var ds = offscreen.CreateDrawingSession())
                    {
                        ds?.Clear(Colors.Transparent);
                        DrawToSession(ds, strokeToRender, clipRegion, tileCanvas.CurrentZoom());
                    }

                    bytes = offscreen.GetPixelBytes();
                }

                // render into tile cache
                var uncropped = new RawImageInterleaved_UInt8
                {
                    Data = bytes,
                    Width = pixelWidth,
                    Height = pixelHeight
                };

                var visualWidth = (int) Math.Ceiling(pixelWidth / tileCanvas.CurrentZoom());
                var visualHeight = (int) Math.Ceiling(pixelHeight / tileCanvas.CurrentZoom());
                var visualTop = (int) Math.Floor(clipRegion.Y + 0.5);
                var visualLeft = (int) Math.Floor(clipRegion.X + 0.5);
                var visualRight = visualLeft + visualWidth;
                var visualBottom = visualTop + visualHeight;

                ThreadPool.QueueUserWorkItem(canv =>
                {
                    var ok = tileCanvas.ImportBytesScaled(uncropped, visualLeft, visualTop, visualRight, visualBottom);
                    if (! ok) {
                        Logging.WriteLogMessage("Tile byte import failed when drawing strokes");
                    }
                    lock (_dryLock)
                    {
                        _renderingInk.Remove(strokeToRender);
                    }
                    tileCanvas.Invalidate(); // show finished strokes
                    _renderTarget.Invalidate();
                });
            }
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
        public void Stroke(PointerEventArgs penEvent, CanvasPixelPosition canvasOffset, [NotNull]TileCanvas target)
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
                    LockToGrid(canvasOffset, target, ref x, ref y);
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

        private static void LockToGrid(CanvasPixelPosition canvasOffset, [NotNull]TileCanvas target, ref double x, ref double y)
        {
            if (canvasOffset == null) return;

            var scale = 1.0 / target.CurrentZoom();
            var grid = TileCanvas.GridSize * scale;

            var bias = (3 * grid) / 5.0;

            x = Math.Round((x + bias) / grid) * grid;
            x -= (canvasOffset.X * scale) % grid;

            y = Math.Round((y + bias) / grid) * grid;
            y -= (canvasOffset.Y * scale) % grid;
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
                var screenQuad = new Quad(0, 0, sender?.ActualWidth ?? 0, sender?.ActualHeight ?? 0);

                g.Clear(Colors.Transparent);
                DrawToSession(g, _stroke.ToArray(), screenQuad, 1.0);

                // Overdraw any ink that is waiting to dry (this is for strokes being drawn)
                DPoint[][] strokes;
                lock (_dryLock) { strokes = _dryingInk.ToArray(); }
                if (strokes != null) foreach (var stroke in strokes) { DrawToSession(g, stroke, screenQuad, 1.0); }

                // overdraw any ink that is being rendered (prevents flicker if the render is running slowly)
                lock (_dryLock) { strokes = _renderingInk.ToArray(); }
                foreach (var stroke in strokes) { DrawToSession(g, stroke, screenQuad, 1.0, true); }
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
                var pts = strokeToRender;
                if (pts == null || pts.Length < 1) return clipRegion;

                // get size and color from dictionary, or set default
                var attr = GetPenAttributes(zoom, pts);
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
                clipRegion.X = (int)minX - (2 * attr.Size.Width);
                clipRegion.Y = (int)minY - (2 * attr.Size.Width);
                clipRegion.Width = (int)(bounds.Width + (4 * attr.Size.Width * zoom)) + 2;
                clipRegion.Height = (int)(bounds.Height + (4 * attr.Size.Width * zoom)) + 2;
            }
            catch (Exception ex)
            {
                Logging.WriteLogMessage(ex.ToString());
            }

            return clipRegion;
        }

        private void DrawToSession([CanBeNull]CanvasDrawingSession g, DPoint[] strokeToRender, [NotNull]Quad clipRegion, double zoom, bool dim = false)
        {
            try
            {
                var pts = strokeToRender;
                if (pts == null || pts.Length < 1) return;

                // using the ink infrastructure for drawing...
                var strokes = new List<InkStroke>();

                var attr = GetPenAttributes(zoom, pts);
                if (dim) {
                    attr.Color = Color.FromArgb(255, 127, 127, 127);
                }
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
                g?.DrawInk(strokes); // this call can be *very* slow! It's bad when using wide strokes at high zoom levels.
            }
            catch (Exception ex)
            {
                Logging.WriteLogMessage(ex.ToString());
            }
        }

        [NotNull]private InkDrawingAttributes GetPenAttributes(double zoom, DPoint[] pts)
        {
            if (pts == null) throw new Exception("Invalid pen points");

            var detail = new DPoint { StylusId = 0 };
            if (pts.Length > 0) detail = pts[0];


            // get size and color from dictionary, or set default
            if (!_penColors.TryGetValue(detail.StylusId, out var color))
            {
                color = detail.IsErase ? Colors.White : Colors.BlueViolet;
            }

            if (!_penSizes.TryGetValue(detail.StylusId, out var size))
            {
                size = detail.IsErase ? 6.5 : 2.5;
            }

            var attr = new InkDrawingAttributes
            {
                Size = new Size(size * zoom, size * zoom),
                Color = color,
                PenTip = PenTipShape.Circle,
                FitToCurve = false
            };
            return attr;
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