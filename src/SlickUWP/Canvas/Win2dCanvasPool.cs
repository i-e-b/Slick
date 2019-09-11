using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml;
using JetBrains.Annotations;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace SlickUWP.Canvas
{
    public static class Win2dCanvasPool {
        [NotNull] private static readonly object _lock = new object();
        [NotNull] private static readonly Queue<CanvasControl> _pool = new Queue<CanvasControl>();

        /// <summary>
        /// Get an available canvas control.
        /// Should be returned to the pool with `Retire`
        /// </summary>
        [NotNull]
        public static CanvasControl Employ()
        {
            lock (_lock)
            {
                if (_pool.TryDequeue(out var result)) {
                    result.ClearColor = Colors.Gold;
                    return result;
                }
            }
            return new CanvasControl
            {
                UseSharedDevice = true,
                Margin = new Thickness(0.0),
                Height = 256,
                Width = 256,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
        }

        public static void Retire(CanvasControl ctrl){
            if (ctrl == null) return;
            lock (_lock)
            {
                ctrl.ClearColor = Colors.Aqua;
                ctrl.Invalidate();
                //ctrl.Visibility = Visibility.Collapsed;
                ctrl.RemoveFromVisualTree();
                _pool.Enqueue(ctrl);
            }
        }

    }
}