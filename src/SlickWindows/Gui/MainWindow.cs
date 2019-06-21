using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using JetBrains.Annotations;
using Microsoft.StylusInput;
using SlickWindows.Canvas;
using SlickWindows.Input;

namespace SlickWindows.Gui
{
    public partial class MainWindow : Form, IDataTriggered, IScrollTarget
    {
        // Declare the real time stylus.
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        [NotNull]private readonly RealTimeStylus _stylusInput;
        [NotNull]private readonly EndlessCanvas _canvas;

        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        [NotNull] private readonly string DefaultLocation;

        private double _lastScalePercent = 100.0;

        public MainWindow(string[] args)
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.UserMouse, true);
            VerticalScroll.Enabled = true;
            HorizontalScroll.Enabled = true;

            DefaultLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Slick");

            InitializeComponent();
            PanScrollReceiver.Initialise(this);

            var initialFile = (args?.Length > 0) ? args[0] : Path.Combine(DefaultLocation, "default.slick");
            _canvas = new EndlessCanvas(Width, Height, DeviceDpi, initialFile, CanvasChanged);

            DoubleBuffered = true;
            if (saveFileDialog != null) saveFileDialog.InitialDirectory = DefaultLocation;

            _stylusInput = new RealTimeStylus(this, true);
            _stylusInput.MultiTouchEnabled = true;
            _stylusInput.AllTouchEnabled = true;

            // Async calls get triggered on the UI thread, so we use this to trigger updates to WinForms visuals.
            _stylusInput.AsyncPluginCollection?.Add(new DataTriggerStylusPlugin(this));

            AddInputPlugin(_stylusInput, new CanvasDrawingPlugin(_canvas, new WinFormsKeyboard()));

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

        [NotNull]private static readonly object _drawLock = new object();
        private volatile bool _ignoreDraw = false;

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e)
        {
            if (_ignoreDraw) return;
            _ignoreDraw = true;
            lock (_drawLock)
            {
                _canvas.RenderToGraphics(e.Graphics, Width, Height);
            }
            _ignoreDraw = false;
        }

        public void CanvasChanged() {
            Invalidate();
        }

        /// <inheritdoc />
        public void DataCollected(RealTimeStylus sender)
        {
            UpdateWindowAndStatus();
        }

        private void UpdateWindowAndStatus()
        {
            Text = $"Slick ({_canvas.X}, {_canvas.Y}); ({Width} x {Height}) {_lastScalePercent:#}%";
            Invalidate();
        }

        private void paletteButton_Click(object sender, EventArgs e)
        {
            new PaletteWindow
            {
                Canvas = _canvas,
                Location = paletteButton?.PointToScreen(new Point(0, 0)) ?? new Point(Left, Top)
            }.ShowDialog();
        }

        private void mapButton_Click(object sender, EventArgs e)
        {
            if (mapButton == null || e == null) return;

            var scale = _canvas.SwitchScale();

            _lastScalePercent = 100.0 / (1 << (scale - 1));
            mapButton.Text = (scale == EndlessCanvas.MaxScale) ? "Canvas" : "Map";

            UpdateWindowAndStatus();
        }

        /// <inheritdoc />
        public void Scroll2D(int dx, int dy)
        {
            _canvas.Scroll(dx, dy);
        }

        private void SetPageButton_Click(object sender, EventArgs e)
        {
            var result = saveFileDialog?.ShowDialog();
            switch (result) {
                case DialogResult.OK:
                case DialogResult.Yes:
                    _canvas.ChangeBasePath(saveFileDialog.FileName);
                    Invalidate();
                    return;

                default:
                    return;
            }
        }

        private void MoreButton_Click(object sender, EventArgs e)
        {
            // show extras interface
            var dlog = new Extras(_canvas);
            dlog.Location = moreButton?.PointToScreen(new Point(-dlog.Width, -dlog.Height)) ?? new Point(Right, Bottom);
            dlog.ShowDialog();
        }

        private void MainWindow_ClientSizeChanged(object sender, EventArgs e)
        {
            _canvas.SetSizeHint(Width, Height);
        }

        private void UndoButton_Click(object sender, EventArgs e)
        {
            _canvas.Undo();
            Invalidate();
        }

        private void MainWindow_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (mapButton == null || e == null) return;
            _canvas.CentreAndZoom(e.X, e.Y);
            _lastScalePercent = 100.0;
            mapButton.Text = "Map";
        }

        private void PinsButton_Click(object sender, EventArgs e)
        {
            new PinsWindow(_canvas)
            {
                Location = pinsButton?.PointToScreen(new Point(0, 0)) ?? new Point(Left, Top)
            }.ShowDialog();
        }

        private void SelectButton_Click(object sender, EventArgs e)
        {
            var inSelect = _canvas.ToggleSelectMode();
            if (selectButton != null) selectButton.BackColor = inSelect ? Color.DarkGray : Color.White;
        }
    }
}
