namespace SlickUWP.Canvas
{
    internal enum InteractionMode
    {
        /// <summary>
        /// Input should be ignored
        /// </summary>
        None,
        
        /// <summary>
        /// Input should be treated as pen strokes
        /// </summary>
        Draw,
        
        /// <summary>
        /// Input should be treated as canvas movement
        /// </summary>
        Move,

        /// <summary>
        /// Tiles should be selected for export
        /// </summary>
        SelectTiles,

        /// <summary>
        /// Input should be passed to palette screen
        /// </summary>
        PalettePicker
    }
}