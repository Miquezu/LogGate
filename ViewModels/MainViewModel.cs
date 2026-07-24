using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogGate.Interfaces;
using LogGate.Models;
using System.Collections.ObjectModel;

namespace LogGate.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly IDataRepository _dataRepository;
        private readonly IDialogService _dialogService;
        private readonly IFileParser _fileParser;

        [ObservableProperty]
        private ObservableCollection<DataItem> _dataItems = new();

        [ObservableProperty]
        private DateTime? _endDate;

        [ObservableProperty]
        private int _filteredCount;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private DateTime? _startDate;

        [ObservableProperty]
        private string _statusMessage = "Готово";

        [ObservableProperty]
        private int _totalCount;

        // Настройки графика смены (по умолчанию с 08:00 до 17:00)
        [ObservableProperty]
        private TimeSpan _shiftStartTime = new TimeSpan(8, 0, 0);

        [ObservableProperty]
        private TimeSpan _shiftEndTime = new TimeSpan(17, 0, 0);

        // Галочка "Опоздавшие"
        [ObservableProperty]
        private bool _showLateArrivals;

        partial void OnShowLateArrivalsChanged(bool value) => ApplyFilters();

        // Галочка "Ушли раньше"
        [ObservableProperty]
        private bool _showEarlyDepartures;

        partial void OnShowEarlyDeparturesChanged(bool value) => ApplyFilters();

        public MainViewModel(IFileParser fileParser, IDataRepository dataRepository, IDialogService dialogService)
        {
            _dataRepository = dataRepository;
            _fileParser = fileParser;
            _dialogService = dialogService;

            LoadDataFromDatabase();
        }

        private void ApplyFilters()
        {
            var query = _dataRepository.GetAllItems().AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(x =>
                    (x.FullName != null && x.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                    (x.PassNumber != null && x.PassNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Department != null && x.Department.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                );
            }

            if (StartDate.HasValue)
                query = query.Where(x => x.EventTime >= StartDate.Value);

            if (EndDate.HasValue)
                query = query.Where(x => x.EventTime <= EndDate.Value.AddDays(1).AddTicks(-1));

            DataItems = new ObservableCollection<DataItem>(query);
            FilteredCount = DataItems.Count;
        }

        // RelayCommand превратит этот метод в команду LoadDataCommand
        [RelayCommand]
        private void LoadData()
        {
            string? selectedPath = _dialogService.OpenFileDialog();

            if (string.IsNullOrEmpty(selectedPath))
                return;

            var parsedData = _fileParser.Parse(selectedPath);
            int addedCount = _dataRepository.SaveItems(parsedData);
            LoadDataFromDatabase();

            if (addedCount > 0)
                _dialogService.ShowMessage($"Успешно добавлено новых записей: {addedCount}");
            else
                _dialogService.ShowMessage("Все записи из этого файла уже есть в базе данных.");
        }

        private void LoadDataFromDatabase()
        {
            var dbData = _dataRepository.GetAllItems();

            DataItems.Clear();
            foreach (var item in dbData)
            {
                DataItems.Add(item);
            }
            TotalCount = dbData.Count;
        }

        partial void OnEndDateChanged(DateTime? value) => ApplyFilters();

        partial void OnSearchTextChanged(string value) => ApplyFilters();

        partial void OnStartDateChanged(DateTime? value) => ApplyFilters();

        [RelayCommand]
        private void ResetFilters()
        {
            SearchText = string.Empty;
            StartDate = null;
            EndDate = null;
            LoadDataFromDatabase();
        }
    }
}