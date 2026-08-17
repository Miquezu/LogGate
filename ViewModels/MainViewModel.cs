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
        _autoImportService?.Dispose(); // Выключаем наблюдателя

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

        if (StartDate.HasValue)
            query = query.Where(x => x.EventTime >= StartDate.Value);

        if (EndDate.HasValue)
            query = query.Where(x => x.EventTime <= EndDate.Value.AddDays(1).AddTicks(-1));

        if (ShowLateArrivals)
            query = query.Where(ScheduleRules.IsLateExpression());

        if (ShowEarlyDepartures)
            query = query.Where(ScheduleRules.IsEarlyDepartureExpression());

        FilteredCount = query.Count();
        TotalPages = (int)Math.Ceiling((double)FilteredCount / PageSize);
        if (TotalPages == 0)
            TotalPages = 1;

        if (resetPage)
            CurrentPage = 1;

        // 1. Применяем динамическую сортировку перед пагинацией
        query = ApplySorting(query);

        // 2. Берем нужную страницу (жесткий OrderByDescending убрали)
        var pagedQuery = query
                      .Skip((CurrentPage - 1) * PageSize)
                      .Take(PageSize);

        DataItems = new ObservableCollection<DataItem>(pagedQuery);
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

            // По умолчанию (если колонка не найдена) сортируем по времени события от новых к старым
            _ => query.OrderByDescending(x => x.EventTime)
        };
    }

    [RelayCommand]
    private async Task GenerateAiReportAsync()
    {
        // 1. Берем данные с учетом фильтров интерфейса из базы
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

        // 2. АГРЕГАЦИЯ: Отбираем только нарушителей и сразу подсчитываем их нарушения
        var violatorsSummary = fullData
            .Where(x => ScheduleRules.IsLate(x) || ScheduleRules.IsEarlyDeparture(x))
            .GroupBy(x => new { x.Department, x.FullName })
            .Select(g => new
            {
                Department = g.Key.Department,
                Name = g.Key.FullName,
                LateCount = g.Count(x => ScheduleRules.IsLate(x)),
                EarlyCount = g.Count(x => ScheduleRules.IsEarlyDeparture(x))
            })
            .ToList();

        if (violatorsSummary.Count == 0)
        {
            MessageBox.Show("За выбранный период нарушений не найдено. Все сотрудники соблюдали график!", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 3. Формируем супер-сжатую таблицу для ИИ
        var sb = new StringBuilder();
        sb.AppendLine("Отдел | ФИО | Количество опозданий | Количество ранних уходов");

        foreach (var v in violatorsSummary)
            sb.AppendLine($"{v.Department} | {v.Name} | {v.LateCount} | {v.EarlyCount}");

        // 4. Открываем окно отчета и привязываем отмену к его закрытию
        var reportWindow = new ReportWindow();
        if (Application.Current.MainWindow != null)
            reportWindow.Owner = Application.Current.MainWindow;

        var cts = new CancellationTokenSource();

        // Выносим логику закрытия в отдельную переменную, чтобы потом можно было от нее отписаться
        EventHandler onWindowClosed = (s, e) =>
        {
            try { cts.Cancel(); } catch { } // Дополнительная страховка
        };

        reportWindow.Closed += onWindowClosed;
        reportWindow.Show();

        try
        {
            var aiService = new AiAnalyzerService();
            // Передаем токен в сервис
            string report = await aiService.AnalyzeDataAsync(sb.ToString(), cts.Token);

            if (!cts.IsCancellationRequested)
            {
                reportWindow.DisplayReport(report);
            }
        }
        catch (Exception ex)
        {
            // Не показываем ошибку, если это мы сами отменили запрос закрытием окна
            if (!cts.IsCancellationRequested)
            {
                reportWindow.Close();
                MessageBox.Show($"Ошибка при обращении к ИИ:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            // САМОЕ ВАЖНОЕ: Отвязываем событие ПЕРЕД тем, как удалить токен!
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
        // Перекидываем выполнение в главный поток UI
        Application.Current.Dispatcher.Invoke(() =>
        {
            LoadDataFromDatabase(); // Обновляем таблицу на экране

            // Можно показать тихое уведомление в статус-баре,
            // но пока выведем обычное окно для наглядности
            _dialogService.ShowMessage($"Фоновый импорт завершен.\nДобавлено новых записей: {addedCount}");
        });
    }

    private void OnAutoImportError(string error)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            _dialogService.ShowMessage(error);
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
}