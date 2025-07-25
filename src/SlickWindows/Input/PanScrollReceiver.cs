﻿namespace SlickWindows.Input
{
    /// <summary>
    /// Handles reading mouse-wheel style events across the application
    /// </summary>
    public static class PanScrollReceiver
    {
        private static readonly object _lock = new();
        private static bool isSetUp = false;
        private static MouseWheelMessageFilter? _mouseFilter;

        public static void Initialise(IScrollTarget target) {
            if (isSetUp) {
                _mouseFilter?.SetTarget(target);
                return;
            }
            lock (_lock) {
                if (isSetUp) return;

                _mouseFilter = new MouseWheelMessageFilter(target);
                Application.AddMessageFilter(_mouseFilter);
            }
        }
    }

    public class MouseWheelMessageFilter : IMessageFilter
    {
        public IScrollTarget? Target { get; private set; }
        // ReSharper disable InconsistentNaming
        // ReSharper disable IdentifierTypo
        public const int MK_CONTROL = 0x0008;
        public const int MK_SHIFT = 0x0004;

        public const int WM_MOUSEWHEEL = 0x020A;
        public const int WM_MOUSEHWHEEL = 0x020E;
        public const int WM_HSCROLL = 0x114;
        public const int WM_VSCROLL = 0x115;

        public const int MOUSEEVENTF_HWHEEL = 0x01000;
        // ReSharper restore InconsistentNaming
        // ReSharper restore IdentifierTypo

        public MouseWheelMessageFilter(IScrollTarget target)
        {
            Target = target;
        }

        public bool PreFilterMessage(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_MOUSEWHEEL:
                    {
                        var delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                        var keyBits = (char)(m.WParam.ToInt64() & 0xFF);
                        var shiftKeyDown = keyBits == MK_SHIFT;

                        if (shiftKeyDown)
                        {
                            Target?.Scroll2D(delta, 0);
                        }
                        else
                        {
                            Target?.Scroll2D(0, delta);
                        }

                        return true;
                    }

                case WM_MOUSEHWHEEL:
                    {
                        // wheel delta
                        var delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);

                        Target?.Scroll2D(-delta, 0);

                        return true;
                    }
            }


            return false;
        }

        public void SetTarget(IScrollTarget target)
        {
            Target = target;
        }
    }
}
