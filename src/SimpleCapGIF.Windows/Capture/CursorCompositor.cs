using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Windows.Interop;

namespace SimpleCapGIF.Windows.Capture;

internal static class CursorCompositor
{
    internal static void Composite(byte[] target, PixelSize targetSize, PixelRect sourceRegion)
    {
        var cursorInfo = new NativeMethods.CursorInfo { Size = (uint)Marshal.SizeOf<NativeMethods.CursorInfo>() };
        if (!NativeMethods.GetCursorInfo(ref cursorInfo) || (cursorInfo.Flags & NativeMethods.CursorShowing) == 0 || cursorInfo.Cursor == 0)
        {
            return;
        }

        if (!NativeMethods.GetIconInfo(cursorInfo.Cursor, out var iconInfo))
        {
            return;
        }

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(cursorInfo.Cursor, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var bitmap = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[checked(bitmap.PixelWidth * bitmap.PixelHeight * 4)];
            bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);

            var scaleX = targetSize.Width / (double)sourceRegion.Width;
            var scaleY = targetSize.Height / (double)sourceRegion.Height;
            var destinationX = (int)Math.Round((cursorInfo.ScreenPosition.X - (int)iconInfo.HotspotX - sourceRegion.X) * scaleX);
            var destinationY = (int)Math.Round((cursorInfo.ScreenPosition.Y - (int)iconInfo.HotspotY - sourceRegion.Y) * scaleY);
            Blend(target, targetSize, pixels, new PixelSize(bitmap.PixelWidth, bitmap.PixelHeight), destinationX, destinationY, scaleX, scaleY);
        }
        finally
        {
            if (iconInfo.ColorBitmap != 0) NativeMethods.DeleteObject(iconInfo.ColorBitmap);
            if (iconInfo.MaskBitmap != 0) NativeMethods.DeleteObject(iconInfo.MaskBitmap);
        }
    }

    private static void Blend(byte[] target, PixelSize targetSize, byte[] cursor, PixelSize cursorSize, int destinationX, int destinationY, double scaleX, double scaleY)
    {
        var scaledWidth = Math.Max(1, (int)Math.Round(cursorSize.Width * scaleX));
        var scaledHeight = Math.Max(1, (int)Math.Round(cursorSize.Height * scaleY));
        for (var y = 0; y < scaledHeight; y++)
        {
            var targetY = destinationY + y;
            if ((uint)targetY >= (uint)targetSize.Height) continue;
            var sourceY = Math.Min(cursorSize.Height - 1, (int)(y / scaleY));
            for (var x = 0; x < scaledWidth; x++)
            {
                var targetX = destinationX + x;
                if ((uint)targetX >= (uint)targetSize.Width) continue;
                var sourceX = Math.Min(cursorSize.Width - 1, (int)(x / scaleX));
                var sourceIndex = ((sourceY * cursorSize.Width) + sourceX) * 4;
                var alpha = cursor[sourceIndex + 3];
                if (alpha == 0) continue;
                var targetIndex = ((targetY * targetSize.Width) + targetX) * 4;
                var inverseAlpha = 255 - alpha;
                target[targetIndex] = (byte)(((cursor[sourceIndex] * alpha) + (target[targetIndex] * inverseAlpha)) / 255);
                target[targetIndex + 1] = (byte)(((cursor[sourceIndex + 1] * alpha) + (target[targetIndex + 1] * inverseAlpha)) / 255);
                target[targetIndex + 2] = (byte)(((cursor[sourceIndex + 2] * alpha) + (target[targetIndex + 2] * inverseAlpha)) / 255);
                target[targetIndex + 3] = 255;
            }
        }
    }
}
