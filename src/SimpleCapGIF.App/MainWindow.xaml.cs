using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using SimpleCapGIF.App.ViewModels;
using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Core.Services;
using SimpleCapGIF.Windows.Capture;
using SimpleCapGIF.Windows.Display;
using SimpleCapGIF.Windows.Encoding;
using SimpleCapGIF.Windows.Storage;
using SimpleCapGIF.Localization;
using Microsoft.Win32;

namespace SimpleCapGIF.App;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WPF Window owns and disposes recording resources from its Closing lifecycle.")]
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();
    private readonly ToolbarPlacementService _placementService = new();
    private readonly CaptureStateMachine _stateMachine = new();
    private readonly OutputSizeEstimator _sizeEstimator = new();
    private readonly SessionStorage _sessionStorage = SessionStorage.CreateDefault();
    private readonly DispatcherTimer _statisticsTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Dictionary<string, double> _calibrationRatios = [];
    private readonly HashSet<string> _hdrWarningsShown = new(StringComparer.OrdinalIgnoreCase);
    private MonitorDescriptor? _monitor;
    private JsonSettingsStore? _settingsStore;
    private FfmpegToolchain? _toolchain;
    private DxgiCaptureSession? _captureSession;
    private FfmpegSampleEstimator? _sampleEstimator;
    private ToolbarWindow? _toolbarWindow;
    private CancellationTokenSource? _operationCancellation;
    private PixelRect? _customRegion;
    private OutputPreset _customPreset = CaptureSettings.Default.OutputPreset;
    private bool _customPresetUserSelected;
    private OutputPreset _acceptedPreset = CaptureSettings.Default.OutputPreset;
    private bool _acceptedPresetUserSelected;
    private string? _sessionDirectory;
    private string _saveFolder = string.Empty;
    private bool _isFullScreen;
    private bool _allowClose;
    private bool _borderCaptureExcluded;
    private Exception? _captureExclusionError;
    private PixelRect? _moveStartRegion;
    private PixelPoint? _moveStartCursor;
    private PixelPoint? _toolbarSelectingOrigin;
    private bool _deferSelectingToolbarPlacement;
    private bool _suppressPresetResponse;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _statisticsTimer.Tick += OnStatisticsTick;
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainWindowViewModel.Region) or nameof(MainWindowViewModel.State)) UpdateVisualLayout();
            if (args.PropertyName == nameof(MainWindowViewModel.SelectedPreset)) OnSelectedPresetChanged();
        };
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        try
        {
            WindowCaptureExclusionService.Exclude(new WindowInteropHelper(this).Handle);
            _borderCaptureExcluded = true;
        }
        catch (Exception exception)
        {
            _captureExclusionError = exception;
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_captureExclusionError is not null) throw _captureExclusionError;
            _monitor = MonitorService.GetMonitorAtCursor();
            PositionBorderWindow();
            WarnIfHdr(_monitor);

            _sessionStorage.CleanupOrphans(DateTimeOffset.UtcNow);
            _settingsStore = new JsonSettingsStore(_sessionStorage.SettingsPath);
            var settings = await _settingsStore.LoadAsync();
            foreach (var pair in settings.CalibrationRatios) _calibrationRatios[pair.Key] = pair.Value;
            _suppressPresetResponse = true;
            _viewModel.ApplySettings(settings.Capture);
            _saveFolder = settings.SaveFolder;

            var initialWidth = Math.Clamp(settings.LastCustomRegionSize.Width, SelectionRegionService.MinimumWidth, _monitor.Bounds.Width);
            var initialHeight = Math.Clamp(settings.LastCustomRegionSize.Height, SelectionRegionService.MinimumHeight, _monitor.Bounds.Height);
            _viewModel.Region = new PixelRect(
                _monitor.Bounds.X + ((_monitor.Bounds.Width - initialWidth) / 2),
                _monitor.Bounds.Y + ((_monitor.Bounds.Height - initialHeight) / 2),
                initialWidth,
                initialHeight);
            _customRegion = _viewModel.Region;
            _customPreset = _viewModel.SelectedPreset;
            _customPresetUserSelected = _viewModel.Settings.PresetUserSelected;
            _acceptedPreset = _customPreset;
            _acceptedPresetUserSelected = _customPresetUserSelected;
            _suppressPresetResponse = false;
            if (!Directory.Exists(_saveFolder))
            {
                if (!PromptForSaveFolder())
                {
                    _allowClose = true;
                    Close();
                    return;
                }

                await SaveSettingsAsync();
            }

            CreateToolbarWindow();
            UpdateVisualLayout();
        }
        catch (Exception exception)
        {
            ShowError(AppStrings.StartAppError, exception);
            Close();
        }
    }

    private void OnWindowSizeChanged(object sender, SizeChangedEventArgs e) => UpdateVisualLayout();

    private void OnHandleDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_monitor is null || _viewModel.State != CaptureUiState.Selecting || _isFullScreen || sender is not Thumb { Tag: string tag } || !Enum.TryParse<ResizeHandle>(tag, out var handle)) return;
        if (handle == ResizeHandle.Move && _moveStartRegion is not null && _moveStartCursor is not null)
        {
            var cursor = MonitorService.GetCursorPosition();
            var proposed = new PixelRect(
                _moveStartRegion.Value.X + cursor.X - _moveStartCursor.Value.X,
                _moveStartRegion.Value.Y + cursor.Y - _moveStartCursor.Value.Y,
                _moveStartRegion.Value.Width,
                _moveStartRegion.Value.Height);
            if (MonitorService.TryGetMonitorContaining(proposed, out var targetMonitor))
            {
                if (targetMonitor.Handle != _monitor.Handle) SetActiveMonitor(targetMonitor);
                _viewModel.Region = proposed;
            }
            else
            {
                _viewModel.Region = proposed.ClampInside(_monitor.Bounds);
            }

            _customRegion = _viewModel.Region;
            return;
        }

        var deltaX = (int)Math.Round(e.HorizontalChange * _monitor.ScaleX, MidpointRounding.AwayFromZero);
        var deltaY = (int)Math.Round(e.VerticalChange * _monitor.ScaleY, MidpointRounding.AwayFromZero);
        _viewModel.Region = SelectionRegionService.ApplyDelta(_viewModel.Region, _monitor.Bounds, handle, deltaX, deltaY);
        _customRegion = _viewModel.Region;
        SetAcceptedPreset(OutputPreset.Original, userSelected: true);
    }

    private void OnMoveDragStarted(object sender, DragStartedEventArgs e)
    {
        if (_viewModel.State != CaptureUiState.Selecting || _isFullScreen) return;
        _moveStartRegion = _viewModel.Region;
        _moveStartCursor = MonitorService.GetCursorPosition();
    }

    private void OnMoveDragCompleted(object sender, DragCompletedEventArgs e)
    {
        _moveStartRegion = null;
        _moveStartCursor = null;
        _customRegion = _viewModel.Region;
    }

    private void OnToggleFullScreenClick(object? sender, EventArgs e)
    {
        if (_monitor is null) return;
        if (_isFullScreen)
        {
            _viewModel.Region = (_customRegion ?? _viewModel.Region).ClampInside(_monitor.Bounds);
            SetAcceptedPreset(_customPreset, _customPresetUserSelected);
        }
        else
        {
            _customRegion = _viewModel.Region;
            _customPreset = _viewModel.SelectedPreset;
            _customPresetUserSelected = _viewModel.Settings.PresetUserSelected;
            _viewModel.Region = _monitor.Bounds;
            SetAcceptedPreset(OutputPreset.Original, userSelected: true);
        }

        _isFullScreen = !_isFullScreen;
        _toolbarWindow?.SetFullScreenState(_isFullScreen);
        UpdateVisualLayout();
    }

    private async void OnChooseFolderClick(object? sender, EventArgs e)
    {
        if (!PromptForSaveFolder()) return;
        try
        {
            await SaveSettingsAsync();
        }
        catch (Exception exception)
        {
            ShowError(AppStrings.SaveFolderSettingsError, exception);
        }
    }

    private async void OnRecordClick(object? sender, EventArgs e)
    {
        if (_viewModel.State != CaptureUiState.Selecting) return;
        try
        {
            if (!_borderCaptureExcluded || _toolbarWindow?.IsCaptureExcluded != true)
            {
                throw _captureExclusionError ?? new InvalidOperationException(AppStrings.CaptureUiExclusionUnavailable);
            }

            _toolchain ??= FfmpegToolchain.Resolve();
            _operationCancellation = new CancellationTokenSource();
            _sessionDirectory = _sessionStorage.CreateSessionDirectory();
            _sampleEstimator = new FfmpegSampleEstimator(
                _toolchain,
                _viewModel.SelectedFormat,
                Path.Combine(_sessionDirectory, "Estimate"),
                sample => Dispatcher.BeginInvoke(() => _sizeEstimator.AddSample(sample)));
            _captureSession = new DxgiCaptureSession(_toolchain, _sampleEstimator);
            _stateMachine.StartRecording();
            _viewModel.State = CaptureUiState.Recording;
            _viewModel.Elapsed = TimeSpan.Zero;
            _viewModel.EstimatedBytes = 0;
            var outputSize = _viewModel.OutputSize;
            var calibrationKey = GetCalibrationKey();
            var calibration = _calibrationRatios.GetValueOrDefault(calibrationKey, 1d);
            _sizeEstimator.Reset(new EstimateProfile(_viewModel.SelectedFormat, _viewModel.SelectedPreset, outputSize.Width, outputSize.Height, _viewModel.SelectedFps, calibration));
            var request = new CaptureRequest(_viewModel.Region, outputSize, _viewModel.SelectedFps, _sessionDirectory);
            await PrepareDesktopForCaptureAsync(_operationCancellation.Token);
            await _captureSession.StartAsync(request, _operationCancellation.Token);
            _statisticsTimer.Start();
            _ = ObserveCaptureFailureAsync(_captureSession, _operationCancellation.Token);
        }
        catch (Exception exception)
        {
            await AbortCurrentOperationAsync();
            ReturnToSelecting();
            ShowError(AppStrings.StartCaptureError, exception);
        }
    }

    private async void OnStopClick(object? sender, EventArgs e)
    {
        if (_viewModel.State != CaptureUiState.Recording || _captureSession is null || _toolchain is null) return;
        _statisticsTimer.Stop();
        _stateMachine.StartEncoding();
        _viewModel.State = CaptureUiState.Encoding;

        try
        {
            var recordedSession = await _captureSession.StopAsync(_operationCancellation?.Token ?? CancellationToken.None);
            var estimatedAtStop = _sizeEstimator.GetEstimate(recordedSession.Duration).Bytes;
            await _captureSession.DisposeAsync();
            _captureSession = null;
            if (_sampleEstimator is not null)
            {
                await _sampleEstimator.DisposeAsync();
                _sampleEstimator = null;
            }
            Directory.CreateDirectory(_saveFolder);
            var destinationPath = OutputFileNameService.CreateAvailablePath(_saveFolder, _viewModel.SelectedFormat, DateTimeOffset.Now);
            var encoder = new FfmpegAnimationEncoder(_toolchain, _viewModel.SelectedFormat);
            var result = await encoder.EncodeAsync(recordedSession, destinationPath, null, _operationCancellation?.Token ?? CancellationToken.None);
            var calibrationKey = GetCalibrationKey();
            if (_sizeEstimator.HasSample)
            {
                _calibrationRatios[calibrationKey] = OutputSizeEstimator.UpdateCalibration(_calibrationRatios.GetValueOrDefault(calibrationKey, 1d), result.Bytes, estimatedAtStop);
            }
            _viewModel.EstimatedBytes = result.Bytes;
            _viewModel.StatusText = AppStrings.Format(AppStrings.SavedSizeFormat, result.Bytes / 1_000_000d);
            await SaveSettingsAsync();
            CleanupSession();
            _stateMachine.Complete();
            _viewModel.State = CaptureUiState.Completed;
            await Task.Delay(TimeSpan.FromSeconds(2), _operationCancellation?.Token ?? CancellationToken.None);
            ReturnToSelecting();
        }
        catch (OperationCanceledException)
        {
            await AbortCurrentOperationAsync();
            ReturnToSelecting();
        }
        catch (Exception exception)
        {
            await AbortCurrentOperationAsync();
            ReturnToSelecting();
            var formatName = _viewModel.SelectedFormat == AnimationFormat.Gif ? "GIF" : "WebP";
            ShowError(AppStrings.Format(AppStrings.SaveAnimationErrorFormat, formatName), exception);
        }
    }

    private void OnStatisticsTick(object? sender, EventArgs e)
    {
        if (_captureSession is null) return;
        var statistics = _captureSession.Statistics;
        _viewModel.Elapsed = statistics.Elapsed;
        _viewModel.EstimatedBytes = _sizeEstimator.GetEstimate(statistics.Elapsed).Bytes;
    }

    private async Task PrepareDesktopForCaptureAsync(CancellationToken cancellationToken)
    {
        UpdateVisualLayout();
        _toolbarWindow?.UpdateLayout();
        await Dispatcher.InvokeAsync(
            static () => { },
            DispatcherPriority.ContextIdle,
            cancellationToken);
        DesktopCompositionService.Flush();
        await Task.Delay(TimeSpan.FromMilliseconds(32), cancellationToken);
        DesktopCompositionService.Flush();
    }

    private async Task ObserveCaptureFailureAsync(DxgiCaptureSession session, CancellationToken cancellationToken)
    {
        var completion = session.Completion;
        if (completion is null) return;
        try
        {
            await completion.ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (_captureSession != session || _viewModel.State != CaptureUiState.Recording) return;
            await AbortCurrentOperationAsync();
            ReturnToSelecting();
            ShowError(AppStrings.CaptureStoppedError, exception);
        }
    }

    private void OnOpenFolderClick(object? sender, EventArgs e)
    {
        if (Directory.Exists(_saveFolder)) Process.Start(new ProcessStartInfo(_saveFolder) { UseShellExecute = true });
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        _toolbarWindow?.Close();
        _toolbarWindow = null;
        if (_allowClose || (_captureSession is null && _viewModel.State == CaptureUiState.Selecting)) return;
        e.Cancel = true;
        _allowClose = true;
        _operationCancellation?.Cancel();
        await AbortCurrentOperationAsync();
        Close();
    }

    private async Task SaveSettingsAsync()
    {
        if (_settingsStore is null) return;
        var captureSettings = _viewModel.Settings;
        if (_isFullScreen)
        {
            captureSettings = captureSettings with
            {
                OutputPreset = _customPreset,
                PresetUserSelected = _customPresetUserSelected,
            };
        }

        await _settingsStore.SaveAsync(new AppSettings
        {
            Capture = captureSettings,
            SaveFolder = _saveFolder,
            LastCustomRegionSize = (_customRegion ?? _viewModel.Region).Size,
            CalibrationRatios = new Dictionary<string, double>(_calibrationRatios),
        });
    }

    private async Task AbortCurrentOperationAsync()
    {
        _statisticsTimer.Stop();
        _operationCancellation?.Cancel();
        if (_captureSession is not null)
        {
            await _captureSession.DisposeAsync();
            _captureSession = null;
        }

        if (_sampleEstimator is not null)
        {
            await _sampleEstimator.DisposeAsync();
            _sampleEstimator = null;
        }

        CleanupSession();
        _operationCancellation?.Dispose();
        _operationCancellation = null;
    }

    private void CleanupSession()
    {
        if (_sessionDirectory is null) return;
        _sessionStorage.CleanupSession(_sessionDirectory);
        _sessionDirectory = null;
    }

    private void ReturnToSelecting()
    {
        if (_stateMachine.State != CaptureUiState.Selecting) _stateMachine.ReturnToSelecting();
        _deferSelectingToolbarPlacement = true;
        _viewModel.State = CaptureUiState.Selecting;
        _operationCancellation?.Dispose();
        _operationCancellation = null;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (_viewModel.State != CaptureUiState.Selecting)
            {
                _deferSelectingToolbarPlacement = false;
                return;
            }

            _toolbarWindow?.UpdateLayout();
            _toolbarSelectingOrigin = null;
            _deferSelectingToolbarPlacement = false;
            UpdateVisualLayout();
        });
    }

    private string GetCalibrationKey() => $"{_viewModel.SelectedFormat}:{_viewModel.SelectedPreset}";

    private bool PromptForSaveFolder()
    {
        var initialDirectory = Directory.Exists(_saveFolder)
            ? _saveFolder
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "SimpleCapGIF");
        var dialog = new OpenFolderDialog
        {
            Title = AppStrings.FolderDialogTitle,
            InitialDirectory = initialDirectory,
        };
        if (dialog.ShowDialog(this) != true) return false;
        _saveFolder = dialog.FolderName;
        return true;
    }

    private void OnSelectedPresetChanged()
    {
        if (_suppressPresetResponse || _monitor is null) return;
        var selected = _viewModel.SelectedPreset;
        if (selected == OutputPreset.Original)
        {
            _acceptedPreset = selected;
            _acceptedPresetUserSelected = _viewModel.Settings.PresetUserSelected;
            return;
        }

        if (!PresetRegionService.TryCreateRegion(_viewModel.Region, _monitor.Bounds, selected, out var region))
        {
            _suppressPresetResponse = true;
            _viewModel.SetPresetProgrammatically(_acceptedPreset, _acceptedPresetUserSelected);
            _suppressPresetResponse = false;
            MessageBox.Show(
                AppStrings.ResolutionTooLarge,
                AppStrings.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _isFullScreen = false;
        _toolbarWindow?.SetFullScreenState(false);
        _viewModel.Region = region;
        _customRegion = region;
        _customPreset = selected;
        _customPresetUserSelected = _viewModel.Settings.PresetUserSelected;
        _acceptedPreset = selected;
        _acceptedPresetUserSelected = _viewModel.Settings.PresetUserSelected;
    }

    private void SetAcceptedPreset(OutputPreset preset, bool userSelected)
    {
        _suppressPresetResponse = true;
        _viewModel.SetPresetProgrammatically(preset, userSelected);
        _suppressPresetResponse = false;
        _acceptedPreset = preset;
        _acceptedPresetUserSelected = userSelected;
    }

    private void CreateToolbarWindow()
    {
        _toolbarWindow = new ToolbarWindow
        {
            Owner = this,
            DataContext = _viewModel,
        };
        _toolbarWindow.FullScreenRequested += OnToggleFullScreenClick;
        _toolbarWindow.FolderRequested += OnChooseFolderClick;
        _toolbarWindow.RecordRequested += OnRecordClick;
        _toolbarWindow.StopRequested += OnStopClick;
        _toolbarWindow.OpenFolderRequested += OnOpenFolderClick;
        _toolbarWindow.ExitRequested += OnExitRequested;
        _toolbarWindow.CaptureExclusionFailed += exception => _captureExclusionError = exception;
        _toolbarWindow.Show();
        if (_captureExclusionError is not null) throw _captureExclusionError;
    }

    private void OnExitRequested(object? sender, EventArgs e) => Close();

    private void SetActiveMonitor(MonitorDescriptor monitor)
    {
        _monitor = monitor;
        _toolbarSelectingOrigin = null;
        _isFullScreen = false;
        _toolbarWindow?.SetFullScreenState(false);
        PositionBorderWindow();
        WarnIfHdr(monitor);
    }

    private void WarnIfHdr(MonitorDescriptor monitor)
    {
        if (!_hdrWarningsShown.Add(monitor.DeviceName) || !MonitorService.IsHdrEnabled(monitor)) return;
        MessageBox.Show(
            AppStrings.HdrWarning,
            AppStrings.HdrWarningTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void PositionBorderWindow()
    {
        if (_monitor is null) return;
        OverlayWindowService.SetPhysicalBounds(new WindowInteropHelper(this).Handle, _monitor.Bounds);
    }

    private void UpdateVisualLayout()
    {
        if (_monitor is null || !IsLoaded) return;
        var localX = (_viewModel.Region.X - _monitor.Bounds.X) / _monitor.ScaleX;
        var localY = (_viewModel.Region.Y - _monitor.Bounds.Y) / _monitor.ScaleY;
        var width = _viewModel.Region.Width / _monitor.ScaleX;
        var height = _viewModel.Region.Height / _monitor.ScaleY;
        Canvas.SetLeft(SelectionBorder, localX);
        Canvas.SetTop(SelectionBorder, localY);
        SelectionBorder.Width = width;
        SelectionBorder.Height = height;
        SelectionBorder.BorderBrush = _viewModel.State == CaptureUiState.Recording
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(235, 48, 58))
            : (System.Windows.Media.Brush)FindResource("AccentBrush");

        var handlesVisible = _viewModel.State == CaptureUiState.Selecting && !_isFullScreen ? Visibility.Visible : Visibility.Collapsed;
        PositionHandle(TopLeftHandle, localX, localY, handlesVisible);
        PositionHandle(TopHandle, localX + (width / 2), localY, handlesVisible);
        PositionHandle(TopRightHandle, localX + width, localY, handlesVisible);
        PositionHandle(RightHandle, localX + width, localY + (height / 2), handlesVisible);
        PositionHandle(BottomRightHandle, localX + width, localY + height, handlesVisible);
        PositionHandle(BottomHandle, localX + (width / 2), localY + height, handlesVisible);
        PositionHandle(BottomLeftHandle, localX, localY + height, handlesVisible);
        PositionHandle(LeftHandle, localX, localY + (height / 2), handlesVisible);
        MoveThumb.IsEnabled = handlesVisible == Visibility.Visible;

        RegionLabelBorder.Visibility = _viewModel.State == CaptureUiState.Selecting ? Visibility.Visible : Visibility.Collapsed;
        RegionLabelBorder.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(RegionLabelBorder, localX + 8);
        Canvas.SetTop(RegionLabelBorder, Math.Max(0, localY - RegionLabelBorder.DesiredSize.Height - 6));

        if (_toolbarWindow is null || _deferSelectingToolbarPlacement && _viewModel.State == CaptureUiState.Selecting) return;
        var toolbarPixels = _toolbarWindow.MeasurePhysicalSize(_monitor.ScaleX, _monitor.ScaleY);
        PixelPoint toolbarPoint;
        if (_viewModel.State == CaptureUiState.Selecting || _toolbarSelectingOrigin is null)
        {
            toolbarPoint = _placementService.Calculate(_viewModel.Region, _monitor.Bounds, _monitor.WorkArea, toolbarPixels, _viewModel.State);
            if (_viewModel.State == CaptureUiState.Selecting) _toolbarSelectingOrigin = toolbarPoint;
        }
        else
        {
            toolbarPoint = new PixelRect(
                _toolbarSelectingOrigin.Value.X,
                _toolbarSelectingOrigin.Value.Y,
                toolbarPixels.Width,
                toolbarPixels.Height).ClampInside(_monitor.Bounds).Location;
        }

        OverlayWindowService.SetPhysicalBounds(
            new WindowInteropHelper(_toolbarWindow).Handle,
            new PixelRect(toolbarPoint.X, toolbarPoint.Y, toolbarPixels.Width, toolbarPixels.Height));
    }

    private static void PositionHandle(FrameworkElement handle, double centerX, double centerY, Visibility visibility)
    {
        handle.Visibility = visibility;
        Canvas.SetLeft(handle, centerX - (handle.Width / 2));
        Canvas.SetTop(handle, centerY - (handle.Height / 2));
    }

    private static void ShowError(string message, Exception exception) =>
        MessageBox.Show($"{message}\n\n{exception.Message}", AppStrings.ErrorDialogTitle, MessageBoxButton.OK, MessageBoxImage.Error);
}
