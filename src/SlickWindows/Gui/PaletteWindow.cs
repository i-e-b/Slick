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
        [NotNull] private readonly RealTimeStylus _colorInput;
        [NotNull] private readonly RealTimeStylus _sizeInput;
        private bool _shouldClose;
        private int _penSize = 5;

        public PaletteWindow()
        {
            _shouldClose = false;

            InitializeComponent();
            if (colorBox == null) throw new Exception("Components not initialised correctly");
            
            colorBox.Image = PaintPalette();
            _colorInput = new RealTimeStylus(colorBox, true) {AllTouchEnabled = true};
            _colorInput.AsyncPluginCollection?.Add(new TouchPointStylusPlugin(this, DeviceDpi));
            _colorInput.Enabled = true;
        }

        public IEndlessCanvas Canvas { get; set; }

        private void PaletteWindow_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Clean up the touch control
            _colorInput.Dispose();
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
            var width = colorBox.Width;
            var qheight = colorBox.Height / 4;
            var height = colorBox.Height - qheight;

            var bmp = new Bitmap(colorBox.Width, colorBox.Height, PixelFormat.Format16bppRgb565);

            // Fixed black and white targets
            using (var gr = Graphics.FromImage(bmp))
            {
                gr.FillRectangle(Brushes.Black, 0, 0, width / 2, qheight);
                gr.FillRectangle(Brushes.White, width / 2, 0, width / 2, qheight);
            }

            var sx = Math.PI / (width * 2);
            var sy = Math.PI / (height * 2);

            // this image generation is really slow!
            for (int y = 0; y < height; y++)
            {
                var r = Saturate(Math.Sin(y * sy) * 255);
                var g = 255 - r;
                for (int x = 0; x < width; x++)
                {
                    var b = Saturate(Math.Sin(x * sx) * 255);
                    bmp.SetPixel(x, y + qheight, Color.FromArgb(r, g, b));
                }
            }

            return bmp;
        }

        private int Saturate(double v)
        {
            if (v < 0) return 0;
            if (v > 255) return 255;
            return (int)v;
        }

        /// <inheritdoc />
        public void Touched(int stylusId, int x, int y)
        {
            if (Canvas == null) return;

            // Work out what colour or size was clicked, send it back to canvas
            using (Bitmap bmp = new Bitmap(colorBox.Image))
            {
                var color = bmp.GetPixel(x, y);
                Canvas.SetPen(stylusId, color, _penSize, InkType.Overwrite);
            }

            // Trigger the palette to close (must do it outside this call)
            _shouldClose = true;
            Invalidate();
        }

        private void PaletteWindow_SizeChanged(object sender, EventArgs e)
        {
            if (colorBox.Image != null) colorBox.Image.Dispose();
            colorBox.Image = PaintPalette();

            Invalidate();
        }

        private void smallPenButton_Click(object sender, EventArgs e)
        {
            _penSize = 2;
        }

        private void medButton_Click(object sender, EventArgs e)
        {
            _penSize = 5;
        }

        private void largeButton_Click(object sender, EventArgs e)
        {
            _penSize = 10;
        }

        private void hugeButton_Click(object sender, EventArgs e)
        {
            _penSize = 30;
        }
    }
}
