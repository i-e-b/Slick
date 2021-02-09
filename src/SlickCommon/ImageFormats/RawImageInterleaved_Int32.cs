namespace SlickCommon.ImageFormats
{
    /// <summary>
    /// Container for 4-channel 8888 image data
    /// as an array of Int32
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class RawImageInterleaved_Int32
    {
        public int[] Data;

        public int Width;
        public int Height;
    }
}