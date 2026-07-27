using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using SimpleCapGIF.Localization;

namespace SimpleCapGIF.Core.Tests;

public sealed partial class LocalizationTests
{
    public static TheoryData<string, string> CultureMappings => new()
    {
        { "en-GB", "en-US" },
        { "ko", "ko-KR" },
        { "ja", "ja-JP" },
        { "es-MX", "es" },
        { "de-AT", "de-DE" },
        { "fr-CA", "fr-FR" },
        { "pt-PT", "pt-BR" },
        { "zh-CN", "zh-Hans" },
        { "zh-SG", "zh-Hans" },
        { "zh-TW", "zh-Hant" },
        { "zh-HK", "zh-Hant" },
        { "zh-Hant-MO", "zh-Hant" },
        { "it-IT", "en-US" },
    };

    [Theory]
    [MemberData(nameof(CultureMappings))]
    public void WindowsCultureMapsToSupportedUiCulture(string requested, string expected) =>
        Assert.Equal(expected, UiCultureResolver.Resolve(CultureInfo.GetCultureInfo(requested)).Name);

    [Fact]
    public void SupportedCultureListIsStableAndUnique()
    {
        Assert.Equal(
            ["en-US", "ko-KR", "ja-JP", "zh-Hans", "pt-BR", "es", "de-DE", "fr-FR", "zh-Hant"],
            UiCultureResolver.SupportedCultures.Select(static culture => culture.Name));
        Assert.Equal(9, UiCultureResolver.SupportedCultures.Select(static culture => culture.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void EveryLocalizedResourceHasTheEnglishKeysAndPlaceholders()
    {
        var english = ReadExactResource(CultureInfo.GetCultureInfo("en-US"));
        Assert.Equal(87, english.Count);

        foreach (var culture in UiCultureResolver.SupportedCultures)
        {
            var localized = ReadExactResource(culture);
            Assert.Equal(english.Keys.Order(), localized.Keys.Order());
            foreach (var key in english.Keys)
            {
                Assert.False(string.IsNullOrWhiteSpace(localized[key]), $"{culture.Name}:{key} is empty.");
                Assert.Equal(Placeholders(english[key]), Placeholders(localized[key]));
            }
        }
    }

    [Fact]
    public void UnsupportedResourceCultureFallsBackToEnglish() =>
        Assert.Equal(
            AppStrings.Get(nameof(AppStrings.Record), CultureInfo.GetCultureInfo("en-US")),
            AppStrings.Get(nameof(AppStrings.Record), CultureInfo.GetCultureInfo("it-IT")));

    [Fact]
    public void SourceContainsNoHardcodedKoreanUiText()
    {
        var repository = FindRepositoryRoot();
        var sourceFiles = Directory.EnumerateFiles(Path.Combine(repository, "src"), "*.*", SearchOption.AllDirectories)
            .Where(static path => Path.GetExtension(path) is ".cs" or ".xaml")
            .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
        var offenders = sourceFiles
            .Where(path => HangulRegex().IsMatch(File.ReadAllText(path)))
            .Select(path => Path.GetRelativePath(repository, path))
            .ToArray();

        Assert.Empty(offenders);
    }

    private static Dictionary<string, string> ReadExactResource(CultureInfo culture)
    {
        var set = AppStrings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.NotNull(set);
        return set.Cast<DictionaryEntry>().ToDictionary(
            static entry => Assert.IsType<string>(entry.Key),
            static entry => Assert.IsType<string>(entry.Value),
            StringComparer.Ordinal);
    }

    private static string[] Placeholders(string value) => PlaceholderRegex().Matches(value)
        .Select(static match => match.Groups[1].Value)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "SimpleCapGIF.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }

    [GeneratedRegex(@"\{(\d+)(?:[^}]*)\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();

    [GeneratedRegex("[\\uAC00-\\uD7A3]", RegexOptions.CultureInvariant)]
    private static partial Regex HangulRegex();
}
