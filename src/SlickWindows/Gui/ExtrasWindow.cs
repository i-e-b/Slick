using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using JetBrains.Annotations;
using SlickWindows.Canvas;

namespace SlickWindows.Gui
{
    public partial class ExtrasWindow : Form
    {
        private readonly EndlessCanvas _target;

        public ExtrasWindow(EndlessCanvas target)
        {
            _target = target;
            InitializeComponent();
        }

        private void ImportButton_Click(object sender, EventArgs e)
        {
            if (_target == null) return;

            // TODO: A proper import.
            // For now, this will just copy my oldest working notes over.
            if (importButton != null) importButton.Enabled = false;
            Refresh();

            if (_target == null) return;
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
            _target.SaveChanges();


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
    }
}
