using CommunityToolkit.Mvvm.ComponentModel;
using LogGate.Interfaces;
using LogGate.Models;
using System.Collections.ObjectModel;

namespace LogGate.ViewModels
{
    public partial class EmployeeCardViewModel : ObservableObject
    {
        private readonly IDataRepository _dataRepository;
        private readonly string _employeeName;

        [ObservableProperty]
        private ObservableCollection<DataItem> _employeeHistory = [];

        [ObservableProperty]
        private int _totalRecords;

        [ObservableProperty]
        private string _windowTitle;

        public EmployeeCardViewModel(string employeeName, IDataRepository dataRepository)
        {
            _employeeName = employeeName;
            _dataRepository = dataRepository;

            WindowTitle = $"История проходов: {_employeeName}";

            LoadEmployeeData();
        }

        private async void LoadEmployeeData()
        {
            var history = await _dataRepository.GetEmployeeHistoryAsync(_employeeName);
            TotalRecords = history.Count;

            EmployeeHistory.Clear();
            foreach (var item in history)
            {
                EmployeeHistory.Add(item);
            }
        }
    }
}