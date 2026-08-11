using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogGate;
using LogGate.Interfaces;
using LogGate.Models;
using LogGate.Services;
using LogGate.ViewModels;
using System.Collections.ObjectModel;

public partial class MainViewModel : ObservableObject
{
    private readonly IDataRepository _dataRepository;
    private readonly IDialogService _dialogService;
    private readonly IFileParser _fileParser;

    [ObservableProperty]
    private ObservableCollection<DataItem> _dataItems = [];

    [ObservableProperty]
    private DateTime? _endDate = DateTime.Today;

    [ObservableProperty]
    private DateTime? _startDate = DateTime.Today;

    [ObservableProperty]
    private int _filteredCount;

    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _selectedDatePreset = "Сегодня";

    [ObservableProperty]
    private TimeSpan _shiftEndTime = new(16, 30, 0);

    [ObservableProperty]
    private TimeSpan _shiftStartTime = new(8, 1, 0);

    [ObservableProperty]
    private bool _showEarlyDepartures;

    [ObservableProperty]
    private bool _showLateArrivals;

    [ObservableProperty]
    private string _statusMessage = "Готово";

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private int _pageSize = 200;

    [ObservableProperty]
    private string _sortColumn = "EventTime";

    [ObservableProperty]
    private bool _sortDescending = true;

    public MainViewModel(IFileParser fileParser, IDataRepository dataRepository, IDialogService dialogService)
    {
        _dataRepository = dataRepository;
        _fileParser = fileParser;
        _dialogService = dialogService;

        _ = InitializeCalendarAsync();

        LoadDataFromDatabase();
    }

    private async Task InitializeCalendarAsync()
    {
        int currentYear = DateTime.Now.Year;

        var cachedDays = _dataRepository.GetShortenedDaysByYear(currentYear);

        // 1. Получаем список предпраздничных дней (как делали раньше)
        var cachedHolidays = _dataRepository.GetShortenedDaysByYear(currentYear);

        // 2. Получаем все графики из новой таблицы
        var workRules = _dataRepository.GetAllWorkRules();

        // 3. Загружаем всё в движок
        ScheduleRules.UpdateRules(workRules, cachedHolidays);
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
            // Строковые и базовые данные
            "RecordNumber" => SortDescending ? query.OrderByDescending(x => x.RecordNumber) : query.OrderBy(x => x.RecordNumber),
            "Post" => SortDescending ? query.OrderByDescending(x => x.Post) : query.OrderBy(x => x.Post),
            "Direction" => SortDescending ? query.OrderByDescending(x => x.Direction) : query.OrderBy(x => x.Direction),

            // Временные метки
            "EventTime" => SortDescending ? query.OrderByDescending(x => x.EventTime) : query.OrderBy(x => x.EventTime),
            "TemperatureTime" => SortDescending ? query.OrderByDescending(x => x.TemperatureTime) : query.OrderBy(x => x.TemperatureTime),
            "AlcotestTime" => SortDescending ? query.OrderByDescending(x => x.AlcotestTime) : query.OrderBy(x => x.AlcotestTime),

            // Числовые показатели
            "Temperature" => SortDescending ? query.OrderByDescending(x => x.Temperature) : query.OrderBy(x => x.Temperature),
            "AlcotestResult" => SortDescending ? query.OrderByDescending(x => x.AlcotestResult) : query.OrderBy(x => x.AlcotestResult),

            // Данные сотрудника
            "FullName" => SortDescending ? query.OrderByDescending(x => x.FullName) : query.OrderBy(x => x.FullName),
            "Position" => SortDescending ? query.OrderByDescending(x => x.Position) : query.OrderBy(x => x.Position),
            "Department" => SortDescending ? query.OrderByDescending(x => x.Department) : query.OrderBy(x => x.Department),
            "EmployeeNumber" => SortDescending ? query.OrderByDescending(x => x.EmployeeNumber) : query.OrderBy(x => x.EmployeeNumber),
            "PassNumber" => SortDescending ? query.OrderByDescending(x => x.PassNumber) : query.OrderBy(x => x.PassNumber),

            // По умолчанию (если колонка не найдена) сортируем по времени события от новых к старым
            _ => query.OrderByDescending(x => x.EventTime)
        };
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

    partial void OnStartDateChanged(DateTime? value) => ApplyFilters();

    partial void OnSortColumnChanged(string value) => ApplyFilters();

    partial void OnSortDescendingChanged(bool value) => ApplyFilters();

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

    [ObservableProperty]
    private DataItem? _selectedItem;

    // Команда для открытия карточки
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
}