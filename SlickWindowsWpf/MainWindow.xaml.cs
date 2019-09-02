using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SlickWindowsWpf
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // Docs say this should exist, but it doesn't.
            //bgInkCanvas.InkPresenter.
        }

        private void BgInkCanvas_StylusDown(object sender, StylusDownEventArgs e)
        {
            //e.StylusDevice.Capture(this, CaptureMode.SubTree);
            diagList.Items.Add($"{e.StylusDevice.Name} down at {e.GetPosition(this)}");
        }

        private void BgInkCanvas_StylusUp(object sender, StylusEventArgs e)
        {
            //e.StylusDevice.Capture(this, CaptureMode.None);
            diagList.Items.Add(e.StylusDevice.Name + " up");
        }

        private void BgInkCanvas_StrokeCollected(object sender, InkCanvasStrokeCollectedEventArgs e)
        {
            diagList.Items.Add($"Stroke with {e.Stroke.StylusPoints.Count} points");

            // this post-fixes the DPI problem in a weird way.
            e.Stroke.Transform(new ScaleTransform(0.5,0.5).Value, true);
        
        }

        private void BgInkCanvas_TouchDown(object sender, TouchEventArgs e)
        {
            diagList.Items.Add("Touch down");
        }

        private void BgInkCanvas_TouchUp(object sender, TouchEventArgs e)
        {
            diagList.Items.Add("Touch up");
        }

        private void BgInkCanvas_TouchMove(object sender, TouchEventArgs e)
        {
            diagList.Items.Add("Touch move");
        }

        private void BgInkCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            diagList.Items.Add("Mouse down");
        }

        private void BgInkCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            diagList.Items.Add("Mouse up");
        }

        private void Window_DpiChanged(object sender, DpiChangedEventArgs e)
        {
            diagList.Items.Add("DPI changed");
            bgInkCanvas.SnapsToDevicePixels = true;
            bgInkCanvas.RenderTransform.Value.Scale(e.OldDpi.DpiScaleX / e.NewDpi.DpiScaleX,e.OldDpi.DpiScaleY / e.NewDpi.DpiScaleY);
            bgInkCanvas.UpdateLayout();
        }
    }
}
