using System;
using System.Diagnostics;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using JetBrains.Annotations;
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
        public bool PinsVisible { get { return pinsView?.Opacity > 0.5; } }

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
            if (pinsView == null) throw new Exception("Palette object is missing");

            ip.ActivateCustomDrying();

            if (ip.UnprocessedInput == null || ip.InputProcessingConfiguration == null || ip.InputConfiguration == null) throw new Exception("Ink Presenter is malformed");

            // These to get all the drawing and input directly
            ip.UnprocessedInput.PointerMoved += UnprocessedInput_PointerMoved;
            ip.UnprocessedInput.PointerPressed += UnprocessedInput_PointerPressed;
            ip.UnprocessedInput.PointerReleased += UnprocessedInput_PointerReleased;

            ip.InputProcessingConfiguration.Mode = InkInputProcessingMode.None;
            ip.InputProcessingConfiguration.RightDragAction = InkInputRightDragAction.AllowProcessing;
            
            ip.InputConfiguration.IsEraserInputEnabled = true;
            ip.InputConfiguration.IsPrimaryBarrelButtonInputEnabled = true;

            ip.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Touch | CoreInputDeviceTypes.Pen;
            ip.IsInputEnabled = true;

            paletteView.Opacity = 0.0; // 1.0 is 100%, 0.0 is 0%
            pinsView.Opacity = 0.0; // 1.0 is 100%, 0.0 is 0%

            _tileStore = LoadTileStore(@"C:\Users\IainBallard\Documents\Slick\test.slick");
            if (renderLayer == null) throw new Exception("Invalid page structure (1)");

            _tileCanvas = new TileCanvas(renderLayer, _tileStore);
            pinsView?.SetConnections(_tileCanvas, _tileStore);

            _wetInk = new WetInkCanvas(wetInkCanvas ?? throw new Exception("Invalid page structure (2)"));


            _tileCanvas?.Invalidate();
        }

        /// <summary>
        /// Load a storage container for the currently selected storage file
        /// </summary>
        [NotNull]private IStorageContainer LoadTileStore([NotNull]string path)
        {
            //var path = @"C:\Users\IainBallard\Documents\Slick\test.slick"; // TODO: proper picker
            var file = Sync.Run(()=>StorageFile.GetFileFromPathAsync(path));
            
            if (file == null || !file.IsAvailable) { throw new Exception("Failed to load Slick file"); }

            
            var accessStream = Sync.Run(() => file.OpenAsync(FileAccessMode.ReadWrite));
            var wrapper = new StreamWrapper(accessStream);
            var store = new LiteDbStorageContainer(wrapper);

            if (_tileCanvas != null) pinsView?.SetConnections(_tileCanvas, store);

            return store;
        }
        

        private void UnprocessedInput_PointerReleased(InkUnprocessedInput sender, PointerEventArgs args)
        {
            // commit the wet ink to the canvas and re-draw
            _wetInk?.CommitTo(_tileCanvas);
            _tileCanvas?.Invalidate();
        }

        ulong lastStamp = 0;
        private void UnprocessedInput_PointerPressed(InkUnprocessedInput sender, PointerEventArgs args)
        {
            _mode = InteractionMode.None;
            if (args?.CurrentPoint == null || paletteView == null || _wetInk == null || _tileCanvas == null) return;
            var thisPoint = args.CurrentPoint;

            // TODO: need to detect double-taps for centre-and-zoom etc.
            var diff = thisPoint.Timestamp - lastStamp;
            lastStamp = thisPoint.Timestamp;
            var tapTime = TimeSpan.FromMilliseconds(diff / 1000.0); // by guesswork

            //var appView = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView();
            //appView.Title = tapTime.TotalSeconds + " seconds";

            // Check for palette
            if (PaletteVisible) {
                _mode = InteractionMode.PalettePicker;
                if (paletteView.IsHit(args))
                {
                    // set pointer to ink...
                    _wetInk.SetPenColor(thisPoint, paletteView.LastColor);
                    _wetInk.SetPenSize(thisPoint, paletteView.LastSize);
                    paletteView.Opacity = 0.0;
                }
                return;
            }


            // Don't allow drawing when zoomed out
            if (_tileCanvas.CurrentZoom() != 1) {
                if (IsDoubleTap(args, tapTime)) {
                    _tileCanvas?.CentreAndZoom(thisPoint.Position.X, thisPoint.Position.Y);
                    SetCorrectZoomControlText();
                }

                _lastPoint = thisPoint.Position;
                _mode = InteractionMode.Move;
                return;
            }

            _lastPoint = thisPoint.Position;

            // Finally, set the interaction mode
            if (args.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift))
            {
                _mode = InteractionMode.Move;
            }
            else if (thisPoint.PointerDevice?.PointerDeviceType == PointerDeviceType.Touch)
            {
                _mode = InteractionMode.Move;
            }
            else
            {
                _mode = InteractionMode.Draw;
                _wetInk?.StartStroke(sender, args);
            }
        }

        private bool IsDoubleTap(PointerEventArgs args, TimeSpan tapTime)
        {
            return tapTime.TotalSeconds > 0 && tapTime.TotalSeconds < 0.6 && Distance(_lastPoint, args.CurrentPoint.Position) < 32;
        }

        private double Distance(Point a, Point b)
        {
            return Math.Sqrt(
                Math.Pow(a.X - b.X, 2)
                +
                Math.Pow(a.Y - b.Y, 2)
                );
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

        /// <summary>Used to rate limit move calls, as it can swamp the UI with changes </summary>
        [NotNull] private readonly Stopwatch moveSw = new Stopwatch();

        private void MoveCanvas([NotNull]PointerEventArgs args)
        {
            var thisPoint = args.CurrentPoint;
            if (thisPoint == null) return;

            if (moveSw.IsRunning && moveSw.ElapsedMilliseconds < 33) return;
            moveSw.Restart();

            var dx = _lastPoint.X - thisPoint.Position.X;
            var dy = _lastPoint.Y - thisPoint.Position.Y;
            if (Math.Abs(dx) < 2 && Math.Abs(dy) < 2) return;

            _tileCanvas?.Scroll(dx, dy);
            _lastPoint = thisPoint.Position;
        }

        private async void PickPageButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (pinsView != null) pinsView.Opacity = 0.0;
            if (paletteView != null) paletteView.Opacity = 0.0;

            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            //picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter?.Add(".slick");

            //NEVER DO THIS: var file = Sync.Run(() => picker.PickSingleFileAsync()); // doing anything synchronous here causes a deadlock.

            // ReSharper disable once PossibleNullReferenceException
            var file = await picker.PickSingleFileAsync().AsTask();
            if (file == null || !file.IsAvailable) return;

            _tileStore?.Dispose();
            _tileStore = LoadTileStore(file.Path);
            _tileCanvas?.ChangeStorage(_tileStore);
            SetCorrectZoomControlText();
            // Application now has read/write access to the picked file
        }

        private void MapModeButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            _tileCanvas?.SwitchScale();
            SetCorrectZoomControlText();
        }

        private void SetCorrectZoomControlText()
        {
            if (mapModeButton != null) mapModeButton.Content = (_tileCanvas?.CurrentZoom() == 4) ? "Canvas" : "Map";
        }

        private void UndoButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            _tileCanvas?.Undo();
        }

        private static CoreCursor drawCurs =  new CoreCursor(CoreCursorType.Cross, 0);
        private static CoreCursor moveCurs =  new CoreCursor(CoreCursorType.SizeAll, 0);

        private void Page_PreviewKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            //  https://blogs.msdn.microsoft.com/devfish/2012/08/01/customcursors-in-windows-8-csharp-metro-applications/
            if (e.Key == VirtualKey.Shift) {
                Window.Current.CoreWindow.PointerCursor = moveCurs;
            }
        }

        private void Page_PreviewKeyUp(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Shift) {
                Window.Current.CoreWindow.PointerCursor = drawCurs;
            }
        }

        private void PinsButton_Click(object sender, RoutedEventArgs e)
        {
            if (pinsView == null || paletteView == null) return;
            paletteView.Opacity = 0.0;
            pinsView.Opacity = pinsView.Opacity >= 0.5 ? 0.0 : 1.0;
        }
        

        private void ShowPaletteButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (pinsView == null || paletteView == null) return;
            pinsView.Opacity = 0.0;
            paletteView.Opacity = paletteView.Opacity >= 0.5 ? 0.0 : 1.0;
        }
    }
}
