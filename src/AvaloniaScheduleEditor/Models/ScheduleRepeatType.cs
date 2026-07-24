namespace AvaloniaScheduleEditor.Models;

/// <summary>
/// 组件支持的重复规则。Cron 用于承载图形界面未覆盖的高级规则。
/// </summary>
public enum ScheduleRepeatType
{
    EverySeconds,
    EveryMinutes,
    EveryHours,
    Daily,
    Weekly,
    Monthly,
    Cron
}
