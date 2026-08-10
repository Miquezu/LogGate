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
        private string _windowTitle;

        [ObservableProperty]
        private ObservableCollection<DataItem> _employeeHistory = [];

        [ObservableProperty]
        private int _totalRecords;

        public EmployeeCardViewModel(string employeeName, IDataRepository dataRepository)
        {
            _employeeName = employeeName;
            _dataRepository = dataRepository;

            WindowTitle = $"История проходов: {_employeeName}";

            LoadEmployeeData();
        }

        private void LoadEmployeeData()
        {
            // Вытягиваем из БД все записи по конкретному ФИО, сортируем от новых к старым
            var history = _dataRepository.GetAllItems()
                .Where(x => x.FullName == _employeeName)
                .OrderByDescending(x => x.EventTime)
                .ToList();

            TotalRecords = history.Count;

            foreach (var item in history)
            {
                EmployeeHistory.Add(item);
            }
        }
    }
}