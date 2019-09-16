namespace SlickUWP.Canvas
{
    public interface ICachedTile
    {
        void SetTileData(byte[] packed);
        byte[] GetTileData();
        void SetState(TileState ready);
        void MarkCorrupted();
    }
}