using System;
using System.IO;

namespace SlickCommon.Storage
{
    public interface IStreamProvider : IDisposable {
        /// <summary>
        /// Open and return the stream.
        /// The stream will be closed when this provider is disposed.
        /// </summary>
        Stream Open();
    }
}