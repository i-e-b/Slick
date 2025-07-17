using System.Drawing.Imaging;
using Microsoft.StylusInput;
using SlickCommon.Canvas;
using SlickWindows.Gui.Components;
using SlickWindows.Input;

namespace SlickWindows.Gui
{
    public partial class PaletteWindow : AutoScaleForm, ITouchTriggered
    {
        private static      Image?         _paletteImage;
        private readonly RealTimeStylus _colorInput;
        private             bool           _shouldClose;
        private             int            _penSize = (int)PenSizes.Default;

        public IEndlessCanvas? Canvas { get; set; }

        public PaletteWindow()
        {
            _shouldClose = false;

            InitializeComponent();
            if (colorBox == null) throw new Exception("Components not initialised correctly");
            
            colorBox.Image = PaintPalette(); // Replace this with a loaded image if you want something custom.
            // ReSharper disable once PossibleNullReferenceException
            colorBox.SizeMode = PictureBoxSizeMode.StretchImage;

            _colorInput = new RealTimeStylus(colorBox, true) {AllTouchEnabled = true};
            _colorInput.AsyncPluginCollection?.Add(new TouchPointStylusPlugin(this, DeviceDpi));
            _colorInput.Enabled = true;
        }

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
        private Image? PaintPalette()
        {
            if (_paletteImage != null) return _paletteImage;
            if (colorBox == null) return null;
            var width = colorBox.Width;
            var thirdWidth = colorBox.Width / 3;
            var qtrHeight = colorBox.Height / 4;
            var height = colorBox.Height - qtrHeight;

            var bmp = new Bitmap(colorBox.Width, colorBox.Height, PixelFormat.Format16bppRgb565);

            // Fixed black and white targets
            using (var gr = Graphics.FromImage(bmp))
            {
                gr.FillRectangle(Brushes.Black, 0, 0, thirdWidth, qtrHeight);
                gr.FillRectangle(Brushes.BlueViolet, thirdWidth, 0, thirdWidth, qtrHeight);
                gr.FillRectangle(Brushes.White, thirdWidth * 2, 0, thirdWidth, qtrHeight);
            }

            // Generate a color swatch at two brightness levels
            var halfWidth = width >> 1;
            var dy = 255.0f / height;
            var dx = 255.0f / halfWidth;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < halfWidth; x++)
                {
                    var c = ColorEncoding.YcocgToColor(100, (int)(y * dy), (int)(x * dx));
                    bmp.SetPixel(x, y + qtrHeight, c);

                    c = ColorEncoding.YcocgToColor(200, (int)(y * dy), (int)(x * dx));
                    bmp.SetPixel(x + halfWidth, y + qtrHeight, c);
                }
            }

            _paletteImage = bmp;
            return bmp;
        }

        public void Touched(int stylusId, int x, int y)
        {
            if (Canvas == null || colorBox?.Image == null) return;



            // Work out what colour or size was clicked, send it back to canvas
            using (Bitmap bmp = new Bitmap(colorBox.Image))
            {
                var mx = (x / (float)colorBox.Width) * bmp.Width;
                var my = (y / (float)colorBox.Height) * bmp.Height;
                if (mx >= bmp.Width) mx = bmp.Width - 1;
                if (my >= bmp.Height) my = bmp.Height - 1;

                var color = bmp.GetPixel((int)mx, (int)my);
                Canvas.SetPen(stylusId, color, _penSize, InkType.Overwrite);
            }

            // Trigger the palette to close (must do it outside this call)
            _shouldClose = true;
            Invalidate();
        }

        private void PaletteWindow_SizeChanged(object sender, EventArgs e)
        {
            Invalidate();
        }

        private void smallPenButton_Click(object sender, EventArgs e)
        {
            _penSize = (int)PenSizes.Small;
        }

        private void medButton_Click(object sender, EventArgs e)
        {
            _penSize = (int)PenSizes.Default;
        }

        private void largeButton_Click(object sender, EventArgs e)
        {
            _penSize = (int)PenSizes.Large;
        }

        private void hugeButton_Click(object sender, EventArgs e)
        {
            _penSize = (int)PenSizes.Huge;
        }

        private void giganticButton_Click(object sender, EventArgs e)
        {
            _penSize = (int)PenSizes.Gigantic;
        }

        private void PaletteWindow_DpiChanged(object sender, DpiChangedEventArgs e)
        {

        }
    }
}
