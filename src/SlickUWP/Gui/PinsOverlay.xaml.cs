using System.Linq;
using Windows.Devices.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using JetBrains.Annotations;
using SlickCommon.Storage;
using SlickUWP.Canvas;

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
            if (existingPinList?.Items == null) return;
            existingPinList.Items.Clear();
            existingPinList.Items.Add(new ListViewItem
            {
                Tag = InfoPin.Centre(),
                Content = "Page Centre"
            });
            
            var pinResult = _storage.ReadAllPins();
            if (pinResult.IsFailure) return;
            var pins = pinResult.ResultData?.OrderBy(p => p?.Description);
            if (pins == null) return;

            foreach (var pin in pins)
            {
                if (pin == null) continue;
                existingPinList.Items.Add(new ListViewItem
                {
                    Tag = pin,
                    Content = pin.Description
                });
            }
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
            addPinButton.IsEnabled = !string.IsNullOrWhiteSpace(newPinNameBox.Text);
        }

        private void AddPinButton_Click(object sender, RoutedEventArgs e)
        {
            // add a new pin to the database and the list view. Then close the overlay
        }

        private void DeleteSelectedPinButton_Click(object sender, RoutedEventArgs e)
        {
            // delete the selected pin. Don't close the overlay
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
    }
}
