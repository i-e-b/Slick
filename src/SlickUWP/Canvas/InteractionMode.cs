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
        /// Input should be passed to palette screen
        /// </summary>
        PalettePicker,

        /// <summary>
        /// Input consists of multiple points, and should scale the canvas
        /// </summary>
        PinchScale,

        /// <summary>
        /// Input should be used to select tiles
        /// </summary>
        SelectTiles
    }
}