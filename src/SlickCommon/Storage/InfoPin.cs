using System.IO;
using JetBrains.Annotations;

namespace SlickCommon.Storage
{
    /// <summary>
    /// A pin that can be placed on a tile.
    /// This supports only one pin per tile, and it pins
    /// the whole tile, not any specific place inside it.
    /// </summary>
    public class InfoPin {
        /// <summary>
        /// matching Tile ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Text of the pin
        /// </summary>
        public string Description { get; set; }

        public static InfoPin Centre()
        {
            return new InfoPin{
                Id = new PositionKey(0,0).ToString(),
                Description = "Page Centre"
            };
        }

        
        /// <summary>
        /// Deserialise from bytes
        /// </summary>
        public static InfoPin FromStream([NotNull]Stream s){
            var r = new BinaryReader(s);
            return new InfoPin{
                Id = r.ReadString(),
                Description = r.ReadString()
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
            w.Write(Description ?? "");

            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }
    }
}