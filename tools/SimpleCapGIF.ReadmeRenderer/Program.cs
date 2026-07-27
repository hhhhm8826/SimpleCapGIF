using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SimpleCapGIF.App;
using SimpleCapGIF.App.ViewModels;
using SimpleCapGIF.Localization;

namespace SimpleCapGIF.ReadmeRenderer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Provide the UI culture and output PNG path.");

        UiCultureResolver.ApplyFromWindows(CultureInfo.GetCultureInfo(args[0]));
        var application = new Application();
        application.Resources["PanelBrush"] = new SolidColorBrush(Color.FromArgb(0xF2, 0x1B, 0x1D, 0x22));
        application.Resources["AccentBrush"] = Brushes.Black;

        var window = new ToolbarWindow { DataContext = new MainWindowViewModel() };
        var root = (FrameworkElement)window.FindName("ToolbarRoot");
        const double previewWidth = 560;
        const double previewHeight = 84;
        root.Width = previewWidth;
        root.Height = previewHeight;
        root.Measure(new Size(previewWidth, previewHeight));
        root.Arrange(new Rect(0, 0, previewWidth, previewHeight));
        root.UpdateLayout();

        const double scale = 2;
        var width = Math.Max(1, (int)Math.Ceiling(root.ActualWidth * scale));
        var height = Math.Max(1, (int)Math.Ceiling(root.ActualHeight * scale));
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.PushTransform(new ScaleTransform(scale, scale));
            context.DrawRectangle(new VisualBrush(root), null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        var outputPath = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
        window.Close();
    }
}
