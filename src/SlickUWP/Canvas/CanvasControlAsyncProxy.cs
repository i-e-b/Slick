using System.Collections.Generic;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using JetBrains.Annotations;
using Microsoft.Graphics.Canvas.UI.Xaml;
using SlickUWP.CrossCutting;

namespace SlickUWP.Canvas
{
    public class TagContainer {
        public CachedTile Tile { get; set; }
        public bool IsAttached { get; set; }
    }

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

        public void AttachToContainer(CanvasControl result, Panel container, CachedTile tile)
        {
            _container = container;
            if (result != null) _ctrl = result;

            _container?.Dispatcher?.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (_ctrl == null) return;

                if (_ctrl.Tag is TagContainer tag) {
                    tag.IsAttached = true;
                    tag.Tile = tile;
                } else _ctrl.Tag = new TagContainer{ IsAttached = true, Tile = tile};

                if (_container?.Children?.Contains(_ctrl) == true) return;
                _container?.Children?.Add(_ctrl);
                RunWaitingCommands();
            });
            
        }

        private void RunWaitingCommands()
        {
            if (_ctrl == null) return;
            _container?.Dispatcher?.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                lock (_commandQueue)
                {
                    while (_commandQueue.TryDequeue(out var cmd))
                    {
                        cmd.Invoke(_ctrl);
                    }
                }
            });
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
            if (_ctrl == null) { return; } // this happens quite a lot.

            if (_container == null) {
                Logging.WriteLogMessage("Lost container object in `RemoveFromContainer`");
                return;
            }
            if (_container?.Dispatcher == null) {
                Logging.WriteLogMessage("Container had no dispatcher in `RemoveFromContainer`");
                return;
            }

            _container.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (_container == null || _ctrl == null) {
                    Logging.WriteLogMessage("Lost control scope in `RemoveFromContainer` -- dispatch phase");
                    return;
                }
                if (_ctrl.Tag is TagContainer tag) {
                    tag.IsAttached = false;
                    tag.Tile = null;
                }
                
                _container.Children?.Remove(_ctrl);
            });
        }
    }
}