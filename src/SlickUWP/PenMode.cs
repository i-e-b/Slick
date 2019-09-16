namespace SlickUWP
{
    public enum PenMode
    {
        /// <summary>
        /// Standard mode. Pen strokes draw ink (unless forced to move)
        /// </summary>
        Ink,

        /// <summary>
        /// Pen strokes select tiles for further operations
        /// </summary>
        Select,

        /// <summary>
        ///TODO: Pen strokes don't get committed to the dry ink layer (and can be erased in a large block)?
        /// </summary>
        Highlight
    }
}