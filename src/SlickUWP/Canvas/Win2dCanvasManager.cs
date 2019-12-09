using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using JetBrains.Annotations;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace SlickUWP.Canvas
{
    /// <summary>
    /// Manages CanvasControls
    /// </summary>
    /// <remarks>This used to be a pool of controls, but UWP has issues with synchronisation</remarks>
    public static class Win2dCanvasManager {
        [NotNull] private static readonly DrawingHub _drawHub = new DrawingHub();

        /// <summary>
        /// Get an available canvas control.
        /// Should be returned to the pool with `Retire`
        /// </summary>
        [NotNull]
        public static CanvasControlAsyncProxy Employ([NotNull] Panel container, [NotNull]CachedTile cachedTile, double x, double y)
        {
            // Need a new canvas
            var proxy = new CanvasControlAsyncProxy(container);
            var targetTile = cachedTile;
            container.Dispatcher?.RunAsync(CoreDispatcherPriority.High, () =>
            {
                var ctrl = new CanvasControl
                {
                    Margin = new Thickness(0.0),
                    Height = 256,
                    Width = 256,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    UseLayoutRounding = false,
                    RenderTransform = new TranslateTransform
                    {
                        X = x,
                        Y = y
                    }
                };

                proxy.QueueAction(canv =>
                {
                    // We have a single common 'Draw' event hook and use context data to pump in image & state
                    if (canv == null) return;
                    canv.Draw += _drawHub.Draw;
                });
                proxy.AttachToContainer(ctrl, container, targetTile);
            });
            return proxy;
        }

        /// <summary>
        /// Remove a control from display and return it to the pool
        /// </summary>
        public static void Retire(CanvasControlAsyncProxy ctrl)
        {
            if (ctrl == null) return;
            ctrl.RemoveFromContainer();
            ctrl.QueueAction(canv => {
                if (canv != null) canv.Draw -= _drawHub.Draw;
            });
        }
    }
}