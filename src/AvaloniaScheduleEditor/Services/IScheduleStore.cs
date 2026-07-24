using AvaloniaScheduleEditor.Models;

namespace AvaloniaScheduleEditor.Services;

/// <summary>
/// 定时配置的持久化抽象。宿主可以使用组件自带的 JSON 实现，
/// 也可以替换为 SQLite、远程配置或宿主自己的设置系统。
/// </summary>
public interface IScheduleStore
{
    Task<ScheduleOptions?> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        ScheduleOptions options,
        CancellationToken cancellationToken = default);
}
