using LogGate.DataAccess;
using LogGate.Interfaces;
using LogGate.Services;
using LogGate.ViewModels;
using System.Windows;

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
        }
    }
}