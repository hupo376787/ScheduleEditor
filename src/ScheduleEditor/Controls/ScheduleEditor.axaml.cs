using Avalonia;
using Avalonia.Controls;
using ScheduleEditor.ViewModels;

namespace ScheduleEditor.Controls;

public partial class ScheduleEditor : UserControl
{
    public static readonly StyledProperty<bool> ShowEverySecondsProperty =
        AvaloniaProperty.Register<ScheduleEditor, bool>(nameof(ShowEverySeconds), false);

    public static readonly StyledProperty<bool> ShowEveryMinutesProperty =
        AvaloniaProperty.Register<ScheduleEditor, bool>(nameof(ShowEveryMinutes), false);

    public static readonly StyledProperty<bool> ShowEveryHoursProperty =
        AvaloniaProperty.Register<ScheduleEditor, bool>(nameof(ShowEveryHours), false);

    public static readonly StyledProperty<bool> ShowDailyProperty =
        AvaloniaProperty.Register<ScheduleEditor, bool>(nameof(ShowDaily), true);

    public static readonly StyledProperty<bool> ShowWeeklyProperty =
        AvaloniaProperty.Register<ScheduleEditor, bool>(nameof(ShowWeekly), true);

    public static readonly StyledProperty<bool> ShowMonthlyProperty =
        AvaloniaProperty.Register<ScheduleEditor, bool>(nameof(ShowMonthly), true);

    public static readonly StyledProperty<bool> ShowCronProperty =
        AvaloniaProperty.Register<ScheduleEditor, bool>(nameof(ShowCron), false);

    public ScheduleEditor()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => ApplyModeVisibility();
    }

    public bool ShowEverySeconds
    {
        get => GetValue(ShowEverySecondsProperty);
        set => SetValue(ShowEverySecondsProperty, value);
    }

    public bool ShowEveryMinutes
    {
        get => GetValue(ShowEveryMinutesProperty);
        set => SetValue(ShowEveryMinutesProperty, value);
    }

    public bool ShowEveryHours
    {
        get => GetValue(ShowEveryHoursProperty);
        set => SetValue(ShowEveryHoursProperty, value);
    }

    public bool ShowDaily
    {
        get => GetValue(ShowDailyProperty);
        set => SetValue(ShowDailyProperty, value);
    }

    public bool ShowWeekly
    {
        get => GetValue(ShowWeeklyProperty);
        set => SetValue(ShowWeeklyProperty, value);
    }

    public bool ShowMonthly
    {
        get => GetValue(ShowMonthlyProperty);
        set => SetValue(ShowMonthlyProperty, value);
    }

    public bool ShowCron
    {
        get => GetValue(ShowCronProperty);
        set => SetValue(ShowCronProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ShowEverySecondsProperty ||
            change.Property == ShowEveryMinutesProperty ||
            change.Property == ShowEveryHoursProperty ||
            change.Property == ShowDailyProperty ||
            change.Property == ShowWeeklyProperty ||
            change.Property == ShowMonthlyProperty ||
            change.Property == ShowCronProperty)
        {
            ApplyModeVisibility();
        }
    }

    private void ApplyModeVisibility()
    {
        if (DataContext is not ScheduleEditorViewModel viewModel)
            return;

        viewModel.SetModeVisibility(
            ShowEverySeconds,
            ShowEveryMinutes,
            ShowEveryHours,
            ShowDaily,
            ShowWeekly,
            ShowMonthly,
            ShowCron);
    }
}
