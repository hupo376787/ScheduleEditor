using AvaloniaScheduleEditor.Models;
using FluentScheduler;

namespace AvaloniaScheduleEditor.Services;

/// <summary>
/// FluentScheduler 的运行时封装。支持间隔、每天、每周、每月、Cron 和防止任务重入。
/// </summary>
public sealed class FluentScheduleService : IFluentScheduleService
{
    private readonly SemaphoreSlim _executionGate = new(1, 1);

    private Schedule? _schedule;
    private bool _disposed;

    public ScheduleOptions? CurrentOptions { get; private set; }

    public bool IsRunning => _schedule?.Running == true;

    public DateTimeOffset? NextRun
    {
        get
        {
            if (!IsRunning || CurrentOptions is not { IsEnabled: true } options)
                return null;

            // 每周和每月模式内部采用“每天到点唤醒后再判断”的方式，
            // 因此必须按真实业务规则计算，不能直接返回 FluentScheduler 的日常唤醒时间。
            if (options.RepeatType is ScheduleRepeatType.Weekly or ScheduleRepeatType.Monthly)
            {
                return ScheduleCalculator.GetNextRun(
                    options,
                    DateTimeOffset.Now);
            }

            return _schedule?.NextRun is { } nextRun
                ? ToDateTimeOffset(nextRun)
                : ScheduleCalculator.GetNextRun(options, DateTimeOffset.Now);
        }
    }

    public event EventHandler? ScheduleChanged;

    public event EventHandler<ScheduleExecutionEventArgs>? ExecutionStarted;

    public event EventHandler<ScheduleExecutionEventArgs>? ExecutionCompleted;

    public event EventHandler<ScheduleExecutionEventArgs>? ExecutionFailed;

    public event EventHandler<ScheduleExecutionEventArgs>? ExecutionSkipped;

    public void Apply(
        ScheduleOptions options,
        Func<CancellationToken, Task> job)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(job);

        var normalized = options.Normalize();
        ScheduleOptionsValidator.Validate(normalized);

        Stop();

        CurrentOptions = normalized.DeepClone();

        if (!normalized.IsEnabled)
        {
            ScheduleChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        var appliedOptions = normalized.DeepClone();
        var hour = appliedOptions.ExecutionTime.Hours;
        var minute = appliedOptions.ExecutionTime.Minutes;

        _schedule = appliedOptions.RepeatType switch
        {
            ScheduleRepeatType.EverySeconds => new Schedule(
                cancellationToken => ExecuteJobAsync(job, cancellationToken),
                run => run.Every(appliedOptions.Interval).Seconds()),

            ScheduleRepeatType.EveryMinutes => new Schedule(
                cancellationToken => ExecuteJobAsync(job, cancellationToken),
                run => run.Every(appliedOptions.Interval).Minutes()),

            ScheduleRepeatType.EveryHours => new Schedule(
                cancellationToken => ExecuteJobAsync(job, cancellationToken),
                run => run.Every(appliedOptions.Interval).Hours()),

            ScheduleRepeatType.Daily => new Schedule(
                cancellationToken => ExecuteJobAsync(job, cancellationToken),
                run => run.Every(1).Days().At(hour, minute)),

            ScheduleRepeatType.Weekly or ScheduleRepeatType.Monthly => new Schedule(
                cancellationToken => ExecuteIfCalendarMatchesAsync(
                    appliedOptions,
                    job,
                    cancellationToken),
                run => run.Every(1).Days().At(hour, minute)),

            ScheduleRepeatType.Cron => new Schedule(
                cancellationToken => ExecuteJobAsync(job, cancellationToken),
                appliedOptions.CronExpression),

            _ => throw new ArgumentOutOfRangeException(
                nameof(options),
                appliedOptions.RepeatType,
                "不支持的重复方式。")
        };

        _schedule.Start();
        ScheduleChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        if (_schedule is not null)
        {
            _schedule.Stop();
            _schedule = null;
        }

        ScheduleChanged?.Invoke(this, EventArgs.Empty);
    }

    private Task ExecuteIfCalendarMatchesAsync(
        ScheduleOptions options,
        Func<CancellationToken, Task> job,
        CancellationToken cancellationToken)
    {
        var now = DateTime.Now;

        if (options.RepeatType == ScheduleRepeatType.Weekly &&
            !options.WeekDays.Contains(now.DayOfWeek))
        {
            return Task.CompletedTask;
        }

        if (options.RepeatType == ScheduleRepeatType.Monthly &&
            now.Day != options.DayOfMonth)
        {
            return Task.CompletedTask;
        }

        return ExecuteJobAsync(job, cancellationToken);
    }

    private async Task ExecuteJobAsync(
        Func<CancellationToken, Task> job,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.Now;

        if (!await _executionGate.WaitAsync(0, cancellationToken))
        {
            ExecutionSkipped?.Invoke(
                this,
                new ScheduleExecutionEventArgs(
                    startedAt,
                    endedAt: DateTimeOffset.Now,
                    skippedBecauseAlreadyRunning: true));

            return;
        }

        try
        {
            ExecutionStarted?.Invoke(
                this,
                new ScheduleExecutionEventArgs(startedAt));

            await job(cancellationToken);

            ExecutionCompleted?.Invoke(
                this,
                new ScheduleExecutionEventArgs(
                    startedAt,
                    DateTimeOffset.Now));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 调度被停止时属于正常取消，不作为任务异常抛出。
        }
        catch (Exception exception)
        {
            ExecutionFailed?.Invoke(
                this,
                new ScheduleExecutionEventArgs(
                    startedAt,
                    DateTimeOffset.Now,
                    exception));
        }
        finally
        {
            _executionGate.Release();
        }
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime dateTime)
    {
        return dateTime.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(dateTime),
            DateTimeKind.Local => new DateTimeOffset(dateTime),
            _ => new DateTimeOffset(
                dateTime,
                TimeZoneInfo.Local.GetUtcOffset(dateTime))
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_schedule is not null)
        {
            try
            {
                _schedule.StopAndBlock(TimeSpan.FromSeconds(5));
            }
            catch
            {
                _schedule.Stop();
            }

            _schedule = null;
        }

        // StopAndBlock 带超时；若业务代码忽略取消令牌，任务可能仍在退出。
        // 不主动 Dispose 信号量，避免尚未结束的 finally 调用 Release 时抛异常。
        GC.SuppressFinalize(this);
    }
}
