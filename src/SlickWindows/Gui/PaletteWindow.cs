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
        private static Image _paletteImage = null;
        [NotNull] private readonly RealTimeStylus _colorInput;
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
            if (_paletteImage != null) return _paletteImage;
            if (colorBox == null) return null;
            var width = colorBox.Width;
            var thirdWidth = colorBox.Width / 3;
            var qheight = colorBox.Height / 4;
            var height = colorBox.Height - qheight;

            var bmp = new Bitmap(colorBox.Width, colorBox.Height, PixelFormat.Format16bppRgb565);

            // Fixed black and white targets
            using (var gr = Graphics.FromImage(bmp))
            {
                gr.FillRectangle(Brushes.Black, 0, 0, thirdWidth, qheight);
                gr.FillRectangle(Brushes.BlueViolet, thirdWidth, 0, thirdWidth, qheight);
                gr.FillRectangle(Brushes.White, thirdWidth * 2, 0, thirdWidth, qheight);
            }

            // Generate a color swatch at two brightness levels
            var halfWidth = width >> 1;
            var dy = 255.0f / height;
            var dx = 255.0f / halfWidth;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < halfWidth; x++)
                {
                    var c = ColorEncoding.ExpPaletteColor(100, (int)(y * dy), (int)(x * dx));
                    bmp.SetPixel(x, y + qheight, c);

                    c = ColorEncoding.ExpPaletteColor(200, (int)(y * dy), (int)(x * dx));
                    bmp.SetPixel(x + halfWidth, y + qheight, c);
                }
            }

            return bmp;
        }

        /// <inheritdoc />
        public void Touched(int stylusId, int x, int y)
        {
            if (Canvas == null || colorBox?.Image == null) return;

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
            if (colorBox == null) return;
            if (_paletteImage != null) {
                _paletteImage.Dispose();
                _paletteImage = null;
            }

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

        private void giganticButton_Click(object sender, EventArgs e)
        {
            _penSize = 200;
        }
    }
}
