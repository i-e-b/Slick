﻿using System;
using System.IO;
using System.Linq;
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
        [NotNull]private readonly LiteDatabase _db;


        public LiteDbStorageContainer(string pageFilePath)
        {
            _pageFilePath = pageFilePath;
            _db = new LiteDatabase(_pageFilePath);

            var nodes = _db.GetCollection<StorageNode>("map");
            nodes?.EnsureIndex("_id", unique: true);
        }

        /// <inheritdoc />
        public Result<StorageNode> Exists(string path)
        {
            var nodes = _db.GetCollection<StorageNode>("map");
            var node = nodes?.Find(Query.In("_id", path), skip: 0, limit: 1)?.SingleOrDefault();

            return (node == null || node.IsDeleted)
                ? Result<StorageNode>.Failure(NotFound)
                : Result<StorageNode>.Success(node);
        }

        /// <inheritdoc />
        public Result<Stream> Read(string path, string type, int version)
        {
            var id = $"{path}/{type}/{version}";

            if (_db.FileStorage == null) return Result<Stream>.Failure(NoDb);

            var file = _db.FileStorage.Find(id)?.FirstOrDefault();
            if (file == null) return Result<Stream>.Failure(NoVersion);

            var ms = new MemoryStream();
            file.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);

            return Result<Stream>.Success(ms);
        }

        /// <inheritdoc />
        public Result<Nothing> UpdateNode(string path, StorageNode node)
        {
            lock (_storageLock)
            {
                var nodes = _db.GetCollection<StorageNode>("map");
                if (nodes == null) return Result<Nothing>.Failure(NoDb);
                nodes.Upsert(path, node);
                return Result<Nothing>.Success(Nothing.Instance);
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
                    db.FileStorage.Delete(id); // make sure it's unique
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
        public Result<InfoPin> SetPin(string path, string description)
        {
            lock (_storageLock)
            {
                var pins = _db.GetCollection<InfoPin>("pins");
                if (pins == null) return Result<InfoPin>.Failure(NoDb);
                var pin = new InfoPin { Id = path, Description = description };
                pins.Upsert(path, pin);
                return Result<InfoPin>.Success(pin);
            }
        }

        /// <inheritdoc />
        public Result<InfoPin> GetPin(string path)
        {
            lock (_storageLock)
            {
                var pins = _db.GetCollection<InfoPin>("pins");
                var pin = pins?.FindById(path);

                return (pin == null)
                    ? Result<InfoPin>.Failure(NotFound)
                    : Result<InfoPin>.Success(pin);
            }
        }

        /// <inheritdoc />
        public void RemovePin(string id)
        {
            lock (_storageLock)
            {
                var pins = _db.GetCollection<InfoPin>("pins");
                pins?.Delete(id);
            }
        }

        /// <inheritdoc />
        public Result<InfoPin[]> ReadAllPins()
        {
            lock (_storageLock)
            {
                var pins = _db.GetCollection<InfoPin>("pins");
                var allPins = pins?.FindAll()?.ToArray();

                return (allPins == null)
                    ? Result<InfoPin[]>.Failure(NotFound)
                    : Result<InfoPin[]>.Success(allPins);
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
            {
                var nodes = _db.GetCollection<StorageNode>("map");
                if (_db.FileStorage == null) return Result<Nothing>.Failure(NoDb);
                if (nodes == null) return Result<Nothing>.Failure(NoDb);
                nodes.EnsureIndex(n => n.IsDeleted);

                // Delete any *old* marked files
                var old = nodes.Find(n => n.IsDeleted);
                if (old != null)
                {
                    foreach (var node in old)
                    {
                        for (int i = node.CurrentVersion; i > 0; i--) // delete files
                        {
                            var id = $"{path}/{type}/{i}";
                            if (!_db.FileStorage.Exists(id)) break;
                            _db.FileStorage.Delete(id);
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
                _db.Shrink();

                return Result.Success(Nothing.Instance);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _db.Dispose();
        }
    }
}