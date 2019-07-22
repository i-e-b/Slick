using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SlickWindows.Gui
{
    public static class FormsHelper {

        /// <summary>
        /// returns false if the window is partly or completely off screen
        /// </summary>
        public static bool IsOnScreen( Form form )
        {
            if (form == null) return true;
            var rect = form.DesktopBounds;
            return Screen.AllScreens.Any(screen => screen?.WorkingArea.Contains(rect) == true);
        }

        /// <summary>
        /// Push a window so it is fully on it's closest screen
        /// </summary>
        public static void NudgeOnScreen(Form form)
        {
            if (form == null) return;

            // find closest screen (In multiple display environments where no display contains the rectangle, the display closest to the rectangle is returned)
            var screen = Screen.FromRectangle(form.DesktopBounds);

            var wind = form.DesktopBounds;
            var area = screen.WorkingArea;

            // push the edges around in order, so we always get the title bar on screen
            if (wind.Bottom > area.Bottom) form.Location = new Point(form.Location.X, form.Location.Y - (wind.Bottom - area.Bottom));
            if (wind.Right > area.Right) form.Location = new Point(form.Location.X - (wind.Right - area.Right), form.Location.Y);
            if (wind.Left < area.Left) form.Location = new Point(form.Location.X - (wind.Left - area.Left), form.Location.Y);
            if (wind.Top < area.Top) form.Location =  new Point(form.Location.X, form.Location.Y - (wind.Top - area.Top));
        }

        [DllImport("gdi32.dll")]
        static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        public static Point GetSystemDpi(Form target)
        {
            Point result = new Point();

            var hwnd = (target == null) ? IntPtr.Zero : target.Handle;
            IntPtr hDC = GetDC(hwnd);

            result.X = GetDeviceCaps(hDC, LOGPIXELSX);
            result.Y = GetDeviceCaps(hDC, LOGPIXELSY);

            ReleaseDC(IntPtr.Zero, hDC);

            return result;
        }        
        const int LOGPIXELSX = 88;
        const int LOGPIXELSY = 90;
    }
}