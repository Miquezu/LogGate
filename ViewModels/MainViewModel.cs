using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LogGate.Services;
using MaterialDesignThemes.Wpf;
using SkiaSharp;
using System.Diagnostics;
using System.Windows;

namespace LogGate.ViewModels;

/// <summary>
/// Главная ViewModel приложения (управление журналами, фильтрация, дашборд, импорт/экспорт).
/// </summary>
public partial class MainViewModel : ObservableObject
{
    public const string AllDepartmentsPreset = "Все подразделения";

    public ISnackbarMessageQueue SnackbarMessageQueue => _dialogService.SnackbarMessageQueue;

    private readonly IDataImportService _dataImportService;
    private readonly IDataRepository _dataRepository;
    private readonly IDialogService _dialogService;
    private readonly IScheduleService _scheduleService;
    private readonly ICalendarService _calendarService;
    private readonly IDashboardService _dashboardService;
    private readonly IExportService _exportService;
    private readonly AiReportManager _aiReportManager;
    private readonly AutoImportService _autoImportService;
    private readonly ILogger<MainViewModel>? _logger;

    private CancellationTokenSource? _filterCts;
    private List<DataItem> _filteredCache = [];

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private ObservableCollection<DataItem> _dataItems = [];

    [ObservableProperty]
    private ObservableCollection<string> _departments = [AllDepartmentsPreset];

    [ObservableProperty]
    private string _selectedDepartment = AllDepartmentsPreset;

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
    private int _lateCount;

    [ObservableProperty]
    private int _earlyCount;

    [ObservableProperty]
    private int _missingAlcotestCount;

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchText) ||
        (SelectedDepartment != AllDepartmentsPreset && !string.IsNullOrEmpty(SelectedDepartment)) ||
        ShowLateArrivals ||
        ShowEarlyDepartures ||
        ShowMissingAlcotest ||
        StartDate != null ||
        EndDate != null;

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

    // Режим отображения: Таблица или Дашборд
    [ObservableProperty]
    private bool _isDashboardView;

    public bool ShowLogsEmptyState => !IsDashboardView && FilteredCount == 0;

    partial void OnIsDashboardViewChanged(bool value) => OnPropertyChanged(nameof(ShowLogsEmptyState));
    partial void OnFilteredCountChanged(int value) => OnPropertyChanged(nameof(ShowLogsEmptyState));

    // KPI Метрики Дашборда
    [ObservableProperty]
    private int _kpiTotalPasses;

    [ObservableProperty]
    private int _kpiUniqueEmployees;

    [ObservableProperty]
    private int _kpiLateCount;

    [ObservableProperty]
    private int _kpiEarlyCount;

    [ObservableProperty]
    private int _kpiCriticalCount;

    [ObservableProperty]
    private string _kpiLatePercentage = "0%";

    [ObservableProperty]
    private string _kpiEarlyPercentage = "0%";

    // Серии графиков LiveCharts
    [ObservableProperty]
    private ISeries[] _hourlyTrafficSeries = [];

    [ObservableProperty]
    private Axis[] _hourlyXAxes = [];

    [ObservableProperty]
    private ISeries[] _violationsPieSeries = [];

    [ObservableProperty]
    private ISeries[] _departmentSeries = [];

    [ObservableProperty]
    private Axis[] _departmentYAxes = [];

    public MainViewModel(
        IDataImportService dataImportService,
        IDataRepository dataRepository,
        IDialogService dialogService,
        IScheduleService scheduleService,
        ICalendarService calendarService,
        IDashboardService dashboardService,
        IExportService exportService,
        AiReportManager aiReportManager,
        AutoImportService autoImportService,
        ILogger<MainViewModel>? logger = null)
    {
        _dataImportService = dataImportService;
        _dataRepository = dataRepository;
        _dialogService = dialogService;
        _scheduleService = scheduleService;
        _calendarService = calendarService;
        _dashboardService = dashboardService;
        _exportService = exportService;
        _aiReportManager = aiReportManager;
        _autoImportService = autoImportService;
        _logger = logger;

        _autoImportService.DataImported += OnAutoDataImported;
        _autoImportService.ImportError += OnAutoImportError;
        _autoImportService.ImportStarted += OnAutoImportStarted;

        _ = InitializeCalendarAsync();
        _ = LoadDepartmentsAsync();
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

    [RelayCommand]
    private void SwitchToLogsView() => IsDashboardView = false;

    [RelayCommand]
    private void SwitchToDashboardView() => IsDashboardView = true;

    [RelayCommand]
    private void DrillDownAll()
    {
        ShowLateArrivals = false;
        ShowEarlyDepartures = false;
        ShowMissingAlcotest = false;
        IsDashboardView = false;
    }

    [RelayCommand]
    private void DrillDownLate()
    {
        ShowEarlyDepartures = false;
        ShowMissingAlcotest = false;
        ShowLateArrivals = true;
        IsDashboardView = false;
    }

    [RelayCommand]
    private void DrillDownEarly()
    {
        ShowLateArrivals = false;
        ShowMissingAlcotest = false;
        ShowEarlyDepartures = true;
        IsDashboardView = false;
    }

    [RelayCommand]
    private void DrillDownCritical()
    {
        ShowLateArrivals = false;
        ShowEarlyDepartures = false;
        ShowMissingAlcotest = true;
        IsDashboardView = false;
    }

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
        OnPropertyChanged(nameof(HasActiveFilters));
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

            var rawData = await _dataRepository.GetFilteredLogsAsync(SearchText, StartDate, EndDate, SelectedDepartment);
            token.ThrowIfCancellationRequested();

            _scheduleService.EvaluateCompliance(rawData);
            token.ThrowIfCancellationRequested();

            LateCount = rawData.Count(x => x.IsLate);
            EarlyCount = rawData.Count(x => x.IsEarlyDeparture);
            MissingAlcotestCount = rawData.Count(item => _scheduleService.RequiresAlcotest(item) && item.AlcotestResult is null);

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
            UpdateDashboard(_filteredCache);

            StatusMessage = "Готово.";
        }
        catch (OperationCanceledException)
        {
            // Запрос был отменен более новым действием пользователя
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при фильтрации и загрузке данных");
            StatusMessage = $"Ошибка загрузки: {ex.Message}";
        }
    }

    private void UpdateDashboard(List<DataItem> items)
    {
        var metrics = _dashboardService.CalculateMetrics(items);

        KpiTotalPasses = metrics.TotalPasses;
        KpiUniqueEmployees = metrics.UniqueEmployees;
        KpiLateCount = metrics.LateCount;
        KpiEarlyCount = metrics.EarlyCount;
        KpiCriticalCount = metrics.AlcoPositiveCount + metrics.HighTempCount;

        KpiLatePercentage = metrics.TotalPasses > 0
            ? $"{(double)metrics.LateCount / metrics.TotalPasses * 100:F1}%"
            : "0%";

        KpiEarlyPercentage = metrics.TotalPasses > 0
            ? $"{(double)metrics.EarlyCount / metrics.TotalPasses * 100:F1}%"
            : "0%";

        // 1. Почасовой трафик (06:00 - 22:00)
        const int startHour = 6;
        const int endHour = 22;
        const int count = endHour - startHour + 1;

        var inValues = new int[count];
        var outValues = new int[count];
        var labels = new string[count];

        for (int h = startHour; h <= endHour; h++)
        {
            int idx = h - startHour;
            inValues[idx] = metrics.HourlyIn[h];
            outValues[idx] = metrics.HourlyOut[h];
            labels[idx] = $"{h:D2}:00";
        }

        HourlyTrafficSeries =
        [
            new ColumnSeries<int>
            {
                Name = "Вход",
                Values = inValues,
                Fill = new SolidColorPaint(SKColor.Parse("#1976D2")),
                Stroke = null,
                Padding = 2
            },
            new ColumnSeries<int>
            {
                Name = "Выход",
                Values = outValues,
                Fill = new SolidColorPaint(SKColor.Parse("#FF9800")),
                Stroke = null,
                Padding = 2
            }
        ];

        HourlyXAxes =
        [
            new Axis
            {
                Labels = labels,
                LabelsPaint = new SolidColorPaint(SKColor.Parse("#616161")),
                TextSize = 11
            }
        ];

        // 2. Круговая диаграмма нарушений
        List<ISeries> pieSeriesList = [];
        SKColor[] colors =
        [
            SKColor.Parse("#F44336"),
            SKColor.Parse("#FF9800"),
            SKColor.Parse("#9C27B0"),
            SKColor.Parse("#E91E63"),
            SKColor.Parse("#3F51B5")
        ];

        int colorIdx = 0;
        foreach (var cat in metrics.ViolationBreakdown)
        {
            if (cat.Count > 0)
            {
                pieSeriesList.Add(new PieSeries<int>
                {
                    Name = $"{cat.Category} ({cat.Count})",
                    Values = [cat.Count],
                    Fill = new SolidColorPaint(colors[colorIdx % colors.Length])
                });
                colorIdx++;
            }
        }

        if (pieSeriesList.Count == 0)
        {
            pieSeriesList.Add(new PieSeries<int>
            {
                Name = "Нарушений не зафиксировано",
                Values = [1],
                Fill = new SolidColorPaint(SKColor.Parse("#4CAF50"))
            });
        }

        ViolationsPieSeries = [.. pieSeriesList];

        // 3. Топ-5 отделов по нарушениям
        if (metrics.TopDepartments.Count > 0)
        {
            var depts = metrics.TopDepartments.AsEnumerable().Reverse().ToList();
            var deptNames = depts.Select(d => d.Department).ToArray();
            var deptCounts = depts.Select(d => d.Count).ToArray();

            DepartmentSeries =
            [
                new RowSeries<int>
                {
                    Name = "Инцидентов",
                    Values = deptCounts,
                    Fill = new SolidColorPaint(SKColor.Parse("#E53935")),
                    Stroke = null
                }
            ];

            DepartmentYAxes =
            [
                new Axis
                {
                    Labels = deptNames,
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#424242")),
                    TextSize = 11
                }
            ];
        }
        else
        {
            DepartmentSeries = [];
            DepartmentYAxes = [];
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
        await _aiReportManager.GenerateReportAsync(SearchText, StartDate, EndDate, SelectedDepartment);
        StatusMessage = "Анализ завершен.";
    }

    private async Task InitializeCalendarAsync()
    {
        int currentYear = DateTime.Now.Year;

        var cachedHolidays = await _dataRepository.GetShortenedDaysByYearAsync(currentYear);

        if (cachedHolidays.Count == 0)
        {
            cachedHolidays = await _calendarService.GetPreHolidaysAsync(currentYear);

            if (cachedHolidays.Count > 0)
            {
                await _dataRepository.SaveShortenedDaysAsync(cachedHolidays);
            }
        }

        var workRules = await _dataRepository.GetAllWorkRulesAsync();
        _scheduleService.UpdateRules(workRules, cachedHolidays);
    }

    public async Task LoadDepartmentsAsync()
    {
        try
        {
            var depts = await _dataRepository.GetDepartmentsAsync();
            var currentSelection = SelectedDepartment;

            Departments.Clear();
            Departments.Add(AllDepartmentsPreset);

            foreach (var dept in depts.OrderBy(d => d))
            {
                Departments.Add(dept);
            }

            if (Departments.Contains(currentSelection))
            {
                SelectedDepartment = currentSelection;
            }
            else
            {
                SelectedDepartment = AllDepartmentsPreset;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при загрузке подразделений");
        }
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        string? selectedPath = _dialogService.OpenFileDialog();
        if (string.IsNullOrEmpty(selectedPath))
            return;

        StatusMessage = $"Импорт файла: {Path.GetFileName(selectedPath)}...";

        try
        {
            int addedCount = await _dataImportService.ImportCsvAsync(selectedPath);

            await LoadDataFromDatabaseAsync();
            await LoadDepartmentsAsync();

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
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при загрузке CSV файла {SelectedPath}", selectedPath);
            StatusMessage = "Ошибка при загрузке файла!";
            _dialogService.ShowError($"Не удалось загрузить файл:\n{ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportDataAsync()
    {
        if (_filteredCache.Count == 0)
        {
            _dialogService.ShowWarning("Нет данных для экспорта с текущими параметрами фильтрации.");
            return;
        }

        var defaultFileName = $"Отчет_СКУД_{DateTime.Now:yyyy-MM-dd_HH-mm}.csv";
        var filePath = _dialogService.SaveFileDialog(defaultFileName);
        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            StatusMessage = $"Экспорт данных ({_filteredCache.Count} записей)...";
            await _exportService.ExportToCsvAsync(_filteredCache, filePath);
            StatusMessage = $"Экспорт успешно завершен ({_filteredCache.Count} записей).";
            var fileName = Path.GetFileName(filePath);
            _dialogService.ShowMessageWithAction(
                $"Экспортировано {_filteredCache.Count} записей: {fileName}",
                "ОТКРЫТЬ",
                () =>
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Не удалось открыть проводник для {FilePath}", filePath);
                    }
                });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при экспорте данных");
            StatusMessage = "Ошибка при экспорте данных!";
            _dialogService.ShowError($"Не удалось экспортировать данные:\n{ex.Message}");
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
            _ = LoadDepartmentsAsync();
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

        (StartDate, EndDate) = value switch
        {
            "Сегодня" => (today, today),
            "Вчера" => (today.AddDays(-1), today.AddDays(-1)),
            "За 7 дней" => (today.AddDays(-7), today),
            "Этот месяц" => (new DateTime(today.Year, today.Month, 1), today),
            "Прошлый месяц" => (new DateTime(today.Year, today.Month, 1).AddMonths(-1), new DateTime(today.Year, today.Month, 1).AddDays(-1)),
            "Сначала года" => (new DateTime(today.Year, 1, 1), today),
            _ => (StartDate, EndDate)
        };
    }

    partial void OnSelectedDepartmentChanged(string value) => RequestDataRefresh(resetPage: true);

    partial void OnShowEarlyDeparturesChanged(bool value) => RequestDataRefresh(resetPage: true);

    partial void OnShowLateArrivalsChanged(bool value) => RequestDataRefresh(resetPage: true);

    partial void OnShowMissingAlcotestChanged(bool value) => RequestDataRefresh(resetPage: true);

    partial void OnSortColumnChanged(string value) => UpdatePagedView();

    partial void OnSortDescendingChanged(bool value) => UpdatePagedView();

    partial void OnStartDateChanged(DateTime? value) => RequestDataRefresh(resetPage: true);

    [RelayCommand]
    private void OpenEmployeeCard()
    {
        if (SelectedItem is null || string.IsNullOrWhiteSpace(SelectedItem.FullName))
            return;

        _dialogService.OpenEmployeeCard(SelectedItem.FullName);
    }

    [RelayCommand]
    private void OpenScheduleSettings()
    {
        _dialogService.OpenScheduleSettings();
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
        SelectedDepartment = AllDepartmentsPreset;
        StartDate = null;
        EndDate = null;
        SelectedDatePreset = null;
        ShowLateArrivals = false;
        ShowEarlyDepartures = false;
        ShowMissingAlcotest = false;
        OnPropertyChanged(nameof(HasActiveFilters));

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
            _logger?.LogError(ex, "Ошибка при сохранении заметок");
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
