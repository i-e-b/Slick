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

            if (tile == null) { // this shouldn't ever be visible
                g.DrawCircle(new Vector2(10, 10), 8, Colors.Blue);
                g.Flush();
                return;
            }

            tile.DrawToSession(sender, g, true);
        }
    }
}