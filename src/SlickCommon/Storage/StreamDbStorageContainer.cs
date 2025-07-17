using System;
using System.Collections.Generic;
using System.IO;
using Containers;
using Containers.Types;
using StreamDb;

namespace SlickCommon.Storage
{
    public class StreamDbStorageContainer : IStorageContainer
    {
        private readonly    IStreamProvider _pageFile;
        private readonly Database        _db;

        // ReSharper disable InconsistentNaming
        private static readonly Exception NotFound = new Exception("The node does not exist");
        // ReSharper restore InconsistentNaming

        public StreamDbStorageContainer(IStreamProvider pageFile)
        {
            _pageFile = pageFile;
            //Database.SetQuickAndDirtyMode(); // only if we hit performance issues.
            _db = Database.TryConnect(_pageFile.Open()) ?? throw new Exception("Failed to open database");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _db.Dispose();
            _pageFile.Dispose();
        }

        /// <inheritdoc />
        public string DisplayName() { return "StreamDB"; }

        /// <inheritdoc />
        public Result<StorageNode> Exists(string path)
        {
            var ok = _db.Get("map/" + path, out var rawStream);

            if (!ok || rawStream == null) return Result<StorageNode>.Failure(NotFound);

            try
            {
                var node = StorageNode.FromStream(rawStream);
                if (node.IsDeleted) return Result<StorageNode>.Failure(NotFound);
                return Result<StorageNode>.Success(node);
            }
            catch (Exception ex)
            {
                return Result.Failure<StorageNode>(ex);
            }
        }

        /// <inheritdoc />
        public Result<Nothing> UpdateNode(string path, StorageNode node)
        {
            if (node == null) throw new Exception("Tried to update a tile node with null data");
            _db.WriteDocument("map/" + path, node.ToStream());
            _db.Flush();

            return Result<Nothing>.Success(Nothing.Instance);
        }


        /// <inheritdoc />
        public Result<InfoPin> SetPin(string path, string description)
        {
            var pin = new InfoPin { Id = path, Description = description };

            _db.WriteDocument($"pins/{path}", pin.ToStream());
            return Result<InfoPin>.Success(pin);
        }

        /// <inheritdoc />
        public Result<InfoPin> GetPin(string path)
        {
            var ok = _db.Get($"pins/{path}", out var rawStream);

            return (ok && rawStream != null)
                ? Result<InfoPin>.Success(InfoPin.FromStream(rawStream))
                : Result<InfoPin>.Failure(NotFound);
        }

        /// <inheritdoc />
        public void RemovePin(string path)
        {
            _db.Delete($"pins/{path}");
        }

        /// <inheritdoc />
        public Result<InfoPin[]> ReadAllPins()
        {
            var pinPaths = _db.Search("pins/");

            var result = new List<InfoPin>();
            foreach (var path in pinPaths)
            {
                var ok = _db.Get(path, out var rawStream);
                if (ok && rawStream != null) {
                    result.Add(InfoPin.FromStream(rawStream));
                }
            }

            return Result<InfoPin[]>.Success(result.ToArray());
        }

        /// <inheritdoc />
        public Result<Stream> Read(string path, string type, int version)
        {
            var id = $"file/{path}/{type}/{version}";
            var ok = _db.Get(id, out var rawStream);

            if (ok) return Result<Stream>.Success(rawStream);
            return Result<Stream>.Failure(NotFound);
        }
        
        /// <inheritdoc />
        public Result<StorageNode> Store(string path, string type, Stream data)
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

        /// <inheritdoc />
        public Result<Nothing> Delete(string path, string type)
        {
            // This is called when a tile is erased to white, or when a version becomes deprecated

            // Plan:
            // 1. Delete any currently marked tiles
            // 2. Write the deleted flag for this one

            // With stream-db, we use the ability to put single documents under multiple paths.
            

            var node = Exists(path);
            if (node.IsFailure || node.ResultData == null) return Result<Nothing>.Success(Nothing.Instance);
            
            // update metadata to mark this deleted
            var data = node.ResultData;
            data.IsDeleted = true;
            _db.WriteDocument("map/" + path, data.ToStream());
            _db.Flush();

            // Wipe out anything in our garbage pile
            var found = _db.Search("garbage/");
            foreach (var junkPath in found)
            {
                _db.Delete(junkPath); // should also delete the root document and all other paths
            }

            // bind the new file into the garbage pile
            var currentVersion =  node.ResultData.CurrentVersion;
            var ok = _db.GetIdByPath("map/" + path, out var id);
            if (ok) {
                _db.BindToPath(id, $"garbage/{path}/{type}/{currentVersion}");
            }
            
            return Result<Nothing>.Success(Nothing.Instance);
        }
    }
}