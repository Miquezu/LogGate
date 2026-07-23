using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogGate.Interfaces;
using LogGate.Models;
using System.Collections.ObjectModel;

namespace LogGate.ViewModels
{
    public partial class MainViewModel: ObservableObject
    {
        private readonly IFileParser _fileParser;
        private readonly IDataRepository _dataRepository;
        private readonly IDialogService _dialogService;

        [ObservableProperty]
        private ObservableCollection<DataItem> _dataItems = new();

        public MainViewModel(IFileParser fileParser, IDataRepository dataRepository, IDialogService dialogService)
        {
            _dataRepository = dataRepository;
            _fileParser = fileParser;
            _dialogService = dialogService;

            LoadDataFromDatabase();
        }

        private void LoadDataFromDatabase()
        {
            var dbData = _dataRepository.GetAllItems();

            DataItems.Clear();
            foreach (var item in dbData)
            {
                DataItems.Add(item);
            }
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
        // Поисковая строка
        [ObservableProperty]
        private string _searchText = string.Empty;

        // Фильтр по датам
        [ObservableProperty]
        private DateTime? _startDate;

        [ObservableProperty]
        private DateTime? _endDate;

        // Метод для фильтрации (вызывается при изменении текста поиска или дат)
        partial void OnSearchTextChanged(string value) => ApplyFilters();
        partial void OnStartDateChanged(DateTime? value) => ApplyFilters();
        partial void OnEndDateChanged(DateTime? value) => ApplyFilters();

        private void ApplyFilters()
        {
            var query = _dataRepository.GetAllItems().AsEnumerable();

            // 1. Поиск по тексту (ФИО, номер пропуска и т.д.)
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                query = query.Where(x =>
                    (x.FullName != null && x.FullName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                    (x.PassNumber != null && x.PassNumber.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Department != null && x.Department.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                );
            }

            // 2. Фильтр по диапазону дат
            if (StartDate.HasValue)
                query = query.Where(x => x.EventTime >= StartDate.Value);

            if (EndDate.HasValue)
                query = query.Where(x => x.EventTime <= EndDate.Value.AddDays(1).AddTicks(-1)); // Включая весь выбранный день

            // Обновляем коллекцию на UI
            DataItems = new ObservableCollection<DataItem>(query);
        }

        // Команда сброса фильтров
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
