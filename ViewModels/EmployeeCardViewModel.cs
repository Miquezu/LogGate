using MaterialDesignThemes.Wpf;
using System.Diagnostics;

namespace LogGate.ViewModels;

/// <summary>
/// ViewModel персональной карточки сотрудника (досье, дисциплинарные показатели, табель учета рабочего времени).
/// </summary>
public partial class EmployeeCardViewModel : ObservableObject
{
    private readonly string _employeeName;
    private readonly IDataRepository _dataRepository;
    private readonly IScheduleService? _scheduleService;
    private readonly ITimesheetService _timesheetService;
    private readonly IExportService _exportService;
    private readonly IDialogService? _dialogService;
    private readonly IPrintService _printService;
    private readonly ILogger<EmployeeCardViewModel>? _logger;

    public ISnackbarMessageQueue SnackbarMessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(3.5));

    [ObservableProperty]
    private ObservableCollection<DataItem> _employeeHistory = [];

    [ObservableProperty]
    private ObservableCollection<DailyWorkRecord> _dailyRecords = [];

    [ObservableProperty]
    private TimesheetSummary _timesheetSummary = new();

    [ObservableProperty]
    private bool _isTimesheetView;

    [ObservableProperty]
    private int _totalWorkDays;

    [ObservableProperty]
    private int _totalRecords;

    [ObservableProperty]
    private string _windowTitle;

    // Данные профиля сотрудника
    [ObservableProperty]
    private string _fullName = string.Empty;

    [ObservableProperty]
    private string _initials = "??";

    [ObservableProperty]
    private string _department = "—";

    [ObservableProperty]
    private string _position = "—";

    [ObservableProperty]
    private string _employeeNumber = "—";

    [ObservableProperty]
    private string _passNumber = "—";

    // Статус нахождения на объекте
    [ObservableProperty]
    private bool _isInside;

    [ObservableProperty]
    private string _statusText = "Загрузка...";

    [ObservableProperty]
    private string _statusDetail = string.Empty;

    // Дисциплинарные и медицинские KPI метрики
    [ObservableProperty]
    private string _punctualityRate = "100%";

    [ObservableProperty]
    private int _lateCount;

    [ObservableProperty]
    private int _earlyCount;

    [ObservableProperty]
    private string _averageTemperature = "—";

    [ObservableProperty]
    private int _alcotestCheckedCount;

    [ObservableProperty]
    private int _alcotestViolationsCount;

    public EmployeeCardViewModel(
        string employeeName,
        IDataRepository dataRepository,
        IScheduleService? scheduleService = null,
        ITimesheetService? timesheetService = null,
        IExportService? exportService = null,
        IDialogService? dialogService = null,
        IPrintService? printService = null,
        ILogger<EmployeeCardViewModel>? logger = null)
    {
        _employeeName = employeeName;
        _dataRepository = dataRepository;
        _scheduleService = scheduleService;
        _timesheetService = timesheetService ?? new Services.TimesheetService();
        _exportService = exportService ?? new Services.CsvExportService();
        _dialogService = dialogService;
        _printService = printService ?? new Services.PrintService();
        _logger = logger;

        FullName = employeeName;
        Initials = GetInitials(employeeName);
        WindowTitle = $"Профиль сотрудника: {_employeeName}";

        _ = LoadEmployeeDataAsync();
    }

    [RelayCommand]
    private void SwitchToHistory() => IsTimesheetView = false;

    [RelayCommand]
    private void SwitchToTimesheet() => IsTimesheetView = true;

    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        if (DailyRecords.Count == 0 && EmployeeHistory.Count == 0)
        {
            SnackbarMessageQueue.Enqueue("Нет данных для экспорта по данному сотруднику.");
            return;
        }

        string safeName = string.Join("_", FullName.Split(Path.GetInvalidFileNameChars()));
        string defaultFileName = $"Табель_{safeName}_{DateTime.Now:yyyy-MM-dd}.csv";

        string? filePath = _dialogService?.SaveFileDialog(defaultFileName);
        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            await _exportService.ExportEmployeeTimesheetAsync(
                FullName,
                Department,
                Position,
                EmployeeNumber,
                PunctualityRate,
                TimesheetSummary,
                DailyRecords,
                filePath);

            string fileName = Path.GetFileName(filePath);
            SnackbarMessageQueue.Enqueue(
                $"Табель успешно экспортирован: {fileName}",
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
            _logger?.LogError(ex, "Ошибка при экспорте табеля сотрудника {FullName}", FullName);
            SnackbarMessageQueue.Enqueue($"Ошибка при экспорте: {ex.Message}");
        }
    }

    [RelayCommand]
    private void PrintDossier()
    {
        if (DailyRecords.Count == 0 && EmployeeHistory.Count == 0)
        {
            SnackbarMessageQueue.Enqueue("Нет данных для печати досье сотрудника.");
            return;
        }

        try
        {
            bool printed = _printService.PrintEmployeeDossier(
                FullName,
                Department,
                Position,
                EmployeeNumber,
                PunctualityRate,
                LateCount,
                EarlyCount,
                TimesheetSummary,
                DailyRecords);

            if (printed)
            {
                SnackbarMessageQueue.Enqueue("Документ успешно отправлен на печать.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при печати досье сотрудника {FullName}", FullName);
            SnackbarMessageQueue.Enqueue($"Ошибка при печати: {ex.Message}");
        }
    }

    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "??";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0].Length > 0 ? parts[0][0].ToString().ToUpperInvariant() : "??";
        return $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant();
    }

    [RelayCommand]
    public async Task LoadEmployeeDataAsync()
    {
        try
        {
            var history = await _dataRepository.GetEmployeeHistoryAsync(_employeeName);

            // Оценка соблюдения графика работы, если сервис доступен
            _scheduleService?.EvaluateCompliance(history);

            TotalRecords = history.Count;

            // Сортировка по убыванию времени (последние события первыми)
            var sorted = history.OrderByDescending(x => x.EventTime).ToList();

            var latest = sorted.FirstOrDefault();
            if (latest != null)
            {
                Department = string.IsNullOrWhiteSpace(latest.Department) ? "Не указан" : latest.Department;
                Position = string.IsNullOrWhiteSpace(latest.Position) ? "Сотрудник" : latest.Position;
                EmployeeNumber = string.IsNullOrWhiteSpace(latest.EmployeeNumber) ? "—" : latest.EmployeeNumber;
                PassNumber = string.IsNullOrWhiteSpace(latest.PassNumber) ? "—" : latest.PassNumber;

                // Определение текущего статуса нахождения на территории
                bool inside = string.Equals(latest.Direction?.Trim(), "Вход", StringComparison.OrdinalIgnoreCase);
                IsInside = inside;
                StatusText = inside ? "На территории предприятия" : "Вне объекта";
                string postInfo = string.IsNullOrWhiteSpace(latest.Post) ? "" : $" ({latest.Post})";
                StatusDetail = inside
                    ? $"Вход: {latest.EventTime:dd.MM.yyyy HH:mm}{postInfo}"
                    : $"Выход: {latest.EventTime:dd.MM.yyyy HH:mm}{postInfo}";
            }
            else
            {
                StatusText = "Нет записей";
                StatusDetail = "В базе данных нет зарегистрированных событий";
            }

            // Подсчет дисциплинарных KPI
            LateCount = sorted.Count(x => x.IsLate);
            EarlyCount = sorted.Count(x => x.IsEarlyDeparture);

            // Температура
            var tempItems = sorted.Where(x => x.Temperature.HasValue && x.Temperature.Value > 0).ToList();
            AverageTemperature = tempItems.Count > 0
                ? $"{tempItems.Average(x => x.Temperature!.Value):F1} °C"
                : "—";

            // Алкотестер
            var alcoItems = sorted.Where(x => x.AlcotestResult.HasValue).ToList();
            AlcotestCheckedCount = alcoItems.Count;
            AlcotestViolationsCount = alcoItems.Count(x => x.AlcotestResult!.Value > 0.0);

            // Индекс пунктуальности (соотношение проходов без опозданий и ранних уходов к общему числу приходов/уходов)
            int totalPunctualityChecks = sorted.Count(x =>
                string.Equals(x.Direction, "Вход", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(x.Direction, "Выход", StringComparison.OrdinalIgnoreCase));

            if (totalPunctualityChecks > 0)
            {
                int violations = LateCount + EarlyCount;
                double rate = Math.Max(0, (double)(totalPunctualityChecks - violations) / totalPunctualityChecks * 100.0);
                PunctualityRate = $"{rate:F0}%";
            }
            else
            {
                PunctualityRate = "100%";
            }

            // Расчет табеля фактически отработанных часов
            var firstWithDept = sorted.FirstOrDefault(x => !string.IsNullOrEmpty(x.Department));
            var rule = _scheduleService?.GetRuleFor(firstWithDept ?? new DataItem { FullName = _employeeName });
            var preHolidays = _scheduleService?.PreHolidays;

            var timesheetResult = _timesheetService.CalculateTimesheet(sorted, rule, preHolidays);
            DailyRecords.Clear();
            foreach (var record in timesheetResult.DailyRecords)
            {
                DailyRecords.Add(record);
            }
            TimesheetSummary = timesheetResult.Summary;
            TotalWorkDays = timesheetResult.Summary.TotalWorkDays;

            EmployeeHistory.Clear();
            foreach (var item in sorted)
            {
                EmployeeHistory.Add(item);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при загрузке данных сотрудника {EmployeeName}", _employeeName);
            SnackbarMessageQueue.Enqueue($"Ошибка загрузки данных сотрудника: {ex.Message}");
        }
    }
}
}