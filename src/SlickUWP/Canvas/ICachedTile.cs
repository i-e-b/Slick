namespace SlickUWP.Canvas
{
    public interface ICachedTile
    {
        void EnsureDataReady();
        byte[] GetTileData();
        void SetState(TileState ready);
        void MarkCorrupted();
        void Deallocate();
    }

}