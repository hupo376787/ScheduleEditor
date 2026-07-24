using System.Collections.ObjectModel;
using Avalonia.Threading;
using ScheduleEditor.Localization;
using ScheduleEditor.Models;
using ScheduleEditor.Services;
using ScheduleEditor.ViewModels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ScheduleEditor.Demo.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly JsonScheduleStore _store;
    private readonly ScheduleManager _scheduleManager;
    private readonly ScheduleLocalizationService _localization;
    private readonly DispatcherTimer _displayTimer;
    private bool _initialized;
    private bool _changingLanguage;
    private bool _disposed;

    public MainWindowViewModel()
    {
        var settingsDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "ScheduleEditorDemo");

        _store = new JsonScheduleStore(
            Path.Combine(settingsDirectory, "schedule.json"));

        _scheduleManager = new ScheduleManager(
            _store,
            DemoJobAsync);

        _localization = new ScheduleLocalizationService("zh-CN");
        _localization.LanguageChanged += OnLanguageChanged;
        _localization.LanguagesChanged += OnLanguagesChanged;

        Editor = new ScheduleEditorViewModel(_localization)
        {
            SaveHandler = SaveScheduleAsync,
            CancelHandler = OnCancelAsync
        };

        RefreshLanguageList();

        _scheduleManager.ScheduleChanged += OnSchedulerChanged;
        _scheduleManager.ExecutionStarted += OnExecutionStarted;
        _scheduleManager.ExecutionCompleted += OnExecutionCompleted;
        _scheduleManager.ExecutionFailed += OnExecutionFailed;
        _scheduleManager.ExecutionSkipped += OnExecutionSkipped;

        _displayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };

        _displayTimer.Tick += (_, _) => RefreshRuntimeStatus();
        _displayTimer.Start();

        AddLog("Demo 已启动，正在等待加载定时配置。");
        RefreshRuntimeStatus();
    }

    public ScheduleEditorViewModel Editor { get; }

    public ObservableCollection<string> Logs { get; } = [];

    public ObservableCollection<ScheduleLanguageInfo> Languages { get; } = [];

    public string SettingsFilePath => _store.FilePath;

    [ObservableProperty]
    private string _currentTimeText = string.Empty;

    [ObservableProperty]
    private string _schedulerStatusText = "未启用";

    [ObservableProperty]
    private string _runtimeNextRunText = "—";

    [ObservableProperty]
    private string _lastRunText = "尚未执行";

    [ObservableProperty]
    private ScheduleLanguageInfo? _selectedLanguage;

    /// <summary>
    /// 程序启动时读取 JSON，并自动恢复 FluentScheduler。
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _initialized = true;
        var settingsExisted = File.Exists(_store.FilePath);

        try
        {
            var options = await _scheduleManager.InitializeAsync(
                new ScheduleOptions
                {
                    IsEnabled = false,
                    RepeatType = ScheduleRepeatType.Daily,
                    ExecutionTime = new TimeSpan(9, 30, 0)
                });

            Editor.Load(options);

            AddLog(settingsExisted
                ? "已从 JSON 加载设置，并自动恢复调度。"
                : "未找到历史设置，已载入默认停用配置。");
        }
        catch (Exception exception)
        {
            AddLog($"加载设置失败：{exception.Message}");
        }

        RefreshRuntimeStatus();
    }

    [RelayCommand]
    private void SetNextMinute()
    {
        var next = DateTime.Now.AddMinutes(1);
        Editor.IsEnabled = true;
        Editor.RepeatType = ScheduleRepeatType.Daily;
        Editor.ExecuteTime = new TimeSpan(next.Hour, next.Minute, 0);

        AddLog($"编辑时间已设为下一分钟 {next:HH:mm}，点击“保存”后生效。");
    }

    [RelayCommand]
    private async Task RunNowAsync()
    {
        AddLog("手动执行测试任务。");
        await DemoJobAsync(CancellationToken.None);
        LastRunText = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
        AddLog("手动测试任务完成。");
    }

    [RelayCommand]
    private async Task LoadSampleLanguageAsync()
    {
        var languageFile = Path.Combine(
            AppContext.BaseDirectory,
            "Languages",
            "ja-JP.json");

        try
        {
            await _localization.AddOrUpdateLanguagePackFromFileAsync(
                languageFile,
                setCurrent: true);

            RefreshLanguageList();
            AddLog($"已加载外部 JSON 语言包：{languageFile}");
        }
        catch (Exception exception)
        {
            AddLog($"加载外部语言包失败：{exception.Message}");
        }
    }

    [RelayCommand]
    private void ClearLogs()
    {
        Logs.Clear();
        AddLog("日志已清空。");
    }

    /// <summary>
    /// 用户点击组件中的“保存”后，由 ScheduleManager 统一保存 JSON 并应用调度。
    /// </summary>
    private async Task SaveScheduleAsync(ScheduleOptions options)
    {
        await _scheduleManager.SaveAndApplyAsync(options);

        AddLog(options.IsEnabled
            ? $"设置已保存并应用：{BuildDescription(options)}"
            : "设置已保存，定时任务已停用。");

        RefreshRuntimeStatus();
    }

    private Task OnCancelAsync()
    {
        AddLog("已撤销未保存的界面修改。");
        return Task.CompletedTask;
    }

    private static async Task DemoJobAsync(CancellationToken cancellationToken)
    {
        // 程序 A 只需要把这里替换为真实业务：导出、上传、采集、清理等。
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
    }

    private void OnSchedulerChanged(object? sender, EventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(RefreshRuntimeStatus);
    }

    private void OnLanguageChanged(object? sender, EventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(RefreshRuntimeStatus);
    }

    private void OnLanguagesChanged(object? sender, EventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(RefreshLanguageList);
    }

    private void OnExecutionStarted(
        object? sender,
        ScheduleExecutionEventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(() =>
        {
            AddLog($"定时任务开始：{eventArgs.StartedAt:yyyy-MM-dd HH:mm:ss}");
            LastRunText = "正在执行…";
        });
    }

    private void OnExecutionCompleted(
        object? sender,
        ScheduleExecutionEventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var duration = eventArgs.Duration?.TotalSeconds ?? 0;
            LastRunText = $"{eventArgs.EndedAt:yyyy-MM-dd HH:mm:ss}";
            AddLog($"定时任务完成，耗时 {duration:F1} 秒。");
            RefreshRuntimeStatus();
        });
    }

    private void OnExecutionFailed(
        object? sender,
        ScheduleExecutionEventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LastRunText = "执行失败";
            AddLog($"定时任务失败：{eventArgs.Exception?.Message}");
            RefreshRuntimeStatus();
        });
    }

    private void OnExecutionSkipped(
        object? sender,
        ScheduleExecutionEventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(() =>
            AddLog("上一次任务仍在执行，本次触发已跳过。"));
    }

    private void RefreshLanguageList()
    {
        _changingLanguage = true;

        try
        {
            Languages.Clear();

            foreach (var language in _localization.AvailableLanguages)
                Languages.Add(language);

            SelectedLanguage = Languages.FirstOrDefault(language =>
                language.Culture.Equals(
                    _localization.CurrentCulture,
                    StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _changingLanguage = false;
        }
    }

    partial void OnSelectedLanguageChanged(ScheduleLanguageInfo? value)
    {
        if (_changingLanguage || value is null)
            return;

        try
        {
            _localization.SetCulture(value.Culture);
            AddLog($"组件语言已切换为：{value.DisplayName}");
        }
        catch (Exception exception)
        {
            AddLog($"切换语言失败：{exception.Message}");
        }
    }

    private void RefreshRuntimeStatus()
    {
        CurrentTimeText = DateTimeOffset.Now.ToString("yyyy-MM-dd ddd HH:mm:ss");

        var options = _scheduleManager.CurrentOptions;

        if (options is null || !options.IsEnabled)
        {
            SchedulerStatusText = "未启用";
            RuntimeNextRunText = "—";
            return;
        }

        SchedulerStatusText = _scheduleManager.IsRunning
            ? "运行中"
            : "已配置，调度器未运行";

        RuntimeNextRunText = _scheduleManager.NextRun?.ToString(
            "yyyy-MM-dd ddd HH:mm:ss") ?? "—";
    }

    private void AddLog(string message)
    {
        Logs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");

        while (Logs.Count > 200)
            Logs.RemoveAt(Logs.Count - 1);
    }

    private static string BuildDescription(ScheduleOptions options)
    {
        var normalized = options.Normalize();
        var time = normalized.ExecutionTime.ToString(@"hh\:mm");

        return normalized.RepeatType switch
        {
            ScheduleRepeatType.EverySeconds => $"每 {normalized.Interval} 秒",
            ScheduleRepeatType.EveryMinutes => $"每 {normalized.Interval} 分钟",
            ScheduleRepeatType.EveryHours => $"每 {normalized.Interval} 小时",
            ScheduleRepeatType.Daily => $"每天 {time}",
            ScheduleRepeatType.Weekly => $"每周{BuildWeekDays(normalized.WeekDays)} {time}",
            ScheduleRepeatType.Monthly => $"每月 {normalized.DayOfMonth} 日 {time}",
            ScheduleRepeatType.Cron => $"Cron：{normalized.CronExpression}",
            _ => normalized.RepeatType.ToString()
        };
    }

    private static string BuildWeekDays(IEnumerable<DayOfWeek> weekDays) =>
        string.Join(
            "、",
            weekDays.Select(day => day switch
            {
                DayOfWeek.Monday => "周一",
                DayOfWeek.Tuesday => "周二",
                DayOfWeek.Wednesday => "周三",
                DayOfWeek.Thursday => "周四",
                DayOfWeek.Friday => "周五",
                DayOfWeek.Saturday => "周六",
                DayOfWeek.Sunday => "周日",
                _ => day.ToString()
            }));

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _displayTimer.Stop();

        _localization.LanguageChanged -= OnLanguageChanged;
        _localization.LanguagesChanged -= OnLanguagesChanged;
        _scheduleManager.ScheduleChanged -= OnSchedulerChanged;
        _scheduleManager.ExecutionStarted -= OnExecutionStarted;
        _scheduleManager.ExecutionCompleted -= OnExecutionCompleted;
        _scheduleManager.ExecutionFailed -= OnExecutionFailed;
        _scheduleManager.ExecutionSkipped -= OnExecutionSkipped;

        Editor.Dispose();
        _scheduleManager.Dispose();

        GC.SuppressFinalize(this);
    }
}
