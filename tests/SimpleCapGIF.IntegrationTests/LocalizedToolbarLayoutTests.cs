using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using SimpleCapGIF.App;
using SimpleCapGIF.App.ViewModels;
using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Localization;

namespace SimpleCapGIF.IntegrationTests;

public sealed class LocalizedToolbarLayoutTests
{
    [Fact]
    public void EveryCultureLoadsLocalizedToolbarWithoutClippingItsRightEdge()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var application = Application.Current ?? new Application();
                application.Resources["PanelBrush"] = new SolidColorBrush(Color.FromRgb(27, 29, 34));
                application.Resources["AccentBrush"] = Brushes.Black;

                foreach (var culture in UiCultureResolver.SupportedCultures)
                {
                    CultureInfo.CurrentUICulture = culture;
                    using var scope = new ToolbarScope();
                    var toolbar = scope.Window;
                    toolbar.DataContext = new MainWindowViewModel();
                    var root = Assert.IsType<Border>(toolbar.FindName("ToolbarRoot"));
                    root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    root.Arrange(new Rect(root.DesiredSize));
                    root.UpdateLayout();

                    var record = Assert.IsType<Button>(toolbar.FindName("RecordButton"));
                    var fullScreen = Assert.IsType<Button>(toolbar.FindName("FullScreenButton"));
                    var exit = Assert.IsType<Button>(toolbar.FindName("ExitButton"));
                    var settings = Assert.IsType<Button>(toolbar.FindName("SettingsButton"));
                    var frameRate = Assert.IsType<ComboBox>(toolbar.FindName("FrameRateComboBox"));
                    var manualAutomaticStop = Assert.IsType<MenuItem>(toolbar.FindName("ManualAutomaticStopMenuItem"));
                    var thirtySecondAutomaticStop = Assert.IsType<MenuItem>(toolbar.FindName("ThirtySecondAutomaticStopMenuItem"));

                    Assert.Equal(AppStrings.Record, record.ToolTip);
                    Assert.Equal(AppStrings.Record, AutomationProperties.GetName(record));
                    Assert.Equal(AppStrings.FullScreen, fullScreen.Content);
                    Assert.Equal(AppStrings.Exit, exit.ToolTip);
                    Assert.Equal(AppStrings.Exit, AutomationProperties.GetName(exit));
                    Assert.Equal(AppStrings.Settings, settings.ToolTip);
                    Assert.Equal(AppStrings.Settings, AutomationProperties.GetName(settings));
                    Assert.True(record.ActualWidth > 0 && frameRate.ActualWidth > 0 && settings.ActualWidth > 0 && exit.ActualWidth > 0, culture.Name);
                    toolbar.SetRecordingPreferences(RecordingPreferences.Default with { AutomaticStopSeconds = 30 });
                    Assert.False(manualAutomaticStop.IsChecked);
                    Assert.True(thirtySecondAutomaticStop.IsChecked);

                    var exitRight = exit.TranslatePoint(new Point(exit.ActualWidth, 0), root).X;
                    var frameRateRight = frameRate.TranslatePoint(new Point(frameRate.ActualWidth, 0), root).X;
                    Assert.InRange(Math.Abs(exitRight - frameRateRight), 0, 0.01);

                    var viewModel = Assert.IsType<MainWindowViewModel>(toolbar.DataContext);
                    viewModel.State = SimpleCapGIF.Core.Models.CaptureUiState.Completed;
                    root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    root.Arrange(new Rect(root.DesiredSize));
                    root.UpdateLayout();
                    var openResult = Assert.IsType<Button>(toolbar.FindName("OpenResultButton"));
                    Assert.Equal(AppStrings.OpenSavedFile, openResult.ToolTip);
                    Assert.Equal(AppStrings.OpenSavedFile, AutomationProperties.GetName(openResult));
                    Assert.True(openResult.Visibility == Visibility.Visible && openResult.ActualWidth > 0, culture.Name);
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "Localized toolbar test timed out.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private sealed class ToolbarScope : IDisposable
    {
        public ToolbarWindow Window { get; } = new();
        public void Dispose() => Window.Close();
    }
}
