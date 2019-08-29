using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using JetBrains.Annotations;

namespace SlickWindows.Gui.Components
{
    /// <summary>
    /// A WinForms window that automatically rescales controls and fonts
    /// </summary>
    public class AutoScaleForm:Form
    {
        public short Dpi = 0, LastDpi = 0;
        protected short OriginalDpi = 0;
        protected float OriginalFontSize;


        [NotNull] private readonly Dictionary<short, Font> _scaleFonts = new Dictionary<short, Font>();
        [NotNull] private readonly Dictionary<Control, Dictionary<short, LayoutRect>> _layoutCache = new Dictionary<Control, Dictionary<short, LayoutRect>>();

        public AutoScaleForm()
        {
            OriginalFontSize = Font.Size;

            SetInitialDPI();
        }

        private void SetInitialDPI()
        {
            using (var win = new Window(this))
            {
                var screen = win.PrimaryScreen();
                screen.GetDpi(out var dx, out var dy);
                Dpi = LastDpi = OriginalDpi = (short) Math.Max(90, Math.Min(dx, dy));
                if (OriginalDpi > 100) OriginalDpi = 96; // Windows DPI is such a mess!
            }
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);

            // Update stored layout.
            if (!_midFlow) StoreLayoutInCache();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Win32.WM_NCCREATE)
            {
                Win32.EnableNonClientDpiScaling(Handle);
                SetInitialDPI();
                RescaleScreen();
            }
            if (m.Msg == Win32.WM_DPICHANGED)
            {
                RescaleScreen();
            }
            base.WndProc(ref m);
        }

        protected virtual void OnRescale(int dpi){

        }
        
        private volatile bool _midFlow = false;
        public void RescaleScreen()
        {
            if (_midFlow) return;
            _midFlow = true;
            using (var win = new Window(this))
            {
                var screen = win.PrimaryScreen();
                screen.GetDpi(out var dx, out var dy);
                Dpi = (short)Math.Min(dx, dy);
                if (OriginalDpi < 90) {
                    _midFlow = false;
                    return;
                }

                var scale = Dpi / (float)OriginalDpi;
                scale = (int)(scale * 4) / 4f; // round to 25% increments

                Dpi = (short) (OriginalDpi * scale); // force DPI into the 25% increments.
                LastDpi = Dpi;

                Font = PickFont(Dpi, scale);

                // If this control has been this size before, set back to original size.
                // Otherwise, save the size that came from scrolling
                RescaleLayoutWithCache();

                OnRescale(Dpi);
            }
            _midFlow = false;
        }

        private void RescaleLayoutWithCache()
        {
            foreach (Control ctrl in Controls)
            {
                RescaleControl(ctrl, Dpi, false);
            }
        }

        private void StoreLayoutInCache()
        {
            foreach (Control ctrl in Controls)
            {
                RescaleControl(ctrl, Dpi, true);
            }
        }

        [NotNull]private Font PickFont(short dpi, float scale)
        {
            try
            {
                return _scaleFonts[dpi] ?? throw new Exception();
            }
            catch
            {
                var oldFont = Font ?? throw new Exception("Win32 error: Form has no current font");
                if (oldFont.FontFamily == null) throw new Exception("Win32 error: Current font has no family");

                // Change font size, and WinForms will rescale
                var newFont = new Font(oldFont.FontFamily, OriginalFontSize * scale, oldFont.Style);

                _scaleFonts.Add(dpi, newFont);
                return newFont;
            }
        }

        private void RescaleControl(Control ctrl, short dpi, bool updateOnly)
        {
            if (ctrl == null) return;
            // the WinForms auto-scaling *almost* works, but it drifts.
            // so we store the layout when we first rescale, then we restore it each time after that.

            var prevMidFlow = _midFlow;
            _midFlow = true;
            if (!updateOnly && _layoutCache.ContainsKey(ctrl) && _layoutCache[ctrl]?.ContainsKey(dpi) == true) {
                // read cached scaling

                var rect = _layoutCache[ctrl][dpi];
                var parent = ctrl.Parent?.ClientRectangle ?? ctrl.Bounds;

                var a = ctrl.Anchor;
                var aTop = a.HasFlag(AnchorStyles.Top);
                var aBottom = a.HasFlag(AnchorStyles.Bottom);
                var aLeft = a.HasFlag(AnchorStyles.Left);
                var aRight = a.HasFlag(AnchorStyles.Right);

                // NOTE: for each anchor flag: if it' set, the rect side is offset. Otherwise it's absolute position
                if (aRight) {
                    ctrl.Left = parent.Right - rect.RightOffset;
                } else {
                    ctrl.Left = rect.Left;
                }
                
                if (aTop && aBottom) {
                    ctrl.Top = rect.Top;
                    ctrl.Height = rect.Height;
                }
                else if (aBottom) {
                    ctrl.Top = parent.Bottom - rect.BottomOffset;
                } else {
                    ctrl.Top = rect.Top;
                }

            }
            else {
                // write current scaling
                // ensure data structures
                if (!_layoutCache.ContainsKey(ctrl)) _layoutCache.Add(ctrl, new Dictionary<short, LayoutRect>());
                var map = _layoutCache[ctrl] ?? throw new Exception();
                var rect = ctrl.Bounds;
                var parent = ctrl.Parent?.ClientRectangle ?? rect;
                var layout = new LayoutRect{
                    Left = rect.Left, Top = rect.Top, Width = rect.Width, Height = rect.Height,
                    RightOffset = parent.Right - rect.Left,
                    BottomOffset = parent.Bottom - rect.Top
                };

                if (!map.ContainsKey(dpi)) map.Add(dpi, layout);
                else map[dpi] = layout;
            }
            _midFlow = prevMidFlow;
        }

        internal struct LayoutRect
        {
            public int Left;
            public int Top;
            public int RightOffset;
            public int BottomOffset;
            public int Width;
            public int Height;
        }

    }
}