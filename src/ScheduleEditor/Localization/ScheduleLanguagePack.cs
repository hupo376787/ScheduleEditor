using System.Text.Json.Serialization;

namespace ScheduleEditor.Localization;

/// <summary>
/// 可序列化的组件语言包。Strings 允许只提供需要覆盖的键。
/// </summary>
public sealed record ScheduleLanguagePack
{
    [JsonPropertyName("culture")]
    public string Culture { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("fallbackCulture")]
    public string? FallbackCulture { get; init; }

    [JsonPropertyName("strings")]
    public Dictionary<string, string> Strings { get; init; } =
        new(StringComparer.OrdinalIgnoreCase);
}
