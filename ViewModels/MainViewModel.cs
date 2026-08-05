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
        private ObservableCollection<DataItem> _dataItems = [];

        [ObservableProperty]
        private DateTime? _endDate;

        [ObservableProperty]
        private int _filteredCount;

        private CancellationTokenSource? _searchCts;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string? _selectedDatePreset;

        [ObservableProperty]
        private TimeSpan _shiftEndTime = new(16, 30, 0);

        [ObservableProperty]
        private TimeSpan _shiftStartTime = new(8, 1, 0);

        [ObservableProperty]
        private bool _showEarlyDepartures;

        [ObservableProperty]
        private bool _showLateArrivals;

        [ObservableProperty]
        private DateTime? _startDate;

        [ObservableProperty]
        private string _statusMessage = "Готово";

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private int _totalPages = 1;

        [ObservableProperty]
        private int _pageSize = 100;

        public MainViewModel(IFileParser fileParser, IDataRepository dataRepository, IDialogService dialogService)
        {
            _dataRepository = dataRepository;
            _fileParser = fileParser;
            _dialogService = dialogService;

            LoadDataFromDatabase();
        }

        public List<string> DatePresets { get; } =
        [
            "Сегодня",
            "Вчера",
            "За 7 дней",
            "Этот месяц"
        ];

        private void ApplyFilters(bool resetPage = true)
        {
            var query = _dataRepository.GetAllItems();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var lowerText = SearchText.ToLower();
                query = query.Where(x =>
                    (x.FullName != null && x.FullName.ToLower().Contains(lowerText)) ||
                    (x.PassNumber != null && x.PassNumber.ToLower().Contains(lowerText)) ||
                    (x.Department != null && x.Department.ToLower().Contains(lowerText))
                );
            }

            if (StartDate.HasValue)
                query = query.Where(x => x.EventTime >= StartDate.Value);

            if (EndDate.HasValue)
                query = query.Where(x => x.EventTime <= EndDate.Value.AddDays(1).AddTicks(-1));

            if (ShowLateArrivals)
                query = query.Where(x => x.EventTime.HasValue &&
                (x.EventTime.Value.Hour > 8 || (x.EventTime.Value.Hour == 8 && x.EventTime.Value.Minute >= 1)) &&
                 x.Direction == "Вход");

            if (ShowEarlyDepartures)
                query = query.Where(x => x.EventTime.HasValue &&
                (x.EventTime.Value.Hour < 16 || (x.EventTime.Value.Hour == 16 && x.EventTime.Value.Minute < 30)) &&
                 x.Direction == "Выход");

            FilteredCount = query.Count();
            TotalPages = (int)Math.Ceiling((double)FilteredCount / PageSize);
            if (TotalPages == 0)
                TotalPages = 1;

            if (resetPage)
                CurrentPage = 1;

            var pagedQuery = query.OrderByDescending(x => x.EventTime)
                          .Skip((CurrentPage - 1) * PageSize)
                          .Take(PageSize);

            DataItems = new ObservableCollection<DataItem>(pagedQuery);
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
            TotalCount = _dataRepository.GetAllItems().Count();
            ApplyFilters(resetPage: true);
        }

        partial void OnEndDateChanged(DateTime? value) => ApplyFilters();

        async partial void OnSearchTextChanged(string value)
        {
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                await Task.Delay(400, token);
                ApplyFilters();
            }
            catch (TaskCanceledException)
            {
            }
        }

        partial void OnSelectedDatePresetChanged(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            switch (value)
            {
                case "Сегодня":
                    StartDate = DateTime.Today;
                    EndDate = DateTime.Today;
                    break;

                case "Вчера":
                    StartDate = DateTime.Today.AddDays(-1);
                    EndDate = DateTime.Today.AddDays(-1);
                    break;

                case "За 7 дней":
                    StartDate = DateTime.Today.AddDays(-7);
                    EndDate = DateTime.Today;
                    break;

                case "Этот месяц":
                    StartDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                    EndDate = DateTime.Today;
                    break;
            }
        }

        partial void OnShowEarlyDeparturesChanged(bool value) => ApplyFilters();

        partial void OnShowLateArrivalsChanged(bool value) => ApplyFilters();

        partial void OnStartDateChanged(DateTime? value) => ApplyFilters();

        [RelayCommand]
        private void ResetFilters()
        {
            SearchText = string.Empty;
            StartDate = null;
            EndDate = null;
            SelectedDatePreset = null;
            ShowLateArrivals = false;
            ShowEarlyDepartures = false;

            LoadDataFromDatabase();
        }

        [RelayCommand]
        private void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                ApplyFilters(resetPage: false);
            }
        }

        [RelayCommand]
        private void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                ApplyFilters(resetPage: false);
            }
        }

        public void Cleanup()
        {
            if (_dataRepository is IDisposable disposableRepo)
                disposableRepo.Dispose();
        }
    }
}