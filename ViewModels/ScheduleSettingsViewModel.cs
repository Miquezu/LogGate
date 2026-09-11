using MaterialDesignThemes.Wpf;

namespace LogGate.ViewModels;

/// <summary>
/// ViewModel настройки правил внутреннего трудового распорядка и графиков работы.
/// </summary>
public partial class ScheduleSettingsViewModel : ObservableObject
{
    private readonly IDataRepository _dataRepository;
    private readonly IDialogService _dialogService;
    private readonly IScheduleService _scheduleService;
    private readonly ILogger<ScheduleSettingsViewModel>? _logger;

    public ISnackbarMessageQueue SnackbarMessageQueue { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(3.5));

    [ObservableProperty]
    private DateTime? _newStartTime = new(2000, 1, 1, 8, 0, 0);

    [ObservableProperty]
    private DateTime? _newEndTime = new(2000, 1, 1, 16, 30, 0);

    [ObservableProperty]
    private bool _newIsPersonal;

    public bool NewIsDepartment
    {
        get => !NewIsPersonal;
        set
        {
            if (value)
                NewIsPersonal = false;
        }
    }

    partial void OnNewIsPersonalChanged(bool value) => OnPropertyChanged(nameof(NewIsDepartment));

    [ObservableProperty]
    private bool _newRequiresAlcotest;

    [ObservableProperty]
    private string _newTargetName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<WorkScheduleRule> _rules = [];

    [ObservableProperty]
    private WorkScheduleRule? _selectedRule;

    public bool HasNoRules => Rules.Count == 0;
    public bool HasRules => Rules.Count > 0;

    public ScheduleSettingsViewModel(
        IDataRepository dataRepository,
        IDialogService dialogService,
        IScheduleService scheduleService,
        ILogger<ScheduleSettingsViewModel>? logger = null)
    {
        _dataRepository = dataRepository;
        _dialogService = dialogService;
        _scheduleService = scheduleService;
        _logger = logger;

        _ = LoadRulesAsync();
    }

    [RelayCommand]
    private void AddRule()
    {
        if (string.IsNullOrWhiteSpace(NewTargetName) || !NewStartTime.HasValue || !NewEndTime.HasValue)
        {
            SnackbarMessageQueue.Enqueue("Пожалуйста, заполните все обязательные поля.");
            return;
        }

        var rule = new WorkScheduleRule
        {
            TargetName = NewTargetName.Trim(),
            IsPersonal = NewIsPersonal,
            StartTime = NewStartTime.Value.TimeOfDay,
            EndTime = NewEndTime.Value.TimeOfDay,
            RequiresAlcotest = NewRequiresAlcotest
        };

        Rules.Add(rule);
        NewTargetName = string.Empty;
        UpdateRulesState();
        SnackbarMessageQueue.Enqueue($"Правило для «{rule.TargetName}» добавлено в список.");
    }

    [RelayCommand]
    private void DeleteRule(WorkScheduleRule? rule = null)
    {
        var target = rule ?? SelectedRule;
        if (target != null)
        {
            Rules.Remove(target);
            UpdateRulesState();
            SnackbarMessageQueue.Enqueue($"Правило «{target.TargetName}» удалено.");
        }
    }

    private void UpdateRulesState()
    {
        OnPropertyChanged(nameof(HasNoRules));
        OnPropertyChanged(nameof(HasRules));
    }

    private async Task LoadRulesAsync()
    {
        try
        {
            var dbRules = await _dataRepository.GetAllWorkRulesAsync();
            Rules = [.. dbRules];
            UpdateRulesState();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка при загрузке правил графиков");
            SnackbarMessageQueue.Enqueue($"Ошибка загрузки правил: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        try
        {
            var rulesList = Rules.ToList();
            await _dataRepository.SaveWorkRulesAsync(rulesList);
            _scheduleService.UpdateRules(rulesList, _scheduleService.PreHolidays);

            SnackbarMessageQueue.Enqueue("Настройки графиков успешно сохранены!");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Ошибка сохранения правил графиков");
            SnackbarMessageQueue.Enqueue($"Ошибка при сохранении: {ex.Message}");
        }
    }
}
}