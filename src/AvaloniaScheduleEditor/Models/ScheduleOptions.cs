namespace AvaloniaScheduleEditor.Models;

/// <summary>
/// 面向业务和持久化的结构化定时配置。普通用户不需要接触 Cron 表达式。
/// </summary>
public sealed record ScheduleOptions
{
    public bool IsEnabled { get; init; } = true;

    public ScheduleRepeatType RepeatType { get; init; } = ScheduleRepeatType.Daily;

    /// <summary>
    /// 每 N 秒、每 N 分钟、每 N 小时模式使用的间隔值。
    /// </summary>
    public int Interval { get; init; } = 1;

    /// <summary>
    /// 每天、每周、每月模式使用的本地执行时刻。
    /// </summary>
    public TimeSpan ExecutionTime { get; init; } = new(9, 30, 0);

    /// <summary>
    /// 每周模式下需要执行的星期。其他模式会忽略此属性。
    /// </summary>
    public List<DayOfWeek> WeekDays { get; init; } =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday
    ];

    /// <summary>
    /// 每月模式下的日期，范围 1～31。不存在该日期的月份会跳过。
    /// </summary>
    public int DayOfMonth { get; init; } = 1;

    /// <summary>
    /// 自定义 Cron 表达式。支持 FluentScheduler/NCronTab 的 5 段或 6 段格式。
    /// </summary>
    public string CronExpression { get; init; } = "0 9 * * *";

    public ScheduleOptions Normalize()
    {
        var normalizedTime = new TimeSpan(
            ExecutionTime.Hours,
            ExecutionTime.Minutes,
            0);

        var normalizedDays = WeekDays
            .Distinct()
            .OrderBy(GetMondayFirstIndex)
            .ToList();

        return this with
        {
            ExecutionTime = normalizedTime,
            WeekDays = normalizedDays,
            CronExpression = CronExpression?.Trim() ?? string.Empty
        };
    }

    public ScheduleOptions DeepClone()
    {
        var normalized = Normalize();
        return normalized with { WeekDays = [.. normalized.WeekDays] };
    }

    private static int GetMondayFirstIndex(DayOfWeek day) =>
        day == DayOfWeek.Sunday ? 6 : (int)day - 1;
}
