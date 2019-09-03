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
    }
}