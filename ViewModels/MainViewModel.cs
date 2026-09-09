using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogGate.Interfaces;
using LogGate.Models;
using LogGate.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace LogGate.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly AiReportManager _aiReportManager;
        private readonly AutoImportService _autoImportService;
        private readonly IDataCleaningService _cleaningService;
        private readonly IDataRepository _dataRepository;
        private readonly IDialogService _dialogService;
        private readonly IFileParser _fileParser;
        private readonly IScheduleService _scheduleService;

        [ObservableProperty]
        private int _currentPage = 1;

        [ObservableProperty]
        private ObservableCollection<DataItem> _dataItems = [];

        [ObservableProperty]
        private DateTime? _endDate = DateTime.Today;

        [ObservableProperty]
        private int _filteredCount;

        [ObservableProperty]
        private int _pageSize = 200;

        private CancellationTokenSource? _searchCts;

        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private string? _selectedDatePreset = "Сегодня";

        [ObservableProperty]
        private DataItem? _selectedItem;

        [ObservableProperty]
        private bool _showEarlyDepartures;

        [ObservableProperty]
        private bool _showLateArrivals;

        [ObservableProperty]
        private bool _showMissingAlcotest;

        [ObservableProperty]
        private string _sortColumn = "EventTime";

        [ObservableProperty]
        private bool _sortDescending = true;

        [ObservableProperty]
        private DateTime? _startDate = DateTime.Today;

        [ObservableProperty]
        private string _statusMessage = "Готово";

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private int _totalPages = 1;

        public MainViewModel(
            IFileParser fileParser,
            IDataCleaningService cleaningService,
            IDataRepository dataRepository,
            IDialogService dialogService,
            AiReportManager aiReportManager,
            IScheduleService scheduleService)
        {
            _fileParser = fileParser;
            _cleaningService = cleaningService;
            _dataRepository = dataRepository;
            _dialogService = dialogService;
            _aiReportManager = aiReportManager;
            _scheduleService = scheduleService;

            _autoImportService = new AutoImportService(_fileParser, _cleaningService, _dataRepository);
            _autoImportService.DataImported += OnAutoDataImported;
            _autoImportService.ImportError += OnAutoImportError;
            _autoImportService.ImportStarted += OnAutoImportStarted;

            _ = InitializeCalendarAsync();
            LoadDataFromDatabase();
        }

        public List<string> DatePresets { get; } =
        [
            "Сегодня",
            "Вчера",
            "За 7 дней",
            "Этот месяц",
            "Прошлый месяц",
            "Сначала года"
        ];

        public void Cleanup()
        {
            _autoImportService.Dispose();
        }

        private void ApplyFilters(bool resetPage = true) => _ = ApplyFiltersAsync(resetPage);

        private async Task ApplyFiltersAsync(bool resetPage = true)
        {
            StatusMessage = "Загрузка и фильтрация данных...";

            try
            {
                var rawData = await _dataRepository.GetFilteredLogsAsync(SearchText, StartDate, EndDate);

                _scheduleService.EvaluateCompliance(rawData);

                IEnumerable<DataItem> filtered = rawData;

                if (ShowLateArrivals)
                    filtered = filtered.Where(x => x.IsLate);

                if (ShowEarlyDepartures)
                    filtered = filtered.Where(x => x.IsEarlyDeparture);

                if (ShowMissingAlcotest)
                    filtered = filtered.Where(item => _scheduleService.RequiresAlcotest(item) && item.AlcotestResult == null);

                var sortedList = ApplySorting(filtered.AsQueryable()).ToList();

                FilteredCount = sortedList.Count;
                TotalPages = (int)Math.Ceiling((double)FilteredCount / PageSize);
                if (TotalPages == 0) TotalPages = 1;
                if (resetPage) CurrentPage = 1;

                var pagedData = sortedList.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
                DataItems = new ObservableCollection<DataItem>(pagedData);
            }
            finally
            {
                StatusMessage = "Готово.";
            }
        }

        private IQueryable<DataItem> ApplySorting(IQueryable<DataItem> query)
        {
            return SortColumn switch
            {
                "RecordNumber" => SortDescending ? query.OrderByDescending(x => x.RecordNumber) : query.OrderBy(x => x.RecordNumber),
                "Post" => SortDescending ? query.OrderByDescending(x => x.Post) : query.OrderBy(x => x.Post),
                "Direction" => SortDescending ? query.OrderByDescending(x => x.Direction) : query.OrderBy(x => x.Direction),
                "EventTime" => SortDescending ? query.OrderByDescending(x => x.EventTime) : query.OrderBy(x => x.EventTime),
                "TemperatureTime" => SortDescending ? query.OrderByDescending(x => x.TemperatureTime) : query.OrderBy(x => x.TemperatureTime),
                "AlcotestTime" => SortDescending ? query.OrderByDescending(x => x.AlcotestTime) : query.OrderBy(x => x.AlcotestTime),
                "Temperature" => SortDescending ? query.OrderByDescending(x => x.Temperature) : query.OrderBy(x => x.Temperature),
                "AlcotestResult" => SortDescending ? query.OrderByDescending(x => x.AlcotestResult) : query.OrderBy(x => x.AlcotestResult),
                "FullName" => SortDescending ? query.OrderByDescending(x => x.FullName) : query.OrderBy(x => x.FullName),
                "Position" => SortDescending ? query.OrderByDescending(x => x.Position) : query.OrderBy(x => x.Position),
                "Department" => SortDescending ? query.OrderByDescending(x => x.Department) : query.OrderBy(x => x.Department),
                "EmployeeNumber" => SortDescending ? query.OrderByDescending(x => x.EmployeeNumber) : query.OrderBy(x => x.EmployeeNumber),
                "PassNumber" => SortDescending ? query.OrderByDescending(x => x.PassNumber) : query.OrderBy(x => x.PassNumber),
                _ => query.OrderByDescending(x => x.EventTime)
            };
        }

        [RelayCommand]
        private async Task GenerateAiReportAsync()
        {
            StatusMessage = "Сбор данных для анализа ИИ...";
            await _aiReportManager.GenerateReportAsync(SearchText, StartDate, EndDate);
            StatusMessage = "Анализ завершен.";
        }

        private async Task InitializeCalendarAsync()
        {
            int currentYear = DateTime.Now.Year;

            var cachedHolidays = await _dataRepository.GetShortenedDaysByYearAsync(currentYear);

            if (cachedHolidays.Count == 0)
            {
                var calendarService = new CalendarService();
                cachedHolidays = await calendarService.GetPreHolidaysAsync(currentYear);

                if (cachedHolidays.Count > 0)
                {
                    await _dataRepository.SaveShortenedDaysAsync(cachedHolidays);
                }
            }

            var workRules = await _dataRepository.GetAllWorkRulesAsync();
            _scheduleService.UpdateRules(workRules, cachedHolidays);
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            string? selectedPath = _dialogService.OpenFileDialog();
            if (string.IsNullOrEmpty(selectedPath))
                return;

            StatusMessage = $"Чтение файла: {Path.GetFileName(selectedPath)}...";

            var rawData = _fileParser.Parse(selectedPath);
            var cleanedData = _cleaningService.CleanAnomalies(rawData);

            StatusMessage = "Сохранение записей в базу данных...";
            int addedCount = await _dataRepository.SaveItemsAsync(cleanedData);

            await LoadDataFromDatabaseAsync();

            if (addedCount > 0)
            {
                StatusMessage = $"Загрузка завершена. Добавлено новых записей: {addedCount}.";
                _dialogService.ShowMessage($"Успешно добавлено новых записей: {addedCount}");
            }
            else
            {
                StatusMessage = "Загрузка завершена. Файл не содержал новых данных.";
                _dialogService.ShowMessage("Все записи из этого файла уже есть в базе данных.");
            }
        }

        private void LoadDataFromDatabase() => _ = LoadDataFromDatabaseAsync();

        private async Task LoadDataFromDatabaseAsync()
        {
            TotalCount = await _dataRepository.GetTotalCountAsync();
            ApplyFilters(resetPage: true);
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

        private void OnAutoDataImported(int addedCount)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LoadDataFromDatabase();
                StatusMessage = $"Готово (фоновый импорт: +{addedCount} записей)";
                _dialogService.ShowMessage($"Фоновый импорт завершен.\nДобавлено новых записей: {addedCount}");
            });
        }

        private void OnAutoImportError(string error)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusMessage = "Ошибка фонового импорта!";
                _dialogService.ShowError(error);
            });
        }

        private void OnAutoImportStarted(string fileName)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                StatusMessage = $"Загрузка файла из папки: {fileName}...";
            });
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

            var today = DateTime.Today;

            switch (value)
            {
                case "Сегодня":
                    StartDate = today;
                    EndDate = today;
                    break;

                case "Вчера":
                    StartDate = today.AddDays(-1);
                    EndDate = today.AddDays(-1);
                    break;

                case "За 7 дней":
                    StartDate = today.AddDays(-7);
                    EndDate = today;
                    break;

                case "Этот месяц":
                    StartDate = new DateTime(today.Year, today.Month, 1);
                    EndDate = today;
                    break;

                case "Прошлый месяц":
                    var firstDayOfThisMonth = new DateTime(today.Year, today.Month, 1);
                    StartDate = firstDayOfThisMonth.AddMonths(-1);
                    EndDate = firstDayOfThisMonth.AddDays(-1);
                    break;

                case "Сначала года":
                    StartDate = new DateTime(today.Year, 1, 1);
                    EndDate = today;
                    break;
            }
        }

        partial void OnShowEarlyDeparturesChanged(bool value) => ApplyFilters();

        partial void OnShowLateArrivalsChanged(bool value) => ApplyFilters();

        partial void OnShowMissingAlcotestChanged(bool value) => ApplyFilters();

        partial void OnSortColumnChanged(string value) => ApplyFilters();

        partial void OnSortDescendingChanged(bool value) => ApplyFilters();

        partial void OnStartDateChanged(DateTime? value) => ApplyFilters();

        [RelayCommand]
        private void OpenEmployeeCard()
        {
            if (SelectedItem == null || string.IsNullOrEmpty(SelectedItem.FullName))
                return;

            var cardViewModel = new EmployeeCardViewModel(SelectedItem.FullName, _dataRepository);
            _dialogService.OpenEmployeeCard(cardViewModel);
        }

        [RelayCommand]
        private void OpenScheduleSettings()
        {
            var settingsViewModel = new ScheduleSettingsViewModel(_dataRepository, _dialogService, _scheduleService);
            _dialogService.OpenScheduleSettings(settingsViewModel);
            ApplyFilters();
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

        [RelayCommand]
        private void ResetFilters()
        {
            StatusMessage = "Сброс фильтров...";

            SearchText = string.Empty;
            StartDate = null;
            EndDate = null;
            SelectedDatePreset = null;
            ShowLateArrivals = false;
            ShowEarlyDepartures = false;
            ShowMissingAlcotest = false;

            LoadDataFromDatabase();
        }

        [RelayCommand]
        private async Task SaveChangesAsync()
        {
            try
            {
                StatusMessage = "Сохранение изменений в базу...";
                await _dataRepository.SaveChangesAsync();
                StatusMessage = "Изменения успешно сохранены.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Ошибка при сохранении данных!";
                _dialogService.ShowError($"Ошибка сохранения:\n{ex.Message}");
            }
        }
    }
}