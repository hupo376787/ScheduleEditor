using ScheduleEditor.Models;
using NCrontab;

namespace ScheduleEditor.Services;

public static class ScheduleCalculator
{
    public static DateTimeOffset? GetNextRun(
        ScheduleOptions options,
        DateTimeOffset now,
        TimeZoneInfo? timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        options = options.Normalize();

        if (!options.IsEnabled)
            return null;

        if ((options.RepeatType is
                 ScheduleRepeatType.EverySeconds or
                 ScheduleRepeatType.EveryMinutes or
                 ScheduleRepeatType.EveryHours) &&
            options.Interval < 1)
        {
            return null;
        }

        if (options.RepeatType == ScheduleRepeatType.Monthly &&
            options.DayOfMonth is < 1 or > 31)
        {
            return null;
        }

        timeZone ??= TimeZoneInfo.Local;

        return options.RepeatType switch
        {
            ScheduleRepeatType.EverySeconds =>
                now.AddSeconds(options.Interval),

            ScheduleRepeatType.EveryMinutes =>
                now.AddMinutes(options.Interval),

            ScheduleRepeatType.EveryHours =>
                now.AddHours(options.Interval),

            ScheduleRepeatType.Daily =>
                GetNextDailyRun(options, now, timeZone),

            ScheduleRepeatType.Weekly =>
                GetNextWeeklyRun(options, now, timeZone),

            ScheduleRepeatType.Monthly =>
                GetNextMonthlyRun(options, now, timeZone),

            ScheduleRepeatType.Cron =>
                GetNextCronRun(options, now, timeZone),

            _ => null
        };
    }

    public static IReadOnlyList<DateTimeOffset> GetNextRuns(
        ScheduleOptions options,
        DateTimeOffset now,
        int count,
        TimeZoneInfo? timeZone = null)
    {
        if (count <= 0)
            return [];

        var result = new List<DateTimeOffset>(count);
        var cursor = now;

        for (var index = 0; index < count; index++)
        {
            var next = GetNextRun(options, cursor, timeZone);
            if (next is null)
                break;

            result.Add(next.Value);
            cursor = next.Value;
        }

        return result;
    }

    private static DateTimeOffset? GetNextDailyRun(
        ScheduleOptions options,
        DateTimeOffset now,
        TimeZoneInfo timeZone)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);

        for (var dayOffset = 0; dayOffset <= 2; dayOffset++)
        {
            var localCandidate = DateTime.SpecifyKind(
                localNow.Date.AddDays(dayOffset).Add(options.ExecutionTime),
                DateTimeKind.Unspecified);

            var candidate = ResolveLocalTime(localCandidate, timeZone);
            if (candidate > now)
                return candidate;
        }

        return null;
    }

    private static DateTimeOffset? GetNextWeeklyRun(
        ScheduleOptions options,
        DateTimeOffset now,
        TimeZoneInfo timeZone)
    {
        if (options.WeekDays.Count == 0)
            return null;

        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);

        // 最多向后检查 8 天，覆盖“今天已过期”的情况。
        for (var dayOffset = 0; dayOffset <= 8; dayOffset++)
        {
            var date = localNow.Date.AddDays(dayOffset);

            if (!options.WeekDays.Contains(date.DayOfWeek))
                continue;

            var localCandidate = DateTime.SpecifyKind(
                date.Add(options.ExecutionTime),
                DateTimeKind.Unspecified);

            var candidate = ResolveLocalTime(localCandidate, timeZone);
            if (candidate > now)
                return candidate;
        }

        return null;
    }

    private static DateTimeOffset? GetNextMonthlyRun(
        ScheduleOptions options,
        DateTimeOffset now,
        TimeZoneInfo timeZone)
    {
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var firstMonth = new DateTime(localNow.Year, localNow.Month, 1);

        // 31 号在部分月份不存在，因此向后检查最多 10 年。
        for (var monthOffset = 0; monthOffset < 120; monthOffset++)
        {
            var month = firstMonth.AddMonths(monthOffset);
            var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);

            if (options.DayOfMonth > daysInMonth)
                continue;

            var localCandidate = DateTime.SpecifyKind(
                new DateTime(
                    month.Year,
                    month.Month,
                    options.DayOfMonth,
                    options.ExecutionTime.Hours,
                    options.ExecutionTime.Minutes,
                    options.ExecutionTime.Seconds),
                DateTimeKind.Unspecified);

            var candidate = ResolveLocalTime(localCandidate, timeZone);
            if (candidate > now)
                return candidate;
        }

        return null;
    }

    private static DateTimeOffset? GetNextCronRun(
        ScheduleOptions options,
        DateTimeOffset now,
        TimeZoneInfo timeZone)
    {
        if (string.IsNullOrWhiteSpace(options.CronExpression))
            return null;

        try
        {
            var fields = options.CronExpression.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

            var schedule = CrontabSchedule.Parse(
                options.CronExpression,
                new CrontabSchedule.ParseOptions
                {
                    IncludingSeconds = fields.Length == 6
                });

            var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
            var localNext = schedule.GetNextOccurrence(localNow.DateTime);

            return ResolveLocalTime(
                DateTime.SpecifyKind(localNext, DateTimeKind.Unspecified),
                timeZone);
        }
        catch
        {
            return null;
        }
    }

    private static DateTimeOffset ResolveLocalTime(
        DateTime localDateTime,
        TimeZoneInfo timeZone)
    {
        // 夏令时跳变可能导致某个本地时间不存在。向后推进到第一个有效分钟。
        while (timeZone.IsInvalidTime(localDateTime))
            localDateTime = localDateTime.AddMinutes(1);

        if (timeZone.IsAmbiguousTime(localDateTime))
        {
            // 时间回拨时选择较早发生的那个时刻。
            var offset = timeZone.GetAmbiguousTimeOffsets(localDateTime).Max();
            return new DateTimeOffset(localDateTime, offset);
        }

        return new DateTimeOffset(
            localDateTime,
            timeZone.GetUtcOffset(localDateTime));
    }
}
