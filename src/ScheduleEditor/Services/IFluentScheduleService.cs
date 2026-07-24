using ScheduleEditor.Models;

namespace ScheduleEditor.Services;

public interface IFluentScheduleService : IDisposable
{
    ScheduleOptions? CurrentOptions { get; }

    bool IsRunning { get; }

    DateTimeOffset? NextRun { get; }

    event EventHandler? ScheduleChanged;

    event EventHandler<ScheduleExecutionEventArgs>? ExecutionStarted;

    event EventHandler<ScheduleExecutionEventArgs>? ExecutionCompleted;

    event EventHandler<ScheduleExecutionEventArgs>? ExecutionFailed;

    event EventHandler<ScheduleExecutionEventArgs>? ExecutionSkipped;

    void Apply(
        ScheduleOptions options,
        Func<CancellationToken, Task> job);

    void Stop();
}
