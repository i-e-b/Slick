namespace SlickUWP.Canvas
{
    public enum TileState
    {
        /// <summary>
        /// Waiting for data to be loaded
        /// </summary>
        Locked,

        /// <summary>
        /// Has no backing store
        /// </summary>
        Empty,

        /// <summary>
        /// loaded from backing store
        /// </summary>
        Ready,
        
        /// <summary>
        /// Data exists in backing store, but can't be loaded
        /// </summary>
        Corrupted
    }
}