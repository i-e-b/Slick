namespace SlickCommon.Canvas
{
    public enum InkType
    {
        /// <summary>
        /// Existing ink replaced (normal)
        /// </summary>
        Overwrite,

        /// <summary>
        /// Existing ink is re-coloured (highlighter mode)
        /// </summary>
        Multiply,

        /// <summary>
        /// Pixel are being set directly, disable any smoothing or composition
        /// </summary>
        Import
    }
}