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
        private InkSynchronizer _is;

        public MainPage()
        {
            this.InitializeComponent();
            
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
        }

        private void UnprocessedInput_PointerPressed(InkUnprocessedInput sender, PointerEventArgs args)
        {
            var name = args.CurrentPoint.Properties.IsEraser ? "eraser" : args.CurrentPoint.PointerDevice.PointerDeviceType.ToString(); // generally correct
            
            text.Text += $" {name} down @ {args.CurrentPoint.Position}";

        }

        private void UnprocessedInput_PointerMoved(InkUnprocessedInput sender, PointerEventArgs args)
        {
            text.Text += ".";

            var transform = new TranslateTransform {
                X = args.CurrentPoint.Position.X,
                Y = args.CurrentPoint.Position.Y
            };
            testCanv.RenderTransform = transform;
        }

        private void TestCanv_Draw(Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl sender, Microsoft.Graphics.Canvas.UI.Xaml.CanvasDrawEventArgs args)
        {
            
            byte[] values = new byte[128*128*4];
            for (int i = 0; i < values.Length; i+=4)
            {
                values[i+0] = (byte)i;
                values[i + 1] = (byte)(i >> 7);
                values[i+2] = 0;
                values[i+3] = 255;
            }


            CanvasBitmap bmp = CanvasBitmap.CreateFromBytes(sender, values, 128, 128, DirectXPixelFormat.B8G8R8A8UIntNormalized); //new CanvasBitmap();
            bmp.SetPixelBytes(values, 0,0,128,128);
            args.DrawingSession.DrawImage(bmp, new Rect(0,0,128,128));
        }
    }
}
