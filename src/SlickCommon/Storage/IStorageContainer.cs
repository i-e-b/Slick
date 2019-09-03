using System;
using System.IO;
using Containers;
using Containers.Types;

namespace SlickCommon.Storage
{
    public interface IStorageContainer: IDisposable
    {
        /// <summary>
        /// Check if there is a storage node at the given path
        /// </summary>
        Result<StorageNode> Exists(string path);

        /// <summary>
        /// Directly update storage metadata without changing stored data
        /// </summary>
        Result<Nothing> UpdateNode(string path, StorageNode node);

        /// <summary>
        /// Write data to a storage path. Existing data is updated, or a new node created
        /// </summary>
        Result<StorageNode> Store(string path, string type, Stream data);

        /// <summary>
        /// Add a pin to a location
        /// </summary>
        Result<InfoPin> SetPin(string path, string description);

        /// <summary>
        /// Read a pin from a location, if there is one
        /// </summary>
        Result<InfoPin> GetPin(string path);

        /// <summary>
        /// Remove a pin by Id
        /// </summary>
        void RemovePin(string id);

        /// <summary>
        /// List all pins in the storage
        /// </summary>
        Result<InfoPin[]> ReadAllPins();

        /// <summary>
        /// Read data from a storage path
        /// </summary>
        Result<Stream> Read(string path, string type, int version);

        /// <summary>
        /// Mark a storage node for removal.
        /// It will be deleted when the container sees fit (to allow for undo)
        /// </summary>
        Result<Nothing> Delete(string path, string type);
    }
}