using System;
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
                _dryingInk.Enqueue(_stroke.ToArray());
                _stroke.Clear(); // ready for next

                // render and copy on a separate thread
                ThreadPool.QueueUserWorkItem(DryWaitingStroke(tileCanvas));
            }
            catch (Exception ex)
            {
                Logging.WriteLogMessage(ex.ToString());
            }
        }

        private WaitCallback DryWaitingStroke([NotNull]TileCanvas tileCanvas)
        {
            return x => {
                byte[] bytes;
                
                // Try to get a waiting stroke (peek, so we can draw the waiting stroke)
                if (!_dryingInk.TryPeek(out var strokeToRender)) return;
                if (strokeToRender.Length < 1) {
                    _dryingInk.TryDequeue(out _);
                    return;
                }

                // Figure out what part of the screen is covered
                var clipRegion = MeasureDrawing(strokeToRender, tileCanvas.CurrentZoom());
                var pixelWidth = (int)clipRegion.Width;
                var pixelHeight = (int)clipRegion.Height;

                // draw to an image
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
                var uncropped = new RawImageInterleaved_UInt8{
                    Data = bytes,
                    Width = pixelWidth,
                    Height = pixelHeight
                };

                var visualWidth = (int)Math.Ceiling(pixelWidth / tileCanvas.CurrentZoom());
                var visualHeight = (int)Math.Ceiling(pixelHeight / tileCanvas.CurrentZoom());
                var visualTop = (int)Math.Round(clipRegion.Y);
                var visualLeft = (int)Math.Round(clipRegion.X);
                var visualRight = visualLeft + visualWidth;
                var visualBottom = visualTop + visualHeight;
                var success = tileCanvas.ImportBytesScaled(uncropped, visualLeft, visualTop, visualRight, visualBottom);

                
                _renderTarget.Invalidate();
                if (success) _dryingInk.TryDequeue(out _); // pull it off the queue (don't do this if a tile was locked)

                // safety check -- if there are still strokes waiting, spawn more threads
                if (_dryingInk.Count > 0) {
                    ThreadPool.QueueUserWorkItem(DryWaitingStroke(tileCanvas));
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
            double grid = TileCanvas.GridSize * scale;

            var bias = grid / 3.0;

            x = Math.Round((x+bias) / grid) * grid;
            x -= (canvasOffset.X * scale) % grid;

            y = Math.Round((y+bias) / grid) * grid;
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
                var pts = strokeToRender;
                if (pts == null || pts.Length < 1) return clipRegion;

                // get size and color from dictionary, or set default
                if (!_penColors.TryGetValue(pts[0].StylusId, out var color)) { color = pts[0].IsErase ? Colors.White : Colors.BlueViolet; }
                if (!_penSizes.TryGetValue(pts[0].StylusId, out var size)) { size = pts[0].IsErase ? 6.5 : 2.5; }

                size *= zoom;

                var attr = new InkDrawingAttributes{
                    Size = new Size(size, size),
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
                clipRegion.X = (int)minX - (2 * size);
                clipRegion.Y = (int)minY - (2 * size);
                clipRegion.Width = (int)(bounds.Width + (8 * size * zoom));
                clipRegion.Height = (int)(bounds.Height + (8 * size * zoom));
            }
            catch (Exception ex)
            {
                Logging.WriteLogMessage(ex.ToString());
            }

            return clipRegion;
        }

        private void DrawToSession([CanBeNull]CanvasDrawingSession g, DPoint[] strokeToRender, [NotNull]Quad clipRegion, double zoom)
        {
            try
            {
                var pts = strokeToRender;
                if (pts == null || pts.Length < 1) return;

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
                g?.DrawInk(strokes);
            }
            catch (Exception ex)
            {
                Logging.WriteLogMessage(ex.ToString());
            }
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