using System.IO;
using JetBrains.Annotations;

namespace SlickCommon.Storage
{
    /// <summary>
    /// Storage meta-data for the IStorageContainer
    /// <para></para>
    /// Note: Only get/set properties are stored and retrieved from LiteDB.
    /// </summary>
    public class StorageNode {
        public string Id { get; set; }
        public int CurrentVersion { get; set; }
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Deserialise from bytes
        /// </summary>
        public static StorageNode FromStream([NotNull]Stream s){
            var r = new BinaryReader(s);
            return new StorageNode{
                Id = r.ReadString(),
                CurrentVersion = r.ReadInt32(),
                IsDeleted = r.ReadBoolean()
            };
        }

        /// <summary>
        /// Serialise to bytes
        /// </summary>
        /// <returns></returns>
        public Stream ToStream() {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);

            w.Write(Id ?? "");
            w.Write(CurrentVersion);
            w.Write(IsDeleted);

            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }
    }
}