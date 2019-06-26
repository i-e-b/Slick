using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using JetBrains.Annotations;

namespace SlickWindows.Gui
{
    /// <summary>
    /// Helper to make custom cursor
    /// </summary>
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
        [NotNull]
        public static Cursor CreateCursorNoResize([NotNull]Bitmap bmp, int xHotSpot, int yHotSpot)
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

        [NotNull] public static Cursor MakeCrosshair()
        {
            using (var bmp = new Bitmap(16,16, PixelFormat.Format32bppArgb)) {
                using (var g = Graphics.FromImage(bmp)) {
                    g.Clear(Color.Transparent);
                    g.DrawLine(Pens.Black, 0,0,16,16);
                    g.DrawLine(Pens.Black, 0,16,16,0);
                    return CreateCursorNoResize(bmp, 8, 8);
                }
            }
        }
    }
}