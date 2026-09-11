using LogGate.ViewModels;
using System.Windows;

namespace LogGate.Views;

/// <summary>
/// Логика взаимодействия для ScheduleSettingsWindow.xaml
/// </summary>
public partial class ScheduleSettingsWindow : Window
{
    public ScheduleSettingsWindow(ScheduleSettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
}