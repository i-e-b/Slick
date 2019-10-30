using System;
using System.IO;
using Containers;
using Containers.Types;
using JetBrains.Annotations;
using StreamDb;

namespace SlickCommon.Storage
{
    public class StreamDbStorageContainer : IStorageContainer
    {
        [NotNull] private readonly IStreamProvider _pageFile;
        [NotNull] private readonly Database _db;
        [NotNull] private readonly object _lock = new object();

        private static readonly Exception NotFound = new Exception("The node does not exist");

        public StreamDbStorageContainer([NotNull]IStreamProvider pageFile)
        {
            _pageFile = pageFile;
            _db = Database.TryConnect(_pageFile.Open()) ?? throw new Exception("Failed to open database");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _db.Dispose();
            _pageFile.Dispose();
        }

        /// <inheritdoc />
        public Result<StorageNode> Exists(string path)
        {
            lock (_lock)
            {
                var ok = _db.Get("map/" + path, out var rawStream);

                if (!ok || rawStream == null) return Result<StorageNode>.Failure(NotFound);

                try {
                    var node = StorageNode.FromStream(rawStream);
                    return Result<StorageNode>.Success(node);
                }
                catch (Exception ex) {
                    return Result.Failure<StorageNode>(ex);
                }
            }
        }

        /// <inheritdoc />
        public Result<Nothing> UpdateNode(string path, StorageNode node)
        {
            lock (_lock)
            {
                if (node == null) throw new Exception("Tried to update a tile node with null data");
                _db.WriteDocument("map/" + path, node.ToStream());
                _db.Flush();

                return Result<Nothing>.Success(Nothing.Instance);
            }
        }

        /// <inheritdoc />
        public Result<StorageNode> Store(string path, string type, Stream data)
        {
            lock (_lock)
            {
                var findExisting = Exists(path);

                StorageNode next;

                if (findExisting.IsFailure || findExisting.ResultData == null)
                {
                    next = new StorageNode { CurrentVersion = 1, Id = path, IsDeleted = false };
                }
                else
                {
                    next = findExisting.ResultData;
                    next.CurrentVersion++;
                    next.IsDeleted = false;
                }

                _db.WriteDocument($"file/{path}/{type}/{next.CurrentVersion}", data);
                UpdateNode(path, next);

                if (next.CurrentVersion > 2) // clean up versions more than one old
                {
                    for (int i = next.CurrentVersion - 2; i > 0; i--)
                    {
                        var id = $"file/{path}/{type}/{i}";
                        _db.Delete(id);
                    }
                }
                _db.Flush();

                return Result<StorageNode>.Success(next);
            }
        }

        /// <inheritdoc />
        public Result<InfoPin> SetPin(string path, string description)
        {
            return Result<InfoPin>.Failure("Not implemented yet");//TODO: IMPLEMENT_ME;
        }

        /// <inheritdoc />
        public Result<InfoPin> GetPin(string path)
        {
            return Result<InfoPin>.Failure("Not implemented yet");//TODO: IMPLEMENT_ME;
        }

        /// <inheritdoc />
        public void RemovePin(string id)
        {
            //TODO: IMPLEMENT_ME();
        }

        /// <inheritdoc />
        public Result<InfoPin[]> ReadAllPins()
        {
            return Result<InfoPin[]>.Failure("Not implemented yet");//TODO: IMPLEMENT_ME;
        }

        /// <inheritdoc />
        public Result<Stream> Read(string path, string type, int version)
        {

            lock (_lock)
            {
                var id = $"file/{path}/{type}/{version}";
                var ok = _db.Get(id, out var rawStream);

                if (ok) return Result<Stream>.Success(rawStream);
                return Result<Stream>.Failure(NotFound);
            }
        }

        /// <inheritdoc />
        public Result<Nothing> Delete(string path, string type)
        {
            // Plan:
            // 1. Delete any currently marked tiles
            // 2. Write the deleted flag for this one

            // TODO: IMPLEMENT_ME
            return Result<Nothing>.Success(Nothing.Instance);
        }
    }
}