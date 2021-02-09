using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using SlickCommon.Canvas;
using SlickWindows.ImageFormats;

namespace SlickWindows.Gui.Components
{
    public partial class FloatingText : UserControl
    {
        public IEndlessCanvas? CanvasTarget { get; set; }

        private static Font? LargeFont;
        private static Font? SmallFont;

        private int _dx, _dy;
        private bool _scaling;
        private readonly int _scale;

        public FloatingText()
        {
            // Read scale
            var asf = ParentForm as AutoScaleForm;
            var dpi = asf?.Dpi ?? DeviceDpi;
            _scale = (dpi > 120) ? 2 : 1;

            if (LargeFont == null) {
                LargeFont = new Font("Arial Black", 16);
            }

            if (SmallFont == null) {
                SmallFont = new Font("Consolas", 10);
            }

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            InitializeComponent();

            if (textBox != null) {
                textBox.Font = SmallFont;
            }
            Cursor = Cursors.Arrow;
        }

        public void NormaliseControlScale() {
            textBiggerButton.Top = Height - textBiggerButton.Height; 
            textSmallerButton.Top = Height - textSmallerButton.Height; 
            mergeButton.Left = Width - mergeButton.Width;
            textBox.Width = Width;
            textBox.Height = textBiggerButton.Top - textBox.Top;
        }
        
        protected override CreateParams CreateParams {
            get {
                // Fix some issues win WinForms
                var cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;  // Turn on WS_EX_COMPOSITED
                return cp;
            }
        } 

        /// <inheritdoc />
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!Capture) return;

            var screen = PointToScreen(e.Location);

            if (ParentForm == null) return;
            var window = ParentForm.PointToClient(screen);

            if (_scaling)
            {
                Width = Math.Max(180 * _scale, window.X - Left);
                Height = Math.Max(62 * _scale, window.Y - Top);
                NormaliseControlScale();
            }
            else
            {
                DoDrag(window);
            }

            ParentForm?.Refresh(); // triggering via parent stops weird drawing issues
        }

        private void DoDrag(Point window)
        {
            // drag the control around
            var left = window.X - _dx;
            var top = window.Y - _dy;

            if (ParentForm == null) return;
            var limit = ParentForm.ClientSize;

            // make sure it doesn't go off the window
            if (left > limit.Width - 48) left = limit.Width - 48;
            if (top > limit.Height - 48) top = limit.Height - 48;
            if (left < 0) left = 0;
            if (top < 0) top = 0;

            Location = new Point(left, top);
        }

        /// <inheritdoc />
        protected override void OnMouseUp(MouseEventArgs e)
        {
            Capture = false;
            _scaling = false;
            base.OnMouseUp(e);
        }

        /// <inheritdoc />
        protected override void OnMouseDown(MouseEventArgs e)
        {
            _dx = e.X;
            _dy = e.Y;

            var corner = (Width - _dx) + (Height - _dy);

            _scaling = (corner < 24 * _scale);
            Capture = true;

            base.OnMouseDown(e);
        }

        private void TextBiggerButton_Click(object sender, EventArgs e)
        {
            if (textBox == null || LargeFont == null) return;
            textBox.Font = LargeFont;
        }

        private void TextSmallerButton_Click(object sender, EventArgs e)
        {
            if (textBox == null || SmallFont == null) return;
            textBox.Font = SmallFont;
        }

        private void MergeButton_Click(object sender, EventArgs e)
        {
            if (CanvasTarget == null || textBox == null) return;

            using (var bmp = new Bitmap(textBox.Width, textBox.Height, PixelFormat.Format32bppPArgb))
            {
                textBox.DrawToBitmap(bmp, new Rectangle(0, 0, textBox.Width, textBox.Height));
                CanvasTarget.CrossLoadImage(SystemImage.ToRaw(bmp), Left + textBox.Left, Top + textBox.Top, textBox.Size);
            }
            Visible = false;
        }

        private void CloseButton_Click(object sender, EventArgs e)
        {
            Visible = false;
        }

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;

            // Draw resize thumb
            for (int i = 0; i < 24; i+= 3)
            {
                g.DrawLine(Pens.White, Width - i - 1, Height, Width, Height - i - 1);
                g.DrawLine(Pens.Black, Width - i, Height, Width, Height - i);
            }
        }
    }
}
