using System.ComponentModel;
using System.Runtime.CompilerServices;
using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Core.Services;
using SimpleCapGIF.Localization;

namespace SimpleCapGIF.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly OutputSizeCalculator _outputSizeCalculator = new();
    private CaptureSettings _settings = CaptureSettings.Default;
    private PixelRect _region = new(0, 0, 800, 450);
    private CaptureUiState _state = CaptureUiState.Selecting;
    private string _statusText = string.Empty;
    private TimeSpan _elapsed;
    private long _estimatedBytes;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<AnimationFormat> Formats { get; } = Enum.GetValues<AnimationFormat>();
    public IReadOnlyList<OutputPreset> Presets { get; } = Enum.GetValues<OutputPreset>();
    public IReadOnlyList<int> FrameRates { get; } = [5, 10, 15, 20, 30];

    public PixelRect Region
    {
        get => _region;
        set
        {
            if (_region == value) return;
            _region = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(OutputSize));
            OnPropertyChanged(nameof(RegionLabel));
        }
    }

    public PixelSize OutputSize => _outputSizeCalculator.Calculate(Region.Size, _settings.OutputPreset);
    public string RegionLabel => $"{Region.Width}×{Region.Height} → {OutputSize.Width}×{OutputSize.Height}";

    public AnimationFormat SelectedFormat
    {
        get => _settings.Format;
        set
        {
            if (_settings.Format == value) return;
            _settings = _settings.WithFormat(value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedFps));
        }
    }

    public OutputPreset SelectedPreset
    {
        get => _settings.OutputPreset;
        set
        {
            if (_settings.OutputPreset == value) return;
            _settings = _settings with { OutputPreset = value, PresetUserSelected = true };
            OnPropertyChanged();
            OnPropertyChanged(nameof(OutputSize));
            OnPropertyChanged(nameof(RegionLabel));
        }
    }

    public int SelectedFps
    {
        get => _settings.FramesPerSecond;
        set
        {
            if (_settings.FramesPerSecond == value) return;
            _settings = (_settings with { FramesPerSecond = value, FpsUserSelected = true }).Validate();
            OnPropertyChanged();
        }
    }

    public CaptureSettings Settings => _settings;

    public void SetPresetProgrammatically(OutputPreset preset, bool userSelected)
    {
        if (_settings.OutputPreset == preset && _settings.PresetUserSelected == userSelected) return;
        _settings = _settings with { OutputPreset = preset, PresetUserSelected = userSelected };
        OnPropertyChanged(nameof(SelectedPreset));
        OnPropertyChanged(nameof(OutputSize));
        OnPropertyChanged(nameof(RegionLabel));
    }

    public void ApplySettings(CaptureSettings settings)
    {
        _settings = settings.Validate();
        OnPropertyChanged(nameof(SelectedFormat));
        OnPropertyChanged(nameof(SelectedPreset));
        OnPropertyChanged(nameof(SelectedFps));
        OnPropertyChanged(nameof(OutputSize));
        OnPropertyChanged(nameof(RegionLabel));
    }

    public CaptureUiState State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSelecting));
            OnPropertyChanged(nameof(IsRecording));
            OnPropertyChanged(nameof(IsEncoding));
            OnPropertyChanged(nameof(IsCompleted));
        }
    }

    public bool IsSelecting => State == CaptureUiState.Selecting;
    public bool IsRecording => State == CaptureUiState.Recording;
    public bool IsEncoding => State == CaptureUiState.Encoding;
    public bool IsCompleted => State == CaptureUiState.Completed;

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public TimeSpan Elapsed
    {
        get => _elapsed;
        set
        {
            if (SetField(ref _elapsed, value)) OnPropertyChanged(nameof(ElapsedText));
        }
    }

    public string ElapsedText => Elapsed.ToString(@"mm\:ss\.f", System.Globalization.CultureInfo.InvariantCulture);

    public long EstimatedBytes
    {
        get => _estimatedBytes;
        set
        {
            if (SetField(ref _estimatedBytes, value))
            {
                OnPropertyChanged(nameof(EstimatedText));
                OnPropertyChanged(nameof(IsSizeWarning));
            }
        }
    }

    public string EstimatedText => AppStrings.Format(AppStrings.EstimatedSizeFormat, EstimatedBytes / 1_000_000d);
    public bool IsSizeWarning => EstimatedBytes >= OutputSizeEstimate.WarningThresholdBytes;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
