using System;
using System.IO;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml.Controls;
using JetBrains.Annotations;
using LiteDB;
using Microsoft.Graphics.Canvas.UI.Xaml;
using SlickCommon.Storage;
using SlickUWP.Adaptors;
using SlickUWP.Canvas;
using StorageNode = SlickUWP.Storage.StorageNode;

// GENERAL PLAN:
/*

    The page will have these layers (bottom to top):

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
    public sealed partial class MainPage : Page
    {
        private IStorageContainer _tileStore;
        private TileCanvas _tileCanvas;
        private WetInkCanvas _wetInk;

        public MainPage()
        {
            InitializeComponent();
            
            _tileStore = LoadTileStore();
            if (renderLayer == null) throw new Exception("Invalid page structure (1)");

            _tileCanvas = new TileCanvas(renderLayer, _tileStore);

            _wetInk = new WetInkCanvas(wetInkCanvas ?? throw new Exception("Invalid page structure (2)"));
            
            
            // Test loading a tile into the page at runtime
           /* dynamicTile = new CanvasControl();
            dynamicTile.Draw += DynamicTile_Draw;
            dynamicTile.Margin = new Thickness(0.0);
            dynamicTile.Height = 256;
            dynamicTile.Width = 256;
            dynamicTile.HorizontalAlignment = HorizontalAlignment.Left;
            dynamicTile.VerticalAlignment = VerticalAlignment.Top;
            renderLayer.Children.Add(dynamicTile);*/
            

            // Set up pen/mouse/touch input
            var ip = baseInkCanvas.InkPresenter;
            // These to get all the drawing and input directly
            ip.ActivateCustomDrying();
            ip.UnprocessedInput.PointerMoved += UnprocessedInput_PointerMoved;
            ip.UnprocessedInput.PointerPressed += UnprocessedInput_PointerPressed;
            ip.UnprocessedInput.PointerReleased += UnprocessedInput_PointerReleased;
            ip.InputProcessingConfiguration.Mode = InkInputProcessingMode.None;
            
            text.Text = "Ready";

            ip.IsInputEnabled = true;
            ip.InputDeviceTypes = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Touch | CoreInputDeviceTypes.Pen;
            ip.InputProcessingConfiguration.RightDragAction = InkInputRightDragAction.AllowProcessing;
            ip.InputConfiguration.IsEraserInputEnabled = true;


            // Quick test of loading LiteDB file

            //var path = @"C:\Users\IainBallard\Documents\Slick\test.slick";
            //var file = Sync.Run(()=>StorageFile.GetFileFromPathAsync(path));
            //TestDbLoad(file);
        }

        /// <summary>
        /// Load a storage container for the currently selected storage file
        /// </summary>
        [NotNull]private IStorageContainer LoadTileStore()
        {
            var path = @"C:\Users\IainBallard\Documents\Slick\test.slick"; // TODO: proper picker
            var file = Sync.Run(()=>StorageFile.GetFileFromPathAsync(path));
            
            if (file == null || !file.IsAvailable) { throw new Exception("Failed to load Slick file"); }
            
            var readStream = Sync.Run(() => file.OpenAsync(FileAccessMode.Read));
            var wrapper = new StreamWrapper(readStream);
            var store = new LiteDbStorageContainer(wrapper);

            return store;
        }
        

        private void UnprocessedInput_PointerReleased(InkUnprocessedInput sender, PointerEventArgs args)
        {
            // commit the wet ink to the canvas and re-draw
            _wetInk?.CommitTo(_tileStore);
            _tileCanvas?.Invalidate();
        }

        private void UnprocessedInput_PointerPressed(InkUnprocessedInput sender, PointerEventArgs args)
        {
            _wetInk?.StartStroke(sender, args);
        }

        private void UnprocessedInput_PointerMoved(InkUnprocessedInput sender, PointerEventArgs args)
        {
            _wetInk?.Stroke(args);
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
                    text.Text += " File OK";
                    TestDbLoad(file);
                }
                else
                {
                    text.Text += " File FAILED";
                }
            }
            catch (Exception ex)
            {
                text.Text += "\r\nException: " + ex;
            }
        }

        // TESTCRAP
        private void DynamicTile_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            /*
            // try loading a tile from a test DB
            
            var path = @"C:\Users\IainBallard\Documents\Slick\test.slick";
            var file = Sync.Run(()=>StorageFile.GetFileFromPathAsync(path));
            if (file != null && file.IsAvailable)
            {
                //using (var readStream = await file.OpenAsync(FileAccessMode.Read)) // this results in an 'object closed' error later on
                using (var readStream = Sync.Run(() => file.OpenAsync(FileAccessMode.Read)))
                {
                    var wrapper = new StreamWrapper(readStream);
                    var store = new LiteDbStorageContainer(wrapper);
                    var pos = new PositionKey(0,0);
                    var name = pos.ToString();
                    var res = store.Exists(name);
                    if (res.IsFailure) return;

                    var version = res.ResultData?.CurrentVersion ?? 1;
                    var img = store.Read(name, "img", version);

                    if (img.IsFailure) return;

                    var fileData = InterleavedFile.ReadFromStream(img.ResultData);
                    var Red = new byte[65536];
                    var Green = new byte[65536];
                    var Blue = new byte[65536];
                    if (fileData != null) WaveletCompress.Decompress(fileData, Red, Green, Blue, 1);

                    var packed = new byte[65536 * 4];

                    for (int i = 0; i < Red.Length; i++)
                    {
                        // could do a EPX 2x here
                        packed[4*i+0] = Blue[i];
                        packed[4*i+1] = Green[i];
                        packed[4*i+2] = Red[i];

                        if (Blue[i] > 250 && Green[i] > 250 && Red[i] > 250){
                            packed[4*i+3] = 0;
                        } else {
                            packed[4*i+3] = 255;
                        }
                    }
                    
                    using (var bmp = CanvasBitmap.CreateFromBytes(sender, packed, 256, 256,
                        DirectXPixelFormat.B8G8R8A8UIntNormalized, 96 , CanvasAlphaMode.Premultiplied))
                    {
                        try
                        {
                            args.DrawingSession.DrawImage(bmp, new Rect(0, 0, 256, 256));
                            text.Text += "; Render from DB succeeded!";
                        }
                        catch (Exception ex)
                        {
                            text.Text += "; Failed to draw: " + ex;
                        }
                    }
                }
            }
            else
            {
                text.Text += $"; can't access DB file";
                args.DrawingSession.DrawCircle(new Vector2(64,64),64, Color.FromArgb(255, 255, 127, 0));
            }
    */
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
                    text.Text += $"; connected to test DB. {nodes.Count()} nodes";
                }
            }
            else
            {
                text.Text += $"; can't access DB file";
            }
        }

        // TESTCRAP
        private void TestCanv_Draw(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
        {
            /*
            using (var bmp = CanvasBitmap.CreateFromBytes(sender, values, 128, 128,
                DirectXPixelFormat.B8G8R8A8UIntNormalized, 96 , CanvasAlphaMode.Ignore)) // pixel format must be on the supported list, or the app will bomb
                // DPI doesn't seem to do anything
            {
                //bmp.SetPixelBytes(values, 0, 0, 128, 128);
                args.DrawingSession.DrawImage(bmp, new Rect(0, 0, 128, 128));
            }
            */
        }

    }
}
