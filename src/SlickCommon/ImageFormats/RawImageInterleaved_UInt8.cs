using System;
using System.Diagnostics.CodeAnalysis;
using SlickCommon.Canvas;

namespace SlickCommon.ImageFormats
{
    /// <summary>
    /// Container for 4-channel 8888 image data
    /// as an array of bytes
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class RawImageInterleaved_UInt8
    {
        public byte[]? Data;

        public int Width;
        public int Height;

        /// <summary>
        /// Create a cropped copy of this image
        /// </summary>
        public RawImageInterleaved_UInt8 Crop(Quad target) {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (Data == null) throw new InvalidOperationException();
            
            // calculate bounds
            int left = Range(0, target.X, Width);
            int top = Range(0, target.Y, Height);
            int right = Range(0, target.X + target.Width, Width);
            int bottom = Range(0, target.Y + target.Height, Height);

            int newWidth = right - left;
            int newHeight = bottom - top;

            if (newWidth < 1 || newHeight < 1) throw new Exception("Invalid bounds in crop");

            var result = new RawImageInterleaved_UInt8 {
                Data = new byte[newWidth*newHeight*4],
                Width = newWidth,
                Height = newHeight
            };

            // copy data
            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    var src_i = ((y + top) * Width * 4) + ((x + left) * 4);
                    var dst_i = (y * newWidth * 4) + (x * 4);

                    result.Data[dst_i + 0] = Data[src_i + 0];
                    result.Data[dst_i + 1] = Data[src_i + 1];
                    result.Data[dst_i + 2] = Data[src_i + 2];
                    result.Data[dst_i + 3] = Data[src_i + 3];
                }
            }
            return result;
        }

        private static int Range(int lower, double value, int upper)
        {
            if (value < lower) return lower;
            if (value > upper) return upper;
            return (int)value;
        }

        public static RawImageInterleaved_UInt8 CropFromData(byte[] bytes, int originalWidth, int originalHeight, int x, int y, int width, int height)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            // calculate bounds
            int left = Range(0, x, originalWidth);
            int top = Range(0, y, originalHeight);
            int right = Range(0, x + width, originalWidth);
            int bottom = Range(0, y + height, originalHeight);

            int newWidth = right - left;
            int newHeight = bottom - top;

            if (newWidth < 1 || newHeight < 1) throw new Exception("Invalid bounds in crop");

            var result = new RawImageInterleaved_UInt8 {
                Data = new byte[newWidth*newHeight*4],
                Width = newWidth,
                Height = newHeight
            };

            // copy data
            for (int iy = 0; iy < newHeight; iy++)
            {
                for (int ix = 0; ix < newWidth; ix++)
                {
                    var src_i = ((iy + top) * originalWidth * 4) + ((ix + left) * 4);
                    var dst_i = (iy * newWidth * 4) + (ix * 4);

                    result.Data[dst_i + 0] = bytes[src_i + 0];
                    result.Data[dst_i + 1] = bytes[src_i + 1];
                    result.Data[dst_i + 2] = bytes[src_i + 2];
                    result.Data[dst_i + 3] = bytes[src_i + 3];
                }
            }
            return result;
        }
    }
}