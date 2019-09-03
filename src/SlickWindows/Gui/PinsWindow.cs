using System;
using System.Linq;
using System.Windows.Forms;
using JetBrains.Annotations;
using SlickCommon.Canvas;
using SlickCommon.Storage;
using SlickWindows.Gui.Components;

namespace SlickWindows.Gui
{
    public partial class PinsWindow : AutoScaleForm
    {
        [NotNull]private readonly IEndlessCanvas _canvas;

        public PinsWindow(IEndlessCanvas canvas)
        {
            _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
            InitializeComponent();

            newPinBox?.Focus();
            
            BuildPinList();
        }

        private void BuildPinList()
        {
            if (pinListView == null) return;

            pinListView.Items.Clear();

            // Set a global 'Centre' pin
            pinListView.Items.Add(new ListViewItem("Page Centre") {Tag = InfoPin.Centre()});

            // Read current pins and fill in the list box
            var pins = _canvas.AllPins().OrderBy(p => p?.Description);
            foreach (var pin in pins)
            {
                if (pin == null) continue;
                pinListView.Items.Add(new ListViewItem(pin.Description) {Tag = pin});
            }
        }

        private void ViewButton_Click(object sender, EventArgs e)
        {
            // centre on pin, close the current window

            var pin = SelectedPin();
            if (pin == null) return;

            _canvas.CentreOnPin(pin);

            Close();
        }

        private void PinListView_DoubleClick(object sender, EventArgs e)
        {
            var pin = SelectedPin();
            if (pin == null) return;

            _canvas.CentreOnPin(pin);

            Close();
        }


        private InfoPin SelectedPin()
        {
            if (pinListView?.SelectedItems == null) return null;
            if (pinListView.SelectedItems.Count < 1) return null;
            return pinListView.SelectedItems[0]?.Tag as InfoPin;
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            // Add a new pin and close
            if (string.IsNullOrWhiteSpace(newPinBox?.Text)) return;

            _canvas.WritePinAtCurrentOffset(newPinBox.Text);

            Close();
        }

        private void PinsWindow_Shown(object sender, EventArgs e)
        {
            FormsHelper.NudgeOnScreen(this);
        }

        private void NewPinBox_TextChanged(object sender, EventArgs e)
        {
            if (addButton == null) return;
            addButton.Enabled = !string.IsNullOrWhiteSpace(newPinBox?.Text);
        }

        private void PinListView_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            if (viewButton == null || deleteButton == null) return;
            viewButton.Enabled = deleteButton.Enabled = pinListView?.SelectedItems.Count > 0;
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            var pin = SelectedPin();
            if (pin == null) return;
            
            _canvas.DeletePin(pin);
            PinListView_ItemSelectionChanged(null, null);
            BuildPinList();
        }
    }
}
