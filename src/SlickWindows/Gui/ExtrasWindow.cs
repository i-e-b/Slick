using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using JetBrains.Annotations;
using SlickCommon.Canvas;
using SlickWindows.Canvas;
using SlickWindows.Gui.Components;
using SlickWindows.ImageFormats;

namespace SlickWindows.Gui
{
    public partial class Extras : AutoScaleForm
    {
        [NotNull] private readonly IEndlessCanvas _target;
        private readonly FloatingImage _importFloat;
        private readonly FloatingText _textFloat;

        public Extras(IEndlessCanvas target, FloatingImage importFloat, FloatingText textFloat)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            _importFloat = importFloat;
            _textFloat = textFloat;
            InitializeComponent();

            if (exportButton != null) exportButton.Enabled = target.SelectedTiles().Count > 0;
        }

        private void ImportButton_Click(object sender, EventArgs e)
        {
            if (_importFloat == null) return;

            _importFloat.NormaliseControlScale();
            string path;
            var result = loadImageDialog?.ShowDialog();
            switch (result)
            {
                case DialogResult.OK:
                case DialogResult.Yes:
                    path = loadImageDialog.FileName;
                    break;
                default: return;
            }
            if (path == null) return;
            try {
                var bmp = new Bitmap(path);
                _importFloat.CandidateImage = bmp;
                _importFloat.CanvasTarget = _target;
                _importFloat.Visible = true;
            }
            catch (Exception ex) {
                MessageBox.Show("Sorry, that image can't be loaded\r\n" + ex, "Failed to load image", MessageBoxButtons.OK);
            }

            Close(); // the "More..." window
        }

        private void ExportButton_Click(object sender, EventArgs e)
        {
            // Pick path
            string path;
            var result = saveJpegDialog?.ShowDialog();
            switch (result)
            {
                case DialogResult.OK:
                case DialogResult.Yes:
                    path = saveJpegDialog.FileName;
                    break;
                default: return;
            }

            // user feedback
            if (exportButton != null) exportButton.Enabled = false;
            Text = "Export: saving to disk";
            Refresh();


            // read selected area
            var selected = _target.SelectedTiles();
            
            int top = int.MaxValue;
            int left = int.MaxValue;
            int right = int.MinValue;
            int bottom = int.MinValue;

            foreach (var key in selected)
            {
                top = Math.Min(top, key.Y);
                left = Math.Min(left, key.X);
                bottom = Math.Max(bottom, key.Y);
                right = Math.Max(right, key.X);
            }

            bottom += 1; right += 1; // include the tile contents
            var width = (right - left) * TileImage.Size;
            var height = (bottom - top) * TileImage.Size;

            // render and save bitmap
            using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                // TODO: fix this
                ((EndlessCanvas)_target).RenderToImage(bmp, top, left, selected);
                Application.DoEvents();
                bmp.SaveJpeg(path);
            }

            
            Text = "Export COMPLETE";
            Refresh();
            if (exportButton != null) exportButton.Enabled = _target.SelectedTiles().Count > 0;
        }

        private void Extras_Shown(object sender, EventArgs e)
        {
            FormsHelper.NudgeOnScreen(this);
        }

        private void TextInputButton_Click(object sender, EventArgs e)
        {
            if (_textFloat != null) {
                _textFloat.NormaliseControlScale();
                _textFloat.Visible = true;
            }
            Close();
        }
    }
}
