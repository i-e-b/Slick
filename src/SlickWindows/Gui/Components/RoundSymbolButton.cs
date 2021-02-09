using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using JetBrains.Annotations;

namespace SlickWindows.Gui.Components
{
    public class RoundSymbolButton : UserControl
    {
        public SymbolType Symbol { get; set; }

        [NotNull] private readonly Pen _black2Px;
        private readonly int _scale;

        public RoundSymbolButton()
        {
            // Read scale
            var asf = ParentForm as AutoScaleForm;
            var dpi = asf?.Dpi ?? DeviceDpi;
            _scale = (dpi > 120) ? 2 : 1;

            Width = 24 * _scale;
            Height = 24 * _scale;
            components = new System.ComponentModel.Container();
            AutoScaleMode = AutoScaleMode.Font;
            _black2Px = new Pen(Color.Black, 2.0f * _scale);
            Width = 24 * _scale;
            Height = 24 * _scale;
        }

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e)
        {
            // draw outer button
            var outline = new GraphicsPath();
            outline.AddEllipse(0, 0, 24 * _scale, 24 * _scale);

            var g = e.Graphics;
            g.CompositingMode = CompositingMode.SourceOver;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillEllipse(Brushes.White, 1 * _scale, 1 * _scale, 22 * _scale, 22 * _scale);
            g.DrawEllipse(_black2Px, 1 * _scale, 1 * _scale, 22 * _scale, 22 * _scale);
            
            Region = new Region(outline);

            // draw specific symbol
            switch(Symbol) {
                case SymbolType.Cross: DrawCross(g); break;
                case SymbolType.MergeArrow: DrawMergeArrow(g); break;
            }
        }

        private void DrawMergeArrow([NotNull]Graphics g)
        {
            g.DrawLine(_black2Px, 12 * _scale, 5 * _scale, 12 * _scale, 18 * _scale);
            g.DrawLine(_black2Px, 12 * _scale, 18 * _scale, 6 * _scale, 12 * _scale);
            g.DrawLine(_black2Px, 12 * _scale, 18 * _scale, 18 * _scale, 12 * _scale);
        }

        private void DrawCross([NotNull]Graphics g)
        {
            g.DrawLine(_black2Px, 7 * _scale, 7 * _scale, 17 * _scale, 17 * _scale);
            g.DrawLine(_black2Px, 7 * _scale, 17 * _scale, 17 * _scale, 7 * _scale);
        }

        /// <inheritdoc />
        protected override void OnPaintBackground(PaintEventArgs e) { }

        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private readonly System.ComponentModel.IContainer? components;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing) { components?.Dispose(); }
            base.Dispose(disposing);
        }
    }
}