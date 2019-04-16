using System;
using System.Windows.Forms;
using JetBrains.Annotations;
using Microsoft.StylusInput;
using Microsoft.StylusInput.PluginData;

namespace SlickWindows
{
    public partial class MainWindow : Form, IDataTriggered
    {
        // Declare the real time stylus.
        [NotNull]private readonly RealTimeStylus StylusInput;

        public MainWindow()
        {
            InitializeComponent();

            StylusInput = new RealTimeStylus(this, true);

            // Async calls get triggered on the UI thread, so we use this to trigger updates to WinForms visuals.
            StylusInput.AsyncPluginCollection?.Add(new DataTriggerStylusPlugin(this));

            AddInputPlugin(new RealtimeRendererPlugin(CreateGraphics()));

            StylusInput.Enabled = true; 
        }

        private void AddInputPlugin(IStylusSyncPlugin plugin)
        {
            if (plugin == null || StylusInput.SyncPluginCollection == null) throw new Exception("Input state not correct");
            var rtsEnabled = StylusInput.Enabled;
            StylusInput.Enabled = false;
            StylusInput.SyncPluginCollection.Add(plugin);
            StylusInput.Enabled = rtsEnabled;
        }

        /// <inheritdoc />
        public void DataCollected(RealTimeStylus sender, CustomStylusData data)
        {

        }

        /// <inheritdoc />
        public void Error(RealTimeStylus sender, ErrorData data)
        {

        }
    }
}
