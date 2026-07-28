using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Application = System.Windows.Application;

namespace CodexLimits.App;

internal static class TrayIconRenderer
{
    private const int TrayCanvasSize = 64;
    private const int WindowCanvasSize = 256;

    public static Image? LoadBaseImage()
    {
        var resource = Application.GetResourceStream(
            new Uri("pack://application:,,,/docs/icon.png", UriKind.Absolute));

        if (resource?.Stream is null)
        {
            return null;
        }

        using (resource.Stream)
        using (var source = Image.FromStream(resource.Stream))
        {
            return new Bitmap(source);
        }
    }

    public static Icon CreateTrayIcon(Image? baseImage)
    {
        using var bitmap = Render(baseImage, TrayCanvasSize, 1);
        var handle = bitmap.GetHicon();

        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    public static System.Windows.Media.ImageSource? CreateWindowIcon(Image? baseImage)
    {
        using var bitmap = Render(baseImage, WindowCanvasSize, 8);
        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stream.Position = 0;

        var image = new System.Windows.Media.Imaging.BitmapImage();
        image.BeginInit();
        image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static Bitmap Render(Image? baseImage, int canvasSize, int padding)
    {
        var bitmap = new Bitmap(canvasSize, canvasSize, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;

        if (baseImage is null)
        {
            using var fallback = SystemIcons.Application.ToBitmap();
            graphics.DrawImage(
                fallback,
                new Rectangle(padding, padding, canvasSize - padding * 2, canvasSize - padding * 2));
            return bitmap;
        }

        using var source = new Bitmap(baseImage);
        var visibleBounds = FindVisibleBounds(source);
        var targetBounds = FitInside(
            visibleBounds.Size,
            new Rectangle(padding, padding, canvasSize - padding * 2, canvasSize - padding * 2));

        graphics.DrawImage(
            source,
            targetBounds,
            visibleBounds,
            GraphicsUnit.Pixel);

        return bitmap;
    }

    private static Rectangle FindVisibleBounds(Bitmap bitmap)
    {
        var minX = bitmap.Width;
        var minY = bitmap.Height;
        var maxX = -1;
        var maxY = -1;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap.GetPixel(x, y).A <= 8)
                {
                    continue;
                }

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        return maxX >= minX && maxY >= minY
            ? Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1)
            : new Rectangle(0, 0, bitmap.Width, bitmap.Height);
    }

    private static Rectangle FitInside(Size sourceSize, Rectangle targetArea)
    {
        var widthRatio = targetArea.Width / (double)Math.Max(sourceSize.Width, 1);
        var heightRatio = targetArea.Height / (double)Math.Max(sourceSize.Height, 1);
        var scale = Math.Min(widthRatio, heightRatio);

        var width = Math.Max((int)Math.Round(sourceSize.Width * scale), 1);
        var height = Math.Max((int)Math.Round(sourceSize.Height * scale), 1);
        var x = targetArea.X + (targetArea.Width - width) / 2;
        var y = targetArea.Y + (targetArea.Height - height) / 2;

        return new Rectangle(x, y, width, height);
    }

    private static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr handle);
    }
}

