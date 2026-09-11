using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogGate.Interfaces;
using LogGate.Models;
using LogGate.Services;
using MaterialDesignThemes.Wpf;
using System.Collections.ObjectModel;

namespace LogGate.ViewModels
{
    public partial class ScheduleSettingsViewModel : ObservableObject
    {
        public ISnackbarMessageQueue SnackbarMessageQueue { get; } = new SnackbarMessageQueue(System.TimeSpan.FromSeconds(3.5));

        private readonly IDataRepository _dataRepository;
        private readonly IDialogService _dialogService;
        private readonly IScheduleService _scheduleService;

        [ObservableProperty]
        private DateTime? _newEndTime = new DateTime(2000, 1, 1, 16, 30, 0);

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

        partial void OnNewIsPersonalChanged(bool value)
        {
            OnPropertyChanged(nameof(NewIsDepartment));
        }

        [ObservableProperty]
        private bool _newRequiresAlcotest;

        // По умолчанию ставим 08:00 и 16:30
        [ObservableProperty]
        private DateTime? _newStartTime = new DateTime(2000, 1, 1, 8, 0, 0);

        [ObservableProperty]
        private string _newTargetName = string.Empty;

        [ObservableProperty]
        private ObservableCollection<WorkScheduleRule> _rules = [];

        public bool HasNoRules => Rules == null || Rules.Count == 0;
        public bool HasRules => Rules != null && Rules.Count > 0;

        [ObservableProperty]
        private WorkScheduleRule? _selectedRule;

        public ScheduleSettingsViewModel(IDataRepository dataRepository, IDialogService dialogService, IScheduleService scheduleService)
        {
            _dataRepository = dataRepository;
            _dialogService = dialogService;
            _scheduleService = scheduleService;

            _ = LoadRulesAsync(); // Асинхронный вызов без блокировки
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
            NewTargetName = string.Empty; // Очищаем поле после добавления
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
            var dbRules = await _dataRepository.GetAllWorkRulesAsync();
            Rules = new ObservableCollection<WorkScheduleRule>(dbRules);
            UpdateRulesState();
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
                SnackbarMessageQueue.Enqueue($"Ошибка при сохранении: {ex.Message}");
            }
        }
    }
}