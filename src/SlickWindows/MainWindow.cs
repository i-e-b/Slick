using System;
using System.Windows.Forms;
using JetBrains.Annotations;
using Microsoft.StylusInput;
using SlickWindows.Canvas;

namespace SlickWindows
{
    public partial class MainWindow : Form, IDataTriggered
    {
        // Declare the real time stylus.
        [NotNull]private readonly RealTimeStylus _stylusInput;
        [NotNull]private readonly EndlessCanvas _canvas;

        public MainWindow()
        {
            InitializeComponent();

            DoubleBuffered = true;
            _canvas = new EndlessCanvas(DeviceDpi);

            _stylusInput = new RealTimeStylus(this, true);
            _stylusInput.MultiTouchEnabled = true;

            // Async calls get triggered on the UI thread, so we use this to trigger updates to WinForms visuals.
            _stylusInput.AsyncPluginCollection?.Add(new DataTriggerStylusPlugin(this));

            AddInputPlugin(_stylusInput, new RealtimeRendererPlugin(_canvas));

            _stylusInput.Enabled = true; 
        }

        private static void AddInputPlugin([NotNull]RealTimeStylus stylusInput, IStylusSyncPlugin plugin)
        {
            if (plugin == null || stylusInput.SyncPluginCollection == null) throw new Exception("Input state not correct");
            var rtsEnabled = stylusInput.Enabled;
            stylusInput.Enabled = false;
            stylusInput.SyncPluginCollection.Add(plugin);
            stylusInput.Enabled = rtsEnabled;
        }

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e)
        {
            _canvas.RenderToGraphics(e.Graphics, Width, Height);
        }

        /// <inheritdoc />
        public void DataCollected(RealTimeStylus sender)
        {
            Text = $"Position = ({_canvas.X}, {_canvas.Y})";
            Invalidate();
        }
    }
}
