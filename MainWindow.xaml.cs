using LogGate.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace LogGate;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += MainWindow_Loaded;
    }

    private void LogsDataGrid_Sorting(object sender, System.Windows.Controls.DataGridSortingEventArgs e)
    {
        e.Handled = true;

        var columnPath = e.Column.SortMemberPath;
        if (string.IsNullOrEmpty(columnPath)) return;

        if (DataContext is MainViewModel vm)
        {
            if (vm.SortColumn == columnPath)
                vm.SortDescending = !vm.SortDescending;
            else
            {
                vm.SortColumn = columnPath;
                vm.SortDescending = false;
            }

            e.Column.SortDirection = vm.SortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending;

            foreach (var col in LogsDataGrid.Columns)
            {
                if (col != e.Column) col.SortDirection = null;
            }
        }
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentPage))
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (LogsDataGrid.Items.Count > 0)
                    LogsDataGrid.ScrollIntoView(LogsDataGrid.Items[0]);
            }, DispatcherPriority.ContextIdle);
        }
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
            viewModel.Cleanup();
    }
}
}