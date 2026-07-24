using ScheduleEditor.Models;

namespace ScheduleEditor.Services;

/// <summary>
/// 统一处理配置读取、保存以及 FluentScheduler 的应用和恢复。
/// </summary>
public sealed class ScheduleManager : IDisposable
{
    private readonly IScheduleStore _store;
    private readonly IFluentScheduleService _scheduler;
    private readonly Func<CancellationToken, Task> _job;
    private readonly bool _ownsScheduler;
    private readonly SemaphoreSlim _updateGate = new(1, 1);
    private bool _disposed;

    public ScheduleManager(
        IScheduleStore store,
        Func<CancellationToken, Task> job)
        : this(
            store,
            new FluentScheduleService(),
            job,
            ownsScheduler: true)
    {
    }

    public ScheduleManager(
        IScheduleStore store,
        IFluentScheduleService scheduler,
        Func<CancellationToken, Task> job,
        bool ownsScheduler = false)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(job);

        _store = store;
        _scheduler = scheduler;
        _job = job;
        _ownsScheduler = ownsScheduler;
    }

    public IScheduleStore Store => _store;

    public ScheduleOptions? CurrentOptions =>
        _scheduler.CurrentOptions?.DeepClone();

    public bool IsRunning => _scheduler.IsRunning;

    public DateTimeOffset? NextRun => _scheduler.NextRun;

    public event EventHandler? ScheduleChanged
    {
        add => _scheduler.ScheduleChanged += value;
        remove => _scheduler.ScheduleChanged -= value;
    }

    public event EventHandler<ScheduleExecutionEventArgs>? ExecutionStarted
    {
        add => _scheduler.ExecutionStarted += value;
        remove => _scheduler.ExecutionStarted -= value;
    }

    public event EventHandler<ScheduleExecutionEventArgs>? ExecutionCompleted
    {
        add => _scheduler.ExecutionCompleted += value;
        remove => _scheduler.ExecutionCompleted -= value;
    }

    public event EventHandler<ScheduleExecutionEventArgs>? ExecutionFailed
    {
        add => _scheduler.ExecutionFailed += value;
        remove => _scheduler.ExecutionFailed -= value;
    }

    public event EventHandler<ScheduleExecutionEventArgs>? ExecutionSkipped
    {
        add => _scheduler.ExecutionSkipped += value;
        remove => _scheduler.ExecutionSkipped -= value;
    }

    /// <summary>
    /// 从存储中读取配置并立即恢复调度。
    /// 找不到配置文件时使用 defaultOptions；仍未提供时使用停用状态的默认配置。
    /// </summary>
    public async Task<ScheduleOptions> InitializeAsync(
        ScheduleOptions? defaultOptions = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _updateGate.WaitAsync(cancellationToken);

        try
        {
            var options = await _store.LoadAsync(cancellationToken)
                          ?? defaultOptions
                          ?? new ScheduleOptions { IsEnabled = false };

            var normalized = options.Normalize();
            ScheduleOptionsValidator.Validate(normalized);

            // Apply 对停用配置同样有效：会停止旧任务并保留当前配置。
            _scheduler.Apply(normalized, _job);

            return normalized.DeepClone();
        }
        finally
        {
            _updateGate.Release();
        }
    }

    /// <summary>
    /// 验证并保存配置，保存成功后立即重新应用调度。
    /// </summary>
    public async Task SaveAndApplyAsync(
        ScheduleOptions options,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(options);

        var normalized = options.Normalize();
        ScheduleOptionsValidator.Validate(normalized);

        await _updateGate.WaitAsync(cancellationToken);

        try
        {
            await _store.SaveAsync(normalized, cancellationToken);
            _scheduler.Apply(normalized, _job);
        }
        finally
        {
            _updateGate.Release();
        }
    }

    /// <summary>
    /// 只停止当前进程中的调度，不修改已保存的配置。
    /// </summary>
    public void Stop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _scheduler.Stop();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_ownsScheduler)
            _scheduler.Dispose();

        // 若调用方在持久化过程中关闭程序，异步方法的 finally 仍需 Release。
        // 因此这里不主动 Dispose _updateGate。
        GC.SuppressFinalize(this);
    }
}
