namespace SlickUWP.Canvas
{
    public class OffscreenTile : ICachedTile
    {
        private byte[] _data;
        public TileState State;

        /// <inheritdoc />
        public void SetTileData(byte[] packed)
        {
            _data = packed;
        }

        /// <inheritdoc />
        public byte[] GetTileData()
        {
            return _data;
        }

        /// <inheritdoc />
        public void SetState(TileState state)
        {
            State = state;
        }

        /// <inheritdoc />
        public void MarkCorrupted()
        {
            State = TileState.Corrupted;
        }
    }
}