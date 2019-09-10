namespace SlickUWP.Canvas
{
    public enum TileState
    {
        Locked, // waiting for data to be loaded
        Empty, // has no backing store
        Ready // loaded from backing store
    }
}