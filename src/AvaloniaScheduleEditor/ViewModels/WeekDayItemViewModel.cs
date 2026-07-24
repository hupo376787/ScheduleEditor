using CommunityToolkit.Mvvm.ComponentModel;

namespace AvaloniaScheduleEditor.ViewModels;

public partial class WeekDayItemViewModel : ObservableObject
{
    public WeekDayItemViewModel(
        DayOfWeek day,
        string displayName,
        string fullDisplayName,
        bool isSelected)
    {
        Day = day;
        _displayName = displayName;
        _fullDisplayName = fullDisplayName;
        _isSelected = isSelected;
    }

    public DayOfWeek Day { get; }

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private string _fullDisplayName;

    [ObservableProperty]
    private bool _isSelected;
}
