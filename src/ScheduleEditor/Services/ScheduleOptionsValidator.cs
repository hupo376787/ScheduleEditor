using ScheduleEditor.Models;
using NCrontab;

namespace ScheduleEditor.Services;

public static class ScheduleOptionsValidator
{
    public static void Validate(ScheduleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!Enum.IsDefined(options.RepeatType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "不支持的重复方式。");
        }

        if ((options.RepeatType is
                 ScheduleRepeatType.EverySeconds or
                 ScheduleRepeatType.EveryMinutes or
                 ScheduleRepeatType.EveryHours) &&
            options.Interval < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "间隔值必须大于或等于 1。");
        }

        if (options.RepeatType == ScheduleRepeatType.Monthly &&
            options.DayOfMonth is < 1 or > 31)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "每月执行日期必须位于 1 到 31 之间。");
        }

        if ((options.RepeatType is
                 ScheduleRepeatType.Daily or
                 ScheduleRepeatType.Weekly or
                 ScheduleRepeatType.Monthly) &&
            (options.ExecutionTime < TimeSpan.Zero ||
             options.ExecutionTime >= TimeSpan.FromDays(1)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "执行时间必须位于 00:00:00 到 23:59:59。");
        }

        if (!options.IsEnabled)
            return;

        if (options.RepeatType == ScheduleRepeatType.Weekly &&
            options.WeekDays.Count == 0)
        {
            throw new ArgumentException(
                "每周模式至少选择一个星期。",
                nameof(options));
        }

        if (options.RepeatType == ScheduleRepeatType.Cron)
            ValidateCronExpression(options.CronExpression);
    }

    public static void ValidateCronExpression(string cronExpression)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);

        var fields = cronExpression.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);

        if (fields.Length is not (5 or 6))
        {
            throw new FormatException(
                "Cron 表达式必须包含 5 段或 6 段。" +
                "5 段：分 时 日 月 星期；6 段：秒 分 时 日 月 星期。");
        }

        CrontabSchedule.Parse(
            cronExpression,
            new CrontabSchedule.ParseOptions
            {
                IncludingSeconds = fields.Length == 6
            });
    }
}
