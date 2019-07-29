using System;
using System.IO;
using System.IO.Compression;
using JetBrains.Annotations;
using SlickWindows.Canvas;

namespace SlickWindows.ImageFormats
{

    /// <summary>
    /// A low-loss image compression format.
    /// It gives nearly the same quality as PNG at lower sizes.
    /// This also allows us to load scaled-down versions of images at reduced processing / loading costs.
    /// </summary>
    public class WaveletCompress
    {
        // This set of coefficients will be used for new and edited tiles
        [NotNull] public static readonly double[] StandardYQuants = { 2, 1 };
        [NotNull] public static readonly double[] StandardCQuants = { 4, 2, 1 };

        [NotNull]
        public static InterleavedFile Compress([NotNull]TileImage img)
        {
            RGBPlanes_To_YuvPlanes_ForcePower2(img.Red, img.Green, img.Blue, img.Width, img.Height,
                out var Y, out var U, out var V,
                out var width, out var height);

            return WaveletDecomposePlanar2(Y, U, V, width, height, img.Width, img.Height);
        }

        public static void Decompress([NotNull]InterleavedFile file, [NotNull]TileImage target, byte scale)
        {
            var pwidth = WaveletRestorePlanar2(file, scale, out var Y, out var U, out var V);
            YuvPlanes_To_RgbPlanes(file.Version, Y, U, V, pwidth, pwidth, pwidth, target.Red, target.Green, target.Blue);
            target.Invalidate();
        }

        // This controls the overall size and quality of the output
        private static void QuantisePlanar2([NotNull]float[] buffer, int ch, int packedLength, QuantiseType mode, [NotNull]double[] fYs, [NotNull]double[] fCs)
        {
            if (packedLength < buffer.Length) packedLength = buffer.Length;
            // Planar two splits in half, starting with top/bottom, and alternating between
            // vertical and horizontal

            // Fibonacci coding strongly prefers small numbers
            var rounds = (int)Math.Log(packedLength, 2);
            for (int r = 0; r < rounds; r++)
            {
                var factors = (ch == 0) ? fYs : fCs;
                float factor = (float)((r >= factors.Length) ? factors[factors.Length - 1] : factors[r]);
                if (mode == QuantiseType.Reduce) factor = 1 / factor;
                
                var len = packedLength >> r;
                
                // handle scale reductions:
                if (len >> 1 >= buffer.Length) continue;

                // expand co-efficients
                if (len >= buffer.Length) len = buffer.Length - 1;
                for (int i = len >> 1; i < len; i++)
                {
                    buffer[i] *= factor;
                }
            }
        }


        /// <summary>
        /// Compress an image to a byte stream
        /// </summary>
        /// <param name="Y">Luminence plane</param>
        /// <param name="U">color plane</param>
        /// <param name="V">color plane</param>
        /// <param name="planeWidth">Width of the YUV planes, in samples. This must be a power-of-two</param>
        /// <param name="planeHeight">Height of the YUV planes, in samples. This must be a power-of-two</param>
        /// <param name="imgWidth">Width of the image region of interest. This must be less-or-equal to the plane width. Does not need to be a power of two</param>
        /// <param name="imgHeight">Height of the image region of interest. This must be less-or-equal to the plane height. Does not need to be a power of two</param>
        [NotNull]
        private static InterleavedFile WaveletDecomposePlanar2([NotNull]float[] Y, [NotNull]float[] U, [NotNull]float[] V, int planeWidth, int planeHeight, int imgWidth, int imgHeight)
        {
            int rounds = (int)Math.Log(planeWidth, 2);

            var p2Height = (int)NextPow2((uint)planeHeight);
            var p2Width = (int)NextPow2((uint)planeWidth);
            var hx = new float[p2Height];
            var wx = new float[p2Width];

            var msY = new MemoryStream();
            var msU = new MemoryStream();
            var msV = new MemoryStream();

            for (int ch = 0; ch < 3; ch++)
            {
                var buffer = Pick(ch, Y, U, V);
                var ms = Pick(ch, msY, msU, msV);

                // DC to AC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] -= 127.5f; }

                // Transform
                for (int i = 0; i < rounds; i++)
                {
                    var height = p2Height >> i;
                    var width = p2Width >> i;

                    // Wavelet decompose vertical
                    for (int x = 0; x < width; x++) // each column
                    {
                        CDF.Fwt97(buffer, hx, height, x, planeWidth);
                    }

                    // Wavelet decompose HALF horizontal
                    for (int y = 0; y < height / 2; y++) // each row
                    {
                        CDF.Fwt97(buffer, wx, width, y * planeWidth, 1);
                    }
                }

                // Reorder, Quantise and reduce co-efficients
                var packedLength = ToStorageOrder2D(buffer, planeWidth, planeHeight, rounds, imgWidth, imgHeight);
                QuantisePlanar2(buffer, ch, packedLength, QuantiseType.Reduce, StandardYQuants, StandardCQuants);

                // Write output
                using (var tmp = new MemoryStream(buffer.Length))
                {   // byte-by-byte writing to DeflateStream is *very* slow, so we buffer
                    DataEncoding.FibonacciEncode(buffer, 0, tmp);
                    using (var gs = new DeflateStream(ms, CompressionLevel.Optimal, true))
                    {
                        tmp.WriteTo(gs);
                        gs.Flush();
                    }
                }
            }

            // interleave the files:
            msY.Seek(0, SeekOrigin.Begin);
            msU.Seek(0, SeekOrigin.Begin);
            msV.Seek(0, SeekOrigin.Begin);
            var container = new InterleavedFile((ushort)imgWidth, (ushort)imgHeight, 1, StandardYQuants, StandardCQuants,
                msY.ToArray(), msU.ToArray(), msV.ToArray());

            msY.Dispose();
            msU.Dispose();
            msV.Dispose();
            return container;
        }

        public static int WaveletRestorePlanar2([NotNull]InterleavedFile container, byte scale, out float[] Y, out float[] U, out float[] V)
        {
            var Ybytes = container.Planes[0];
            var Ubytes = container.Planes[1];
            var Vbytes = container.Planes[2];

            var yQuants = container.QuantiserSettings_Y ?? StandardYQuants;
            var cQuants = container.QuantiserSettings_C ?? StandardCQuants;

            if (Ybytes == null || Ubytes == null || Vbytes == null) throw new NullReferenceException("Planes were not read from image correctly");

            int imgWidth = container.Width;
            int imgHeight = container.Height;

            // the original image source's internal buffer size
            var packedLength = NextPow2(imgWidth) * NextPow2(imgHeight);

            // scale by a power of 2
            if (scale < 1) scale = 1;
            var scaleShift = scale - 1;
            if (scale > 1)
            {
                imgWidth >>= scaleShift;
                imgHeight >>= scaleShift;
            }
            var planeWidth = NextPow2(imgWidth);
            var planeHeight = NextPow2(imgHeight);

            var sampleCount = planeHeight * planeWidth;

            Y = new float[sampleCount];
            U = new float[sampleCount];
            V = new float[sampleCount];

            var hx = new float[planeHeight];
            var wx = new float[planeWidth];

            int rounds = (int)Math.Log(planeWidth, 2);

            for (int ch = 0; ch < 3; ch++)
            {

                var buffer = Pick(ch, Y, U, V);
                var storedData = new MemoryStream(Pick(ch, Ybytes, Ubytes, Vbytes));

                using (var gs = new DeflateStream(storedData, CompressionMode.Decompress))
                {
                    var ms = new MemoryStream(sampleCount);
                    gs.CopyTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    DataEncoding.FibonacciDecode(ms, buffer);
                }

                // Re-expand co-efficients
                QuantisePlanar2(buffer, ch, packedLength, QuantiseType.Expand, yQuants, cQuants);
                FromStorageOrder2D(buffer, planeWidth, planeHeight, rounds, imgWidth, imgHeight, scaleShift);

                // Restore
                for (int i = rounds - 1; i >= 0; i--)
                {
                    var height = planeHeight >> i;
                    var width = planeWidth >> i;

                    // Wavelet restore HALF horizontal
                    for (int y = 0; y < height / 2; y++) // each row
                    {
                        CDF.Iwt97(buffer, wx, width, y * planeWidth, 1);
                    }

                    // Wavelet restore vertical
                    for (int x = 0; x < width; x++) // each column
                    {
                        CDF.Iwt97(buffer, hx, height, x, planeWidth);
                    }
                }

                // AC to DC
                for (int i = 0; i < buffer.Length; i++) { buffer[i] += 127.5f; }
            }
            return planeWidth;
        }

        /// <summary>
        /// Return the smallest number that is a power-of-two
        /// greater than or equal to the input
        /// </summary>
        public static uint NextPow2(uint c)
        {
            c--;
            c |= c >> 1;
            c |= c >> 2;
            c |= c >> 4;
            c |= c >> 8;
            c |= c >> 16;
            return ++c;
        }

        private static int NextPow2(int c) => (int)NextPow2((uint)c);

        [NotNull] private static T Pick<T>(int i, [NotNull][ItemNotNull] params T[] opts) => opts[i];

        /// <summary>
        /// Restore image byte order from storage format to image format
        /// </summary>
        public static void FromStorageOrder2D([NotNull]float[] buffer, int srcWidth, int srcHeight, int rounds, int imgWidth, int imgHeight, int scale = 0)
        {
            var storage = new float[buffer.Length];

            // Do like the CDF reductions, but put all depths together before the next scale.
            int incrPos = 0;

            // first, any unreduced value
            var height = srcWidth >> rounds;
            var width = srcWidth >> rounds;

            for (int y = 0; y < height; y++)
            {
                var yo = y * srcWidth;
                for (int x = 0; x < width; x++)
                {
                    storage[yo + x] = buffer[incrPos++];
                }
            }

            var lowerDiff = (srcHeight - imgHeight) / 2;
            var eastDiff = (srcWidth - imgWidth) / 2;

            // prevent over-reading on non-power-two images:
            // this knocks-out the last two co-efficient blocks
            var limit = (imgHeight / 2) * (imgWidth / 2);
            if (scale < 1) limit = buffer.Length;


            // now the reduced coefficients in order from most to least significant
            for (int i = rounds - 1; i >= 0; i--)
            {
                height = srcHeight >> i;
                width = srcWidth >> i;
                var left = width >> 1;
                var top = height >> 1;
                var right = width - (eastDiff >> i);
                var lowerEnd = height - (lowerDiff >> i);
                var eastEnd = top - (lowerDiff >> i);

                if (incrPos > limit) { break; }

                // vertical block
                // from top to the height of the horz block,
                // from left=(right most of prev) to right
                for (int x = left; x < right; x++) // each column
                {
                    for (int y = 0; y < eastEnd ;y++)
                    {
                        var yo = y * srcWidth;
                        storage[yo + x] = buffer[incrPos++];
                    }
                }

                // horizontal block
                for (int y = top; y < lowerEnd; y++) // each row
                {
                    var yo = y * srcWidth;
                    for (int x = 0; x < right; x++)
                    {
                        storage[yo + x] = buffer[incrPos++];
                    }
                }
            }

            // copy back to buffer
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = storage[i];
            }
        }

        /// <summary>
        /// Pack the image coefficients into an order that is good for progressive loading and compression
        /// Returns total number of samples used (packed into lower range)
        /// </summary>
        public static int ToStorageOrder2D([NotNull]float[] buffer, int srcWidth, int srcHeight, int rounds, int imgWidth, int imgHeight)
        {
            var storage = new float[buffer.Length];

            // midpoint(top) to lower;
            // lower is (bottom -  (srcHeight-imgHeight)/2)

            // Do like the CDF reductions, but put all depths together before the next scale.
            int incrPos = 0;

            // first, any unreduced value
            var height = srcWidth >> rounds;
            var width = srcWidth >> rounds;

            for (int y = 0; y < height; y++)
            {
                var yo = y * srcWidth;
                for (int x = 0; x < width; x++)
                {
                    storage[incrPos++] = buffer[yo + x];
                }
            }

            var lowerDiff = (srcHeight - imgHeight) / 2;
            var eastDiff = (srcWidth - imgWidth) / 2;

            // now the reduced coefficients in order from most to least significant
            for (int i = rounds - 1; i >= 0; i--)
            {
                height = srcHeight >> i;
                width = srcWidth >> i;
                var left = width >> 1;
                var top = height >> 1;
                var right = width - (eastDiff >> i);
                var lowerEnd = height - (lowerDiff >> i);
                var eastEnd = top - (lowerDiff >> i);

                // vertical block
                // from top to the height of the horz block,
                // from left=(right most of prev) to right
                for (int x = left; x < right; x++) // each column
                {
                    for (int y = 0; y < eastEnd; y++)
                    {
                        var yo = y * srcWidth;
                        storage[incrPos++] = buffer[yo + x];
                    }
                }

                // horizontal block
                for (int y = top; y < lowerEnd; y++) // each row
                {
                    var yo = y * srcWidth;
                    for (int x = 0; x < right; x++)
                    {
                        storage[incrPos++] = buffer[yo + x];
                    }
                }
            }

            // copy back to buffer
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = storage[i];
            }
            return incrPos;
        }

        
        public static void YUV_To_RGB_Old(float Y, float U, float V, out byte R, out byte G, out byte B)
        {
            unchecked
            {
                if (Y > 220) // threshold to white (used by an old version of the image format)
                {
                    R = 255; G = 255; B = 255;
                    return;
                }

                R = Clip(1.164f * (Y - 16) + 0.0f * (U - 127.5f) + 1.596f * (V - 127.5f));
                G = Clip(1.164f * (Y - 16) + -0.392f * (U - 127.5f) + -0.813f * (V - 127.5f));
                B = Clip(1.164f * (Y - 16) + 2.017f * (U - 127.5f) + 0.0f * (V - 127.5f));
            }
        }

        public static void YUV_To_RGB(float Y, float U, float V, out byte R, out byte G, out byte B)
        {
            unchecked
            {
                R = Clip(1.164f * (Y - 16) + 0.0f * (U - 127.5f) + 1.596f * (V - 127.5f));
                G = Clip(1.164f * (Y - 16) + -0.392f * (U - 127.5f) + -0.813f * (V - 127.5f));
                B = Clip(1.164f * (Y - 16) + 2.017f * (U - 127.5f) + 0.0f * (V - 127.5f));
            }
        }

        private static byte Clip(float s)
        {
            if (s > 255.0f) return 255; // we threshold bright colors to white
            if (s < 0.0f) return 0;
            return (byte)s;
        }

        private static void RGB_To_YUV(byte R, byte G, byte B, out float Y, out float U, out float V)
        {
            if (R >= 254 && G >= 254 && B >= 254) {
                Y = 280; U = 127.5f; V = 127.5f; // treat white specially (set Y very high)
                return;
            }

            Y = 16f + (0.257f * R + 0.504f * G + 0.098f * B);
            U = 127.5f + (-0.148f * R + -0.291f * G + 0.439f * B);
            V = 127.5f + (0.439f * R + -0.368f * G + -0.071f * B);
        }

        public static void YuvPlanes_To_RgbPlanes(int version, [NotNull]float[] Y, [NotNull]float[] U, [NotNull]float[] V,
            int srcWidth, int dstWidth, int dstHeight,
             [NotNull]byte[] Red, [NotNull]byte[] Green, [NotNull]byte[] Blue)
        {
            int stride = srcWidth;

            for (int y = 0; y < dstHeight; y++)
            {
                var dst_yo = stride * y;
                var src_yo = srcWidth * y;
                for (int x = 0; x < dstWidth; x++)
                {
                    var src_i = src_yo + x;
                    var dst_i = dst_yo + x;
                    byte r, g, b;
                    if (version < 2) {
                        YUV_To_RGB_Old(Y[src_i], U[src_i], V[src_i], out  r, out  g, out  b);
                    } else {
                        YUV_To_RGB(Y[src_i], U[src_i], V[src_i], out  r, out  g, out  b);
                    }
                    Red[dst_i] = r;
                    Green[dst_i] = g;
                    Blue[dst_i] = b;
                }
            }
        }

        // This handles non-power-two input sizes
        public static void RGBPlanes_To_YuvPlanes_ForcePower2(
            [NotNull]byte[] Red, [NotNull]byte[] Green, [NotNull]byte[] Blue,
            int srcWidth, int srcHeight,
            out float[] Y, out float[] U, out float[] V,
            out int width, out int height)
        {
            width = NextPow2(srcWidth);
            height = NextPow2(srcHeight);

            var len = height * width;

            Y = new float[len];
            U = new float[len];
            V = new float[len];
            float yv = 0, u = 0, v = 0;
            int stride = srcWidth;
            for (int y = 0; y < srcHeight; y++)
            {
                var src_yo = stride * y;
                var dst_yo = width * y;
                for (int x = 0; x < srcWidth; x++)
                {
                    var src_i = src_yo + x;
                    var dst_i = dst_yo + x;
                    RGB_To_YUV(Red[src_i], Green[src_i], Blue[src_i], out yv, out u, out v);
                    Y[dst_i] = yv;
                    U[dst_i] = u;
                    V[dst_i] = v;
                }
                // Continue filling any extra space with the last sample (stops zero-ringing)
                for (int x = srcWidth; x < width; x++)
                {
                    var dst_i = dst_yo + x;
                    Y[dst_i] = yv;
                    U[dst_i] = u;
                    V[dst_i] = v;
                }
            }
            // fill any remaining rows with copies of the one above (full size, so we get the x-smear too)
            var end = srcHeight * width;
            for (int f = end; f < len; f++)
            {
                Y[f] = Y[f - width];
                U[f] = U[f - width];
                V[f] = V[f - width];
            }
        }

    }
}