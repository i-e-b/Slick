using System;
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
        private Panel _container;
        [CanBeNull] private volatile CanvasControl _ctrl;
        [NotNull]   private readonly Queue<Action<CanvasControl>> _commandQueue;

        public CanvasControlAsyncProxy(Panel container)
        {
            _container = container;
            _commandQueue = new Queue<Action<CanvasControl>>();
        }

        public void AttachToContainer(Panel container)
        {
            _container = container;
            // ReSharper disable InconsistentlySynchronizedField
            _container?.Dispatcher?.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (_container?.Children?.Contains(_ctrl) == true) return;
                _container?.Children?.Add(_ctrl);
            });
            // ReSharper restore InconsistentlySynchronizedField
        }

        public void Set(CanvasControl result)
        {
            _ctrl = result;
            RunWaitingCommands();
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

        public void QueueAction(Action<CanvasControl> action)
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