using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogGate.Interfaces;
using LogGate.Models;
using LogGate.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace LogGate.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly AiReportManager _aiReportManager;
    private readonly AutoImportService _autoImportService;
    private readonly IDataCleaningService _cleaningService;
    private readonly IDataRepository _dataRepository;
    private readonly IDialogService _dialogService;
    private readonly IFileParser _fileParser;
    private readonly IScheduleService _scheduleService;

    private CancellationTokenSource? _filterCts;
    private List<DataItem> _filteredCache = [];

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
        IScheduleService scheduleService,
        AutoImportService autoImportService)
    {
        _fileParser = fileParser;
        _cleaningService = cleaningService;
        _dataRepository = dataRepository;
        _dialogService = dialogService;
        _aiReportManager = aiReportManager;
        _scheduleService = scheduleService;
        _autoImportService = autoImportService;

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
        _autoImportService.DataImported -= OnAutoDataImported;
        _autoImportService.ImportError -= OnAutoImportError;
        _autoImportService.ImportStarted -= OnAutoImportStarted;
        _filterCts?.Cancel();
        _filterCts?.Dispose();
    }

    private void RequestDataRefresh(bool resetPage = true, int delayMs = 0)
    {
        _filterCts?.Cancel();
        _filterCts?.Dispose();
        _filterCts = new CancellationTokenSource();
        var token = _filterCts.Token;

        _ = RefreshDataAsync(resetPage, delayMs, token);
    }

    private async Task RefreshDataAsync(bool resetPage, int delayMs, CancellationToken token)
    {
        try
        {
            if (delayMs > 0)
            {
                await Task.Delay(delayMs, token);
            }

            StatusMessage = "Загрузка и фильтрация данных...";

            var rawData = await _dataRepository.GetFilteredLogsAsync(SearchText, StartDate, EndDate);
            token.ThrowIfCancellationRequested();

            _scheduleService.EvaluateCompliance(rawData);
            token.ThrowIfCancellationRequested();

            IEnumerable<DataItem> filtered = rawData;

            if (ShowLateArrivals)
                filtered = filtered.Where(x => x.IsLate);

            if (ShowEarlyDepartures)
                filtered = filtered.Where(x => x.IsEarlyDeparture);

            if (ShowMissingAlcotest)
                filtered = filtered.Where(item => _scheduleService.RequiresAlcotest(item) && item.AlcotestResult is null);

            _filteredCache = [.. filtered];
            token.ThrowIfCancellationRequested();

            FilteredCount = _filteredCache.Count;
            TotalPages = Math.Max(1, (int)Math.Ceiling((double)FilteredCount / PageSize));
            if (resetPage) CurrentPage = 1;

            UpdatePagedView();
            StatusMessage = "Готово.";
        }
        catch (OperationCanceledException)
        {
            // Запрос был отменен более новым действием пользователя
        }
        catch (Exception ex)
        {
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
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
        RequestDataRefresh(resetPage: true);
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage++;
            UpdatePagedView();
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

    partial void OnEndDateChanged(DateTime? value) => RequestDataRefresh(resetPage: true);

    partial void OnSearchTextChanged(string value) => RequestDataRefresh(resetPage: true, delayMs: 400);

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

    partial void OnShowEarlyDeparturesChanged(bool value) => RequestDataRefresh(resetPage: true);

    partial void OnShowLateArrivalsChanged(bool value) => RequestDataRefresh(resetPage: true);

    partial void OnShowMissingAlcotestChanged(bool value) => RequestDataRefresh(resetPage: true);

    partial void OnSortColumnChanged(string value) => UpdatePagedView();

    partial void OnSortDescendingChanged(bool value) => UpdatePagedView();

    partial void OnStartDateChanged(DateTime? value) => RequestDataRefresh(resetPage: true);

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
        RequestDataRefresh(resetPage: false);
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
        {
            CurrentPage--;
            UpdatePagedView();
        }
    }

    [RelayCommand]
    private void ResetFilters()
    {
        StatusMessage = "Сброс фильтров...";

        _filterCts?.Cancel();

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
            await _dataRepository.UpdateItemsNotesAsync(DataItems);
            StatusMessage = "Изменения успешно сохранены.";
            _dialogService.ShowMessage("Примечания успешно сохранены в базе данных.");
        }
        catch (Exception ex)
        {
            StatusMessage = "Ошибка при сохранении данных!";
            _dialogService.ShowError($"Ошибка сохранения:\n{ex.Message}");
        }
    }

    private void UpdatePagedView()
    {
        var sorted = ApplySorting(_filteredCache.AsQueryable());
        var pagedData = sorted.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();
        DataItems = [.. pagedData];
    }
}