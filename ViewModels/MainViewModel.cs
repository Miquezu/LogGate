using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogGate;
using LogGate.Interfaces;
using LogGate.Models;
using LogGate.Services;
using LogGate.ViewModels;
using LogGate.Views;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;

public partial class MainViewModel : ObservableObject
{
    private readonly AutoImportService _autoImportService;
    private readonly IDataRepository _dataRepository;
    private readonly IDialogService _dialogService;
    private readonly IFileParser _fileParser;

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
    private TimeSpan _shiftEndTime = new(16, 30, 0);

    [ObservableProperty]
    private TimeSpan _shiftStartTime = new(8, 1, 0);

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

    public MainViewModel(IFileParser fileParser, IDataRepository dataRepository, IDialogService dialogService)
    {
        _dataRepository = dataRepository;
        _fileParser = fileParser;
        _dialogService = dialogService;

        _autoImportService = new AutoImportService(_fileParser, _dataRepository);
        _autoImportService.DataImported += OnAutoDataImported;
        _autoImportService.ImportError += OnAutoImportError;
        _autoImportService.ImportStarted += OnAutoImportStarted; // Новая подписка

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
        _autoImportService?.Dispose();

        if (_dataRepository is IDisposable disposableRepo)
            disposableRepo.Dispose();
    }

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

        if (StartDate.HasValue) query = query.Where(x => x.EventTime >= StartDate.Value);
        if (EndDate.HasValue) query = query.Where(x => x.EventTime <= EndDate.Value.AddDays(1).AddTicks(-1));

        var memoryData = query.AsEnumerable();

        if (ShowLateArrivals)
            memoryData = memoryData.Where(x => ScheduleRules.IsLate(x));

        if (ShowEarlyDepartures)
            memoryData = memoryData.Where(x => ScheduleRules.IsEarlyDeparture(x));

        if (ShowMissingAlcotest)
            memoryData = memoryData.Where(item => ScheduleRules.RequiresAlcotest(item) && item.AlcotestResult == null);

        var finalDataList = memoryData.ToList();

        FilteredCount = finalDataList.Count;
        TotalPages = (int)Math.Ceiling((double)FilteredCount / PageSize);
        if (TotalPages == 0) TotalPages = 1;
        if (resetPage) CurrentPage = 1;

        var sortedData = ApplySorting(finalDataList);
        var pagedData = sortedData.Skip((CurrentPage - 1) * PageSize).Take(PageSize);

        DataItems = new ObservableCollection<DataItem>(pagedData);
    }

    private IEnumerable<DataItem> ApplySorting(IEnumerable<DataItem> query)
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
        if (StartDate.HasValue) query = query.Where(x => x.EventTime >= StartDate.Value);
        if (EndDate.HasValue) query = query.Where(x => x.EventTime <= EndDate.Value.AddDays(1).AddTicks(-1));

        var fullData = query.ToList();

        if (fullData.Count == 0)
        {
            MessageBox.Show("Нет данных для анализа за этот период.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int totalRecords = fullData.Count;
        int totalEmployees = fullData.Where(x => x.FullName != null).Select(x => x.FullName).Distinct().Count();

        var violators = fullData.Where(item =>
            (item.Temperature > 37.2) ||
            (item.AlcotestResult > 0) ||
            (ScheduleRules.RequiresAlcotest(item) && item.AlcotestResult == null) ||
            ScheduleRules.IsLate(item) ||
            ScheduleRules.IsEarlyDeparture(item)
        ).ToList();

        if (violators.Count == 0)
        {
            MessageBox.Show("За выбранный период нарушений не найдено. Все сотрудники соблюдали правила!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var shedule = _dataRepository.GetAllWorkRules().ToList();

        var sb = new StringBuilder();

        sb.AppendLine("=== ВНУТРЕННИЕ РЕГЛАМЕНТЫ ПРЕДПРИЯТИЯ ===");

        var alcoRequiredTargets = _dataRepository.GetAllWorkRules()
            .Where(r => r.RequiresAlcotest)
            .Select(r => r.TargetName)
            .ToList();

        sb.AppendLine("Отделы и сотрудники, обязанные проходить алкотест:");
        if (alcoRequiredTargets.Count != 0)
            sb.AppendLine(string.Join(", ", alcoRequiredTargets));
        else
            sb.AppendLine("- Обязательное прохождение не назначено.");
        sb.AppendLine();

        sb.AppendLine("Установленные графики работы:");
        var workRules = _dataRepository.GetAllWorkRules();
        if (workRules.Count != 0)
        {
            foreach (var rule in workRules)
            {
                string targetType = rule.IsPersonal ? "(Индивидуальный)" : "(Отдел)";
                sb.AppendLine($"- {rule.TargetName} {targetType}: с {rule.StartTime:hh\\:mm} до {rule.EndTime:hh\\:mm}");
            }
        }
        else
            sb.AppendLine("- Используется стандартный график по умолчанию.");
        sb.AppendLine();

        sb.AppendLine("=== СТАТИСТИКА ЗА ПЕРИОД ===");
        sb.AppendLine($"Всего зафиксировано проходов: {totalRecords}");
        sb.AppendLine($"Всего уникальных сотрудников прошло: {totalEmployees}");
        sb.AppendLine($"Выявлено нарушений/инцидентов: {violators.Count}");
        sb.AppendLine();

        sb.AppendLine("=== ДЕТАЛИЗАЦИЯ ИНЦИДЕНТОВ (Только нарушения) ===");

        foreach (var item in violators)
            sb.AppendLine($"{item.EventTime:dd.MM HH:mm} {item.Direction} | {item.FullName} ({item.Position}, {item.Department}) | Т:{item.Temperature} | Алко:{item.AlcotestResult} | Прим: {item.Note}");

        var reportWindow = new ReportWindow();
        if (Application.Current.MainWindow != null)
            reportWindow.Owner = Application.Current.MainWindow;

        var cts = new CancellationTokenSource();

        EventHandler onWindowClosed = (s, e) =>
        {
            try { cts.Cancel(); } catch { }
        };

        reportWindow.Closed += onWindowClosed;
        reportWindow.Show();

        try
        {
            var aiService = new AiAnalyzerService();
            string report = await aiService.AnalyzeDataAsync(sb.ToString(), cts.Token);

            if (!cts.IsCancellationRequested)
                reportWindow.DisplayReport(report);
        }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
            {
                reportWindow.Close();
                MessageBox.Show($"Ошибка при обращении к ИИ:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            reportWindow.Closed -= onWindowClosed;
            cts.Dispose(); // Очищаем память
        }
    }

    private async Task InitializeCalendarAsync()
    {
        int currentYear = DateTime.Now.Year;
        var cachedDays = _dataRepository.GetShortenedDaysByYear(currentYear);
        var cachedHolidays = _dataRepository.GetShortenedDaysByYear(currentYear);
        var workRules = _dataRepository.GetAllWorkRules();
        ScheduleRules.UpdateRules(workRules, cachedHolidays);
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
            _dialogService.ShowMessage(error);
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

        // Создаем ViewModel для нового окна, передаем ФИО и репозиторий
        var cardViewModel = new EmployeeCardViewModel(SelectedItem.FullName, _dataRepository);

        // Создаем и показываем само окно
        var cardWindow = new EmployeeCardWindow(cardViewModel);
        cardWindow.Show(); // Show() позволяет открыть несколько карточек, ShowDialog() заблокирует главное окно
    }

    [RelayCommand]
    private void OpenScheduleSettings()
    {
        var settingsViewModel = new ScheduleSettingsViewModel(_dataRepository, _dialogService);
        var settingsWindow = new LogGate.Views.ScheduleSettingsWindow(settingsViewModel);
        settingsWindow.ShowDialog();
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
        SearchText = string.Empty;
        StartDate = null;
        EndDate = null;
        SelectedDatePreset = null;
        ShowLateArrivals = false;
        ShowEarlyDepartures = false;

        LoadDataFromDatabase();
    }

    [RelayCommand]
    private void SaveChanges()
    {
        try
        {
            _dataRepository.SaveChanges();
            StatusMessage = "Изменения успешно сохранены в базу.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка сохранения:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}