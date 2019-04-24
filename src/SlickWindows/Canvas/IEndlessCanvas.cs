using System.Drawing;

namespace SlickWindows.Canvas
{
    public interface IEndlessCanvas
    {
        double X { get; }
        double Y { get; }

        /// <summary>
        /// Display from the current offset into a graphics output
        /// </summary>
        void RenderToGraphics(Graphics g, int width, int height);

        /// <summary>
        /// move the offset
        /// </summary>
        void Scroll(double dx, double dy);

        /// <summary>
        /// Draw curve in the current inking colour
        /// </summary>
        void Ink(DPoint start, DPoint end);

        /// <summary>
        /// Save any tiles changed since last save
        /// </summary>
        void SaveChanges();

        /// <summary>
        /// Set current inking color, size, etc
        /// </summary>
        void SetPen(int stylusId, Color color, double size, InkType type);
    }
}