using LogGate.ViewModels;
using System.Windows;

namespace LogGate;

/// <summary>
/// Логика взаимодействия для EmployeeCardWindow.xaml
/// </summary>
public partial class EmployeeCardWindow : Window
{
    public EmployeeCardWindow(EmployeeCardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
}