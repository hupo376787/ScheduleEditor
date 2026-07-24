using Avalonia.Controls;
using AvaloniaScheduleEditor.Demo.ViewModels;

namespace AvaloniaScheduleEditor.Demo;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        Opened += async (_, _) => await _viewModel.InitializeAsync();
        Closing += (_, _) => _viewModel.Dispose();
    }
}
