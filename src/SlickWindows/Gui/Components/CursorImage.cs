﻿using System.Diagnostics.CodeAnalysis;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SlickWindows.Gui.Components
{
    /// <summary>
    /// Helper to make custom cursor
    /// </summary>
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public class CursorImage
    {
        public struct IconInfo
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetIconInfo(IntPtr hIcon, ref IconInfo pIconInfo);
        [DllImport("user32.dll")]
        public static extern IntPtr CreateIconIndirect(ref IconInfo icon);

        /// <summary>
        /// Create a cursor from a bitmap without resizing and with the specified hot spot
        /// </summary>
        public static Cursor CreateCursorNoResize(Bitmap bmp, int xHotSpot, int yHotSpot)
        {
            IntPtr ptr = bmp.GetHicon();
            IconInfo tmp = new IconInfo();
            GetIconInfo(ptr, ref tmp);
            tmp.xHotspot = xHotSpot;
            tmp.yHotspot = yHotSpot;
            tmp.fIcon = false;
            ptr = CreateIconIndirect(ref tmp);
            return new Cursor(ptr);
        }

        public static Cursor MakeCrosshair(int scale)
        {
            using (var bmp = new Bitmap(16 * scale, 16 * scale, PixelFormat.Format32bppArgb))
            using (var pen = new Pen(Color.Black, 1.0f * scale))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.DrawLine(pen, 0, 0, 16 * scale, 16 * scale);
                g.DrawLine(pen, 0, 16 * scale, 16 * scale, 0);
                return CreateCursorNoResize(bmp, 8 * scale, 8 * scale);
            }
        }

        public static Cursor MakeMove(int scale)
        {
            using (var bmp = new Bitmap(16 * scale, 16 * scale, PixelFormat.Format32bppArgb))
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.FillPolygon(
                    Brushes.Black,
                    new[]{
                        new PointF(0,8),
                        new PointF(8,0),
                        new PointF(16,8),
                        new PointF(8,16)
                        }
                        .Select(p=> new PointF(p.X * scale, p.Y*scale)).ToArray()
                    );
                return CreateCursorNoResize(bmp, 8 * scale, 8 * scale);
            }
        }
    }
}