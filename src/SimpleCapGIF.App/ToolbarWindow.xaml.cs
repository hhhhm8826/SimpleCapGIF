using System.Windows;
using System.Windows.Interop;
using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Localization;
using SimpleCapGIF.Windows.Display;

namespace SimpleCapGIF.App;

public partial class ToolbarWindow : Window
{
    public ToolbarWindow() => InitializeComponent();

    public event EventHandler? FullScreenRequested;
    public event EventHandler? FolderRequested;
    public event EventHandler? RecordRequested;
    public event EventHandler? StopRequested;
    public event EventHandler? OpenFolderRequested;
    public event EventHandler? ExitRequested;
    public event Action<Exception>? CaptureExclusionFailed;

    public bool IsCaptureExcluded { get; private set; }

    public void SetFullScreenState(bool isFullScreen) =>
        FullScreenButton.Content = isFullScreen ? AppStrings.ReturnToRegion : AppStrings.FullScreen;

    public PixelSize MeasurePhysicalSize(double scaleX, double scaleY)
    {
        ToolbarRoot.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return new PixelSize(
            Math.Max(1, (int)Math.Ceiling(ToolbarRoot.DesiredSize.Width * scaleX)),
            Math.Max(1, (int)Math.Ceiling(ToolbarRoot.DesiredSize.Height * scaleY)));
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            WindowCaptureExclusionService.Exclude(new WindowInteropHelper(this).Handle);
            IsCaptureExcluded = true;
        }
        catch (Exception exception)
        {
            CaptureExclusionFailed?.Invoke(exception);
        }
    }

    private void OnFullScreenClick(object sender, RoutedEventArgs e) => FullScreenRequested?.Invoke(this, EventArgs.Empty);
    private void OnFolderClick(object sender, RoutedEventArgs e) => FolderRequested?.Invoke(this, EventArgs.Empty);
    private void OnRecordClick(object sender, RoutedEventArgs e) => RecordRequested?.Invoke(this, EventArgs.Empty);
    private void OnStopClick(object sender, RoutedEventArgs e) => StopRequested?.Invoke(this, EventArgs.Empty);
    private void OnOpenFolderClick(object sender, RoutedEventArgs e) => OpenFolderRequested?.Invoke(this, EventArgs.Empty);
    private void OnExitClick(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);
}
