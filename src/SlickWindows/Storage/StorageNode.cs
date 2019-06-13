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
}