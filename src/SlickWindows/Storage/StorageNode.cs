namespace SlickWindows.Storage
{
    /// <summary>
    /// Storage meta-data for the IStorageContainer
    /// <para></para>
    /// Note: Only get/set properties are stored and retrieved from LiteDB.
    /// </summary>
    public class StorageNode {
        public int CurrentVersion { get; set; }
        public bool IsDeleted { get; set; }
        public string Id { get; set; }
    }

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
    }
}