using SlickCommon.Storage;

namespace SlickCommon.Canvas
{
    /// <summary>
    /// Stores a tile position, and a pixel offset inside it.
    /// This is invariant of scale
    /// </summary>
    public class CanvasPixelPosition {
        public PositionKey TilePosition { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
    }
}