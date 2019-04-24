using System.Windows.Forms;
using JetBrains.Annotations;
using Microsoft.StylusInput;
using SlickWindows.Canvas;

namespace SlickWindows.Gui
{
    public partial class PaletteWindow : Form, ITouchTriggered
    {
        [NotNull]private readonly RealTimeStylus _stylusInput;

        public PaletteWindow()
        {
            InitializeComponent();
            
            _stylusInput = new RealTimeStylus(this, true);
            _stylusInput.AllTouchEnabled = true;

            // Async calls get triggered on the UI thread, so we use this to trigger updates to WinForms visuals.
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
            // TODO: render a standard colour palette.
        }

        /// <inheritdoc />
        public void Touched(int stylusId, int x, int y)
        {
            //TODO_IMPLEMENT_ME();
            // Work out what colour or size was clicked, send it back to canvas
        }
    }
}
