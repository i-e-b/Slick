using System.IO;
using JetBrains.Annotations;
using SlickCommon.Storage;

namespace SlickWindows.Canvas
{
    public class SystemIoFile : IStreamProvider
    {
        [NotNull] private static readonly object _lock = new object();
        [NotNull] private readonly string _path;
        [CanBeNull] private Stream _openStream;


        public SystemIoFile([NotNull]string path)
        {
            _path = path;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            lock(_lock) {
                _openStream?.Dispose();
                _openStream = null;
            }
        }

        /// <inheritdoc />
        public Stream Open()
        {
            lock(_lock) {
                if (_openStream != null) return _openStream;

                _openStream = File.Open(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                return _openStream;
            }
        }
    }
}