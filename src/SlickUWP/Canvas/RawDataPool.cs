using System.Collections.Generic;
using JetBrains.Annotations;

namespace SlickUWP.Canvas
{
    /// <summary>
    /// Pools raw data arrays to reduce GC time
    /// </summary>
    public static class RawDataPool {
        [NotNull]private static readonly Queue<byte[]> _available = new Queue<byte[]>();
        [NotNull]private static readonly object _lock = new object();

        [NotNull]public static byte[] Capture() {
            lock(_lock){
                if (_available.TryDequeue(out var data)) return data;
            }

            return new byte[CachedTile.ByteSize];
        }

        public static void Release(byte[] data) {
            if (data == null) return;
            lock(_lock) {
                _available.Enqueue(data);
            }
        }
    }
}