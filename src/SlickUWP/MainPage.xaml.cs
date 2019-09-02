using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Microsoft.Graphics.Canvas;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace SlickUWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly byte[] values;

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

            var ip = baseInkCanvas.InkPresenter;
            
            
            // These to have Windows handle drawing and input
            //ip.StrokesCollected += InkPresenter_StrokesCollected;
            //ip.StrokesErased += Ip_StrokesErased ;
            //ip.InputProcessingConfiguration.Mode = InkInputProcessingMode.Inking;

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
    }
}
