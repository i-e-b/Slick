﻿using System;
using System.Collections.Generic;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using JetBrains.Annotations;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace SlickUWP.Canvas
{
    public static class Win2dCanvasPool {
        [NotNull] private static readonly object _lock = new object();
        [NotNull] private static readonly Queue<CanvasControlAsyncProxy> _pool = new Queue<CanvasControlAsyncProxy>();
        [NotNull] private static readonly DrawingHub _drawHub = new DrawingHub();

        /// <summary>
        /// Get an available canvas control.
        /// Should be returned to the pool with `Retire`
        /// </summary>
        [NotNull]
        public static CanvasControlAsyncProxy Employ([NotNull] Panel container, [NotNull]CachedTile cachedTile)
        {
            var dispatcher = container.Dispatcher ?? throw new Exception("Container panel had no valid dispatcher");
            
            lock (_lock)
            {
                if (_pool.TryDequeue(out var result))
                {
                    result.QueueAction(canv =>
                    {
                        canv.Tag = cachedTile;
                        canv.Visibility = Visibility.Visible;
                        canv.Opacity = 1;
                        canv.Invalidate();
                        canv.Draw += _drawHub.Draw;
                    });
                    result.AttachToContainer(container);
                    return result;
                }
            }

            // Need a new canvas
            var proxy = new CanvasControlAsyncProxy(container);
            var tileToUse = cachedTile;
            dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var ctrl = new CanvasControl
                {
                    //UseSharedDevice = true,
                    CacheMode = new BitmapCache(),
                    Margin = new Thickness(0.0),
                    Height = 256,
                    Width = 256,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Tag = tileToUse
                    // We have a single common 'Draw' event hook and use context data to pump in image & state
                };

                ctrl.Draw += _drawHub.Draw;

                proxy.Set(ctrl);
                proxy.AttachToContainer(container);
                ctrl.Invalidate();
            });

            return proxy;
        }

        /// <summary>
        /// Remove a control from display and return it to the pool
        /// </summary>
        public static void Retire(CanvasControlAsyncProxy ctrl)
        {
            if (ctrl == null) return;
            lock (_lock)
            {
                _pool.Enqueue(ctrl);
            }
            ctrl.QueueAction(canv =>
            {
                canv.Draw -= _drawHub.Draw;
                canv.Visibility = Visibility.Collapsed;
                canv.Opacity = 0.0;
                //canv.Tag = null;
            });
            ctrl.RemoveFromContainer();
        }

        /// <summary>
        /// Call this once your changes are stable, to ensure UWP doesn't screw up your visibility states.
        /// </summary>
        public static void Sanitise() {

            lock (_lock)
            {
                var retired = _pool.ToArray();
                foreach (var control in retired)
                {
                    control.QueueAction(canv =>
                    {
                        canv.Visibility = Visibility.Collapsed;
                        //canv.Tag = null;
                        canv.Invalidate();
                    });
                }
            }
        }

    }
}