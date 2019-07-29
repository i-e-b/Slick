﻿using System;
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

        public AutoScaleForm()
        {
            OriginalFontSize = Font.Size;
            
            using (var win = new Window(this))
            {
                var screen = win.PrimaryScreen();
                screen.GetDpi(out var dx, out var dy);
                Dpi = LastDpi = OriginalDpi = (short)Math.Min(dx, dy);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Win32.WM_NCCREATE)
            {
                Win32.EnableNonClientDpiScaling(Handle);
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
                if (OriginalDpi < 90 || LastDpi == Dpi) {
                    _midFlow = false;
                    return;
                }
                LastDpi = Dpi;

                var scale = Dpi / (float)OriginalDpi;

                // TODO: gate the DPI to round numbers here?

                Font = PickFont(Dpi, scale);

                // If this control has been this size before, set back to original size.
                // Otherwise, save the size that came from scrolling
                foreach (Control ctrl in Controls) { RescaleControl(ctrl, Dpi); }

                OnRescale(Dpi);
            }
            _midFlow = false;
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

        [NotNull]private readonly Dictionary<Control, Dictionary<short, LayoutRect>> _layoutCache = new Dictionary<Control, Dictionary<short, LayoutRect>> ();
        private void RescaleControl(Control ctrl, short dpi)
        {
            if (ctrl == null) return;
            // the WinForms auto-scaling *almost* works, but it drifts.
            // so we store the layout when we first rescale, then we restore it each time after that.

            if (_layoutCache.ContainsKey(ctrl) && _layoutCache[ctrl]?.ContainsKey(dpi) == true) {
                // read

                var rect = _layoutCache[ctrl][dpi];
                var parent = ctrl.Parent?.ClientRectangle ?? ctrl.Bounds;

                // NOTE: for each anchor flag: if it' set, the rect side is offset. Otherwise it's absolute position
                if (ctrl.Anchor.HasFlag(AnchorStyles.Right)) {
                    ctrl.Left = parent.Right - rect.RightOffset;
                } else {
                    ctrl.Left = rect.Left;
                }
                
                if (ctrl.Anchor.HasFlag(AnchorStyles.Bottom)) {
                    ctrl.Top = parent.Bottom - rect.BottomOffset;
                } else {
                    ctrl.Top = rect.Top;
                }

            }
            else {
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
            }
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