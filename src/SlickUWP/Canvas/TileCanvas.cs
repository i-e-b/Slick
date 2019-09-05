using Windows.UI.Xaml.Controls;
using JetBrains.Annotations;
using SlickCommon.Storage;

namespace SlickUWP.Canvas
{
    /// <summary>
    /// Handles scrolling and scaling inputs, manages a set of tile-image controls for display
    /// </summary>
    public class TileCanvas
    {
        [NotNull] private readonly Grid _displayContainer;
        [NotNull] private readonly IStorageContainer _tileStore;

        public double X;
        public double Y;

        /// <summary>
        /// Start rendering tiles into a display container. Always starts at 0,0
        /// </summary>
        public TileCanvas([NotNull]Grid displayContainer, [NotNull]IStorageContainer tileStore)
        {
            _displayContainer = displayContainer;
            _tileStore = tileStore;
            X = 0.0;
            Y = 0.0;

            Invalidate();
        }

        /// <summary>
        /// Rebuild the tile cache.
        /// Use this if the window size changes or the tile store is updated
        /// </summary>
        public void Invalidate()
        {
            
        }

        /// <summary>
        /// Relative move of the canvas (e.g. from touch scrolling)
        /// </summary>
        public void Scroll(double dx, double dy){ }

        /// <summary>
        /// Set an absolute scroll position
        /// </summary>
        public void ScrollTo(double x, double y){ }

        /// <summary>
        /// Rotate through scaling options
        /// </summary>
        public int SwitchScale() { return 0; }

        /// <summary>
        /// Centre on the given point, and set scale as 1:1
        /// </summary>
        public void CentreAndZoom(int wX, int wY){ }

        /// <summary>
        /// Remove all tiles from the display container.
        /// <para></para>
        /// Call when page is being switched.
        /// </summary>
        public void Close() {

        }
    }
}