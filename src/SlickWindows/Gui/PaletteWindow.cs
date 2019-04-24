using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using JetBrains.Annotations;
using Microsoft.StylusInput;
using SlickWindows.Canvas;
using SlickWindows.Input;

namespace SlickWindows.Gui
{
    public partial class PaletteWindow : Form, ITouchTriggered
    {
        [NotNull]private readonly RealTimeStylus _stylusInput;
        private bool _shouldClose;

        public PaletteWindow()
        {
            _shouldClose = false;

            InitializeComponent();
            if (pictureBox == null) throw new Exception("Components not initialised correctly");
            
            pictureBox.Image = PaintPalette();
            _stylusInput = new RealTimeStylus(pictureBox, true) {AllTouchEnabled = true};
            _stylusInput.AsyncPluginCollection?.Add(new TouchPointStylusPlugin(this, DeviceDpi));
            _stylusInput.Enabled = true;

        }

        public IEndlessCanvas Canvas { get; set; }

        private void PaletteWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Clean up the touch control
            _stylusInput.Dispose();
        }

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e)
        {
            if (_shouldClose) {
                Close();
                return;
            }
            base.OnPaint(e);
        }

        /// <summary>
        /// render a standard colour palette.
        /// </summary>
        private Image PaintPalette()
        {
            var width = pictureBox.Width;
            var qheight = pictureBox.Height / 4;
            var height = pictureBox.Height - qheight;

            var bmp = new Bitmap(pictureBox.Width, pictureBox.Height, PixelFormat.Format16bppRgb565);

            // Fixed black and white targets
            using (var gr = Graphics.FromImage(bmp))
            {
                gr.FillRectangle(Brushes.Black, 0, 0, width / 2, qheight);
                gr.FillRectangle(Brushes.White, width / 2, 0, width / 2, qheight);
            }

            var sx = Math.PI / (width * 2);
            var sy = Math.PI / (height * 2);

            for (int y = 0; y < height; y++)
            {
                var g = Math.Cos(y * sy) * 255;
                var b = 255 - g;
                for (int x = 0; x < width; x++)
                {
                    var r = Math.Sin(x * sx) * 255;
                    bmp.SetPixel(x, y + qheight, Color.FromArgb((int)r, (int)g, (int)b));
                }
            }

            return bmp;
        }

        /// <inheritdoc />
        public void Touched(int stylusId, int x, int y)
        {
            if (Canvas == null) return;

            // Work out what colour or size was clicked, send it back to canvas
            using (Bitmap bmp = new Bitmap(pictureBox.Image))
            {
                var color = bmp.GetPixel(x, y);
                Canvas.SetPen(stylusId, color, 4, InkType.Overwrite);
            }

            // Trigger the palette to close (must do it outside this call)
            _shouldClose = true;
            Invalidate();
        }

        private void PaletteWindow_SizeChanged(object sender, EventArgs e)
        {
            Invalidate();
        }
    }
}
