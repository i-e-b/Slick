using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

namespace SlickWindows.ImageFormats
{
    public static class Save
    {
        public static void SaveJpeg(this Bitmap src, string? filePath, int quality = 95)
        {
            if (src == null) throw new Exception("Extension method Save.SaveJpeg called on null object");
            if (string.IsNullOrWhiteSpace(filePath!)) throw new ArgumentNullException(nameof(filePath));

            var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "/";
            filePath = Path.Combine(basePath, filePath!);

            var p = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(p!))
            {
                Directory.CreateDirectory(p!);
            }
            using (var fs = new FileStream(filePath, FileMode.Create))
            {
                JpegStream(src, fs, quality);
                fs.Close();
            }
        }
        
        public static void SaveBmp(this Bitmap src, string filePath)
        {
            if (src == null) throw new Exception("Extension method Save.SaveBmp called on null object");
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));

            var basePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "/";
            filePath = Path.Combine(basePath, filePath);

            var p = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(p!))
            {
                Directory.CreateDirectory(p!);
            }
            if (File.Exists(filePath)) File.Delete(filePath);
            src.Save(filePath, ImageFormat.Bmp);
        }

        private static void JpegStream([NotNull]Bitmap src, [NotNull]Stream outputStream, int quality = 95)
        {
            var encoder = ImageCodecInfo.GetImageEncoders().First(c => c?.FormatID == ImageFormat.Jpeg.Guid);
            if (encoder == null) throw new Exception("JPEG encoder not available. Check .Net runtime version");

            var qualityParam = Encoder.Quality ?? throw new Exception("Encoder quality parameter missing. Check .Net runtime version");
            var parameters = new EncoderParameters(1) { 
                Param = {
                    [0] = new EncoderParameter(qualityParam, quality)
                }
            };

            src.Save(outputStream, encoder, parameters);
            outputStream.Flush();
        }
    }
}