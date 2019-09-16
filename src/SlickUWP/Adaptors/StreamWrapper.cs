using System;
using System.IO;
using Windows.Storage.Streams;
using JetBrains.Annotations;
using SlickCommon.Storage;
using SlickUWP.CrossCutting;

namespace SlickUWP.Adaptors
{
    internal class StreamWrapper :Stream,  IStreamProvider
    {
        private readonly IRandomAccessStream _source;
        [NotNull] private static readonly object _lock = new object();
        [NotNull] private readonly string _path;
        [CanBeNull] private Stream _openStream;

        public StreamWrapper(IRandomAccessStream source)
        {
            _source = source;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            try {
                _openStream = null;
                _source.Dispose();
            } catch (Exception ex) {
                Logging.WriteLogMessage(ex.ToString());
            }
        }

        /// <inheritdoc />
        public Stream Open()
        {
            return this;
        }


        private void EnsureStream()
        {
            if (_openStream == null)
            {
                _openStream = _source.AsStream();
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