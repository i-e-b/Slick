using System;
using System.IO;
using JetBrains.Annotations;
using SlickCommon.Storage;

namespace SlickWindows.Canvas
{
    public class SystemIoFile : Stream, IStreamProvider
    {
        [NotNull] private static readonly object _lock = new();
        [NotNull] private readonly string _path;
        private FileStream? _openStream;


        public SystemIoFile([NotNull]string path)
        {
            _path = path;
        }
        
        /// <inheritdoc />
        public Stream Open()
        {
            return this;
        }

        /// <inheritdoc />
        public new void Dispose()
        {
            lock(_lock) {
                _openStream?.Dispose();
                _openStream = null;
            }
        }

        private void EnsureStream() {
                if (_openStream == null) {
                
                    _openStream = new FileStream(_path,
                        FileMode.OpenOrCreate,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        0x10000,
                        FileOptions.WriteThrough | FileOptions.RandomAccess);
                }
        }

        /// <inheritdoc />
        public override void Flush()
        {
            lock (_lock)
            {
                EnsureStream();
                if (_openStream == null) throw new Exception("Failed to open file");
                _openStream.Flush();
            }
        }

        /// <inheritdoc />
        public override long Seek(long offset, SeekOrigin origin)
        {
            lock (_lock)
            {
                EnsureStream();
                if (_openStream == null) throw new Exception("Failed to open file");
                return _openStream.Seek(offset, origin);
            }
        }

        /// <inheritdoc />
        public override void SetLength(long value)
        {
            lock (_lock)
            {
                EnsureStream();
                if (_openStream == null) throw new Exception("Failed to open file");
                _openStream.SetLength(value);
            }
        }

        /// <inheritdoc />
        public override int Read(byte[] buffer, int offset, int count)
        {
            lock (_lock)
            {
                EnsureStream();
                if (_openStream == null) throw new Exception("Failed to open file");
                return _openStream.Read(buffer, offset, count);
            }
        }

        /// <inheritdoc />
        public override void Write(byte[] buffer, int offset, int count)
        {
            lock (_lock)
            {
                EnsureStream();
                if (_openStream == null) throw new Exception("Failed to open file");
                _openStream.Write(buffer, offset, count);
            }
        }

        /// <inheritdoc />
        public override bool CanRead
        {
            get
            {

                lock (_lock)
                {
                    EnsureStream();
                    if (_openStream == null) throw new Exception("Failed to open file");
                    return _openStream.CanRead;
                }
            }
        }

        /// <inheritdoc />
        public override bool CanSeek
        {
            get
            {

                lock (_lock)
                {
                    EnsureStream();
                    if (_openStream == null) throw new Exception("Failed to open file");
                    return _openStream.CanSeek;
                }
            }
        }

        /// <inheritdoc />
        public override bool CanWrite
        {
            get
            {

                lock (_lock)
                {
                    EnsureStream();
                    if (_openStream == null) throw new Exception("Failed to open file");
                    return _openStream.CanWrite;
                }
            }
        }

        /// <inheritdoc />
        public override long Length
        {
            get
            {

                lock (_lock)
                {
                    EnsureStream();
                    if (_openStream == null) throw new Exception("Failed to open file");
                    return _openStream.Length;
                }
            }
        }

        /// <inheritdoc />
        public override long Position 
        {
            get
            {
                lock (_lock)
                {
                    EnsureStream();
                    if (_openStream == null) throw new Exception("Failed to open file");
                    return _openStream.Position;
                }
            }
            set {
                lock (_lock)
                {
                    EnsureStream();
                    if (_openStream == null) throw new Exception("Failed to open file");
                    _openStream.Position = value;
                }
            }
        }

    }
}