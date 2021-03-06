﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Core.Preview;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using JetBrains.Annotations;
using SlickCommon.Storage;
using SlickUWP.Adaptors;
using SlickUWP.Canvas;
using SlickUWP.CrossCutting;


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
        
        [NotNull] private readonly Stopwatch moveSw = new Stopwatch();
        double sumScrollX,sumScrollY;
        Point _lastPoint;
        InteractionMode _interactionMode = InteractionMode.None;
        PenMode _penMode = PenMode.Ink;

        public bool PaletteVisible { get { return paletteView?.Opacity > 0.5; } }
        public bool PinsVisible { get { return pinsView?.Opacity > 0.5; } }

        public MainPage()
        {
            InitializeComponent();

            var view = SystemNavigationManagerPreview.GetForCurrentView();
            if (view != null) view.CloseRequested += OnCloseRequest;

            KeyDown += MainPage_KeyDown;

            if (exportTilesButton != null) exportTilesButton.Visibility = Visibility.Collapsed;
            if (importImageButton != null) importImageButton.Visibility = Visibility.Collapsed;
        }

        private void MainPage_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e == null) return;

            switch (e.Key)
            {
                case VirtualKey.Delete:
                    if (_penMode != PenMode.Select) return;
                    _tileCanvas?.EraseSelectedTiles();
                    SelectTilesButton_Click(null,null);
                    return;
            }
        }

        private void OnCloseRequest(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        {
            if (_tileStore == null) return;

            _tileStore.Dispose();
            _tileStore = null;
        }

        /// <summary>
        /// Start the canvas up with the default document
        /// </summary>
        private void Page_Loaded(object sender, RoutedEventArgs e)
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

            ip.InputDeviceTypes = CoreInputDeviceTypes.Mouse /*| CoreInputDeviceTypes.Touch*/ | CoreInputDeviceTypes.Pen;
            ip.IsInputEnabled = true;

            paletteView.Opacity = 0.0; // 1.0 is 100%, 0.0 is 0%
            pinsView.Opacity = 0.0; // 1.0 is 100%, 0.0 is 0%

            var defaultFile = Path.Combine(ApplicationData.Current?.RoamingFolder?.Path, "default.slick");
            if (defaultFile == null) throw new Exception("Failed to pick an initial page path");
            _tileStore = Sync.Run(() => LoadTileStore(defaultFile));
            if (_tileStore == null) throw new Exception("Failed to load initial page");
            if (renderLayer == null) throw new Exception("Invalid page structure (1)");

            _tileCanvas = new TileCanvas(renderLayer, _tileStore);
            pinsView?.SetConnections(_tileCanvas, _tileStore);
            ImageImportFloater?.SetCanvasTarget(_tileCanvas);
            TextFloater?.SetCanvasTarget(_tileCanvas);

            _wetInk = new WetInkCanvas(wetInkCanvas ?? throw new Exception("Invalid page structure (2)"));

            _tileCanvas?.Invalidate();
            
            SetTitleBarString(Path.GetFileNameWithoutExtension(defaultFile));
        }

        /// <summary>
        /// Load a storage container for the currently selected storage file
        /// </summary>
        [NotNull]private async Task<IStorageContainer> LoadTileStore([NotNull]string path)
        {
            
            var directoryName = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directoryName)) throw new Exception("Path directory is invalid");

            var folder = await StorageFolder.GetFolderFromPathAsync(directoryName).NotNull();
            if (folder == null) { throw new Exception("Path to Slick file is not available"); }

            var fileName = Path.GetFileName(path);
            if (string.IsNullOrWhiteSpace(fileName)) throw new Exception("Path file name is invalid");
            var file = await folder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists).NotNull();

            if (file == null || !file.IsAvailable) { throw new Exception("Failed to load Slick file"); }

            if (_tileStore != null) {
                _tileStore.Dispose();
                _tileStore = null;
            }

            var accessStream = await file.OpenAsync(FileAccessMode.ReadWrite).NotNull();
            var wrapper = new StreamWrapper(accessStream);

            var store = LoadStorageFile(wrapper);

            if (_tileCanvas != null) {
                pinsView?.SetConnections(_tileCanvas, store);
                ImageImportFloater?.SetCanvasTarget(_tileCanvas);
                TextFloater?.SetCanvasTarget(_tileCanvas);
            }

            return store;
        }

        [NotNull]private static IStorageContainer LoadStorageFile(StreamWrapper wrapper)
        {
            if (wrapper == null) throw new Exception("Could not read file -- failed to access file system");
            try
            {
                return new StreamDbStorageContainer(wrapper);
            }
            catch (Exception streamReason)
            {
                try
                {
                    return new LiteDbStorageContainer(wrapper);
                }
                catch (Exception liteReason)
                {
                    Logging.WriteLogMessage("Failed to read as a StreamDB file:\r\n" + streamReason);
                    Logging.WriteLogMessage("Failed to read as a LiteDB file:\r\n" + liteReason);
                    throw new Exception("Could not read file -- it's either damaged or not a Slick file.");
                }
            }
        }


        private void UnprocessedInput_PointerReleased(InkUnprocessedInput sender, PointerEventArgs args)
        {
            // commit the wet ink to the canvas and re-draw
            _wetInk?.CommitTo(_tileCanvas);
            //RateLimitInvalidate();
        }


        private void UnprocessedInput_PointerPressed(InkUnprocessedInput sender, PointerEventArgs args)
        {
            if (args?.CurrentPoint == null || paletteView == null || _wetInk == null || _tileCanvas == null) return;
            var thisPoint = args.CurrentPoint;

            args.Handled = true;
            var isTouch = thisPoint.PointerDevice?.PointerDeviceType == PointerDeviceType.Touch;
            var shiftPressed = args.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift);

            // Check for palette
            if (PaletteVisible) {
                _interactionMode = InteractionMode.PalettePicker;
                if (!paletteView.IsHit(args)) return;

                // set pointer to ink...
                _wetInk.SetPenColor(thisPoint, paletteView.LastColor);
                _wetInk.SetPenSize(thisPoint, paletteView.LastSize);
                paletteView.Opacity = 0.0;
                return;
            }

            _lastPoint = thisPoint.Position;

            // if in selection mode, don't change
            if (_penMode == PenMode.Select)
            {
                if (shiftPressed || isTouch)
                {
                    _interactionMode = InteractionMode.Move;
                }
                else
                {
                    _interactionMode = InteractionMode.SelectTiles;
                }
                return;
            }

            _interactionMode = InteractionMode.None;

            // Finally, set the interaction mode
            if (shiftPressed)
            {
                _interactionMode = InteractionMode.Move;
            }
            else
            {
                if (isTouch)
                {
                    _interactionMode = InteractionMode.Move;
                }
                else
                {
                    _interactionMode = InteractionMode.Draw;
                    _wetInk?.StartStroke(sender, args);
                }
            }
        }

        private void UnprocessedInput_PointerMoved(InkUnprocessedInput sender, PointerEventArgs args)
        {
            if (args?.CurrentPoint == null || _tileCanvas == null) return;


            if (_processingPointer) return;
            try
            {
                _processingPointer = true;

                switch (_interactionMode)
                {
                    case InteractionMode.Move:
                        MoveCanvas(args);
                        return;

                    case InteractionMode.Draw:
                        var canvasOffset = _tileCanvas?.ScreenToCanvas(0, 0);
                        _wetInk?.Stroke(args, canvasOffset, _tileCanvas);
                        return;

                    case InteractionMode.SelectTiles:
                        _tileCanvas?.AddSelection(args.CurrentPoint.Position.X, args.CurrentPoint.Position.Y);
                        _tileCanvas?.Invalidate();
                        return;

                    default: return;
                }
            }
            finally
            {
                _processingPointer = false;
            }
        }

        
        private void SetTitleBarString(string text) {
            try {
                var appView = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView();
                if (appView == null) return;

                var storeType = _tileStore?.DisplayName() ?? "No file loaded";
                appView.Title = $"Slick {GetAppVersion()} - {text} - {storeType}";
            }
            catch {
                // ignore
            }
        }

        public static string GetAppVersion()
        {
            var package = Package.Current;
            var packageId = package?.Id;
            var version = packageId?.Version;

            if (version == null) return "?";

            return $"{version.Value.Major}.{version.Value.Minor}.{version.Value.Build}.{version.Value.Revision}";
        }


        private volatile bool _processingPointer = false;

        private async void PickPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (pinsView != null) pinsView.Opacity = 0.0;
            if (paletteView != null) paletteView.Opacity = 0.0;

            // Save picker won't pick existing files without prompting for overwrite
            var picker = new Windows.Storage.Pickers.FileSavePicker {CommitButtonText = "Select"};
            picker.FileTypeChoices?.Add("Slick files", new[] { ".slick" });


            // TODO: How do we stop the 'replace' message?
            // TODO: how do we ignore selecting the currently open file?
            // I think I need to make my own file picker :-(

            //NEVER DO THIS: var file = Sync.Run(() => picker.PickSingleFileAsync()); // doing anything synchronous here causes a deadlock.

            // ReSharper disable once PossibleNullReferenceException
            var file = await picker.PickSaveFileAsync().AsTask();
            if (file == null || !file.IsAvailable) return;

            var newStore = await LoadTileStore(file.Path);
            if (newStore == null) return;
            _tileStore?.Dispose();
            _tileStore = newStore;
            _tileCanvas?.ChangeStorage(_tileStore);
            // Application now has read/write access to the picked file

            SetTitleBarString(file.DisplayName);
        }

        private void MapModeButton_Click(object sender, RoutedEventArgs e)
        {
            _tileCanvas?.ResetScale();
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            _tileCanvas?.Undo();
        }

        private void Page_PreviewKeyDown(object sender, [NotNull]Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            //  https://blogs.msdn.microsoft.com/devfish/2012/08/01/customcursors-in-windows-8-csharp-metro-applications/
            if (e.Key == VirtualKey.Shift) {
            }
        }

        private void Page_PreviewKeyUp(object sender, [NotNull]Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Shift)
            {
            }
        }

        private void PinsButton_Click(object sender, RoutedEventArgs e)
        {
            if (pinsView == null || paletteView == null) return;
            paletteView.Opacity = 0.0;
            pinsView.Opacity = pinsView.Opacity >= 0.5 ? 0.0 : 1.0;
            pinsView.Visibility = (pinsView.Opacity >= 0.5) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowPaletteButton_Click(object sender, RoutedEventArgs e)
        {
            if (pinsView == null || paletteView == null) return;
            pinsView.Opacity = 0.0;
            paletteView.Opacity = paletteView.Opacity >= 0.5 ? 0.0 : 1.0;
        }

        private void SelectTilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tileCanvas == null || selectTilesButton == null || pickPageButton == null || exportTilesButton == null || importImageButton == null) return;

            if (_penMode == PenMode.Select) {
                // Toggle OFF
                _interactionMode = InteractionMode.None;
                _penMode = PenMode.Ink;
                exportTilesButton.Visibility = Visibility.Collapsed;
                importImageButton.Visibility = Visibility.Collapsed;
                selectTilesButton.Background = pickPageButton.Background;
                _tileCanvas?.ClearSelection();
            }
            else {
                // Toggle ON
                _interactionMode = InteractionMode.None;
                _penMode = PenMode.Select;
                exportTilesButton.Visibility = Visibility.Visible;
                importImageButton.Visibility = Visibility.Visible;
                selectTilesButton.Background = new SolidColorBrush(Colors.CadetBlue);
            }
        }
        
        private async void ExportTilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tileCanvas == null) return;

            // Read selected tiles
            var rawImage = _tileCanvas.ExportBytes(_tileCanvas.SelectedTiles());
            if (rawImage == null) {
                Logging.WriteLogMessage("ExportTilesButton_Click -> Failed to read tile data");
                return;
            }

            // Select output path
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            picker.FileTypeChoices?.Add("PNG image", new[] { ".png" });

            var file = await picker.PickSaveFileAsync().AsTask().NotNull();
            if (file == null || !file.IsAvailable) {
                Logging.WriteLogMessage("ExportTilesButton_Click -> Failed to get output path");
                return;
            }

            // Save raw image as a PNG file
            using (var fileStream = await file.OpenAsync(FileAccessMode.ReadWrite).NotNull()) 
            { 
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, fileStream).NotNull(); 
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, 
                    (uint)rawImage.Width, 
                    (uint)rawImage.Height,
                    96.0, 
                    96.0, 
                    rawImage.Data); 
                await encoder.FlushAsync().NotNull(); 
            } 
        }

        private async void ImportImageButton_Click(object sender, RoutedEventArgs e)
        {
            if (ImageImportFloater == null) return;

            // Pick an image file
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter?.Add(".png");
            picker.FileTypeFilter?.Add(".jpg");
            picker.FileTypeFilter?.Add(".jpeg");
            picker.FileTypeFilter?.Add(".bmp");

            var result = await picker.PickSingleFileAsync().NotNull();
            if (result?.IsAvailable != true) return;

            // load image into importer
            await ImageImportFloater.LoadFile(result);

            // Show the floating importer
            ImageImportFloater.Margin = new Thickness(128, 128, 0, 0);
            ImageImportFloater.Visibility = Visibility.Visible;
        }
        

        private void textInputButton_Click(object sender, RoutedEventArgs e)
        {
            if (TextFloater == null) return;

            TextFloater.Margin = new Thickness(128, 128, 0, 0);
            TextFloater.Visibility = Visibility.Visible;
        }

        private void baseInkCanvas_ManipulationDelta(object sender, Windows.UI.Xaml.Input.ManipulationDeltaRoutedEventArgs e)
        {
            if (e == null || _tileCanvas == null) return;
            if (e.IsInertial) return; // disable intertia

            var disp = Dispatcher;

            if (disp == null) return;
            
            if (Math.Abs(e.Delta.Expansion) > 2) {
                disp.RunAsync(CoreDispatcherPriority.Normal, () => { _tileCanvas.DeltaScale(e.Delta.Scale); });
            }

            sumScrollX -= e.Delta.Translation.X;
            sumScrollY -= e.Delta.Translation.Y;

            var thresh = _tileCanvas.CurrentZoom();
            if (Math.Abs(sumScrollX) < thresh && Math.Abs(sumScrollY) < thresh) return;
            if (moveSw.IsRunning && moveSw.ElapsedMilliseconds < 10) return;

            moveSw.Restart();
            _tileCanvas.Scroll(sumScrollX, sumScrollY);
            sumScrollX = 0;
            sumScrollY = 0;
            _tileCanvas?.Invalidate();
        }

        /// <summary>Used to rate limit move calls, as it can swamp the UI with changes </summary>
        private void MoveCanvas([NotNull]PointerEventArgs args)
        {
            var thisPoint = args.CurrentPoint;
            if (thisPoint == null || _tileCanvas == null) return;

            if (moveSw.IsRunning && moveSw.ElapsedMilliseconds < 10) return;

            var thresh = _tileCanvas.CurrentZoom();
            var dx = _lastPoint.X - thisPoint.Position.X;
            var dy = _lastPoint.Y - thisPoint.Position.Y;
            if (Math.Abs(dx) < thresh && Math.Abs(dy) < thresh) return;

            moveSw.Restart();
            _tileCanvas?.Scroll(dx, dy);
            _lastPoint = thisPoint.Position;

            _tileCanvas?.Invalidate();
        }

        private void baseInkCanvas_ManipulationCompleted(object sender, Windows.UI.Xaml.Input.ManipulationCompletedRoutedEventArgs e)
        {
            //?
        }

        private void baseInkCanvas_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (e == null || _tileCanvas == null) return;

            // centre and zoom
            var pos = e.GetPosition(baseInkCanvas);
            _tileCanvas.CentreAndZoom(pos.X, pos.Y);
        }

        private void baseInkCanvas_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            //if (e == null || _tileCanvas == null) return;
        }

        private  void baseInkCanvas_PointerWheelChanged(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e == null || _tileCanvas == null) return;

            // wheel-based scrolling
            var point = e.GetCurrentPoint(baseInkCanvas);
            if (point?.Properties == null) return;

            var isHorz = point.Properties.IsHorizontalMouseWheel;
            var delta = point.Properties.MouseWheelDelta / 2.0; // value by experiment

            // flip direction on shift
            if (e.KeyModifiers == VirtualKeyModifiers.Shift) isHorz = !isHorz;

            // zoom with control key
            if (e.KeyModifiers == VirtualKeyModifiers.Control) {
                delta /= 1000.0; // value by experiment
                _tileCanvas.DeltaScale(delta + 1);
                _tileCanvas?.Invalidate();
                return;
            }

            if (isHorz) _tileCanvas.Scroll(-delta, 0);
            else _tileCanvas.Scroll(0, delta);
            _tileCanvas?.Invalidate();
        }

        public async Task LoadActivationFile(FileActivatedEventArgs args)
        {
            if (args?.Files == null) return;  
            if (args.Files.Count < 1) return;

            var path = args.Files[0]?.Path;
            if (path == null) return;

            
            var newStore = await LoadTileStore(path);
            if (newStore == null) return;
            _tileStore?.Dispose();
            _tileStore = newStore;
            _tileCanvas?.ChangeStorage(_tileStore);

            // The number of files received is args.Files.Size
            // The name of the first file is args.Files[0].Name
        }
    }
}
