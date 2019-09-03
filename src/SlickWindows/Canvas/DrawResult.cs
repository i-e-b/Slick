using System;

namespace SlickWindows.Canvas
{
    [Flags]
    internal enum DrawResult
    {
        /// <summary>
        /// The tile is empty, and could be removed
        /// </summary>
        Empty = 0,

        /// <summary>
        /// Image is not empty
        /// </summary>
        Marked = 1,

        /// <summary>
        /// Draw went over the bounds of this tile
        /// </summary>
        Partial = 2
    }
}