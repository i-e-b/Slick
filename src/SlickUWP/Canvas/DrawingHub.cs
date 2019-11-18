using System.Numerics;
using Windows.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;

namespace SlickUWP.Canvas
{
    /// <summary>
    /// A single hook point for drawing onto tiles
    /// </summary>
    public class DrawingHub {
        public void Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            var g = args?.DrawingSession;
            if (sender == null || g == null) return;

            var tile = sender.Tag as CachedTile; // this is set in CachedTile

            if (tile == null) { // this shows while the UWP is waiting to do async calls
                g.DrawCircle(new Vector2(128, 128), 24, Colors.Gray);
                g.Flush();
                return;
            }

            tile.DrawToSession(sender, g, true);
        }
    }
}