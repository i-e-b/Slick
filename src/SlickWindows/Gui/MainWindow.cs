using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using JetBrains.Annotations;
using Microsoft.StylusInput;
using SlickWindows.Canvas;
using SlickWindows.Gui.Components;
using SlickWindows.Input;

namespace SlickWindows.Gui
{
    public partial class MainWindow : AutoScaleForm, IDataTriggered, IScrollTarget
    {
        // Declare the real time stylus.
        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        [NotNull]private readonly RealTimeStylus _stylusInput;
        [NotNull]private readonly EndlessCanvas _canvas;

        // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
        [NotNull] private readonly string DefaultLocation;

        // custom cursors
        [NotNull] private readonly Cursor InkCrosshair_LoDpi;
        [NotNull] private readonly Cursor InkCrosshair_HiDpi;
        [NotNull] private readonly Cursor MoveCursor_LoDpi;
        [NotNull] private readonly Cursor MoveCursor_HiDpi;

        private double _lastScalePercent = 100.0;

        public MainWindow(string[] args)
        {
            InitializeComponent();
            Closing += MainWindow_Closing;

            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.UserMouse, true);
            VerticalScroll.Enabled = false;
            HorizontalScroll.Enabled = false;

            DefaultLocation = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Slick");
            PanScrollReceiver.Initialise(this);

            RescaleScreen(); // get a reliable DPI figure (DeviceDpi is nonsense)
            var initialFile = (args?.Length > 0) ? args[0] : Path.Combine(DefaultLocation, "default.slick");
            _canvas = new EndlessCanvas(Width, Height, Dpi, initialFile, CanvasChanged);
            _scale = 1;
            if (floatingText1 != null) { floatingText1.CanvasTarget = _canvas; floatingText1.Visible = false; }

            if (saveFileDialog != null) saveFileDialog.InitialDirectory = DefaultLocation;
            

            InkCrosshair_LoDpi = CursorImage.MakeCrosshair(1);
            InkCrosshair_HiDpi = CursorImage.MakeCrosshair(2);
            SetCursorForState();


            UpdateWindowAndStatus();

            _stylusInput = new RealTimeStylus(this, true);
            _stylusInput.MultiTouchEnabled = true;
            _stylusInput.AllTouchEnabled = true;

            // Async calls get triggered on the UI thread, so we use this to trigger updates to WinForms visuals.
            _stylusInput.AsyncPluginCollection?.Add(new DataTriggerStylusPlugin(this));

            AddInputPlugin(_stylusInput, new CanvasDrawingPlugin(this, _canvas, new WinFormsKeyboard()));

            _stylusInput.Enabled = true; 
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _canvas.Dispose();
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
        private volatile bool _ignoreDraw;
        private int _scale;
        private bool _shiftDown;

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e)
        {
            if (_ignoreDraw) return;
            _ignoreDraw = true;
            lock (_drawLock)
            {
                _canvas.RenderToGraphics(e.Graphics, Width, Height, e.ClipRectangle);
            }
            _ignoreDraw = false;
        }

        public void CanvasChanged(Rectangle dirtyRect) {
            Invalidate(dirtyRect);
        }

        /// <inheritdoc />
        public void DataCollected(RealTimeStylus sender) { }

        private void UpdateWindowAndStatus()
        {
            Text = $"{_canvas.FileName()} - Slick {_lastScalePercent:#}%";
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

            _scale = _canvas.SwitchScale();

            _lastScalePercent = 100.0 / (1 << (_scale - 1));
            mapButton.Text = (_scale == EndlessCanvas.MaxScale) ? "Canvas" : "Map";

            SetCursorForState();
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
                    UpdateWindowAndStatus();
                    Invalidate();
                    return;

                default:
                    return;
            }
        }

        private void MoreButton_Click(object sender, EventArgs e)
        {
            // show extras interface
            var dlog = new Extras(_canvas, floatingImage1, floatingText1);
            dlog.Location = moreButton?.PointToScreen(new Point(-dlog.Width, -dlog.Height)) ?? new Point(Right, Bottom);
            dlog.ShowDialog();
        }

        private void MainWindow_ClientSizeChanged(object sender, EventArgs e)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (_canvas == null) return;
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
            _scale = 1;
            mapButton.Text = "Map";
            SetCursorForState();
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

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            _shiftDown = e?.Shift ?? false;
            SetCursorForState();
        }

        private void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            _shiftDown = false;
            SetCursorForState();
        }

        private void MainWindow_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            SetCursorForState();
        }

        protected override void OnRescale(int dpi)
        {
            SetCursorForState();
            Invalidate();
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (_canvas != null) {
                _canvas.Dpi = dpi;
                _canvas.ResetTileCache();
            }
        }

        private void SetCursorForState()
        {
            if (_scale == 1 && !_shiftDown)
            {
                if (Dpi > 120) Cursor = InkCrosshair_HiDpi;
                else Cursor = InkCrosshair_LoDpi;
            }
            else
            {
                Cursor = Cursors.NoMove2D;
            }
        }

        private void TextButton_Click(object sender, EventArgs e)
        {
            if (floatingText1 != null) {
                floatingText1.NormaliseControlScale();
                floatingText1.Visible = true;
            }
        }
    }
}
