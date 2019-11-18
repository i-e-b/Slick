using System.Collections.Generic;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using JetBrains.Annotations;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace SlickUWP.Canvas
{
    /// <summary>
    /// Class that works around threading/async issues in UWP
    /// </summary>
    public class CanvasControlAsyncProxy {
        public delegate void QueueActionDelegate([NotNull]CanvasControl str);

        private Panel _container;
        [CanBeNull] private volatile CanvasControl _ctrl;
        [NotNull]   private readonly Queue<QueueActionDelegate> _commandQueue;

        public CanvasControlAsyncProxy(Panel container)
        {
            _container = container;
            _commandQueue = new Queue<QueueActionDelegate>();
        }

        public void AttachToContainer(CanvasControl result, Panel container, object tag)
        {
            _container = container;
            if (result != null) _ctrl = result;

            // ReSharper disable InconsistentlySynchronizedField
            _container?.Dispatcher?.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                if (_ctrl == null) return;
                _ctrl.Tag = tag;

                if (_container?.Children?.Contains(_ctrl) == true) return;
                _container?.Children?.Add(_ctrl);
                RunWaitingCommands();
            });
            
            // ReSharper restore InconsistentlySynchronizedField
        }

        private void RunWaitingCommands()
        {
            if (_ctrl == null) return;
            lock (_commandQueue)
            {
                _container?.Dispatcher?.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    while (_commandQueue.Count > 0)
                    {
                        _commandQueue.Dequeue()?.Invoke(_ctrl);
                    }

                    _ctrl?.Invalidate();
                    _container.InvalidateArrange();
                });
            }
        }


        public void QueueAction(QueueActionDelegate action)
        {
            lock (_commandQueue)
            {
                _commandQueue.Enqueue(action);
                RunWaitingCommands();
            }
        }

        public void RemoveFromContainer()
        {
            if (_ctrl == null) return;
            // ReSharper disable InconsistentlySynchronizedField
            _container?.Dispatcher?.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                _container?.Children?.Remove(_ctrl);
            });
            // ReSharper restore InconsistentlySynchronizedField
        }
    }
}