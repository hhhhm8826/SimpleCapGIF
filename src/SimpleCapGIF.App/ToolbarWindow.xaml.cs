using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Core.Models;
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
    public event EventHandler? CancelRequested;
    public event EventHandler? OpenResultRequested;
    public event EventHandler? OpenFolderRequested;
    public event EventHandler? ExitRequested;
    public event Action<bool>? IncludeCursorRequested;
    public event Action<int>? StartDelayRequested;
    public event Action<int>? AutomaticStopRequested;
    public event Action<GlobalHotKeyPreset>? GlobalHotKeyRequested;
    public event Action<Exception>? CaptureExclusionFailed;

    public bool IsCaptureExcluded { get; private set; }

    public void SetFullScreenState(bool isFullScreen) =>
        FullScreenButton.Content = isFullScreen ? AppStrings.ReturnToRegion : AppStrings.FullScreen;

    public void SetRecordingPreferences(RecordingPreferences preferences)
    {
        IncludeCursorMenuItem.IsChecked = preferences.IncludeCursor;
        ImmediateDelayMenuItem.IsChecked = preferences.StartDelaySeconds == 0;
        ThreeSecondDelayMenuItem.IsChecked = preferences.StartDelaySeconds == 3;
        FiveSecondDelayMenuItem.IsChecked = preferences.StartDelaySeconds == 5;
        ManualAutomaticStopMenuItem.IsChecked = preferences.AutomaticStopSeconds == 0;
        ThreeSecondAutomaticStopMenuItem.IsChecked = preferences.AutomaticStopSeconds == 3;
        FiveSecondAutomaticStopMenuItem.IsChecked = preferences.AutomaticStopSeconds == 5;
        TenSecondAutomaticStopMenuItem.IsChecked = preferences.AutomaticStopSeconds == 10;
        FifteenSecondAutomaticStopMenuItem.IsChecked = preferences.AutomaticStopSeconds == 15;
        ThirtySecondAutomaticStopMenuItem.IsChecked = preferences.AutomaticStopSeconds == 30;
        F12HotKeyMenuItem.IsChecked = preferences.GlobalHotKey == GlobalHotKeyPreset.F12;
        AltF9HotKeyMenuItem.IsChecked = preferences.GlobalHotKey == GlobalHotKeyPreset.AltF9;
        ControlShiftRHotKeyMenuItem.IsChecked = preferences.GlobalHotKey == GlobalHotKeyPreset.ControlShiftR;
        DisabledHotKeyMenuItem.IsChecked = preferences.GlobalHotKey == GlobalHotKeyPreset.Disabled;
    }

    public void CloseMenus()
    {
        SettingsMenu.IsOpen = false;
        if (StopButton.ContextMenu is not null) StopButton.ContextMenu.IsOpen = false;
    }

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
    private void OnCancelClick(object sender, RoutedEventArgs e) => CancelRequested?.Invoke(this, EventArgs.Empty);
    private void OnOpenResultClick(object sender, RoutedEventArgs e) => OpenResultRequested?.Invoke(this, EventArgs.Empty);
    private void OnOpenFolderClick(object sender, RoutedEventArgs e) => OpenFolderRequested?.Invoke(this, EventArgs.Empty);
    private void OnExitClick(object sender, RoutedEventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        SettingsMenu.PlacementTarget = SettingsButton;
        SettingsMenu.Placement = PlacementMode.Bottom;
        SettingsMenu.IsOpen = true;
    }

    private void OnIncludeCursorClick(object sender, RoutedEventArgs e) =>
        IncludeCursorRequested?.Invoke(IncludeCursorMenuItem.IsChecked);

    private void OnStartDelayClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string value } && int.TryParse(value, out var seconds))
        {
            StartDelayRequested?.Invoke(seconds);
        }
    }

    private void OnAutomaticStopClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string value } && int.TryParse(value, out var seconds))
        {
            AutomaticStopRequested?.Invoke(seconds);
        }
    }

    private void OnHotKeyClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string value } && Enum.TryParse<GlobalHotKeyPreset>(value, out var preset))
        {
            GlobalHotKeyRequested?.Invoke(preset);
        }
    }

    private void OnSettingsContextMenuOpened(object sender, RoutedEventArgs e) => ExcludePopup(sender as ContextMenu);
    private void OnCaptureContextMenuOpened(object sender, RoutedEventArgs e) => ExcludePopup(sender as ContextMenu);

    private static void ExcludePopup(ContextMenu? menu)
    {
        if (menu is null) return;
        menu.Opacity = 0;
        menu.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (!menu.IsOpen) return;
            try
            {
                if (PresentationSource.FromVisual(menu) is not HwndSource source)
                {
                    throw new InvalidOperationException(AppStrings.CaptureUiExclusionUnavailable);
                }

                WindowCaptureExclusionService.Exclude(source.Handle);
                menu.Opacity = 1;
            }
            catch
            {
                menu.IsOpen = false;
            }
        });
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }
}
