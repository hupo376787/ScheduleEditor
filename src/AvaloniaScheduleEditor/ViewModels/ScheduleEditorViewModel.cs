using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Threading;
using AvaloniaScheduleEditor.Localization;
using AvaloniaScheduleEditor.Models;
using AvaloniaScheduleEditor.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AvaloniaScheduleEditor.ViewModels;

public partial class ScheduleEditorViewModel : ObservableObject, IDisposable
{
    private readonly IScheduleLocalizationService _localization;
    private ScheduleOptions _savedOptions = new();
    private bool _disposed;

    public ScheduleEditorViewModel(IScheduleLocalizationService? localization = null)
    {
        _localization = localization ?? new ScheduleLocalizationService();

        WeekDays =
        [
            CreateWeekDay(DayOfWeek.Monday, true),
            CreateWeekDay(DayOfWeek.Tuesday, true),
            CreateWeekDay(DayOfWeek.Wednesday, true),
            CreateWeekDay(DayOfWeek.Thursday, true),
            CreateWeekDay(DayOfWeek.Friday, true),
            CreateWeekDay(DayOfWeek.Saturday, false),
            CreateWeekDay(DayOfWeek.Sunday, false)
        ];

        foreach (var item in WeekDays)
            item.PropertyChanged += OnWeekDayPropertyChanged;

        _localization.LanguageChanged += OnLanguageChanged;
        ApplyLocalizedText();
        Load(_savedOptions);
    }

    public ObservableCollection<WeekDayItemViewModel> WeekDays { get; }
    public IScheduleLocalizationService Localization => _localization;

    public Func<ScheduleOptions, Task>? SaveHandler { get; set; }
    public Func<Task>? CancelHandler { get; set; }

    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private ScheduleRepeatType _repeatType = ScheduleRepeatType.Daily;
    [ObservableProperty] private decimal? _intervalValue = 1;
    [ObservableProperty] private TimeSpan? _executeTime = new TimeSpan(9, 30, 0);
    [ObservableProperty] private decimal? _dayOfMonth = 1;
    [ObservableProperty] private string _cronExpression = "0 9 * * *";

    // 默认仅显示每天、每周、每月。
    [ObservableProperty] private bool _showEverySeconds;
    [ObservableProperty] private bool _showEveryMinutes;
    [ObservableProperty] private bool _showEveryHours;
    [ObservableProperty] private bool _showDaily = true;
    [ObservableProperty] private bool _showWeekly = true;
    [ObservableProperty] private bool _showMonthly = true;
    [ObservableProperty] private bool _showCron;

    [ObservableProperty] private string _nextRunText = "—";
    [ObservableProperty] private string _summaryText = string.Empty;
    [ObservableProperty] private string? _errorText;
    [ObservableProperty] private string? _feedbackText;
    [ObservableProperty] private bool _isBusy;

    [ObservableProperty] private string _titleText = string.Empty;
    [ObservableProperty] private string _descriptionText = string.Empty;
    [ObservableProperty] private string _enabledText = string.Empty;
    [ObservableProperty] private string _disabledText = string.Empty;
    [ObservableProperty] private string _repeatModeText = string.Empty;
    [ObservableProperty] private string _everySecondsText = string.Empty;
    [ObservableProperty] private string _everyMinutesText = string.Empty;
    [ObservableProperty] private string _everyHoursText = string.Empty;
    [ObservableProperty] private string _dailyText = string.Empty;
    [ObservableProperty] private string _weeklyText = string.Empty;
    [ObservableProperty] private string _monthlyText = string.Empty;
    [ObservableProperty] private string _cronText = string.Empty;
    [ObservableProperty] private string _intervalText = string.Empty;
    [ObservableProperty] private string _executionTimeText = string.Empty;
    [ObservableProperty] private string _weeklyDaysText = string.Empty;
    [ObservableProperty] private string _dayOfMonthText = string.Empty;
    [ObservableProperty] private string _monthlyDayHelpText = string.Empty;
    [ObservableProperty] private string _cronExpressionText = string.Empty;
    [ObservableProperty] private string _cronHelpText = string.Empty;
    [ObservableProperty] private string _nextRunLabelText = string.Empty;
    [ObservableProperty] private string _cancelText = string.Empty;
    [ObservableProperty] private string _saveText = string.Empty;

    public bool IsEverySeconds
    {
        get => RepeatType == ScheduleRepeatType.EverySeconds;
        set { if (value) RepeatType = ScheduleRepeatType.EverySeconds; }
    }

    public bool IsEveryMinutes
    {
        get => RepeatType == ScheduleRepeatType.EveryMinutes;
        set { if (value) RepeatType = ScheduleRepeatType.EveryMinutes; }
    }

    public bool IsEveryHours
    {
        get => RepeatType == ScheduleRepeatType.EveryHours;
        set { if (value) RepeatType = ScheduleRepeatType.EveryHours; }
    }

    public bool IsDaily
    {
        get => RepeatType == ScheduleRepeatType.Daily;
        set { if (value) RepeatType = ScheduleRepeatType.Daily; }
    }

    public bool IsWeekly
    {
        get => RepeatType == ScheduleRepeatType.Weekly;
        set { if (value) RepeatType = ScheduleRepeatType.Weekly; }
    }

    public bool IsMonthly
    {
        get => RepeatType == ScheduleRepeatType.Monthly;
        set { if (value) RepeatType = ScheduleRepeatType.Monthly; }
    }

    public bool IsCron
    {
        get => RepeatType == ScheduleRepeatType.Cron;
        set { if (value) RepeatType = ScheduleRepeatType.Cron; }
    }

    public bool IsIntervalMode => RepeatType is
        ScheduleRepeatType.EverySeconds or
        ScheduleRepeatType.EveryMinutes or
        ScheduleRepeatType.EveryHours;

    public bool UsesExecutionTime => RepeatType is
        ScheduleRepeatType.Daily or
        ScheduleRepeatType.Weekly or
        ScheduleRepeatType.Monthly;

    public string IntervalUnitText => RepeatType switch
    {
        ScheduleRepeatType.EverySeconds => _localization.Get(ScheduleTextKeys.SecondsUnit),
        ScheduleRepeatType.EveryMinutes => _localization.Get(ScheduleTextKeys.MinutesUnit),
        ScheduleRepeatType.EveryHours => _localization.Get(ScheduleTextKeys.HoursUnit),
        _ => string.Empty
    };

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);
    public bool HasFeedback => !string.IsNullOrWhiteSpace(FeedbackText);

    public void SetModeVisibility(
        bool showEverySeconds,
        bool showEveryMinutes,
        bool showEveryHours,
        bool showDaily,
        bool showWeekly,
        bool showMonthly,
        bool showCron)
    {
        ShowEverySeconds = showEverySeconds;
        ShowEveryMinutes = showEveryMinutes;
        ShowEveryHours = showEveryHours;
        ShowDaily = showDaily;
        ShowWeekly = showWeekly;
        ShowMonthly = showMonthly;
        ShowCron = showCron;
        EnsureSelectedModeVisible();
    }

    public void SetCulture(string culture) => _localization.SetCulture(culture);

    public void AddOrUpdateLanguagePackFromJson(string json, bool setCurrent = false) =>
        _localization.AddOrUpdateLanguagePackFromJson(json, setCurrent);

    public void AddOrUpdateLanguageOverridesFromJson(
        string culture,
        string json,
        string? displayName = null,
        string? fallbackCulture = null,
        bool setCurrent = false) =>
        _localization.AddOrUpdateLanguageOverridesFromJson(
            culture, json, displayName, fallbackCulture, setCurrent);

    public Task AddOrUpdateLanguagePackFromFileAsync(
        string filePath,
        bool setCurrent = false,
        CancellationToken cancellationToken = default) =>
        _localization.AddOrUpdateLanguagePackFromFileAsync(
            filePath, setCurrent, cancellationToken);

    public void Load(ScheduleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var normalized = options.Normalize();
        _savedOptions = normalized.DeepClone();

        IsEnabled = normalized.IsEnabled;
        RepeatType = normalized.RepeatType;
        IntervalValue = normalized.Interval;
        ExecuteTime = normalized.ExecutionTime;
        DayOfMonth = normalized.DayOfMonth;
        CronExpression = normalized.CronExpression;

        foreach (var item in WeekDays)
            item.IsSelected = normalized.WeekDays.Contains(item.Day);

        EnsureSelectedModeVisible();
        ErrorText = null;
        FeedbackText = null;
        RefreshPreview();
    }

    public ScheduleOptions GetCurrentOptions()
    {
        return new ScheduleOptions
        {
            IsEnabled = IsEnabled,
            RepeatType = RepeatType,
            Interval = DecimalToInt(IntervalValue, 1),
            ExecutionTime = ExecuteTime ?? TimeSpan.Zero,
            WeekDays = WeekDays.Where(item => item.IsSelected).Select(item => item.Day).ToList(),
            DayOfMonth = DecimalToInt(DayOfMonth, 1),
            CronExpression = CronExpression ?? string.Empty
        }.Normalize();
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task SaveAsync()
    {
        ErrorText = null;
        FeedbackText = null;

        if (!TryBuildOptions(out var options))
            return;

        IsBusy = true;
        try
        {
            if (SaveHandler is not null)
                await SaveHandler(options);

            _savedOptions = options.DeepClone();
            FeedbackText = _localization.Get(options.IsEnabled
                ? ScheduleTextKeys.FeedbackSavedEnabled
                : ScheduleTextKeys.FeedbackSavedDisabled);
            RefreshPreview();
        }
        catch (Exception exception)
        {
            ErrorText = _localization.Format(ScheduleTextKeys.ErrorSaveFailed, exception.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task CancelAsync()
    {
        Load(_savedOptions);
        if (CancelHandler is not null)
            await CancelHandler();
    }

    private bool CanInteract() => !IsBusy;

    private bool TryBuildOptions(out ScheduleOptions options)
    {
        options = GetCurrentOptions();

        if (!HasAnyVisibleMode())
        {
            ErrorText = _localization.Get(ScheduleTextKeys.ErrorNoVisibleModes);
            return false;
        }

        if (!IsEnabled)
            return true;

        if (IsIntervalMode && (IntervalValue is null or < 1 || IntervalValue > int.MaxValue))
        {
            ErrorText = _localization.Get(ScheduleTextKeys.ErrorChooseInterval);
            return false;
        }

        if (UsesExecutionTime && ExecuteTime is null)
        {
            ErrorText = _localization.Get(ScheduleTextKeys.ErrorChooseTime);
            return false;
        }

        if (RepeatType == ScheduleRepeatType.Weekly && options.WeekDays.Count == 0)
        {
            ErrorText = _localization.Get(ScheduleTextKeys.ErrorChooseWeekDay);
            return false;
        }

        if (RepeatType == ScheduleRepeatType.Monthly &&
            (DayOfMonth is null or < 1 or > 31))
        {
            ErrorText = _localization.Get(ScheduleTextKeys.ErrorChooseMonthDay);
            return false;
        }

        if (RepeatType == ScheduleRepeatType.Cron)
        {
            if (string.IsNullOrWhiteSpace(CronExpression))
            {
                ErrorText = _localization.Get(ScheduleTextKeys.ErrorChooseCron);
                return false;
            }

            try
            {
                ScheduleOptionsValidator.ValidateCronExpression(CronExpression);
            }
            catch (Exception exception)
            {
                ErrorText = _localization.Format(ScheduleTextKeys.ErrorInvalidCron, exception.Message);
                return false;
            }
        }

        return true;
    }

    private void EnsureSelectedModeVisible()
    {
        if (IsModeVisible(RepeatType))
            return;

        var next = new[]
        {
            ScheduleRepeatType.Daily,
            ScheduleRepeatType.Weekly,
            ScheduleRepeatType.Monthly,
            ScheduleRepeatType.EverySeconds,
            ScheduleRepeatType.EveryMinutes,
            ScheduleRepeatType.EveryHours,
            ScheduleRepeatType.Cron
        }.FirstOrDefault(IsModeVisible);

        if (HasAnyVisibleMode())
            RepeatType = next;
    }

    private bool HasAnyVisibleMode() =>
        ShowEverySeconds || ShowEveryMinutes || ShowEveryHours ||
        ShowDaily || ShowWeekly || ShowMonthly || ShowCron;

    private bool IsModeVisible(ScheduleRepeatType type) => type switch
    {
        ScheduleRepeatType.EverySeconds => ShowEverySeconds,
        ScheduleRepeatType.EveryMinutes => ShowEveryMinutes,
        ScheduleRepeatType.EveryHours => ShowEveryHours,
        ScheduleRepeatType.Daily => ShowDaily,
        ScheduleRepeatType.Weekly => ShowWeekly,
        ScheduleRepeatType.Monthly => ShowMonthly,
        ScheduleRepeatType.Cron => ShowCron,
        _ => false
    };

    private static int DecimalToInt(decimal? value, int fallback)
    {
        if (value is null)
            return fallback;

        var truncated = decimal.Truncate(value.Value);
        if (truncated > int.MaxValue)
            return int.MaxValue;
        if (truncated < int.MinValue)
            return int.MinValue;

        return decimal.ToInt32(truncated);
    }

    private WeekDayItemViewModel CreateWeekDay(DayOfWeek day, bool isSelected) =>
        new(day, GetWeekDayTextKey(day, true), GetWeekDayTextKey(day, false), isSelected);

    private string GetWeekDayTextKey(DayOfWeek day, bool shortName) =>
        _localization.Get(GetWeekDayKey(day, shortName));

    private void OnWeekDayPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName != nameof(WeekDayItemViewModel.IsSelected))
            return;

        ClearMessagesAndRefresh();
    }

    private void OnLanguageChanged(object? sender, EventArgs eventArgs)
    {
        if (_disposed)
            return;

        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplyLocalizedText();
            RefreshPreview();
            return;
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed)
                return;
            ApplyLocalizedText();
            RefreshPreview();
        });
    }

    private void ApplyLocalizedText()
    {
        TitleText = _localization.Get(ScheduleTextKeys.Title);
        DescriptionText = _localization.Get(ScheduleTextKeys.Description);
        EnabledText = _localization.Get(ScheduleTextKeys.Enabled);
        DisabledText = _localization.Get(ScheduleTextKeys.Disabled);
        RepeatModeText = _localization.Get(ScheduleTextKeys.RepeatMode);
        EverySecondsText = _localization.Get(ScheduleTextKeys.EverySeconds);
        EveryMinutesText = _localization.Get(ScheduleTextKeys.EveryMinutes);
        EveryHoursText = _localization.Get(ScheduleTextKeys.EveryHours);
        DailyText = _localization.Get(ScheduleTextKeys.Daily);
        WeeklyText = _localization.Get(ScheduleTextKeys.Weekly);
        MonthlyText = _localization.Get(ScheduleTextKeys.Monthly);
        CronText = _localization.Get(ScheduleTextKeys.Cron);
        IntervalText = _localization.Get(ScheduleTextKeys.Interval);
        ExecutionTimeText = _localization.Get(ScheduleTextKeys.ExecutionTime);
        WeeklyDaysText = _localization.Get(ScheduleTextKeys.WeeklyDays);
        DayOfMonthText = _localization.Get(ScheduleTextKeys.DayOfMonth);
        MonthlyDayHelpText = _localization.Get(ScheduleTextKeys.MonthlyDayHelp);
        CronExpressionText = _localization.Get(ScheduleTextKeys.CronExpression);
        CronHelpText = _localization.Get(ScheduleTextKeys.CronHelp);
        NextRunLabelText = _localization.Get(ScheduleTextKeys.NextRun);
        CancelText = _localization.Get(ScheduleTextKeys.Cancel);
        SaveText = _localization.Get(ScheduleTextKeys.Save);
        OnPropertyChanged(nameof(IntervalUnitText));

        foreach (var item in WeekDays)
        {
            item.DisplayName = GetWeekDayTextKey(item.Day, true);
            item.FullDisplayName = GetWeekDayTextKey(item.Day, false);
        }

        ErrorText = null;
        FeedbackText = null;
    }

    private void RefreshPreview()
    {
        var options = GetCurrentOptions();
        SummaryText = BuildSummary(options);

        try
        {
            var nextRun = ScheduleCalculator.GetNextRun(options, DateTimeOffset.Now);
            NextRunText = nextRun?.ToString(
                              _localization.Get(ScheduleTextKeys.DateTimeFormat),
                              _localization.CurrentCultureInfo)
                          ?? _localization.Get(options.IsEnabled
                              ? ScheduleTextKeys.NextRunSelectValidDate
                              : ScheduleTextKeys.NextRunDisabled);
        }
        catch
        {
            NextRunText = _localization.Get(ScheduleTextKeys.NextRunSelectValidDate);
        }
    }

    private string BuildSummary(ScheduleOptions options)
    {
        if (!options.IsEnabled)
            return _localization.Get(ScheduleTextKeys.SummaryDisabled);

        var timeText = options.ExecutionTime.ToString(@"hh\:mm");

        return options.RepeatType switch
        {
            ScheduleRepeatType.EverySeconds => _localization.Format(
                ScheduleTextKeys.SummaryEverySeconds, options.Interval),
            ScheduleRepeatType.EveryMinutes => _localization.Format(
                ScheduleTextKeys.SummaryEveryMinutes, options.Interval),
            ScheduleRepeatType.EveryHours => _localization.Format(
                ScheduleTextKeys.SummaryEveryHours, options.Interval),
            ScheduleRepeatType.Daily => _localization.Format(
                ScheduleTextKeys.SummaryDaily, timeText),
            ScheduleRepeatType.Weekly => BuildWeeklySummary(options, timeText),
            ScheduleRepeatType.Monthly => _localization.Format(
                ScheduleTextKeys.SummaryMonthly, options.DayOfMonth, timeText),
            ScheduleRepeatType.Cron => _localization.Format(
                ScheduleTextKeys.SummaryCron, options.CronExpression),
            _ => string.Empty
        };
    }

    private string BuildWeeklySummary(ScheduleOptions options, string timeText)
    {
        if (options.WeekDays.Count == 0)
            return _localization.Get(ScheduleTextKeys.SummarySelectWeekDays);

        var dayText = string.Join(
            _localization.Get(ScheduleTextKeys.ListSeparator),
            options.WeekDays.Select(day => GetWeekDayTextKey(day, false)));

        return _localization.Format(ScheduleTextKeys.SummaryWeekly, dayText, timeText);
    }

    private static string GetWeekDayKey(DayOfWeek day, bool shortName) => (day, shortName) switch
    {
        (DayOfWeek.Monday, true) => ScheduleTextKeys.MondayShort,
        (DayOfWeek.Tuesday, true) => ScheduleTextKeys.TuesdayShort,
        (DayOfWeek.Wednesday, true) => ScheduleTextKeys.WednesdayShort,
        (DayOfWeek.Thursday, true) => ScheduleTextKeys.ThursdayShort,
        (DayOfWeek.Friday, true) => ScheduleTextKeys.FridayShort,
        (DayOfWeek.Saturday, true) => ScheduleTextKeys.SaturdayShort,
        (DayOfWeek.Sunday, true) => ScheduleTextKeys.SundayShort,
        (DayOfWeek.Monday, false) => ScheduleTextKeys.MondayLong,
        (DayOfWeek.Tuesday, false) => ScheduleTextKeys.TuesdayLong,
        (DayOfWeek.Wednesday, false) => ScheduleTextKeys.WednesdayLong,
        (DayOfWeek.Thursday, false) => ScheduleTextKeys.ThursdayLong,
        (DayOfWeek.Friday, false) => ScheduleTextKeys.FridayLong,
        (DayOfWeek.Saturday, false) => ScheduleTextKeys.SaturdayLong,
        (DayOfWeek.Sunday, false) => ScheduleTextKeys.SundayLong,
        _ => day.ToString()
    };

    private void ClearMessagesAndRefresh()
    {
        ErrorText = null;
        FeedbackText = null;
        RefreshPreview();
    }

    partial void OnIsEnabledChanged(bool value) => ClearMessagesAndRefresh();

    partial void OnRepeatTypeChanged(ScheduleRepeatType value)
    {
        OnPropertyChanged(nameof(IsEverySeconds));
        OnPropertyChanged(nameof(IsEveryMinutes));
        OnPropertyChanged(nameof(IsEveryHours));
        OnPropertyChanged(nameof(IsDaily));
        OnPropertyChanged(nameof(IsWeekly));
        OnPropertyChanged(nameof(IsMonthly));
        OnPropertyChanged(nameof(IsCron));
        OnPropertyChanged(nameof(IsIntervalMode));
        OnPropertyChanged(nameof(UsesExecutionTime));
        OnPropertyChanged(nameof(IntervalUnitText));
        ClearMessagesAndRefresh();
    }

    partial void OnIntervalValueChanged(decimal? value) => ClearMessagesAndRefresh();
    partial void OnExecuteTimeChanged(TimeSpan? value) => ClearMessagesAndRefresh();
    partial void OnDayOfMonthChanged(decimal? value) => ClearMessagesAndRefresh();
    partial void OnCronExpressionChanged(string value) => ClearMessagesAndRefresh();

    partial void OnShowEverySecondsChanged(bool value) => EnsureSelectedModeVisible();
    partial void OnShowEveryMinutesChanged(bool value) => EnsureSelectedModeVisible();
    partial void OnShowEveryHoursChanged(bool value) => EnsureSelectedModeVisible();
    partial void OnShowDailyChanged(bool value) => EnsureSelectedModeVisible();
    partial void OnShowWeeklyChanged(bool value) => EnsureSelectedModeVisible();
    partial void OnShowMonthlyChanged(bool value) => EnsureSelectedModeVisible();
    partial void OnShowCronChanged(bool value) => EnsureSelectedModeVisible();

    partial void OnErrorTextChanged(string? value) => OnPropertyChanged(nameof(HasError));
    partial void OnFeedbackTextChanged(string? value) => OnPropertyChanged(nameof(HasFeedback));

    partial void OnIsBusyChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _localization.LanguageChanged -= OnLanguageChanged;
        foreach (var item in WeekDays)
            item.PropertyChanged -= OnWeekDayPropertyChanged;
        GC.SuppressFinalize(this);
    }
}
