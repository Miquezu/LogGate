using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LogGate.Interfaces;
using LogGate.Models;
using LogGate.Services;
using System.Collections.ObjectModel;

namespace LogGate.ViewModels
{
    public partial class ScheduleSettingsViewModel : ObservableObject
    {
        private readonly IDataRepository _dataRepository;
        private readonly IDialogService _dialogService;
        private readonly IScheduleService _scheduleService;

        [ObservableProperty]
        private DateTime? _newEndTime = new DateTime(2000, 1, 1, 16, 30, 0);

        [ObservableProperty]
        private bool _newIsPersonal;

        // По умолчанию ставим 08:00 и 16:30
        [ObservableProperty]
        private DateTime? _newStartTime = new DateTime(2000, 1, 1, 8, 0, 0);

        [ObservableProperty]
        private string _newTargetName = string.Empty;

        [ObservableProperty]
        private ObservableCollection<WorkScheduleRule> _rules = [];

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
                _dialogService.ShowMessage("Пожалуйста, заполните все поля.");
                return;
            }

            var rule = new WorkScheduleRule
            {
                TargetName = NewTargetName.Trim(),
                IsPersonal = NewIsPersonal,
                StartTime = NewStartTime.Value.TimeOfDay,
                EndTime = NewEndTime.Value.TimeOfDay
            };

            Rules.Add(rule);
            NewTargetName = string.Empty; // Очищаем поле после добавления
        }

        [RelayCommand]
        private void DeleteRule()
        {
            if (SelectedRule != null)
                Rules.Remove(SelectedRule);
        }

        private async Task LoadRulesAsync()
        {
            var dbRules = await _dataRepository.GetAllWorkRulesAsync();
            Rules = new ObservableCollection<WorkScheduleRule>(dbRules);
        }

        [RelayCommand]
        private async Task SaveChangesAsync()
        {
            try
            {
                var rulesList = Rules.ToList();
                await _dataRepository.SaveWorkRulesAsync(rulesList);

                _scheduleService.UpdateRules(rulesList, _scheduleService.PreHolidays);

                _dialogService.ShowMessage("Настройки графиков успешно сохранены!");
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage($"Ошибка при сохранении: {ex.Message}");
            }
        }
    }
}