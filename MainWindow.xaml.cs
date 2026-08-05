using LogGate.DataAccess;
using LogGate.Interfaces;
using LogGate.Services;
using LogGate.ViewModels;
using System.Windows;
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

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
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
    }
}