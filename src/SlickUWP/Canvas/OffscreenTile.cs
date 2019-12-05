namespace SlickUWP.Canvas
{
    public class OffscreenTile : ICachedTile
    {
        private byte[] _data;
        public TileState State;

        /// <inheritdoc />
        public void EnsureDataReady()
        {
            _data = RawImagePool.Capture();
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

        /// <inheritdoc />
        public void Deallocate()
        {
            RawImagePool.Release(_data);
            _data = null;
        }
    }
}