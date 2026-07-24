namespace AvaloniaScheduleEditor.Services;

public sealed class ScheduleExecutionEventArgs : EventArgs
{
    public ScheduleExecutionEventArgs(
        DateTimeOffset startedAt,
        DateTimeOffset? endedAt = null,
        Exception? exception = null,
        bool skippedBecauseAlreadyRunning = false)
    {
        StartedAt = startedAt;
        EndedAt = endedAt;
        Exception = exception;
        SkippedBecauseAlreadyRunning = skippedBecauseAlreadyRunning;
    }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? EndedAt { get; }

    public Exception? Exception { get; }

    public bool SkippedBecauseAlreadyRunning { get; }

    public TimeSpan? Duration =>
        EndedAt.HasValue ? EndedAt.Value - StartedAt : null;
}
