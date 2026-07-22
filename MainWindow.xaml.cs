using LogGate.Interfaces;
using LogGate.Services;
using LogGate.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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

            // 1. Создаем экземпляр нашего парсера
            IFileParser fileParser = new CsvFileParser();

            // 2. Создаем ViewModel и передаем в нее готовый парсер
            MainViewModel viewModel = new MainViewModel(fileParser);

            DataContext = viewModel;
        }
    }
}