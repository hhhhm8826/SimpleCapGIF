using System.Globalization;
using System.Windows.Data;
using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Localization;

namespace SimpleCapGIF.App.Converters;

public sealed class OptionDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        AnimationFormat.Gif => "GIF",
        AnimationFormat.WebP => "WebP",
        OutputPreset.P640x360 => "640×360",
        OutputPreset.P800x450 => "800×450",
        OutputPreset.P960x540 => "960×540",
        OutputPreset.P1280x720 => "1280×720",
        OutputPreset.Original => AppStrings.Original,
        int framesPerSecond => $"{framesPerSecond} FPS",
        _ => value?.ToString() ?? string.Empty,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
