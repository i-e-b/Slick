using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;

namespace SlickWindows.ImageFormats
{
    /// <summary>
    /// A stream container for progressive image files.
    /// </summary>
    public class InterleavedFile {
        /// <summary>
        /// File version. There is limited support for older versions.
        /// This shouldn't ever go above 32.
        /// </summary>
        public const ushort CurrentVersion = 2;
        
        /// <summary>
        /// File version used
        /// </summary>
        public ushort Version { get; protected set; }

        /// <summary>
        /// Image size in X dimension
        /// </summary>
        public ushort Width { get; }
        /// <summary>
        /// Image size in Y dimension
        /// </summary>
        public ushort Height { get; }
        /// <summary>
        /// Image size in Z dimension
        /// </summary>
        public ushort Depth { get; }

        /// <summary>
        /// Quantiser settings for non-color planes used to create the image.
        /// </summary>
        public double[] QuantiserSettings_Y { get; set; }

        /// <summary>
        /// Quantiser settings for color planes used to create the image.
        /// </summary>
        public double[] QuantiserSettings_C { get; set; }

        /// <summary>
        /// All planes. Expected to be in order: Y,U,V,extra
        /// </summary>
        [NotNull]public byte[][] Planes { get; }

        // Old quantiser settings from before they were stored with the image
        [NotNull]public static readonly double[] OldYQuants = { 15, 8, 5, 3, 2.0};
        [NotNull]public static readonly double[] OldCQuants = { 20, 10, 4.0 };

        // NOTE: not sure if it's better to compress-then-interleave or the other way around

        // Format:
        // [xSize:uint_16], [ySize:uint_16], [zSize:uint_16],
        // [PlaneCount: uint_8] x { [byteSize:uint_64] }
        // [Plane0,byte0:uint_8] ... [PlaneN,byte0], [Plane0,byte1:uint_8]...

        /// <summary>
        /// Create a file from buffers
        /// </summary>
        /// <param name="width">Size in X dimension</param>
        /// <param name="height">Size in Y dimension</param>
        /// <param name="depth">Size in Z dimension. For 2D images, this should be 1</param>
        /// <param name="planes">byte buffers for each image plane</param>
        /// <param name="yQuants">Quantiser settings for non-color planes</param>
        /// <param name="cQuants">Quantiser settings for color planes</param>
        public InterleavedFile(ushort width, ushort height, ushort depth, double[] yQuants, double[] cQuants, params byte[][] planes)
        {
            if (planes == null || planes.Length < 1 || planes.Length > 100) throw new Exception("Must have between 1 and 100 planes");

            Width = width;
            Height = height;
            Depth = depth;
            Planes = planes;
            QuantiserSettings_Y = yQuants;
            QuantiserSettings_C = cQuants;
        }

        /// <summary>
        /// Create an empty container for restoring buffers
        /// </summary>
        public InterleavedFile(ushort width, ushort height, ushort depth, int planeCount)
        {
            Width = width;
            Height = height;
            Depth = depth;
            Planes = new byte[planeCount][];
        }

        public void WriteToStream([NotNull]Stream output)
        {
            WriteStreamHeaders(output);

            // now, spin through each plane IN ORDER, removing it when empty
            // this is a slow byte-wise method. TODO: optimise.
            long i = 0;
            while (true) {
                var anything = false;

                for (int p = 0; p < Planes.Length; p++)
                {
                    if (Planes[p]?.Length > i) {
                        anything = true;
                        output.WriteByte(Planes[p][i]);
                    }
                }
                i++;

                if (!anything) break; // all planes empty
            }
        }

        private void WriteStreamHeaders(Stream output)
        {
            WriteU16(CurrentVersion, output);

            // All planes must have the same base physical size
            WriteU16(Width, output);
            WriteU16(Height, output);
            WriteU16(Depth, output);

            WriteQuantiserSettings(output, QuantiserSettings_Y, QuantiserSettings_C);

            // Each plane can have a different byte size
            WriteU8(Planes.Length, output);
            for (int i = 0; i < Planes.Length; i++)
            {
                if (Planes[i] == null) throw new Exception("Invalid planar data when writing file stream headers");
                WriteU64(Planes[i].LongLength, output);
            }
        }


        /// <summary>
        /// Read from a source file. If the source is trucated, the recovery will go as far as possible
        /// </summary>
        public static InterleavedFile ReadFromStream([NotNull]Stream input) {
            ReadU16(input, out var version);

            if (version > 32) { // probably a version 1 file
                input.Seek(0, SeekOrigin.Begin);
                input.Position = 0;
                version = 1;
            }


            // All planes must have the same base physical size
            ReadU16(input, out var width);
            ReadU16(input, out var height);
            ReadU16(input, out var depth);

            // Read quantiser settings if we expect them
            var yquant = new List<double>();
            var cquant = new List<double>();
            if (version >= 2)
            {   // we should have quantiser information
                ReadQuantiserSettings(input, yquant, cquant);
            }
            if (yquant.Count < 1) yquant.AddRange(OldYQuants);
            if (cquant.Count < 1) cquant.AddRange(OldCQuants);

            // Each plane can have a different byte size
            ReadU8(input, out var planesLength);

            var result = new InterleavedFile(width, height, depth, planesLength);
            if (result.Planes == null) throw new Exception("Interleaved file did not have a planes container");

            result.Version = version;
            result.QuantiserSettings_Y = yquant.ToArray();
            result.QuantiserSettings_C = cquant.ToArray();

            // allocate the buffers
            long i;
            for (i = 0; i < planesLength; i++)
            {
                ReadU64(input, out var psize);
                if (psize > 10_000_000) throw new Exception("Plane data was outside of expected bounds (this is a safety check)");
                result.Planes[i] = new byte[psize];
            }


            // Read into buffer in order. This is the exact inverse of the write
            i = 0;
            while (true) {
                var anything = false;

                for (int p = 0; p < planesLength; p++)
                {
                    if (result.Planes[p] == null) throw new Exception("Invalid planar data buffer when reading planar data");

                    if (result.Planes[p].Length > i) {
                        var value = input.ReadByte();
                        if (value < 0) break; // truncated?

                        anything = true;
                        result.Planes[p][i] = (byte)value;
                    }
                }
                i++;

                if (!anything) break; // all planes full
            }

            return result;
        }

        
        private static void WriteQuantiserSettings(Stream output, double[] yquant, double[] cquant)
        {
            if (yquant==null || cquant == null) {
                WriteU8(0, output);
                WriteU8(0, output);
                return;
            }
            WriteU8(yquant.Length, output);
            WriteU8(cquant.Length, output);

            for (int q = 0; q < yquant.Length; q++)
            {
                var qs = yquant[q] * 100.0;
                WriteU16((ushort)qs, output);
            }

            for (int q = 0; q < cquant.Length; q++)
            {
                var qs = cquant[q] * 100.0;
                WriteU16((ushort)qs, output);
            }
        }

        private static void ReadQuantiserSettings(Stream input, List<double> yquant, List<double> cquant)
        {
            ReadU8(input, out var yqCount);
            ReadU8(input, out var cqCount);

            if (yquant == null || cquant == null) return;

            for (int q = 0; q < yqCount; q++)
            {
                ReadU16(input, out var qs);
                yquant.Add(qs / 100.0);
            }

            for (int q = 0; q < cqCount; q++)
            {
                ReadU16(input, out var qs);
                cquant.Add(qs / 100.0);
            }
        }

        private static bool ReadU8(Stream rs, out int value) {
            value = rs.ReadByte();
            return value >= 0;
        }

        private static void WriteU8(int value, Stream ws) {
            ws?.WriteByte((byte)value);
        }
        
        private static bool ReadU16(Stream rs, out ushort value) {
            value = 0;
            var hi = rs.ReadByte();
            var lo = rs.ReadByte();
            if (hi < 0 || lo < 0) return false;

            value = (ushort)((hi << 8) | (lo));
            return true;
        }

        private static void WriteU16(ushort value, Stream ws) {
            byte hi = (byte)((value >> 8) & 0xff);
            byte lo = (byte)((value     ) & 0xff);
            ws?.WriteByte(hi);
            ws?.WriteByte(lo);
        }

        private static bool ReadU64(Stream rs, out ulong value)
        {
            value = 0;
            for (int i = 56; i >= 0; i -= 8)
            {
                var b = (long)rs.ReadByte();
                if (b < 0) return false;
                value |= (ulong)(b << i);
            }
            return true;
        }

        private static void WriteU64(long srcvalue, Stream ws)
        {
            ulong value = (ulong)srcvalue;
            for (int i = 56; i >= 0; i -= 8)
            {
                byte b = (byte)((value >> i) & 0xff);
                ws?.WriteByte(b);
            }
        }
    }
}