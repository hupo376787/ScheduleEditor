using System.Globalization;

namespace AvaloniaScheduleEditor.Localization;

public interface IScheduleLocalizationService
{
    string CurrentCulture { get; }

    CultureInfo CurrentCultureInfo { get; }

    IReadOnlyList<ScheduleLanguageInfo> AvailableLanguages { get; }

    event EventHandler? LanguageChanged;

    event EventHandler? LanguagesChanged;

    string Get(string key);

    string Format(string key, params object?[] arguments);

    void SetCulture(string culture);

    void AddOrUpdateLanguagePack(
        ScheduleLanguagePack languagePack,
        bool setCurrent = false);

    void AddOrUpdateLanguagePackFromJson(
        string json,
        bool setCurrent = false);

    void AddOrUpdateLanguageOverridesFromJson(
        string culture,
        string json,
        string? displayName = null,
        string? fallbackCulture = null,
        bool setCurrent = false);

    Task AddOrUpdateLanguagePackFromFileAsync(
        string filePath,
        bool setCurrent = false,
        CancellationToken cancellationToken = default);
}
