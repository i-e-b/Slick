﻿using System;
using System.Drawing;
using System.Windows.Forms;
using JetBrains.Annotations;
using Microsoft.StylusInput;
using SlickWindows.Canvas;
using SlickWindows.Gui;
using SlickWindows.Input;

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
            _canvas = new EndlessCanvas(DeviceDpi, @"C:\Temp\CanvTest");

            _stylusInput = new RealTimeStylus(this, true);
            _stylusInput.MultiTouchEnabled = true;
            _stylusInput.AllTouchEnabled = true;

            // Async calls get triggered on the UI thread, so we use this to trigger updates to WinForms visuals.
            _stylusInput.AsyncPluginCollection?.Add(new DataTriggerStylusPlugin(this));

            AddInputPlugin(_stylusInput, new RealtimeRendererPlugin(_canvas, new WinFormsKeyboard()));

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
            Text = $"Slick ({_canvas.X}, {_canvas.Y})";
            Invalidate();
        }

        private void paletteButton_Click(object sender, EventArgs e)
        {
            var pal = new PaletteWindow
            {
                Canvas = _canvas,
                Location = paletteButton.PointToScreen(new Point(0,0))
            };
            pal.ShowDialog();
        }

        private void mapButton_Click(object sender, EventArgs e)
        {

        }
    }
}