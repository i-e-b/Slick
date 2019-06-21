using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using JetBrains.Annotations;
using SlickWindows.Canvas;
using SlickWindows.ImageFormats;

namespace SlickWindows.Gui
{
    public partial class Extras : Form
    {
        [NotNull] private readonly EndlessCanvas _target;

        public Extras(EndlessCanvas target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
            InitializeComponent();

            if (exportButton != null) exportButton.Enabled = target.SelectedTiles().Count > 0;
        }

        private void ImportButton_Click(object sender, EventArgs e)
        {
            // TODO: A proper import.
            // For now, this will just copy my oldest working notes over.
            if (importButton != null) importButton.Enabled = false;
            Refresh();

            var importLocation = @"C:\Temp\CanvTest";

            // read all files, work out their tile location.
            var files = Directory.GetFiles(importLocation);
            var i = 1;
            foreach (var path in files)
            {
                Text = $"Import: {i} of {files.Length}";

                var key = PositionKey.Parse(Path.GetFileNameWithoutExtension(path));
                if (key == null) throw new Exception("what?");
                CrossLoadImage(path, key, _target);

                Application.DoEvents(); // prevent freezing in a half-assed way.
                i++;
            }

            Text = "Import: saving to disk";
            Application.DoEvents();
            _target.SaveChanges();


            Text = "Import: COMPLETE";
            if (importButton != null) importButton.Enabled = true;
            Refresh();
        }

        public static void CrossLoadImage([NotNull]string path, [NotNull]PositionKey originalPosition, [NotNull]EndlessCanvas target)
        {
            using (var bmp = new Bitmap(Image.FromFile(path)))
            {
                var width = bmp.Width;
                var height = bmp.Height;

                var tX = originalPosition.X * width;
                var tY = originalPosition.Y * height;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var color = bmp.GetPixel(x,y);
                        if (color.R ==255 && color.G == 255 && color.B == 255) continue; // skip white pixels
                        target.SetPixel(color, x + tX, y + tY);
                    }
                }
            }
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
                _target.RenderToImage(bmp, top, left, selected);
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
    }
}
