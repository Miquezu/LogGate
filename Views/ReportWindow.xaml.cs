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

namespace LogGate.Views
{
    /// <summary>
    /// Логика взаимодействия для ReportWindow.xaml
    /// </summary>
    public partial class ReportWindow : Window
    {
        public ReportWindow()
        {
            InitializeComponent();

            LoadingPanel.Visibility = Visibility.Visible;
            MarkdownViewer.Visibility = Visibility.Collapsed;
        }

        public void DisplayReport(string markdownText)
        {
            // Скрываем прогресс-бар
            LoadingPanel.Visibility = Visibility.Collapsed;

            // Передаем текст в Markdig и показываем его
            MarkdownViewer.Markdown = markdownText;
            MarkdownViewer.Visibility = Visibility.Visible;
        }
    }
}