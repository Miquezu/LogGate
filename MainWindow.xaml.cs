using LogGate.DataAccess;
using LogGate.Interfaces;
using LogGate.Models;
using LogGate.Services;
using LogGate.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace LogGate
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainViewModel(new CsvFileParser(), new DataRepository(), new OpenDialog());
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CurrentPage")
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (LogsDataGrid.Items.Count > 0)
                        LogsDataGrid.ScrollIntoView(LogsDataGrid.Items[0]);
                }), DispatcherPriority.ContextIdle);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
                viewModel.Cleanup();
        }

        private void LogsDataGrid_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not MainViewModel viewModel)
                return;

            if (LogsDataGrid.SelectedItem is DataItem item)
                viewModel.SelectedItem = item;

            if (viewModel.OpenEmployeeCardCommand.CanExecute(null))
                viewModel.OpenEmployeeCardCommand.Execute(null);
        }

        private void LogsDataGrid_Sorting(object sender, System.Windows.Controls.DataGridSortingEventArgs e)
        {
            e.Handled = true;

            var columnPath = e.Column.SortMemberPath;
            if (string.IsNullOrEmpty(columnPath)) return;

            if (DataContext is MainViewModel vm)
            {
                // Меняем параметры сортировки во ViewModel
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
    }
}