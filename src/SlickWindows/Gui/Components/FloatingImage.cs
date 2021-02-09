using System;
using System.Drawing;
using System.Windows.Forms;
using SlickCommon.Canvas;
using SlickWindows.Gui.Components;
using SlickWindows.ImageFormats;

namespace SlickWindows.Gui
{
    public partial class FloatingImage : UserControl
    {
        /// <summary>
        /// Image to be shown
        /// </summary>
        public Image? CandidateImage
        {
            get => _candidateImage;
            set { _candidateImage = value; Invalidate(); }
        }

        public IEndlessCanvas? CanvasTarget { get; set; }

        private bool _scaling;
        private int _dx,_dy;
        private Image? _candidateImage;
        private int _scale;

        public FloatingImage()
        {
            // Read scale
            var asf = ParentForm as AutoScaleForm;
            var dpi = asf?.Dpi ?? DeviceDpi;
            _scale = (dpi > 120) ? 2 : 1;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            InitializeComponent();
        }

        public void NormaliseControlScale() {
            mergeButton.Left = Width - mergeButton.Width;
        }

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (CandidateImage == null) {
                g.Clear(Color.IndianRed); // you shouldn't ever see this
                return;
            }

            // Draw image scaled
            g.DrawImage(CandidateImage, new Rectangle(0, 0, Width, Height));

            // Draw resize thumb
            for (int i = 0; i < 24; i+= 3)
            {
                g.DrawLine(Pens.White, Width - i - 1, Height, Width, Height - i - 1);
                g.DrawLine(Pens.Black, Width - i, Height, Width, Height - i);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        /// <inheritdoc />
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (!Capture) return;

            var screen = PointToScreen(e.Location);

            if (ParentForm == null) return;
            var window = ParentForm.PointToClient(screen);

            if (_scaling)
            {
                Width = Math.Max(48, window.X - Left);
                Height = Math.Max(48, window.Y - Top);
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
            if (left > limit.Width - 24) left = limit.Width - 24;
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

        private void RemoveButton_Click(object sender, System.EventArgs e)
        {
            if (CandidateImage != null) CandidateImage.Dispose();
            CandidateImage = null;

            Visible = false;
        }

        private void MergeButton_Click(object sender, System.EventArgs e)
        {
            if (CanvasTarget == null || CandidateImage == null) return;

            // Merge into canvas tiles
            using (var bmp = new Bitmap(CandidateImage))
            {
                CanvasTarget.CrossLoadImage(SystemImage.ToRaw(bmp), Left, Top, Size);
            }

            // close the floater
            if (CandidateImage != null) CandidateImage.Dispose();
            CandidateImage = null;
            Visible = false;
        }


    }
}
