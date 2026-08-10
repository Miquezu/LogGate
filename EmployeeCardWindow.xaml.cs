using LogGate.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace LogGate
{
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