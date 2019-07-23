namespace SlickWindows.Canvas
{
    /// <summary>
    /// Stores a tile position, and a pixel offset inside it.
    /// This is invariant of scale
    /// </summary>
    public class CanvasPixelPosition {
        public PositionKey TilePosition { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        public void GetAbsolute(out double xOffset, out double yOffset)
        {
            xOffset = (TilePosition.X * TileImage.Size) + X;
            yOffset = (TilePosition.Y * TileImage.Size) + Y;
        }
    }
}