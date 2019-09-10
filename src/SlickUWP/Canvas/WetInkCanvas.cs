using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using Windows.Foundation;
using Windows.Graphics.DirectX;
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
                Console.WriteLine(ex);
            }
        }

        private WaitCallback DryWaitingStroke([NotNull]TileCanvas tileCanvas, int width, int height)
        {
            return x => {
                byte[] bytes;
                Quad coverage;
                
                // Try to get a waiting stroke (peek, so we can draw the waiting stroke)
                if (!_dryingInk.TryPeek(out var strokeToRender)) return;

                var device = CanvasDevice.GetSharedDevice(); // NEVER dispose of 'GetSharedDevice' or put it in a `using`. You will crash your program.
                using (var offscreen = new CanvasRenderTarget(device, width, height, 96, DirectXPixelFormat.B8G8R8A8UIntNormalized, CanvasAlphaMode.Premultiplied))
                {
                    using (var ds = offscreen.CreateDrawingSession())
                    {
                        ds?.Clear(Colors.Transparent);
                        coverage = DrawToSession(ds, strokeToRender);
                    }
                    bytes = offscreen.GetPixelBytes();
                }

                // render into tile cache
                // todo: handle the case where a tile is still locked
                tileCanvas.ImportBytes(new RawImageInterleaved_UInt8{
                    Data = bytes,
                    Width = width,
                    Height = height
                }, coverage.X, coverage.Y, coverage.Width, coverage.Height, 0, 0);

                _renderTarget.Invalidate();
                _dryingInk.TryDequeue(out _); // pull it off the queue
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
        public void Stroke(PointerEventArgs penEvent)
        {
            try
            {
                if (penEvent?.CurrentPoint?.Properties == null) return;

                _stroke.Add(new DPoint
                {
                    X = penEvent.CurrentPoint.Position.X,
                    Y = penEvent.CurrentPoint.Position.Y,
                    StylusId = GuessPointerId(penEvent.CurrentPoint),
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
                DrawToSession(g, _stroke.ToArray());

                // Overdraw any ink that is still drying...
                var drying = _dryingInk.ToArray();
                foreach (var stroke in drying)
                {
                    DrawToSession(g, stroke);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        [NotNull]
        private Quad DrawToSession(CanvasDrawingSession g, DPoint[] strokeToRender)
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
                if (!_penSizes.TryGetValue(pts[0].StylusId, out var size)) { size = 2; }

                var attr = new InkDrawingAttributes{
                    Size = new Size(size,size),
                    Color = color,
                    PenTip = PenTipShape.Circle
                };
                var s = new CoreIncrementalInkStroke(attr, Matrix3x2.Identity);

                for (int i = 0; i < pts.Length; i++)
                {
                    var current = pts[i];
                    s.AppendInkPoints(new[]{
                        new InkPoint(new Point(current.X, current.Y), (float) current.Pressure)
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
                Console.WriteLine(ex);
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