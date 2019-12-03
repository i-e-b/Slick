namespace SlickCommon.Canvas
{
    /// <summary>
    /// A rectangle by another name
    /// </summary>
    public class Quad
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;

        public Quad(int x, int y, int width, int height) { X = x; Y = y; Width = width; Height = height; }
        public Quad(double x, double y, double width, double height) { X = x; Y = y; Width = width; Height = height; }
    }
}