using LogGate.Interfaces;
using Microsoft.Win32;
using System.Windows;

namespace LogGate.Services
{
    public class OpenDialog:IDialogService
    {
        public string? OpenFileDialog()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Выберите .csv файл",
                Filter = "CSV files(*.csv)|*.csv|All files(*.*)|*.*"
            };
            bool? result = dialog.ShowDialog();
            if(result == true) return dialog.FileName;
            return null;
        }
        public void ShowMessage(string message) 
            => MessageBox.Show(message, "Уведомление", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
