using System.IO;
using JetBrains.Annotations;

namespace SlickCommon.ImageFormats
{
    /// <summary>
    /// Tools to convert arrays to different encodings
    /// </summary>
    public static class DataEncoding
    {
        /// <summary>
        /// Decode a stream of byte values into an existing array of doubles
        /// </summary>
        /// <param name="input">Readable stream for input</param>
        /// <param name="output">An existing value buffer. If there is more input
        /// than buffer space, the end of the input will be truncated</param>
        public static void FibonacciDecode([NotNull]Stream input, [NotNull]float[] output)
        {
            // Read a byte, scan through bits building up a number until we hit `b11`
            // Then move on to the next

            int bv;

            bool lastWas1 = false;
            uint accum = 0;
            uint pos = 0;
            var bytePos = 0;
            int outIdx = 0;
            int outLimit = output.Length;

            while ((bv = input.ReadByte()) >= 0) {

                while (bytePos++ < 8) {
                    if (outIdx >= outLimit) return; // end of buffer
                    uint f = (uint)((bv >> (8 - bytePos)) & 0x01);

                    if (f > 0) {
                        if (lastWas1) {
                            // convert back to signed, add to list
                            if (accum > 0) {
                                long n = accum - 1L;
                                if ((n & 1) == 0) output[outIdx++] = ((int)(n >> 1));
                                else output[outIdx++] = ((int)(((n + 1) >> 1) * -1));
                            } // else damaged data
                            // `b11`; reset, move to next number
                            accum = 0;
                            pos = 0;
                            lastWas1 = false;
                            continue;
                        }
                        lastWas1 = true;
                    } else lastWas1 = false;

                    accum += f * _fibonacciSequence[pos + 2];
                    pos++;
                }
                
                bytePos = 0;
            }
        }
        
        [NotNull]private static readonly uint[] _fibonacciSequence = {0,1,1,2,3,5,8,13,21,34,55,89,144,233,377,610,987,1597,
            2584,4181,6765,10946,17711,28657,46368,75025,121393,196418,317811,514229  };


        /// <summary>
        /// Encode an array of integer values into a byte stream.
        /// The input of double values are truncated during encoding.
        /// </summary>
        /// <param name="buffer">Input buffer. Values will be truncated and must be in the range +- 196418</param>
        /// <param name="length">Number of samples to encode. Must be equal-or-less than buffer length. To encode entire buffer, pass zero.</param>
        /// <param name="output">Writable stream for output</param>
        public static void FibonacciEncode([NotNull]float[] buffer, int length, [NotNull]Stream output)
        {
            var bf = new byte[8]; // if each bit is set. Value is 0xFF or 0x00
            var v = new byte[]{ 1<<7, 1<<6, 1<<5, 1<<4, 1<<3, 1<<2, 1<<1, 1 }; // values of the flag
            var bytePos = 0;

            if (length <= 0) length = buffer.Length;

            // for each number, build up the fib code.
            // any time we exceed a byte we write it out and reset
            // Negative numbers are handled by the same process as `SignedToUnsigned`
            // this streams out numbers MSB-first (?)

            for (var idx = 0; idx < length; idx++)
            {
                var inValue = buffer[idx];
                // Signed to unsigned
                int n = (int)inValue;
                n = (n >= 0) ? (n * 2) : (n * -2) - 1; // value to be encoded
                n += 1; // always greater than zero

                // Fibonacci encode
                ulong res = 0UL;
                var maxIdx = -1;

                // find starting position
                var i = 2;
                while (_fibonacciSequence[i] < n) i++;

                // scan backwards marking value bits
                while (n > 0)
                {
                    if (_fibonacciSequence[i] <= n)
                    {
                        res |= 1UL << (i - 2);
                        n -= (int)_fibonacciSequence[i];
                        if (maxIdx < i) maxIdx = i;
                    }
                    i--;
                }
                res |= 1UL << (maxIdx - 1);

                // output to stream
                for (int boc = 0; boc < maxIdx; boc++)
                {
                    bf[bytePos] = (byte)(0xFF * ((res >> (boc)) & 1));
                    bytePos++;

                    if (bytePos > 7)
                    { // completed a byte (same as above)
                        int bv = (bf[0] & v[0]) | (bf[1] & v[1]) | (bf[2] & v[2]) | (bf[3] & v[3]) | (bf[4] & v[4]) | (bf[5] & v[5]) | (bf[6] & v[6]) | (bf[7] & v[7]);
                        output.WriteByte((byte)bv);
                        bf[0] = bf[1] = bf[2] = bf[3] = bf[4] = bf[5] = bf[6] = bf[7] = 0;
                        bytePos = 0;
                    }
                }
            }

            // If we didn't land on a byte boundary, push the last one out here
            if (bytePos != 0) { // completed a byte (slightly different to the others above)
                int bv = (bf[0] & v[0]) | (bf[1] & v[1]) | (bf[2] & v[2]) | (bf[3] & v[3]) | (bf[4] & v[4]) | (bf[5] & v[5]) | (bf[6] & v[6]) | (bf[7] & v[7]);
                output.WriteByte((byte)bv);
            }
        }
    }
}