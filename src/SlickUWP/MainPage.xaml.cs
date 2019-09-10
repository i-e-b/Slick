using System;
using System.IO;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml.Controls;
using JetBrains.Annotations;
using LiteDB;
using SlickCommon.Storage;
using SlickUWP.Adaptors;
using SlickUWP.Canvas;

/*

    The page has these layers (bottom to top):

    BackgroundCanvas (a container for dynamic tiles)
        [DynamicTiles] (renderer for Endless canvas tiles, handles offsets)
    InkCanvas (captures user input)
    WetCanvas (draws any currently-active wet ink)
    Buttons (for palette, load, map etc)


    Each *visible* image tile in the endless canvas gets its own `CanvasControl`
*/

namespace SlickUWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    // ReSharper disable once RedundantExtendsListEntry
    public sealed partial class MainPage : Page
    {
        private IStorageContainer _tileStore;
        private TileCanvas _tileCanvas;
        private WetInkCanvas _wetInk;
        
        Point _lastPoint;
        InteractionMode _mode = InteractionMode.None;

        public bool PaletteVisible { get { return paletteView?.Opacity > 0.5; } }

        public MainPage()
        {
            InitializeComponent();
        }


        /// <summary>
        /// Start the canvas up with the default document
        /// </summary>
        private void Page_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            // Set up pen/mouse/touch input
            var ip = baseInkCanvas?.InkPresenter;

            if (ip == null) throw new Exception("Base ink presenter is missing");
            if (paletteView == null) throw new Exception("Palette object is missing");

            ip.ActivateCustomDrying();

            if (ip.UnprocessedInput == null || ip.InputProcessingConfiguration == null || ip.InputConfiguration == null) throw new Exception("Ink Presenter is malformed");

            // These to get all the drawing and input directly
            ip.UnprocessedInput.PointerMoved += UnprocessedInput_PointerMoved;
            ip.UnprocessedInput.PointerPressed += UnprocessedInput_PointerPressed;
            ip.UnprocessedInput.PointerReleased += UnprocessedInput_PointerReleased;
            ip.InputProcessingConfiguration.Mode = InkInputProcessingMode.None;
            
            ip.IsInputEnabled = true;
            ip.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Touch | CoreInputDeviceTypes.Pen;
            ip.InputProcessingConfiguration.RightDragAction = InkInputRightDragAction.AllowProcessing;
            ip.InputConfiguration.IsEraserInputEnabled = true;

            paletteView.Opacity = 0.0; // 1.0 is 100%, 0.0 is 0%


            _tileStore = LoadTileStore();
            if (renderLayer == null) throw new Exception("Invalid page structure (1)");

            _tileCanvas = new TileCanvas(renderLayer, _tileStore);

            _wetInk = new WetInkCanvas(wetInkCanvas ?? throw new Exception("Invalid page structure (2)"));


            _tileCanvas?.Invalidate();
        }

        /// <summary>
        /// Load a storage container for the currently selected storage file
        /// </summary>
        [NotNull]private IStorageContainer LoadTileStore()
        {
            var path = @"C:\Users\IainBallard\Documents\Slick\test.slick"; // TODO: proper picker
            var file = Sync.Run(()=>StorageFile.GetFileFromPathAsync(path));
            
            if (file == null || !file.IsAvailable) { throw new Exception("Failed to load Slick file"); }
            
            var accessStream = Sync.Run(() => file.OpenAsync(FileAccessMode.ReadWrite));
            var wrapper = new StreamWrapper(accessStream);
            var store = new LiteDbStorageContainer(wrapper);

            return store;
        }
        

        private void UnprocessedInput_PointerReleased(InkUnprocessedInput sender, PointerEventArgs args)
        {
            // commit the wet ink to the canvas and re-draw
            _wetInk?.CommitTo(_tileCanvas);
            _tileCanvas?.Invalidate();
        }


        private void UnprocessedInput_PointerPressed(InkUnprocessedInput sender, PointerEventArgs args)
        {
            _mode = InteractionMode.None;
            if (args?.CurrentPoint == null || paletteView == null || _wetInk == null) return;


            if (PaletteVisible) {
                _mode = InteractionMode.PalettePicker;
                if (paletteView.IsHit(args))
                {
                    // set pointer to ink...
                    _wetInk.SetPenColor(args.CurrentPoint, paletteView.LastColor);
                    _wetInk.SetPenSize(args.CurrentPoint, paletteView.LastSize);
                    paletteView.Opacity = 0.0;
                }
                return;
            }
            
            _lastPoint = args.CurrentPoint.Position;

            if (args.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift))
            {
                _mode = InteractionMode.Move;
            }
            else if (args.CurrentPoint.PointerDevice?.PointerDeviceType == PointerDeviceType.Touch)
            {
                _mode = InteractionMode.Move;
            }
            else
            {
                _mode = InteractionMode.Draw;
                _wetInk?.StartStroke(sender, args);
            }
        }


        private void UnprocessedInput_PointerMoved(InkUnprocessedInput sender, PointerEventArgs args)
        {
            if (args == null) return;

            switch (_mode) {
                case InteractionMode.Move:
                    MoveCanvas(args);
                    return;

                case InteractionMode.Draw:
                    _wetInk?.Stroke(args);
                    return;

                default: return;
            }
        }

        private void MoveCanvas([NotNull]PointerEventArgs args)
        {
            var thisPoint = args.CurrentPoint;
            if (thisPoint == null) return;

            var dx = _lastPoint.X - thisPoint.Position.X;
            var dy = _lastPoint.Y - thisPoint.Position.Y;
            _tileCanvas?.Scroll(dx, dy);

            _lastPoint = thisPoint.Position;
        }

        private async void PickPageButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
                //picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add(".slick");

                //var file = Sync.Run(() => picker.PickSingleFileAsync()); // doing anything synchronous here causes a deadlock.
                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    // Application now has read/write access to the picked file
                    //text.Text += " File OK";
                    TestDbLoad(file);
                }
                else
                {
                    //text.Text += " File FAILED";
                }
            }
            catch (Exception ex)
            {
                //text.Text += "\r\nException: " + ex;
            }
        }

        // TESTCRAP
        private void TestDbLoad(StorageFile file)
        {
            if (file != null && file.IsAvailable)
            {
                using (IRandomAccessStream readStream = Sync.Run(() => file.OpenAsync(FileAccessMode.Read)))
                {
                    var db = new LiteDatabase(readStream.AsStream());
                    var nodes = db.GetCollection<StorageNode>("map");
                    //text.Text += $"; connected to test DB. {nodes.Count()} nodes";
                }
            }
            else
            {
                //text.Text += $"; can't access DB file";
            }
        }

        private void ShowPaletteButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (paletteView.Opacity >= 0.5)
            {
                // hide palette
                paletteView.Opacity = 0.0;
            }
            else
            {
                // show palette
                paletteView.Opacity = 1.0;
            }

        }
    }
}
