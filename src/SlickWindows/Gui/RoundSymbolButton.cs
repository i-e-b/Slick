using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using JetBrains.Annotations;

namespace SlickWindows.Gui
{
    public partial class RoundSymbolButton : UserControl
    {
        public SymbolType Symbol { get; set; }

        [NotNull] private readonly Pen _black2Px;

        public RoundSymbolButton()
        {
            Width = 24;
            Height = 24;
            InitializeComponent();
            _black2Px = new Pen(Color.Black, 2.0f);
            Width = 24;
            Height = 24;
        }

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e)
        {
            // draw outer button
            var outline = new GraphicsPath();
            outline.AddEllipse(0,0,24,24);

            var g = e.Graphics;
            g.CompositingMode = CompositingMode.SourceOver;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillEllipse(Brushes.White, 1, 1, 22, 22);
            g.DrawEllipse(_black2Px, 1, 1, 22, 22);
            
            Region = new Region(outline);

            // draw specific symbol
            switch(Symbol) {
                case SymbolType.Cross: DrawCross(g); break;
                case SymbolType.MergeArrow: DrawMergeArrow(g); break;
            }
        }

        private void DrawMergeArrow([NotNull]Graphics g)
        {
            g.DrawLine(_black2Px, 12, 5, 12, 18);
            g.DrawLine(_black2Px, 12, 18, 6, 12);
            g.DrawLine(_black2Px, 12, 18, 18, 12);
        }

        private void DrawCross([NotNull]Graphics g)
        {
            g.DrawLine(_black2Px, 7, 7, 17, 17);
            g.DrawLine(_black2Px, 7, 17, 17, 7);
        }

        /// <inheritdoc />
        protected override void OnPaintBackground(PaintEventArgs e) { }
    }

    public enum SymbolType
    {
        Cross, MergeArrow
    }
}
