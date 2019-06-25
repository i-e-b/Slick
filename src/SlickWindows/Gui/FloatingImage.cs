using System.Drawing;
using System.Windows.Forms;
using JetBrains.Annotations;
using SlickWindows.Canvas;

namespace SlickWindows.Gui
{
    public partial class FloatingImage : UserControl
    {
        /// <summary>
        /// Image to be shown
        /// </summary>
        public Image CandidateImage
        {
            get { return _candidateImage; }
            set { _candidateImage = value; Invalidate(); }
        }

        public EndlessCanvas CanvasTarget { get; set; }

        private bool _scaling;
        private int _dx,_dy;
        private Image _candidateImage;

        public FloatingImage()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            InitializeComponent();
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
                Width = window.X - Left;
                Height = window.Y - Top;
                Invalidate();
            }
            else
            {
                DoDrag(window);
            }
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
        protected override void OnMouseDown(MouseEventArgs e)
        {
            _dx = e.X;
            _dy = e.Y;

            var corner = (Width - _dx) + (Height - _dy);

            _scaling = (corner < 24);
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
            CrossLoadImage(CandidateImage, CanvasTarget, Left, Top, Size);

            // close the floater
            if (CandidateImage != null) CandidateImage.Dispose();
            CandidateImage = null;
            Visible = false;
        }

        
        public static void CrossLoadImage([NotNull] Image img, [NotNull] EndlessCanvas target, int px, int py, Size size)
        {
            using (var bmp = new Bitmap(img, size))
            {
                // TODO: scaling when we're in 'map' mode in the canvas.

                var width = bmp.Width;
                var height = bmp.Height;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var color = bmp.GetPixel(x,y);
                        if (color.R ==255 && color.G == 255 && color.B == 255) continue; // skip white pixels
                        target.SetPixel(color, x + px, y + py);
                    }
                }
            }
            target.SaveChanges();
            target.Invalidate();
        }


        /// <inheritdoc />
        protected override void OnMouseUp(MouseEventArgs e)
        {
            Capture = false;
            _scaling = false;
            base.OnMouseUp(e);
        }
    }
}
