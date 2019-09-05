using System.Drawing;
using SlickCommon.ImageFormats;
using SlickCommon.Storage;

namespace SlickCommon.Canvas
{
    public interface ITileImage
    {
        int Width { get; }
        int Height { get; }
        PositionKey Position { get; set; }
        bool ImageIsBlank();

        /// <summary>
        /// Clear internal caches. Call this if you change the image data
        /// </summary>
        void Invalidate();

        /// <summary>
        /// Get the raw image data to display this tile
        /// </summary>
        /// <param name="drawScale">Current zoom level (used by Map mode)</param>
        /// <param name="visualScale">DPI scaling</param>
        RawImageInterleaved GetRawImage(byte drawScale, float visualScale);

        /// <summary>
        /// Draw an ink point on this tile. Returns true if the tile contents were changed, false otherwise
        /// </summary>
        bool DrawOnTile(double px, double py, double radius, Color penColor, InkType inkPenType, int drawScale);
    }
}