using System.Linq;
using System.Threading;
using Windows.Devices.Input;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using JetBrains.Annotations;
using SlickCommon.Storage;
using SlickUWP.Canvas;
using SlickUWP.CrossCutting;

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace SlickUWP.Gui
{
    public sealed partial class PinsOverlay : UserControl
    {
        int deselect = 0;
        private TileCanvas _view;
        private IStorageContainer _storage;

        public PinsOverlay()
        {
            InitializeComponent();

            existingPinList.Items.Add(new ListViewItem
            {
                Tag = InfoPin.Centre(),
                Content = "Page Centre"
            });
            

            // This crazyness is required to allow us to deselect items in the list.
            existingPinList.SelectionMode = ListViewSelectionMode.Extended;
            existingPinList.DoubleTapped += ExistingPinList_DoubleTapped;
            existingPinList.SelectionChanged += ExistingPinList_SelectionChanged;
        }

        /// <summary>
        /// Conect to a datastore and a canvas view
        /// </summary>
        public void SetConnections([NotNull]TileCanvas view, [NotNull]IStorageContainer storage) {
            _view = view;
            _storage = storage;

            // Load pins (adding the default centre view)
            ThreadPool.QueueUserWorkItem(x => { ReloadPins(); });
        }

        private void ReloadPins()
        {
            if (existingPinList?.Items == null || _storage == null) return;

            var pinResult = _storage.ReadAllPins();
            if (pinResult.IsFailure) return;
            var pins = pinResult.ResultData?.OrderBy(p => p?.Description);
            if (pins == null) return;

            existingPinList.Dispatcher?.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                existingPinList.Items.Clear();
                existingPinList.Items.Add(new ListViewItem
                {
                    Tag = InfoPin.Centre(),
                    Content = "Page Centre"
                });

                foreach (var pin in pins)
                {
                    if (pin == null) continue;
                    existingPinList.Items.Add(new ListViewItem
                    {
                        Tag = pin,
                        Content = pin.Description
                    });
                }
            });
        }

        private void ExistingPinList_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            CentreOnPin(existingPinList?.SelectedItem);

            deselect = (e.PointerDeviceType == PointerDeviceType.Touch) ? 1 : 2;
            existingPinList.SelectedIndex = -1;

            HidePinsOverlay();
        }

        private void ExistingPinList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // enable or disable the view and delete buttons
            if (deselect > 0)
            {
                existingPinList.SelectedIndex = -1;
                deselect--;
            }

            var enable = existingPinList.SelectedItems.Count > 0;
            viewSelectedPinButton.IsEnabled = enable;
            deleteSelectedPinButton.IsEnabled = enable;
        }

        private void NewPinNameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // enable or disable the add button
            if (addPinButton == null) return;
            addPinButton.IsEnabled = !string.IsNullOrWhiteSpace(newPinNameBox?.Text);
        }

        private void AddPinButton_Click(object sender, RoutedEventArgs e)
        {
            // add a new pin to the database and the list view. Then close the overlay
            if (_view == null || _storage == null) return;
            if (string.IsNullOrWhiteSpace(newPinNameBox?.Text)) return;

            var pos = _view.PositionOfCurrentCentre();
            _storage.SetPin(pos.ToString(), newPinNameBox?.Text);
            newPinNameBox.Text = "";

            ThreadPool.QueueUserWorkItem(x => { ReloadPins(); });

            HidePinsOverlay();
        }

        private void DeleteSelectedPinButton_Click(object sender, RoutedEventArgs e)
        {
            // delete the selected pin. Don't close the overlay
            if (_view == null || _storage == null) return;
            if (existingPinList?.SelectedItem == null) return;
            
            if (!(existingPinList?.SelectedItem is ListViewItem item)) return;
            if (!(item.Tag is InfoPin pin)) return;

            _storage.RemovePin(pin.Id);
            ThreadPool.QueueUserWorkItem(x => { ReloadPins(); });
        }

        private void ViewSelectedPinButton_Click(object sender, RoutedEventArgs e)
        {
            CentreOnPin(existingPinList?.SelectedItem);

            // centre the canvas on the selected pin location, then close the overlay
            deselect = 0;
            existingPinList.SelectedIndex = -1;

            HidePinsOverlay();
        }
        

        private void HidePinsOverlay()
        {
            Opacity = 0;
        }

        private void CentreOnPin(object selectedItem)
        {
            if (!(selectedItem is ListViewItem item)) return;
            if (!(item.Tag is InfoPin pin)) return;

            var pos = PositionKey.Parse(pin.Id);
            _view.CentreOn(pos);
        }

        /// <summary>
        /// Show error log in default text editor.
        /// This is tucked away here for simplicity
        /// </summary>
        private void ErrorLogButton_Click(object sender, RoutedEventArgs e)
        {
            Logging.LaunchLogInEditor();
        }
    }
}
