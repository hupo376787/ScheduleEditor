using System.Globalization;
using System.Text.Json;

namespace ScheduleEditor.Localization;

/// <summary>
/// 组件本地化服务。内置简体中文和英文，支持运行时注册完整或部分 JSON 语言包。
/// </summary>
public sealed class ScheduleLocalizationService : IScheduleLocalizationService
{
    public const string ChineseCulture = "zh-CN";
    public const string EnglishCulture = "en-US";

    private readonly object _syncRoot = new();
    private readonly Dictionary<string, ScheduleLanguagePack> _languagePacks =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private string _currentCulture = EnglishCulture;

    public ScheduleLocalizationService(string? initialCulture = null)
    {
        AddOrUpdateLanguagePack(CreateChinesePack());
        AddOrUpdateLanguagePack(CreateEnglishPack());

        _currentCulture = ResolveInitialCulture(initialCulture);
    }

    public string CurrentCulture
    {
        get
        {
            lock (_syncRoot)
                return _currentCulture;
        }
    }

    public CultureInfo CurrentCultureInfo
    {
        get
        {
            var culture = CurrentCulture;

            try
            {
                return CultureInfo.GetCultureInfo(culture);
            }
            catch (CultureNotFoundException)
            {
                return CultureInfo.InvariantCulture;
            }
        }
    }

    public IReadOnlyList<ScheduleLanguageInfo> AvailableLanguages
    {
        get
        {
            lock (_syncRoot)
            {
                return _languagePacks.Values
                    .Select(pack => new ScheduleLanguageInfo(
                        pack.Culture,
                        string.IsNullOrWhiteSpace(pack.DisplayName)
                            ? pack.Culture
                            : pack.DisplayName))
                    .OrderBy(language => language.DisplayName)
                    .ToList();
            }
        }
    }

    public event EventHandler? LanguageChanged;

    public event EventHandler? LanguagesChanged;

    public string Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (_syncRoot)
        {
            return TryGetTextCore(_currentCulture, key, new HashSet<string>(
                       StringComparer.OrdinalIgnoreCase), out var text)
                ? text
                : key;
        }
    }

    public string Format(string key, params object?[] arguments)
    {
        var template = Get(key);

        try
        {
            return string.Format(CurrentCultureInfo, template, arguments);
        }
        catch (FormatException)
        {
            // 客户自定义语言包中的格式占位符不正确时，避免组件界面直接崩溃。
            return template;
        }
    }

    public void SetCulture(string culture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);

        bool changed;

        lock (_syncRoot)
        {
            var resolved = ResolveRegisteredCulture(culture)
                           ?? throw new KeyNotFoundException(
                               $"Language pack '{culture}' is not registered.");

            changed = !string.Equals(
                _currentCulture,
                resolved,
                StringComparison.OrdinalIgnoreCase);

            _currentCulture = resolved;
        }

        if (changed)
            LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddOrUpdateLanguagePack(
        ScheduleLanguagePack languagePack,
        bool setCurrent = false)
    {
        ArgumentNullException.ThrowIfNull(languagePack);
        ArgumentException.ThrowIfNullOrWhiteSpace(languagePack.Culture);

        var culture = languagePack.Culture.Trim();
        bool currentLanguageUpdated;

        lock (_syncRoot)
        {
            if (_languagePacks.TryGetValue(culture, out var existing))
            {
                var mergedStrings = new Dictionary<string, string>(
                    existing.Strings,
                    StringComparer.OrdinalIgnoreCase);

                foreach (var pair in languagePack.Strings ?? new Dictionary<string, string>())
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key))
                        mergedStrings[pair.Key] = pair.Value ?? string.Empty;
                }

                _languagePacks[culture] = languagePack with
                {
                    Culture = culture,
                    DisplayName = string.IsNullOrWhiteSpace(languagePack.DisplayName)
                        ? existing.DisplayName
                        : languagePack.DisplayName.Trim(),
                    FallbackCulture = string.IsNullOrWhiteSpace(languagePack.FallbackCulture)
                        ? existing.FallbackCulture
                        : languagePack.FallbackCulture.Trim(),
                    Strings = mergedStrings
                };
            }
            else
            {
                _languagePacks[culture] = languagePack with
                {
                    Culture = culture,
                    DisplayName = string.IsNullOrWhiteSpace(languagePack.DisplayName)
                        ? culture
                        : languagePack.DisplayName.Trim(),
                    FallbackCulture = string.IsNullOrWhiteSpace(languagePack.FallbackCulture)
                        ? EnglishCulture
                        : languagePack.FallbackCulture.Trim(),
                    Strings = new Dictionary<string, string>(
                        languagePack.Strings ?? new Dictionary<string, string>(),
                        StringComparer.OrdinalIgnoreCase)
                };
            }

            if (setCurrent)
                _currentCulture = culture;

            currentLanguageUpdated = setCurrent || string.Equals(
                _currentCulture,
                culture,
                StringComparison.OrdinalIgnoreCase);
        }

        LanguagesChanged?.Invoke(this, EventArgs.Empty);

        if (currentLanguageUpdated)
            LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddOrUpdateLanguagePackFromJson(
        string json,
        bool setCurrent = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var pack = JsonSerializer.Deserialize<ScheduleLanguagePack>(
                       json,
                       _jsonOptions)
                   ?? throw new JsonException("The language pack JSON is empty.");

        AddOrUpdateLanguagePack(pack, setCurrent);
    }

    public void AddOrUpdateLanguageOverridesFromJson(
        string culture,
        string json,
        string? displayName = null,
        string? fallbackCulture = null,
        bool setCurrent = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(culture);
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(
                          json,
                          _jsonOptions)
                      ?? throw new JsonException("The language override JSON is empty.");

        AddOrUpdateLanguagePack(
            new ScheduleLanguagePack
            {
                Culture = culture,
                DisplayName = displayName ?? string.Empty,
                FallbackCulture = fallbackCulture,
                Strings = new Dictionary<string, string>(
                    strings,
                    StringComparer.OrdinalIgnoreCase)
            },
            setCurrent);
    }

    public async Task AddOrUpdateLanguagePackFromFileAsync(
        string filePath,
        bool setCurrent = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        AddOrUpdateLanguagePackFromJson(json, setCurrent);
    }

    private string ResolveInitialCulture(string? initialCulture)
    {
        lock (_syncRoot)
        {
            return ResolveRegisteredCulture(initialCulture)
                   ?? ResolveRegisteredCulture(CultureInfo.CurrentUICulture.Name)
                   ?? (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName
                       .Equals("zh", StringComparison.OrdinalIgnoreCase)
                       ? ChineseCulture
                       : EnglishCulture);
        }
    }

    private string? ResolveRegisteredCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
            return null;

        if (_languagePacks.ContainsKey(culture))
            return _languagePacks.Keys.First(key => key.Equals(
                culture,
                StringComparison.OrdinalIgnoreCase));

        var languagePrefix = culture.Split('-', '_')[0];

        return _languagePacks.Keys.FirstOrDefault(key =>
            key.Split('-', '_')[0].Equals(
                languagePrefix,
                StringComparison.OrdinalIgnoreCase));
    }

    private bool TryGetTextCore(
        string culture,
        string key,
        ISet<string> visitedCultures,
        out string text)
    {
        text = string.Empty;

        if (!visitedCultures.Add(culture) ||
            !_languagePacks.TryGetValue(culture, out var pack))
        {
            return false;
        }

        if (pack.Strings.TryGetValue(key, out text!))
            return true;

        if (!string.IsNullOrWhiteSpace(pack.FallbackCulture) &&
            TryGetTextCore(pack.FallbackCulture, key, visitedCultures, out text))
        {
            return true;
        }

        if (!culture.Equals(EnglishCulture, StringComparison.OrdinalIgnoreCase) &&
            TryGetTextCore(EnglishCulture, key, visitedCultures, out text))
        {
            return true;
        }

        return false;
    }

    private static ScheduleLanguagePack CreateChinesePack() => new()
    {
        Culture = ChineseCulture,
        DisplayName = "简体中文",
        FallbackCulture = EnglishCulture,
        Strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ScheduleTextKeys.Title] = "自动执行",
            [ScheduleTextKeys.Description] = "程序运行期间，按设定规则自动执行任务。",
            [ScheduleTextKeys.Enabled] = "启用",
            [ScheduleTextKeys.Disabled] = "停用",
            [ScheduleTextKeys.RepeatMode] = "重复方式",
            [ScheduleTextKeys.EverySeconds] = "每 N 秒",
            [ScheduleTextKeys.EveryMinutes] = "每 N 分钟",
            [ScheduleTextKeys.EveryHours] = "每 N 小时",
            [ScheduleTextKeys.Daily] = "每天",
            [ScheduleTextKeys.Weekly] = "每周",
            [ScheduleTextKeys.Monthly] = "每月",
            [ScheduleTextKeys.Cron] = "Cron",
            [ScheduleTextKeys.Interval] = "间隔",
            [ScheduleTextKeys.SecondsUnit] = "秒",
            [ScheduleTextKeys.MinutesUnit] = "分钟",
            [ScheduleTextKeys.HoursUnit] = "小时",
            [ScheduleTextKeys.ExecutionTime] = "执行时间",
            [ScheduleTextKeys.WeeklyDays] = "每周日期",
            [ScheduleTextKeys.DayOfMonth] = "每月日期",
            [ScheduleTextKeys.MonthlyDayHelp] = "范围 1～31；当月不存在该日期时，本月跳过。",
            [ScheduleTextKeys.CronExpression] = "Cron 表达式",
            [ScheduleTextKeys.CronHelp] = "支持 5 段（分 时 日 月 星期）或 6 段（秒 分 时 日 月 星期）。",
            [ScheduleTextKeys.NextRun] = "下次执行",
            [ScheduleTextKeys.Cancel] = "取消",
            [ScheduleTextKeys.Save] = "保存",
            [ScheduleTextKeys.SummaryDisabled] = "自动执行当前处于停用状态。",
            [ScheduleTextKeys.SummaryEverySeconds] = "每 {0} 秒自动执行一次。",
            [ScheduleTextKeys.SummaryEveryMinutes] = "每 {0} 分钟自动执行一次。",
            [ScheduleTextKeys.SummaryEveryHours] = "每 {0} 小时自动执行一次。",
            [ScheduleTextKeys.SummaryDaily] = "每天 {0} 自动执行。",
            [ScheduleTextKeys.SummaryWeekly] = "每周{0} {1} 自动执行。",
            [ScheduleTextKeys.SummaryMonthly] = "每月 {0} 日 {1} 自动执行。",
            [ScheduleTextKeys.SummaryCron] = "按照 Cron 表达式“{0}”自动执行。",
            [ScheduleTextKeys.SummarySelectWeekDays] = "请选择每周执行日期。",
            [ScheduleTextKeys.NextRunSelectValidDate] = "请选择有效的执行规则",
            [ScheduleTextKeys.NextRunDisabled] = "已停用",
            [ScheduleTextKeys.FeedbackSavedEnabled] = "定时设置已保存并生效。",
            [ScheduleTextKeys.FeedbackSavedDisabled] = "定时任务已停用。",
            [ScheduleTextKeys.ErrorChooseInterval] = "间隔必须是大于或等于 1 的整数。",
            [ScheduleTextKeys.ErrorChooseTime] = "请选择执行时间。",
            [ScheduleTextKeys.ErrorChooseWeekDay] = "每周执行时，至少选择一个星期。",
            [ScheduleTextKeys.ErrorChooseMonthDay] = "每月日期必须位于 1 到 31 之间。",
            [ScheduleTextKeys.ErrorChooseCron] = "请输入 Cron 表达式。",
            [ScheduleTextKeys.ErrorInvalidCron] = "Cron 表达式无效：{0}",
            [ScheduleTextKeys.ErrorNoVisibleModes] = "至少需要显示一种重复方式。",
            [ScheduleTextKeys.ErrorSaveFailed] = "保存失败：{0}",
            [ScheduleTextKeys.DateTimeFormat] = "yyyy-MM-dd ddd HH:mm:ss",
            [ScheduleTextKeys.ListSeparator] = "、",
            [ScheduleTextKeys.MondayShort] = "一",
            [ScheduleTextKeys.TuesdayShort] = "二",
            [ScheduleTextKeys.WednesdayShort] = "三",
            [ScheduleTextKeys.ThursdayShort] = "四",
            [ScheduleTextKeys.FridayShort] = "五",
            [ScheduleTextKeys.SaturdayShort] = "六",
            [ScheduleTextKeys.SundayShort] = "日",
            [ScheduleTextKeys.MondayLong] = "星期一",
            [ScheduleTextKeys.TuesdayLong] = "星期二",
            [ScheduleTextKeys.WednesdayLong] = "星期三",
            [ScheduleTextKeys.ThursdayLong] = "星期四",
            [ScheduleTextKeys.FridayLong] = "星期五",
            [ScheduleTextKeys.SaturdayLong] = "星期六",
            [ScheduleTextKeys.SundayLong] = "星期日"
        }
    };

    private static ScheduleLanguagePack CreateEnglishPack() => new()
    {
        Culture = EnglishCulture,
        DisplayName = "English",
        FallbackCulture = null,
        Strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ScheduleTextKeys.Title] = "Automatic execution",
            [ScheduleTextKeys.Description] = "Run the task automatically according to the configured rule while the app is running.",
            [ScheduleTextKeys.Enabled] = "Enabled",
            [ScheduleTextKeys.Disabled] = "Disabled",
            [ScheduleTextKeys.RepeatMode] = "Repeat",
            [ScheduleTextKeys.EverySeconds] = "Every N seconds",
            [ScheduleTextKeys.EveryMinutes] = "Every N minutes",
            [ScheduleTextKeys.EveryHours] = "Every N hours",
            [ScheduleTextKeys.Daily] = "Daily",
            [ScheduleTextKeys.Weekly] = "Weekly",
            [ScheduleTextKeys.Monthly] = "Monthly",
            [ScheduleTextKeys.Cron] = "Cron",
            [ScheduleTextKeys.Interval] = "Interval",
            [ScheduleTextKeys.SecondsUnit] = "seconds",
            [ScheduleTextKeys.MinutesUnit] = "minutes",
            [ScheduleTextKeys.HoursUnit] = "hours",
            [ScheduleTextKeys.ExecutionTime] = "Execution time",
            [ScheduleTextKeys.WeeklyDays] = "Days of week",
            [ScheduleTextKeys.DayOfMonth] = "Day of month",
            [ScheduleTextKeys.MonthlyDayHelp] = "Enter 1–31. Months without that date are skipped.",
            [ScheduleTextKeys.CronExpression] = "Cron expression",
            [ScheduleTextKeys.CronHelp] = "Supports 5 fields (minute hour day month weekday) or 6 fields (second minute hour day month weekday).",
            [ScheduleTextKeys.NextRun] = "Next run",
            [ScheduleTextKeys.Cancel] = "Cancel",
            [ScheduleTextKeys.Save] = "Save",
            [ScheduleTextKeys.SummaryDisabled] = "Automatic execution is currently disabled.",
            [ScheduleTextKeys.SummaryEverySeconds] = "Run every {0} seconds.",
            [ScheduleTextKeys.SummaryEveryMinutes] = "Run every {0} minutes.",
            [ScheduleTextKeys.SummaryEveryHours] = "Run every {0} hours.",
            [ScheduleTextKeys.SummaryDaily] = "Run every day at {0}.",
            [ScheduleTextKeys.SummaryWeekly] = "Run every {0} at {1}.",
            [ScheduleTextKeys.SummaryMonthly] = "Run on day {0} of every month at {1}.",
            [ScheduleTextKeys.SummaryCron] = "Run according to Cron expression \"{0}\".",
            [ScheduleTextKeys.SummarySelectWeekDays] = "Select at least one day of the week.",
            [ScheduleTextKeys.NextRunSelectValidDate] = "Select a valid schedule",
            [ScheduleTextKeys.NextRunDisabled] = "Disabled",
            [ScheduleTextKeys.FeedbackSavedEnabled] = "The schedule was saved and is now active.",
            [ScheduleTextKeys.FeedbackSavedDisabled] = "The scheduled task was disabled.",
            [ScheduleTextKeys.ErrorChooseInterval] = "The interval must be an integer greater than or equal to 1.",
            [ScheduleTextKeys.ErrorChooseTime] = "Select an execution time.",
            [ScheduleTextKeys.ErrorChooseWeekDay] = "Select at least one weekday for a weekly schedule.",
            [ScheduleTextKeys.ErrorChooseMonthDay] = "The day of month must be between 1 and 31.",
            [ScheduleTextKeys.ErrorChooseCron] = "Enter a Cron expression.",
            [ScheduleTextKeys.ErrorInvalidCron] = "Invalid Cron expression: {0}",
            [ScheduleTextKeys.ErrorNoVisibleModes] = "At least one repeat mode must be visible.",
            [ScheduleTextKeys.ErrorSaveFailed] = "Unable to save: {0}",
            [ScheduleTextKeys.DateTimeFormat] = "yyyy-MM-dd ddd HH:mm:ss",
            [ScheduleTextKeys.ListSeparator] = ", ",
            [ScheduleTextKeys.MondayShort] = "Mon",
            [ScheduleTextKeys.TuesdayShort] = "Tue",
            [ScheduleTextKeys.WednesdayShort] = "Wed",
            [ScheduleTextKeys.ThursdayShort] = "Thu",
            [ScheduleTextKeys.FridayShort] = "Fri",
            [ScheduleTextKeys.SaturdayShort] = "Sat",
            [ScheduleTextKeys.SundayShort] = "Sun",
            [ScheduleTextKeys.MondayLong] = "Monday",
            [ScheduleTextKeys.TuesdayLong] = "Tuesday",
            [ScheduleTextKeys.WednesdayLong] = "Wednesday",
            [ScheduleTextKeys.ThursdayLong] = "Thursday",
            [ScheduleTextKeys.FridayLong] = "Friday",
            [ScheduleTextKeys.SaturdayLong] = "Saturday",
            [ScheduleTextKeys.SundayLong] = "Sunday"
        }
    };
}
