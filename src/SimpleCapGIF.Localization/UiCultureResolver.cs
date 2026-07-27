using System.Globalization;

namespace SimpleCapGIF.Localization;

public static class UiCultureResolver
{
    private static readonly IReadOnlyList<CultureInfo> Cultures = Array.AsReadOnly(
    [
        CultureInfo.GetCultureInfo("en-US"),
        CultureInfo.GetCultureInfo("ko-KR"),
        CultureInfo.GetCultureInfo("ja-JP"),
        CultureInfo.GetCultureInfo("zh-Hans"),
        CultureInfo.GetCultureInfo("pt-BR"),
        CultureInfo.GetCultureInfo("es"),
        CultureInfo.GetCultureInfo("de-DE"),
        CultureInfo.GetCultureInfo("fr-FR"),
        CultureInfo.GetCultureInfo("zh-Hant"),
    ]);

    public static IReadOnlyList<CultureInfo> SupportedCultures => Cultures;

    public static CultureInfo Resolve(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        var name = culture.Name;
        var language = culture.TwoLetterISOLanguageName;
        return language switch
        {
            "en" => CultureInfo.GetCultureInfo("en-US"),
            "ko" => CultureInfo.GetCultureInfo("ko-KR"),
            "ja" => CultureInfo.GetCultureInfo("ja-JP"),
            "pt" => CultureInfo.GetCultureInfo("pt-BR"),
            "es" => CultureInfo.GetCultureInfo("es"),
            "de" => CultureInfo.GetCultureInfo("de-DE"),
            "fr" => CultureInfo.GetCultureInfo("fr-FR"),
            "zh" when IsTraditionalChinese(name) => CultureInfo.GetCultureInfo("zh-Hant"),
            "zh" => CultureInfo.GetCultureInfo("zh-Hans"),
            _ => CultureInfo.GetCultureInfo("en-US"),
        };
    }

    public static CultureInfo ApplyFromWindows(CultureInfo windowsUiCulture)
    {
        var resolved = Resolve(windowsUiCulture);
        CultureInfo.CurrentUICulture = resolved;
        CultureInfo.DefaultThreadCurrentUICulture = resolved;
        return resolved;
    }

    private static bool IsTraditionalChinese(string name) =>
        name.Contains("Hant", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("zh-TW", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("zh-HK", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("zh-MO", StringComparison.OrdinalIgnoreCase);
}
