using System;
using System.IO;
using Containers;
using Containers.Types;
using JetBrains.Annotations;
using LiteDB;

namespace SlickWindows.Storage
{
    public class LiteDbStorageContainer : IStorageContainer
    {
        private readonly string _pageFilePath;
        [NotNull] private readonly object _storageLock = new object();

        private static readonly Exception NotFound = new Exception("The node does not exist");
        private static readonly Exception NoVersion = new Exception("The listed version is missing");
        private static readonly Exception NoDb = new Exception("Could not connect to DB");


        public LiteDbStorageContainer(string pageFilePath)
        {
            _pageFilePath = pageFilePath;
        }

        /// <inheritdoc />
        public Result<StorageNode> Exists(string path)
        {
            lock (_storageLock)
                using (var db = new LiteDatabase(_pageFilePath))
                {
                    var nodes = db.GetCollection<StorageNode>("map");
                    var node = nodes?.FindById(path);

                    return (node == null || node.IsDeleted)
                        ? Result<StorageNode>.Failure(NotFound)
                        : Result<StorageNode>.Success(node);
                }
        }

        /// <inheritdoc />
        public Result<StorageNode> Store(string path, string type, Stream data)
        {
            lock (_storageLock)
                using (var db = new LiteDatabase(_pageFilePath))
                {
                    var nodes = db.GetCollection<StorageNode>("map");
                    if (nodes == null) return Result<StorageNode>.Failure(NoDb);
                    var node = nodes.FindById(path);


                    if (node == null)
                    {
                        node = new StorageNode { CurrentVersion = 1, Id = path, IsDeleted = false };
                    }
                    else
                    {
                        node.CurrentVersion++;
                        node.IsDeleted = false;
                    }

                    if (node.Id == null) throw new Exception("Loading storage node failed!");

                    if (db.FileStorage == null) return Result<StorageNode>.Failure(NoDb);

                    var id = $"{path}/{type}/{node.CurrentVersion}";
                    db.FileStorage.Upload(id, type, data);
                    nodes.Upsert(path, node);

                    // if version is greater than 2, scan back to delete old versions
                    if (node.CurrentVersion > 2) // clean up versions more than one old
                    {
                        for (int i = node.CurrentVersion - 2; i > 0; i--)
                        {
                            id = $"{path}/{type}/{i}";
                            if (!db.FileStorage.Exists(id)) break;
                            db.FileStorage.Delete(id);
                        }
                    }

                    return Result<StorageNode>.Success(node);
                }
        }

        /// <inheritdoc />
        public Result<Stream> Read(string path, string type, int version)
        {
            lock (_storageLock)
                using (var db = new LiteDatabase(_pageFilePath))
                {
                    var id = $"{path}/{type}/{version}";

                    if (db.FileStorage == null) return Result<Stream>.Failure(NoDb);
                    if (!db.FileStorage.Exists(id)) return Result<Stream>.Failure(NoVersion);

                    var ms = new MemoryStream();
                    db.FileStorage.Download(id, ms);
                    ms.Seek(0, SeekOrigin.Begin);

                    return Result<Stream>.Success(ms);
                }
        }

        /// <inheritdoc />
        public Result<Nothing> Delete(string path, string type)
        {
            // Plan:
            // 1. Delete any currently marked tiles
            // 2. Write the deleted flag for this one
            // 3. Compact the database

            lock (_storageLock)
                using (var db = new LiteDatabase(_pageFilePath))
                {
                    var nodes = db.GetCollection<StorageNode>("map");
                    if (db.FileStorage == null) return Result<Nothing>.Failure(NoDb);
                    if (nodes == null) return Result<Nothing>.Failure(NoDb);
                    nodes.EnsureIndex(n => n.IsDeleted);

                    // Delete any *old* marked files
                    var old = nodes.Find(n=>n.IsDeleted);
                    if (old != null)
                    {
                        foreach (var node in old)
                        {
                            for (int i = node.CurrentVersion; i > 0; i--) // delete files
                            {
                                var id = $"{path}/{type}/{i}";
                                if (!db.FileStorage.Exists(id)) break;
                                db.FileStorage.Delete(id);
                            }
                            nodes.Delete(node.Id); // delete meta data
                        }
                    }

                    // mark the target node
                    var target = nodes.FindById(path);
                    if (target != null)
                    {
                        target.IsDeleted = true;
                        nodes.Upsert(path, target);
                    }

                    // compact the db
                    db.Shrink();

                    return Result.Success(Nothing.Instance);
                }
        }
    }
}