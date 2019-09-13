using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using JetBrains.Annotations;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace SlickUWP.Canvas
{
    /// <summary>
    /// A single hook point for drawing onto tiles
    /// </summary>
    public class DrawingHub {
        public void Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var g = args?.DrawingSession;
            if (sender == null || g == null) return;

            var tile = sender.Tag as CachedTile; // this is set in CachedTile

            if (tile == null) { // this shouldn't ever be visible
                g.DrawCircle(new Vector2(10, 10), 8, Colors.Blue);
                g.Flush();
                return;
            }

            tile.DrawToSession(sender, g);
        }
    }


    public static class Win2dCanvasPool {
        [NotNull] private static readonly object _lock = new object();
        [NotNull] private static readonly Queue<CanvasControl> _pool = new Queue<CanvasControl>();
        [NotNull] private static readonly DrawingHub _drawHub = new DrawingHub();

        /// <summary>
        /// Get an available canvas control.
        /// Should be returned to the pool with `Retire`
        /// </summary>
        /// <param name="container"></param>
        /// <param name="cachedTile"></param>
        [NotNull]
        public static CanvasControl Employ([NotNull] Panel container, [NotNull]CachedTile cachedTile)
        {
            var dispatcher = container.Dispatcher ?? throw new Exception("Container panel had no valid dispatcher");

            lock (_lock)
            {
                if (_pool.TryDequeue(out var result)) {
                    dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        result.Tag = cachedTile;
                        result.Invalidate();
                        result.Visibility = Visibility.Visible;
                    });
                    return result;
                }
            }

            // Need a new canvas
            var ctrl = new CanvasControl
            {
                UseSharedDevice = true,
                Margin = new Thickness(0.0),
                Height = 256,
                Width = 256,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                // We have a single common 'Draw' event hook and use context data to pump in image & state
            };


            ctrl.Draw += _drawHub.Draw;
            ctrl.Invalidate();

            dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { 
                container.Children?.Add(ctrl);
                ctrl.Tag = cachedTile;
            });

            return ctrl;
        }

        public static void Retire(CanvasControl ctrl){
            if (ctrl == null) return;
            var dispatcher = ctrl.Dispatcher ?? throw new Exception("Container panel had no valid dispatcher");

            lock (_lock)
            {
                _pool.Enqueue(ctrl);
            }

            dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                ctrl.Tag = null;
                ctrl.Visibility = Visibility.Collapsed;
            });
        }

    }
}