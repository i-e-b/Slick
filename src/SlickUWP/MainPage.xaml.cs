using System;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using LiteDB;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;

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
        private readonly byte[] values;
        private CanvasControl dynamicTile;

        public MainPage()
        {
            this.InitializeComponent();
            
            
            
            values = new byte[128*128*4];

            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
                {
                    var i = (y * 128 * 4) + (x * 4);
                    byte v = (byte) ((x == y) ? 0 : 255);

                    values[i+0] = v;
                    values[i+1] = v;
                    values[i+2] = v;
                    values[i+3] = 255;
                }
            }

            // Test loading a tile into the page at runtime
            dynamicTile = new CanvasControl();
            dynamicTile.Draw += DynamicTile_Draw;
            dynamicTile.Margin = new Thickness(0.0);
            dynamicTile.Height = 128;
            dynamicTile.Width = 128;
            dynamicTile.HorizontalAlignment = HorizontalAlignment.Left;
            dynamicTile.VerticalAlignment = VerticalAlignment.Top;
            windowGrid.Children.Add(dynamicTile);
            

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

            var path = @"C:\Users\IainBallard\Documents\Slick\test.slick";
            var file = Sync.Run(()=>StorageFile.GetFileFromPathAsync(path));
            TestDbLoad(file);
        }

        private void DynamicTile_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            args.DrawingSession.DrawCircle(new Vector2(64,64),64, Color.FromArgb(255, 255, 127, 0));
        }

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

        private void UnprocessedInput_PointerReleased(InkUnprocessedInput sender, PointerEventArgs args)
        {
            var name = args.CurrentPoint.Properties.IsEraser ? "eraser" : args.CurrentPoint.PointerDevice.PointerDeviceType.ToString(); // never correct
            text.Text += $" {name} up";

            // TODO: commit the wet ink to the canvas
        }

        private void UnprocessedInput_PointerPressed(InkUnprocessedInput sender, PointerEventArgs args)
        {
            var name = args.CurrentPoint.Properties.IsEraser ? "eraser" : args.CurrentPoint.PointerDevice.PointerDeviceType.ToString(); // generally correct
            
            text.Text += $" {name} down @ {args.CurrentPoint.Position}";

            // TODO: start some "wet" ink
        }

        private void UnprocessedInput_PointerMoved(InkUnprocessedInput sender, PointerEventArgs args)
        {
            text.Text += ".";

            // TODO: update the wet ink object

            // endless canvas: should have a set of tile images that are offset and filled based on the underlying
            // infinite image
            var transform = new TranslateTransform {
                X = args.CurrentPoint.Position.X,
                Y = args.CurrentPoint.Position.Y
            };
            testCanv.RenderTransform = transform;


            if (args.CurrentPoint.Position.X < 1 || args.CurrentPoint.Position.Y < 1) return;

            // play with update:
            var y = (int)(args.CurrentPoint.Position.Y % 128) * 128*4;
            var x = (int)(args.CurrentPoint.Position.X % 128) * 4;
            values[y+x+0] = 0;
            values[y+x+1] = 0;
            values[y+x+2] = 0;

            // Any time we update a tile in the backing.
            testCanv.Invalidate();
        }

        private void TestCanv_Draw(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
        {
            using (var bmp = CanvasBitmap.CreateFromBytes(sender, values, 128, 128,
                DirectXPixelFormat.B8G8R8A8UIntNormalized, 96 /*dpi*/, CanvasAlphaMode.Ignore)) // pixel format must be on the supported list, or the app will bomb
                // DPI doesn't seem to do anything
            {
                bmp.SetPixelBytes(values, 0, 0, 128, 128);
                args.DrawingSession.DrawImage(bmp, new Rect(0, 0, 128, 128));
            }
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
    }
    /// <summary>
    /// Helper class to properly wait for async tasks
    /// </summary>
    public static class Sync  
    {
        private static readonly TaskFactory _taskFactory = new
            TaskFactory(CancellationToken.None,
                TaskCreationOptions.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);



        /// <summary>
        /// Run an async function synchronously and return the result
        /// </summary>
        public static TResult Run<TResult>(Func<IAsyncOperation<TResult>> func)
        {
            return _taskFactory.StartNew(() => func().AsTask()).Unwrap().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Run an async function synchronously and return the result
        /// </summary>
        public static TResult Run<TResult>(Func<Task<TResult>> func) => _taskFactory.StartNew(func).Unwrap().GetAwaiter().GetResult();

        /// <summary>
        /// Run an async function synchronously
        /// </summary>
        public static void Run(Func<Task> func) => _taskFactory.StartNew(func).Unwrap().GetAwaiter().GetResult();
    }
}
