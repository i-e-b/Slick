﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace SlickCommon.ImageFormats
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
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public double[]? QuantiserSettings_Y { get; set; }

        /// <summary>
        /// Quantiser settings for color planes used to create the image.
        /// </summary>
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        public double[]? QuantiserSettings_C { get; set; }

        /// <summary>
        /// All planes. Expected to be in order: Y,U,V,extra
        /// </summary>
        public byte[][]? Planes { get; }

        // Old quantiser settings from before they were stored with the image
        public static readonly   double[] OldYQuants = { 15, 8, 5, 3, 2.0};
        public static readonly double[] OldCQuants = { 20, 10, 4.0 };

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

        public void WriteToStream(Stream output)
        {
            WriteStreamHeaders(output);

            // now, spin through each plane IN ORDER, removing it when empty
            long i = 0;
            while (true) {
                var anything = false;

                var planeCount = Planes?.Length ?? 0;
                for (int p = 0; p < planeCount; p++)
                {
                    if (!(Planes![p].Length > i)) continue;
                    
                    anything = true;
                    output.WriteByte(Planes[p][i]);
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
            var planeLength = Planes?.Length ?? 0;
            WriteU8(planeLength, output);
            for (int i = 0; i < planeLength; i++)
            {
                if (Planes![i] == null) throw new Exception("Invalid planar data when writing file stream headers");
                WriteU64(Planes[i].LongLength, output);
            }
        }


        /// <summary>
        /// Read from a source file. If the source is truncated, the recovery will go as far as possible
        /// </summary>
        public static InterleavedFile ReadFromStream(Stream input) {
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
            var yQuant = new List<double>();
            var cQuant = new List<double>();
            if (version >= 2)
            {   // we should have quantiser information
                ReadQuantiserSettings(input, yQuant, cQuant);
            }
            if (yQuant.Count < 1) yQuant.AddRange(OldYQuants);
            if (cQuant.Count < 1) cQuant.AddRange(OldCQuants);

            // Each plane can have a different byte size
            ReadU8(input, out var planeCount);

            if (planeCount < 1 || planeCount > 6) planeCount = 3;//throw new Exception($"Plane count does not make sense (expected 1..6, got {planeCount})");
            var result = new InterleavedFile(width, height, depth, planeCount);
            if (result.Planes == null) throw new Exception("Interleaved file did not have a planes container");

            result.Version = version;
            result.QuantiserSettings_Y = yQuant.ToArray();
            result.QuantiserSettings_C = cQuant.ToArray();

            // allocate the buffers
            long i;
            for (i = 0; i < planeCount; i++)
            {
                var ok = ReadU64(input, out var pSize);
                if (!ok || pSize > 10_000_000) return result;//throw new Exception("Plane data was outside of expected bounds (this is a safety check)");
                result.Planes[i] = new byte[pSize];
            }


            // Read into buffer in order. This is the exact inverse of the write
            i = 0;
            while (true) {
                var anything = false;

                for (int p = 0; p < planeCount; p++)
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

        
        private static void WriteQuantiserSettings(Stream output, double[]? yQuant, double[]? cQuant)
        {
            if (yQuant == null || cQuant == null)
            {
                WriteU8(0, output);
                WriteU8(0, output);
                return;
            }
            WriteU8(yQuant.Length, output);
            WriteU8(cQuant.Length, output);

            for (int q = 0; q < yQuant.Length; q++)
            {
                var qs = yQuant[q] * 100.0;
                WriteU16((ushort)qs, output);
            }

            for (int q = 0; q < cQuant.Length; q++)
            {
                var qs = cQuant[q] * 100.0;
                WriteU16((ushort)qs, output);
            }
        }

        private static void ReadQuantiserSettings(Stream input, List<double>? yQuant, List<double>? cQuant)
        {
            var ok = ReadU8(input, out var yqCount);
            ok &= ReadU8(input, out var cqCount);

            if (!ok || yQuant == null || cQuant == null) return;

            for (int q = 0; q < yqCount; q++)
            {
                if (ReadU16(input, out var qs))
                {
                    yQuant.Add(qs / 100.0);
                }
            }

            for (int q = 0; q < cqCount; q++)
            {
                if (ReadU16(input, out var qs))
                {
                    cQuant.Add(qs / 100.0);
                }
            }
        }

        private static bool ReadU8(Stream rs, out int value) {
            value = rs.ReadByte();
            return value >= 0;
        }

        private static void WriteU8(int value, Stream? ws) {
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

        private static void WriteU16(ushort value, Stream? ws) {
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

        private static void WriteU64(long srcValue, Stream ws)
        {
            ulong value = (ulong)srcValue;
            for (int i = 56; i >= 0; i -= 8)
            {
                byte b = (byte)((value >> i) & 0xff);
                ws?.WriteByte(b);
            }
        }
    }
}